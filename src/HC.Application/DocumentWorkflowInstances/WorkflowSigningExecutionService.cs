using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using HC.BnnSoftSigns;
using HC.DocumentAssignments;
using HC.DocumentFiles;
using HC.Localization;
using HC.SignatureSettings;
using HC.UserSignatures;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using PdfSharpDrawing = PdfSharp.Drawing;
using PdfSharpIO = PdfSharp.Pdf.IO;
using UglyToad.PdfPig;
using Volo.Abp;
using Volo.Abp.BlobStoring;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Volo.Abp.Identity;
using Volo.Abp.Linq;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Timing;
using Volo.Abp.Users;

namespace HC.DocumentWorkflowInstances;

public interface IWorkflowSigningExecutionService
{
    Task ApplyElectronicSignatureAsync(DocumentAssignment assignment, DocumentWorkflowInstance instance, string? noteContent, Guid? selectedUserSignatureId = null);

    Task ApplyDigitalSignatureAsync(DocumentAssignment assignment, DocumentWorkflowInstance instance, string? noteContent, Guid? selectedUserSignatureId = null);

    Task<Guid?> PrepareSubmissionPlaceholdersAsync(Guid? sourceFileId, Guid documentId, string? htmlContent);

    Task<byte[]> ResolveSignatureImageBytesAsync(string signatureImage);

    byte[] ReplacePdfPlaceholders(
        byte[] pdfBytes,
        int stepOrder,
        byte[] signatureImageBytes,
        string fullName,
        string noteContent);
}

public sealed class WorkflowSigningExecutionService : IWorkflowSigningExecutionService, ITransientDependency
{
    private readonly IDocumentAssignmentRepository _documentAssignmentRepository;
    private readonly IRepository<DocumentFile, Guid> _documentFileRepository;
    private readonly IUserSignatureRepository _userSignatureRepository;
    private readonly IRepository<IdentityUser, Guid> _identityUserRepository;
    private readonly IRepository<SignatureSetting, Guid> _signatureSettingRepository;
    private readonly IBlobContainer _blobContainer;
    private readonly ICurrentUser _currentUser;
    private readonly ICurrentTenant _currentTenant;
    private readonly IClock _clock;
    private readonly IGuidGenerator _guidGenerator;
    private readonly IAsyncQueryableExecuter _asyncExecuter;
    private readonly IStringLocalizer<HCResource> _localizer;
    private readonly ILogger<WorkflowSigningExecutionService> _logger;

    public WorkflowSigningExecutionService(
        IDocumentAssignmentRepository documentAssignmentRepository,
        IRepository<DocumentFile, Guid> documentFileRepository,
        IUserSignatureRepository userSignatureRepository,
        IRepository<IdentityUser, Guid> identityUserRepository,
        IRepository<SignatureSetting, Guid> signatureSettingRepository,
        IBlobContainer blobContainer,
        ICurrentUser currentUser,
        ICurrentTenant currentTenant,
        IClock clock,
        IGuidGenerator guidGenerator,
        IAsyncQueryableExecuter asyncExecuter,
        IStringLocalizer<HCResource> localizer,
        ILogger<WorkflowSigningExecutionService> logger)
    {
        _documentAssignmentRepository = documentAssignmentRepository;
        _documentFileRepository = documentFileRepository;
        _userSignatureRepository = userSignatureRepository;
        _identityUserRepository = identityUserRepository;
        _signatureSettingRepository = signatureSettingRepository;
        _blobContainer = blobContainer;
        _currentUser = currentUser;
        _currentTenant = currentTenant;
        _clock = clock;
        _guidGenerator = guidGenerator;
        _asyncExecuter = asyncExecuter;
        _localizer = localizer;
        _logger = logger;
    }

    public async Task ApplyDigitalSignatureAsync(
        DocumentAssignment assignment,
        DocumentWorkflowInstance instance,
        string? noteContent,
        Guid? selectedUserSignatureId = null)
    {
        var currentUserId = _currentUser.Id ?? throw new UserFriendlyException(_localizer["NotAuthorizedForThisAction"]);
        var now = _clock.Now;

        var sigQueryable = await _userSignatureRepository.GetQueryableAsync();
        var signatureQuery = sigQueryable.Where(s => s.IdentityUserId == currentUserId
            && s.SignType == nameof(SignType.DIGITAL)
            && s.IsActive);
        if (selectedUserSignatureId.HasValue)
        {
            signatureQuery = signatureQuery.Where(s => s.Id == selectedUserSignatureId.Value);
        }

        var signature = await _asyncExecuter.FirstOrDefaultAsync(signatureQuery);
        if (selectedUserSignatureId.HasValue && signature == null)
        {
            throw new UserFriendlyException(_localizer["SelectedUserSignatureNotFound"]);
        }


        if (signature == null)
        {
            throw new UserFriendlyException(_localizer["UserHasNoDigitalSignature"]);
        }

        if (string.IsNullOrWhiteSpace(signature.SignatureImage))
        {
            throw new UserFriendlyException(_localizer["SignatureImageNotConfigured"]);
        }

        if (string.IsNullOrWhiteSpace(signature.TokenRef))
        {
            throw new UserFriendlyException(_localizer["DigitalSignatureTokenRefRequired"]);
        }

        if (string.IsNullOrWhiteSpace(signature.Secret))
        {
            throw new UserFriendlyException(_localizer["DigitalSignatureSecretRequired"]);
        }

        if (string.IsNullOrWhiteSpace(signature.SealImg))
        {
            throw new UserFriendlyException(_localizer["DigitalSignatureSealImageRequired"]);
        }

        if (signature.ValidFrom.HasValue && signature.ValidFrom.Value > now)
        {
            throw new UserFriendlyException(_localizer["SignatureNotYetValid"]);
        }

        if (signature.ValidTo.HasValue && signature.ValidTo.Value < now)
        {
            throw new UserFriendlyException(_localizer["SignatureExpired"]);
        }

        var settingQueryable = await _signatureSettingRepository.GetQueryableAsync();
        var signatureSetting = await _asyncExecuter.FirstOrDefaultAsync(
            settingQueryable.Where(x => 
            x.ProviderCode == signature.ProviderCode
             && x.IsActive));

        if (signatureSetting == null || string.IsNullOrWhiteSpace(signatureSetting.ApiEndpoint))
        {
            throw new UserFriendlyException(_localizer["DigitalSignatureProviderNotFound"]);
        }

        if (string.IsNullOrWhiteSpace(signatureSetting.LayoutImg))
        {
            throw new UserFriendlyException(_localizer["DigitalSignatureLayoutImageRequired"]);
        }

        if (!assignment.DocumentFileResultId.HasValue)
        {
            throw new UserFriendlyException(_localizer["NoFileToSign"]);
        }

        var sourceFile = await _documentFileRepository.GetAsync(assignment.DocumentFileResultId.Value);
        if (string.IsNullOrWhiteSpace(sourceFile.Path))
        {
            throw new UserFriendlyException(_localizer["NoFileToSign"]);
        }

        var pdfBytes = await _blobContainer.GetAllBytesAsync(sourceFile.Path);
        var signatureImageBytes = await ResolveSignatureImageBytesAsync(signature.SignatureImage);
        var sealImageBytes = await ResolveSignatureImageBytesAsync(signature.SealImg!);
        var layoutImageBytes = await ResolveSignatureImageBytesAsync(signatureSetting.LayoutImg!);
        var layoutHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(layoutImageBytes));
        _logger.LogInformation(
            "[DIGITAL_SIGN] Loaded signing assets | AssignmentId={AssignmentId} | ProviderCode={ProviderCode} | LayoutPath={LayoutPath} | LayoutBytes={LayoutBytes} | LayoutSha256={LayoutSha256} | SignatureBytes={SignatureBytes} | SealBytes={SealBytes}",
            assignment.Id,
            signature.ProviderCode,
            signatureSetting.LayoutImg,
            layoutImageBytes.Length,
            layoutHash,
            signatureImageBytes.Length,
            sealImageBytes.Length);

        var user = await _identityUserRepository.GetAsync(currentUserId);
        var fullName = $"{user.Surname} {user.Name}".Trim();
        if (string.IsNullOrWhiteSpace(fullName))
        {
            fullName = user.UserName ?? "Unknown";
        }

        var placeholderTag = $"<<Sign{assignment.StepOrder:D2}>>";
        var signer = new SignText(signature.TokenRef, signature.Secret, signatureSetting.ApiEndpoint);

        byte[]? signedPdfBytes;
        try
        {
            signedPdfBytes = signer.SignTextLocationCustomizeV2(new SignPdfInput
            {
                datapdf = pdfBytes,
                chukytuoi = signatureImageBytes,
                condau = sealImageBytes,
                anhkhung = layoutImageBytes,
                signaturename = Guid.NewGuid().ToString("N"),
                nguoiky = fullName,
                chucvu = string.Empty,
                fontsize = 9,
                fontcolor = "#002f7a",
                fontname = "Times New Roman",
                pagesign = -1,
                typesignature = 3,
                hashalg = "SHA-256",
                textsign = placeholderTag,
                width = signatureSetting.SignWidth > 0 ? signatureSetting.SignWidth : 150,
                height = signatureSetting.SignHeight > 0 ? signatureSetting.SignHeight : 70,
                imgwidth = signatureSetting.SignWidth > 0 ? signatureSetting.SignWidth : 150,
                imgheight = signatureSetting.SignHeight > 0 ? signatureSetting.SignHeight : 70,
                borderstyle = 0,
                bordercolor = "#000000"
            }, xOffset: 10, yOffset: 42);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[DIGITAL_SIGN] Provider signing failed. AssignmentId={AssignmentId}", assignment.Id);
            throw new UserFriendlyException(_localizer["DigitalSigningFailed", ex.Message]);
        }

        if (signedPdfBytes == null || signedPdfBytes.Length == 0)
        {
            throw new UserFriendlyException(_localizer["DigitalSigningFailed", "Provider returned empty response"]);
        }

        try
        {
            signedPdfBytes = ReplacePdfNameAndNotePlaceholders(
                signedPdfBytes,
                assignment.StepOrder,
                fullName,
                noteContent ?? string.Empty);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[DIGITAL_SIGN] Error replacing name/note placeholders. StepOrder={StepOrder}", assignment.StepOrder);
            throw new UserFriendlyException(_localizer["ErrorProcessingPdf", ex.Message]);
        }

        var extension = Path.GetExtension(sourceFile.Name);
        if (string.IsNullOrWhiteSpace(extension))
        {
            extension = ".pdf";
        }

        var newBlobPath = $"{WorkflowConstants.BlobPathDigitalSigned}{Guid.NewGuid()}{extension}";
        await _blobContainer.SaveAsync(newBlobPath, signedPdfBytes);

        var hash = Convert.ToBase64String(System.Security.Cryptography.SHA256.HashData(signedPdfBytes));
        var signedFile = new DocumentFile(
            _guidGenerator.Create(),
            null,
            sourceFile.Name,
            true,
            now,
            newBlobPath,
            hash
        );
        signedFile.TenantId = _currentTenant.Id;
        await _documentFileRepository.InsertAsync(signedFile, autoSave: true);

        assignment.DocumentFileResultId = signedFile.Id;
        await _documentAssignmentRepository.UpdateAsync(assignment, autoSave: true);
    }

    public async Task<Guid?> PrepareSubmissionPlaceholdersAsync(Guid? sourceFileId, Guid documentId, string? htmlContent)
    {
        if (!sourceFileId.HasValue)
        {
            return sourceFileId;
        }

        var currentUserId = _currentUser.Id ?? throw new UserFriendlyException(_localizer["NotAuthorizedForThisAction"]);
        var sourceFile = await _documentFileRepository.GetAsync(sourceFileId.Value);
        if (string.IsNullOrWhiteSpace(sourceFile.Path))
        {
            throw new UserFriendlyException(_localizer["NoFileToSign"]);
        }

        var (signature, _, fullName) = await GetValidatedElectronicSignatureAsync(currentUserId);

        byte[] pdfBytes;
        byte[] signatureImageBytes;
        try
        {
            pdfBytes = await _blobContainer.GetAllBytesAsync(sourceFile.Path);
            signatureImageBytes = await ResolveSignatureImageBytesAsync(signature.SignatureImage);
        }
        catch (UserFriendlyException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[SUBMIT_PREPARE] Error loading source PDF or signature image. SourceFileId={SourceFileId}", sourceFileId.Value);
            throw new UserFriendlyException(_localizer["ErrorProcessingPdf", ex.Message]);
        }

        byte[] preparedPdfBytes;
        try
        {
            preparedPdfBytes = ReplacePreparedPlaceholders(
                pdfBytes,
                signatureImageBytes,
                fullName,
                htmlContent ?? string.Empty,
                _clock.Now);
        }
        catch (UserFriendlyException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[SUBMIT_PREPARE] Error preparing PDF placeholders. SourceFileId={SourceFileId}", sourceFileId.Value);
            throw new UserFriendlyException(_localizer["ErrorProcessingPdf", ex.Message]);
        }

        var extension = Path.GetExtension(sourceFile.Name);
        if (string.IsNullOrWhiteSpace(extension))
        {
            extension = ".pdf";
        }

        var newBlobPath = $"{WorkflowConstants.BlobPathSigningSteps}{Guid.NewGuid()}{extension}";
        await _blobContainer.SaveAsync(newBlobPath, preparedPdfBytes);

        var hash = Convert.ToBase64String(System.Security.Cryptography.SHA256.HashData(preparedPdfBytes));
        var preparedFile = new DocumentFile(
            _guidGenerator.Create(),
            documentId,
            sourceFile.Name,
            false,
            _clock.Now,
            newBlobPath,
            hash
        );
        preparedFile.TenantId = _currentTenant.Id;
        await _documentFileRepository.InsertAsync(preparedFile, autoSave: true);

        return preparedFile.Id;
    }

    public async Task ApplyElectronicSignatureAsync(
        DocumentAssignment assignment,
        DocumentWorkflowInstance instance,
        string? noteContent,
        Guid? selectedUserSignatureId = null)
    {
        var currentUserId = _currentUser.Id ?? throw new UserFriendlyException(_localizer["NotAuthorizedForThisAction"]);
        var now = _clock.Now;
        var (signature, _, fullName) = await GetValidatedElectronicSignatureAsync(currentUserId, selectedUserSignatureId);

        if (!assignment.DocumentFileResultId.HasValue)
        {
            throw new UserFriendlyException(_localizer["NoFileToSign"]);
        }

        var sourceFile = await _documentFileRepository.GetAsync(assignment.DocumentFileResultId.Value);
        if (string.IsNullOrEmpty(sourceFile.Path))
        {
            throw new UserFriendlyException(_localizer["NoFileToSign"]);
        }

        var pdfBytes = await _blobContainer.GetAllBytesAsync(sourceFile.Path);
        byte[] signatureImageBytes;
        try
        {
            signatureImageBytes = await ResolveSignatureImageBytesAsync(signature.SignatureImage);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ELECTRONIC_SIGN] Error resolving signature image for user {UserId}", currentUserId);
            throw new UserFriendlyException(_localizer["ErrorReadingSignatureImage"]);
        }

        byte[] signedPdfBytes;
        try
        {
            signedPdfBytes = ReplacePdfPlaceholders(
                pdfBytes,
                assignment.StepOrder,
                signatureImageBytes,
                fullName,
                noteContent ?? "");
        }
        catch (UserFriendlyException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ELECTRONIC_SIGN] Error replacing PDF placeholders. StepOrder={StepOrder}", assignment.StepOrder);
            throw new UserFriendlyException(_localizer["ErrorProcessingPdf", ex.Message]);
        }

        string newBlobPath;
        try
        {
            var extension = Path.GetExtension(sourceFile.Name);
            if (string.IsNullOrEmpty(extension))
            {
                extension = ".pdf";
            }

            newBlobPath = $"{WorkflowConstants.BlobPathElectronicSigned}{Guid.NewGuid()}{extension}";
            await _blobContainer.SaveAsync(newBlobPath, signedPdfBytes);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ELECTRONIC_SIGN] Error uploading signed PDF to blob storage");
            throw new UserFriendlyException(_localizer["ElectronicSigningFailed", "Cannot upload signed file"]);
        }

        DocumentFile signedFile;
        try
        {
            var hash = Convert.ToBase64String(System.Security.Cryptography.SHA256.HashData(signedPdfBytes));
            signedFile = new DocumentFile(
                _guidGenerator.Create(),
                null,
                sourceFile.Name,
                true,
                now,
                newBlobPath,
                hash);
            signedFile.TenantId = _currentTenant.Id;
            await _documentFileRepository.InsertAsync(signedFile, autoSave: true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ELECTRONIC_SIGN] Error creating signed DocumentFile record");
            throw new UserFriendlyException(_localizer["ElectronicSigningFailed", "Cannot save signed file record"]);
        }

        try
        {
            assignment.DocumentFileResultId = signedFile.Id;
            await _documentAssignmentRepository.UpdateAsync(assignment, autoSave: true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ELECTRONIC_SIGN] Error updating assignment DocumentFileResultId. AssignmentId={AssignmentId}", assignment.Id);
            throw new UserFriendlyException(_localizer["ElectronicSigningFailed", "Cannot update assignment"]);
        }
    }

    public async Task<byte[]> ResolveSignatureImageBytesAsync(string signatureImage)
    {
        if (string.IsNullOrWhiteSpace(signatureImage))
        {
            throw new UserFriendlyException(_localizer["SignatureImageNotConfigured"]);
        }

        if (signatureImage.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
        {
            var commaIndex = signatureImage.IndexOf(',');
            if (commaIndex > 0 && commaIndex < signatureImage.Length - 1)
            {
                var base64Data = signatureImage[(commaIndex + 1)..];
                return Convert.FromBase64String(base64Data);
            }
        }

        if (!signatureImage.Contains('/') && !signatureImage.Contains('\\'))
        {
            try
            {
                return Convert.FromBase64String(signatureImage);
            }
            catch (FormatException)
            {
                // Not valid base64, fall through to blob storage
            }
        }

        try
        {
            return await _blobContainer.GetAllBytesAsync(signatureImage);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[SIGNING] Error reading signature image from blob storage. Path={Path}", signatureImage);
            throw new UserFriendlyException(_localizer["ErrorReadingSignatureImage"]);
        }
    }

    private async Task<(UserSignature Signature, IdentityUser User, string FullName)> GetValidatedElectronicSignatureAsync(
        Guid currentUserId,
        Guid? selectedUserSignatureId = null)
    {
        UserSignature? signature;
        try
        {
            var sigQueryable = await _userSignatureRepository.GetQueryableAsync();
            var signatureQuery = sigQueryable.Where(s => s.IdentityUserId == currentUserId
                && s.SignType == nameof(SignType.ELECTRONIC)
                && s.IsActive);
            if (selectedUserSignatureId.HasValue)
            {
                signatureQuery = signatureQuery.Where(s => s.Id == selectedUserSignatureId.Value);
            }

            signature = await _asyncExecuter.FirstOrDefaultAsync(signatureQuery);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ELECTRONIC_SIGN] Error querying user signatures for user {UserId}", currentUserId);
            throw new UserFriendlyException(_localizer["ElectronicSigningFailed", ex.Message]);
        }

        if (signature == null)
        {
            if (selectedUserSignatureId.HasValue)
            {
                throw new UserFriendlyException(_localizer["SelectedUserSignatureNotFound"]);
            }

            throw new UserFriendlyException(_localizer["UserHasNoElectronicSignature"]);
        }

        if (!signature.IsActive)
        {
            throw new UserFriendlyException(_localizer["SignatureNotActivated"]);
        }

        if (string.IsNullOrWhiteSpace(signature.SignatureImage))
        {
            throw new UserFriendlyException(_localizer["SignatureImageNotConfigured"]);
        }

        var now = _clock.Now;
        if (signature.ValidFrom.HasValue && signature.ValidFrom.Value > now)
        {
            throw new UserFriendlyException(_localizer["SignatureNotYetValid"]);
        }

        if (signature.ValidTo.HasValue && signature.ValidTo.Value < now)
        {
            throw new UserFriendlyException(_localizer["SignatureExpired"]);
        }

        var user = await _identityUserRepository.GetAsync(currentUserId);
        var fullName = $"{user.Surname} {user.Name}".Trim();
        if (string.IsNullOrWhiteSpace(fullName))
        {
            fullName = user.UserName ?? "Unknown";
        }

        return (signature, user, fullName);
    }

    private sealed class PlaceholderPosition
    {
        public int PageIndex { get; set; }
        public double X { get; set; }
        public double YTop { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
        public double FontSize { get; set; }
        public string Type { get; set; } = string.Empty;
    }

    private sealed class PlaceholderSearchItem
    {
        public string Tag { get; set; } = string.Empty;

        public string Type { get; set; } = string.Empty;
    }

    public byte[] ReplacePdfPlaceholders(
        byte[] pdfBytes,
        int stepOrder,
        byte[] signatureImageBytes,
        string fullName,
        string noteContent)
    {
        var suffix = stepOrder.ToString("D2");
        return ReplacePdfPlaceholdersInternal(
            pdfBytes,
            new List<PlaceholderSearchItem>
            {
                new() { Tag = $"<<Sign{suffix}>>", Type = "SIGN" },
                new() { Tag = $"<<FullName{suffix}>>", Type = "FULLNAME" },
                new() { Tag = $"<<NoteContent{suffix}>>", Type = "NOTE" },
            },
            signatureImageBytes,
            fullName,
            noteContent);
    }

    private byte[] ReplacePdfNameAndNotePlaceholders(
        byte[] pdfBytes,
        int stepOrder,
        string fullName,
        string noteContent)
    {
        var suffix = stepOrder.ToString("D2");
        return ReplacePdfPlaceholdersInternal(
            pdfBytes,
            new List<PlaceholderSearchItem>
            {
                new() { Tag = $"<<FullName{suffix}>>", Type = "FULLNAME" },
                new() { Tag = $"<<NoteContent{suffix}>>", Type = "NOTE" },
            },
            signatureImageBytes: null,
            fullName,
            noteContent);
    }

    private byte[] ReplacePreparedPlaceholders(
        byte[] pdfBytes,
        byte[] signatureImageBytes,
        string fullName,
        string htmlContent,
        DateTime currentDate)
    {
        var searchPairs = new List<PlaceholderSearchItem>
        {
            new() { Tag = "<<DD>>", Type = "CURRENT_DAY" },
            new() { Tag = "<<MM>>", Type = "CURRENT_MONTH" },
            new() { Tag = "<<YYYY>>", Type = "CURRENT_YEAR" },
            new() { Tag = "<<ContentToBeApproved>>", Type = "HTML_CONTENT" },
            new() { Tag = "<<PreparedBySign>>", Type = "PREPARED_SIGN" },
            new() { Tag = "<<PreparedFullName>>", Type = "PREPARED_FULLNAME" },
        };

        var (positions, pageHeights) = FindPlaceholderPositions(pdfBytes, searchPairs);
        if (!positions.Any())
        {
            return pdfBytes;
        }

        using var inputStream = new MemoryStream(pdfBytes);
        var document = PdfSharpIO.PdfReader.Open(inputStream, PdfSharpIO.PdfDocumentOpenMode.Modify);

        foreach (var pos in positions)
        {
            if (pos.PageIndex >= document.PageCount)
            {
                continue;
            }

            var page = document.Pages[pos.PageIndex];
            var gfx = PdfSharpDrawing.XGraphics.FromPdfPage(page, PdfSharpDrawing.XGraphicsPdfPageOptions.Append);

            try
            {
                var pgHeight = pageHeights[pos.PageIndex];
                double x = pos.X;
                double y = pgHeight - pos.YTop;
                double w = pos.Width;
                double h = pos.Height;

                var whiteRect = new PdfSharpDrawing.XRect(x, y, w, h);
                gfx.DrawRectangle(PdfSharpDrawing.XBrushes.White, whiteRect);

                switch (pos.Type)
                {
                    case "PREPARED_SIGN":
                        if (signatureImageBytes != null && signatureImageBytes.Length > 0)
                        {
                            using var imgStream = new MemoryStream(signatureImageBytes);
                            var img = PdfSharpDrawing.XImage.FromStream(imgStream);
                            var imgAspect = (double)img.PixelWidth / img.PixelHeight;
                            var fitWidth = w;
                            var fitHeight = w / imgAspect;
                            if (fitHeight > h * 3)
                            {
                                fitHeight = h * 3;
                                fitWidth = fitHeight * imgAspect;
                            }

                            var imgX = x;
                            var imgY = y - (fitHeight - h) / 2;
                            gfx.DrawImage(img, imgX, imgY, fitWidth, fitHeight);
                        }
                        break;

                    case "PREPARED_FULLNAME":
                        var preparedNameFont = new PdfSharpDrawing.XFont("Helvetica", pos.FontSize);
                        gfx.DrawString(fullName, preparedNameFont, PdfSharpDrawing.XBrushes.Black,
                            whiteRect, PdfSharpDrawing.XStringFormats.CenterLeft);
                        break;

                    case "HTML_CONTENT":
                        var preparedContentFont = new PdfSharpDrawing.XFont("Helvetica", Math.Max(pos.FontSize - 1, 8));
                        gfx.DrawString(htmlContent ?? string.Empty, preparedContentFont, PdfSharpDrawing.XBrushes.Black,
                            whiteRect, PdfSharpDrawing.XStringFormats.CenterLeft);
                        break;

                    case "CURRENT_DAY":
                        var dayFont = new PdfSharpDrawing.XFont("Helvetica", Math.Max(pos.FontSize, 8));
                        gfx.DrawString(currentDate.ToString("dd"), dayFont, PdfSharpDrawing.XBrushes.Black,
                            whiteRect, PdfSharpDrawing.XStringFormats.CenterLeft);
                        break;

                    case "CURRENT_MONTH":
                        var monthFont = new PdfSharpDrawing.XFont("Helvetica", Math.Max(pos.FontSize, 8));
                        gfx.DrawString(currentDate.ToString("MM"), monthFont, PdfSharpDrawing.XBrushes.Black,
                            whiteRect, PdfSharpDrawing.XStringFormats.CenterLeft);
                        break;

                    case "CURRENT_YEAR":
                        var yearFont = new PdfSharpDrawing.XFont("Helvetica", Math.Max(pos.FontSize, 8));
                        gfx.DrawString(currentDate.ToString("yyyy"), yearFont, PdfSharpDrawing.XBrushes.Black,
                            whiteRect, PdfSharpDrawing.XStringFormats.CenterLeft);
                        break;
                }
            }
            finally
            {
                gfx.Dispose();
            }
        }

        using var outputStream = new MemoryStream();
        document.Save(outputStream);
        return outputStream.ToArray();
    }

    private byte[] ReplacePdfPlaceholdersInternal(
        byte[] pdfBytes,
        IReadOnlyList<PlaceholderSearchItem> searchPairs,
        byte[]? signatureImageBytes,
        string fullName,
        string noteContent,
        DateTime? currentDate = null)
    {
        var (positions, pageHeights) = FindPlaceholderPositions(pdfBytes, searchPairs);

        if (!positions.Any())
        {
            return pdfBytes;
        }

        using var inputStream = new MemoryStream(pdfBytes);
        var document = PdfSharpIO.PdfReader.Open(inputStream, PdfSharpIO.PdfDocumentOpenMode.Modify);

        foreach (var pos in positions)
        {
            if (pos.PageIndex >= document.PageCount)
            {
                continue;
            }

            var page = document.Pages[pos.PageIndex];
            var gfx = PdfSharpDrawing.XGraphics.FromPdfPage(page, PdfSharpDrawing.XGraphicsPdfPageOptions.Append);

            var pgHeight = pageHeights[pos.PageIndex];
            double x = pos.X;
            double y = pgHeight - pos.YTop;
            double w = pos.Width;
            double h = pos.Height;

            var whiteRect = new PdfSharpDrawing.XRect(x, y, w, h);
            gfx.DrawRectangle(PdfSharpDrawing.XBrushes.White, whiteRect);

            switch (pos.Type)
            {
                case "SIGN":
                case "PREPARED_SIGN":
                    if (signatureImageBytes != null && signatureImageBytes.Length > 0)
                    {
                        using var imgStream = new MemoryStream(signatureImageBytes);
                        var img = PdfSharpDrawing.XImage.FromStream(imgStream);
                        var imgAspect = (double)img.PixelWidth / img.PixelHeight;
                        var fitWidth = w;
                        var fitHeight = w / imgAspect;
                        if (fitHeight > h * 3)
                        {
                            fitHeight = h * 3;
                            fitWidth = fitHeight * imgAspect;
                        }

                        var imgX = x;
                        var imgY = y - (fitHeight - h) / 2;
                        gfx.DrawImage(img, imgX, imgY, fitWidth, fitHeight);
                    }
                    break;

                case "FULLNAME":
                case "PREPARED_FULLNAME":
                    var nameFont = new PdfSharpDrawing.XFont("Helvetica", pos.FontSize);
                    gfx.DrawString(fullName, nameFont, PdfSharpDrawing.XBrushes.Black,
                        whiteRect, PdfSharpDrawing.XStringFormats.CenterLeft);
                    break;

                case "NOTE":
                    var noteFont = new PdfSharpDrawing.XFont("Helvetica", Math.Max(pos.FontSize - 1, 8));
                    gfx.DrawString(noteContent, noteFont, PdfSharpDrawing.XBrushes.Black,
                        whiteRect, PdfSharpDrawing.XStringFormats.CenterLeft);
                    break;

                case "HTML_CONTENT":
                    var htmlContentFont = new PdfSharpDrawing.XFont("Helvetica", Math.Max(pos.FontSize - 1, 8));
                    gfx.DrawString(noteContent, htmlContentFont, PdfSharpDrawing.XBrushes.Black,
                        whiteRect, PdfSharpDrawing.XStringFormats.CenterLeft);
                    break;

                case "CURRENT_DAY":
                    var dayFont = new PdfSharpDrawing.XFont("Helvetica", Math.Max(pos.FontSize, 8));
                    gfx.DrawString(currentDate?.ToString("dd") ?? string.Empty, dayFont, PdfSharpDrawing.XBrushes.Black,
                        whiteRect, PdfSharpDrawing.XStringFormats.CenterLeft);
                    break;

                case "CURRENT_MONTH":
                    var monthFont = new PdfSharpDrawing.XFont("Helvetica", Math.Max(pos.FontSize, 8));
                    gfx.DrawString(currentDate?.ToString("MM") ?? string.Empty, monthFont, PdfSharpDrawing.XBrushes.Black,
                        whiteRect, PdfSharpDrawing.XStringFormats.CenterLeft);
                    break;

                case "CURRENT_YEAR":
                    var yearFont = new PdfSharpDrawing.XFont("Helvetica", Math.Max(pos.FontSize, 8));
                    gfx.DrawString(currentDate?.ToString("yyyy") ?? string.Empty, yearFont, PdfSharpDrawing.XBrushes.Black,
                        whiteRect, PdfSharpDrawing.XStringFormats.CenterLeft);
                    break;
            }

            gfx.Dispose();
        }

        using var outputStream = new MemoryStream();
        document.Save(outputStream);
        return outputStream.ToArray();
    }

    private static (List<PlaceholderPosition> Positions, double[] PageHeights) FindPlaceholderPositions(
        byte[] pdfBytes,
        IReadOnlyList<PlaceholderSearchItem> searchPairs)
    {
        var positions = new List<PlaceholderPosition>();
        double[] pageHeights;

        using var pdfPigDoc = PdfDocument.Open(pdfBytes);
        pageHeights = new double[pdfPigDoc.NumberOfPages];

        for (int p = 0; p < pdfPigDoc.NumberOfPages; p++)
        {
            var page = pdfPigDoc.GetPage(p + 1);
            pageHeights[p] = page.Height;

            var letters = page.Letters.ToList();
            if (!letters.Any())
            {
                continue;
            }

            var fullText = string.Concat(letters.Select(l => l.Value));
            foreach (var searchItem in searchPairs)
            {
                var searchFromIndex = 0;
                while (searchFromIndex < fullText.Length)
                {
                    var index = fullText.IndexOf(searchItem.Tag, searchFromIndex, StringComparison.Ordinal);
                    if (index < 0)
                    {
                        index = fullText.IndexOf(searchItem.Tag, searchFromIndex, StringComparison.OrdinalIgnoreCase);
                        if (index < 0)
                        {
                            break;
                        }
                    }

                    var placeholderLetters = letters.Skip(index).Take(searchItem.Tag.Length).ToList();
                    if (placeholderLetters.Count < searchItem.Tag.Length)
                    {
                        break;
                    }

                    var minX = placeholderLetters.Min(l => l.GlyphRectangle.Left);
                    var minY = placeholderLetters.Min(l => l.GlyphRectangle.Bottom);
                    var maxX = placeholderLetters.Max(l => l.GlyphRectangle.Right);
                    var maxY = placeholderLetters.Max(l => l.GlyphRectangle.Top);
                    var fontSize = (double)placeholderLetters.First().FontSize;

                    positions.Add(new PlaceholderPosition
                    {
                        PageIndex = p,
                        X = minX,
                        YTop = maxY,
                        Width = maxX - minX,
                        Height = maxY - minY,
                        FontSize = fontSize > 0 ? fontSize : 10,
                        Type = searchItem.Type
                    });

                    searchFromIndex = index + searchItem.Tag.Length;
                }
            }
        }

        return (positions, pageHeights);
    }

}
