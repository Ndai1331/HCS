using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using HC.BnnSoftSigns;
using HC.DocumentAssignments;
using HC.DocumentFiles;
using HC.DocumentPdfViewer;
using HC.Localization;
using HC.SignatureSettings;
using HC.UserSignatures;
using Microsoft.Extensions.Configuration;
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
    /// <summary>PDFsharp family name; resolves to LiberationSans-*.ttf via CustomFontResolver (e.g. LiberationSans-Regular.ttf on Linux).</summary>
    private const string PdfPlaceholderTextFontFamily = "Liberation Sans";

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
    private readonly ILoggerFactory _loggerFactory;
    private readonly IConfiguration _configuration;

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
        ILogger<WorkflowSigningExecutionService> logger,
        ILoggerFactory loggerFactory,
        IConfiguration configuration)
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
        _loggerFactory = loggerFactory;
        _configuration = configuration;
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
        var signTextLogger = _loggerFactory.CreateLogger<SignText>();
        var signer = new SignText(signature.TokenRef, signature.Secret, signatureSetting.ApiEndpoint, signTextLogger);

        var pdfForSigning = ReplacePdfNameAndNotePlaceholders(
            pdfBytes,
            assignment.StepOrder,
            fullName,
            noteContent ?? string.Empty);

        byte[]? signedPdfBytes;
        try
        {
            signedPdfBytes = signer.SignTextLocationCustomizeV2(new SignPdfInput
            {
                datapdf = pdfForSigning,
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
            signedPdfBytes = ReplacePdfSignPlaceholderWhiteout(signedPdfBytes, assignment.StepOrder);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[DIGITAL_SIGN] Could not white-out Sign placeholder. StepOrder={StepOrder}", assignment.StepOrder);
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

        byte[] fileBytes;
        byte[] signatureImageBytes;
        try
        {
            fileBytes = await _blobContainer.GetAllBytesAsync(sourceFile.Path);
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

        var sourceExtension = ResolveFileExtension(sourceFile);
        if (sourceExtension != ".pdf" && sourceExtension != ".doc" && sourceExtension != ".docx")
        {
            return sourceFileId;
        }

        if ((sourceExtension == ".doc" || sourceExtension == ".docx") && string.IsNullOrWhiteSpace(htmlContent))
        {
            throw new UserFriendlyException(_localizer["The {0} field is required.", _localizer["SigningContent"]]);
        }

        byte[] pdfBytes;
        if (sourceExtension == ".pdf")
        {
            // PDF: replace placeholders directly in PDF
            pdfBytes = ReplacePreparedPlaceholders(
                fileBytes,
                signatureImageBytes,
                fullName,
                htmlContent ?? string.Empty,
                _clock.Now);
        }
        else
        {
            // Word: replace in Word first (preserves layout for <<ContentToBeApproved>>), then convert to PDF
            byte[] wordBytesToConvert = fileBytes;
            if (sourceExtension == ".doc")
            {
                wordBytesToConvert = await ConvertWordToDocxAsync(fileBytes);
            }

            var replacedWordBytes = WordPlaceholderReplacer.ReplacePlaceholders(
                wordBytesToConvert,
                signatureImageBytes,
                fullName,
                htmlContent ?? string.Empty,
                _clock.Now);

            pdfBytes = await ConvertWordToPdfAsync(replacedWordBytes, ".docx");
        }

        var extension = ".pdf";
        var newBlobPath = $"{WorkflowConstants.BlobPathSigningSteps}{Guid.NewGuid()}{extension}";
        await _blobContainer.SaveAsync(newBlobPath, pdfBytes);

        var hash = Convert.ToBase64String(System.Security.Cryptography.SHA256.HashData(pdfBytes));
        var outputFileName = sourceExtension != ".pdf"
            ? Path.ChangeExtension(sourceFile.Name, ".pdf") ?? sourceFile.Name
            : sourceFile.Name;
        var preparedFile = new DocumentFile(
            _guidGenerator.Create(),
            documentId,
            outputFileName,
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
        catch (UserFriendlyException)
        {
            throw;
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
            throw new UserFriendlyException(_localizer[GetImageReadErrorLocalizationKey(signatureImage)]);
        }
    }

    private static string GetImageReadErrorLocalizationKey(string imagePath)
    {
        if (imagePath.StartsWith("signature-layout-images/", StringComparison.OrdinalIgnoreCase))
        {
            return "DigitalSignatureLayoutImageNotFound";
        }

        if (imagePath.StartsWith("user-seal-images/", StringComparison.OrdinalIgnoreCase))
        {
            return "DigitalSignatureSealImageNotFound";
        }

        if (imagePath.StartsWith("user-signature-images/", StringComparison.OrdinalIgnoreCase))
        {
            return "SignatureImageBlobNotFound";
        }

        return "ErrorReadingSignatureImage";
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

    private static string ResolveFileExtension(DocumentFile sourceFile)
    {
        var extension = Path.GetExtension(sourceFile.Name);
        if (string.IsNullOrWhiteSpace(extension) && !string.IsNullOrWhiteSpace(sourceFile.Path))
        {
            extension = Path.GetExtension(sourceFile.Path);
        }

        return string.IsNullOrWhiteSpace(extension) ? string.Empty : extension.ToLowerInvariant();
    }

    /// <summary>
    /// Strips HTML tags for plain-text display in PDF (RichText editor outputs HTML).
    /// </summary>
    private static string StripHtmlTags(string html)
    {
        if (string.IsNullOrWhiteSpace(html)) return string.Empty;
        var stripped = Regex.Replace(html, @"<[^>]*>", " ");
        stripped = Regex.Replace(stripped, @"\s+", " ");
        return stripped.Trim();
    }

    /// <summary>
    /// Converts HTML to plain text preserving line breaks (br, p). Matches WordPlaceholderReplacer for ContentToBeApproved.
    /// </summary>
    private static string HtmlToPlainWithLineBreaks(string html)
    {
        if (string.IsNullOrWhiteSpace(html)) return string.Empty;
        var opt = RegexOptions.IgnoreCase;
        var text = Regex.Replace(html, @"<br\s*/?>", "\n", opt);
        text = Regex.Replace(text, @"</p>\s*<p[^>]*>", "\n", opt);
        text = Regex.Replace(text, @"<p[^>]*>", "\n", opt);
        text = Regex.Replace(text, @"</p>", "\n", opt);
        text = Regex.Replace(text, @"<[^>]*>", " ", opt);
        text = Regex.Replace(text, @"[^\S\n]+", " ", opt);
        return text.Trim();
    }

    private async Task<byte[]> ConvertWordToDocxAsync(byte[] sourceBytes)
    {
        return await ConvertWordWithLibreOfficeAsync(sourceBytes, ".doc", "docx");
    }

    private async Task<byte[]> ConvertWordToPdfAsync(byte[] sourceBytes, string sourceExtension)
    {
        return await ConvertWordWithLibreOfficeAsync(sourceBytes, sourceExtension, "pdf");
    }

    private async Task<byte[]> ConvertWordWithLibreOfficeAsync(byte[] sourceBytes, string sourceExtension, string outputFormat)
    {
        var sofficePath = _configuration["LibreOffice:SofficePath"];
        if (string.IsNullOrWhiteSpace(sofficePath))
        {
            sofficePath = "soffice";
        }

        var tempDir = Path.Combine(Path.GetTempPath(), $"hc-sign-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        var inputPath = Path.Combine(tempDir, $"source{sourceExtension}");
        await File.WriteAllBytesAsync(inputPath, sourceBytes);

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = sofficePath,
                Arguments = $"--headless --convert-to {outputFormat} --outdir \"{tempDir}\" \"{inputPath}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi) ?? throw new UserFriendlyException(_localizer["WordToPdfConversionFailed"]);
            await process.WaitForExitAsync();

            if (process.ExitCode != 0)
            {
                var error = await process.StandardError.ReadToEndAsync();
                _logger.LogError("[SUBMIT_PREPARE] Word conversion failed. Format={Format}, ExitCode={ExitCode}, Error={Error}", outputFormat, process.ExitCode, error);
                throw new UserFriendlyException(_localizer["WordToPdfConversionFailed"]);
            }

            var outputExt = outputFormat == "pdf" ? ".pdf" : ".docx";
            var outputPath = Path.Combine(tempDir, $"source{outputExt}");
            if (!File.Exists(outputPath))
            {
                throw new UserFriendlyException(_localizer["WordToPdfConversionFailed"]);
            }

            return await File.ReadAllBytesAsync(outputPath);
        }
        catch (UserFriendlyException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[SUBMIT_PREPARE] Unexpected error when converting Word to {Format}.", outputFormat);
            throw new UserFriendlyException(_localizer["WordToPdfConversionFailed"]);
        }
        finally
        {
            try
            {
                if (Directory.Exists(tempDir))
                {
                    Directory.Delete(tempDir, true);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[SUBMIT_PREPARE] Failed to cleanup temp word conversion directory: {TempDir}", tempDir);
            }
        }
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

    /// <summary>
    /// Replaces placeholders for electronic signing. Uses same logic as PrepareSubmissionPlaceholders:
    /// - Step-based: &lt;&lt;SignXX&gt;&gt;, &lt;&lt;FullNameXX&gt;&gt;, &lt;&lt;NoteContentXX&gt;&gt;
    /// - Prepared (trình ký style): &lt;&lt;PreparedBySign&gt;&gt;, &lt;&lt;PreparedFullName&gt;&gt; - replaced with signer's image/name
    /// </summary>
    public byte[] ReplacePdfPlaceholders(
        byte[] pdfBytes,
        int stepOrder,
        byte[] signatureImageBytes,
        string fullName,
        string noteContent)
    {
        var suffix = stepOrder.ToString("D2");
        var searchItems = new List<PlaceholderSearchItem>
        {
            new() { Tag = $"<<Sign{suffix}>>", Type = "SIGN" },
            new() { Tag = $"<<FullName{suffix}>>", Type = "FULLNAME" },
            new() { Tag = $"<<NoteContent{suffix}>>", Type = "NOTE" },
            // Same as trình ký (prepare): replace Prepared placeholders with signer's image/name
            new() { Tag = "<<PreparedBySign>>", Type = "PREPARED_SIGN" },
            new() { Tag = "<<PreparedFullName>>", Type = "PREPARED_FULLNAME" },
        };
        return ReplacePdfPlaceholdersInternal(
            pdfBytes,
            searchItems,
            signatureImageBytes,
            fullName,
            noteContent);
    }

    /// <summary>
    /// Replaces FullName and NoteContent placeholders for digital signing. Same placeholders as electronic signing.
    /// Also replaces &lt;&lt;PreparedFullName&gt;&gt; (trình ký style) with signer's name.
    /// </summary>
    private byte[] ReplacePdfNameAndNotePlaceholders(
        byte[] pdfBytes,
        int stepOrder,
        string fullName,
        string noteContent)
    {
        var suffix = stepOrder.ToString("D2");
        var searchItems = new List<PlaceholderSearchItem>
        {
            new() { Tag = $"<<FullName{suffix}>>", Type = "FULLNAME" },
            new() { Tag = $"<<NoteContent{suffix}>>", Type = "NOTE" },
            // Same as trình ký: replace PreparedFullName with signer's name
            new() { Tag = "<<PreparedFullName>>", Type = "PREPARED_FULLNAME" },
        };
        return ReplacePdfPlaceholdersInternal(
            pdfBytes,
            searchItems,
            signatureImageBytes: null,
            fullName,
            noteContent);
    }

    /// <summary>
    /// White-out &lt;&lt;SignNN&gt;&gt; placeholder in Bnn-signed PDF. Bnn draws the signature but does not remove the text.
    /// </summary>
    private byte[] ReplacePdfSignPlaceholderWhiteout(byte[] pdfBytes, int stepOrder)
    {
        var suffix = stepOrder.ToString("D2");
        return ReplacePdfPlaceholdersInternal(
            pdfBytes,
            new List<PlaceholderSearchItem>
            {
                new() { Tag = $"<<Sign{suffix}>>", Type = "SIGN_WHITEOUT" },
            },
            signatureImageBytes: null,
            fullName: string.Empty,
            noteContent: string.Empty);
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
                            var opaquePrepBytes = SignatureImageHelper.FlattenTransparency(signatureImageBytes);
                            using var imgStream = new MemoryStream(opaquePrepBytes);
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
                        var preparedNameFont = new PdfSharpDrawing.XFont(PdfPlaceholderTextFontFamily, pos.FontSize);
                        gfx.DrawString(fullName, preparedNameFont, PdfSharpDrawing.XBrushes.Black,
                            whiteRect, PdfSharpDrawing.XStringFormats.CenterLeft);
                        break;

                    case "HTML_CONTENT":
                        var preparedContentFont = new PdfSharpDrawing.XFont(PdfPlaceholderTextFontFamily, Math.Max(pos.FontSize - 1, 8));
                        var plainText = StripHtmlTags(htmlContent ?? string.Empty);
                        gfx.DrawString(plainText, preparedContentFont, PdfSharpDrawing.XBrushes.Black,
                            whiteRect, PdfSharpDrawing.XStringFormats.CenterLeft);
                        break;

                    case "CURRENT_DAY":
                        var dayFont = new PdfSharpDrawing.XFont(PdfPlaceholderTextFontFamily, Math.Max(pos.FontSize, 8));
                        gfx.DrawString(currentDate.ToString("dd"), dayFont, PdfSharpDrawing.XBrushes.Black,
                            whiteRect, PdfSharpDrawing.XStringFormats.CenterLeft);
                        break;

                    case "CURRENT_MONTH":
                        var monthFont = new PdfSharpDrawing.XFont(PdfPlaceholderTextFontFamily, Math.Max(pos.FontSize, 8));
                        gfx.DrawString(currentDate.ToString("MM"), monthFont, PdfSharpDrawing.XBrushes.Black,
                            whiteRect, PdfSharpDrawing.XStringFormats.CenterLeft);
                        break;

                    case "CURRENT_YEAR":
                        var yearFont = new PdfSharpDrawing.XFont(PdfPlaceholderTextFontFamily, Math.Max(pos.FontSize, 8));
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
                case "SIGN_WHITEOUT":
                    // Bnn draws signature at this position; we only white-out the placeholder text
                    break;

                case "SIGN":
                case "PREPARED_SIGN":
                    if (signatureImageBytes != null && signatureImageBytes.Length > 0)
                    {
                        var opaqueSignBytes = SignatureImageHelper.FlattenTransparency(signatureImageBytes);
                        using var imgStream = new MemoryStream(opaqueSignBytes);
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
                    var nameFont = new PdfSharpDrawing.XFont(PdfPlaceholderTextFontFamily, pos.FontSize);
                    gfx.DrawString(fullName, nameFont, PdfSharpDrawing.XBrushes.Black,
                        whiteRect, PdfSharpDrawing.XStringFormats.CenterLeft);
                    break;

                case "NOTE":
                    // Match ContentToBeApproved: strip HTML, preserve line breaks (same as trình ký)
                    var notePlainText = HtmlToPlainWithLineBreaks(noteContent ?? string.Empty);
                    var noteFont = new PdfSharpDrawing.XFont(PdfPlaceholderTextFontFamily, Math.Max(pos.FontSize - 1, 8));
                    gfx.DrawString(notePlainText, noteFont, PdfSharpDrawing.XBrushes.Black,
                        whiteRect, PdfSharpDrawing.XStringFormats.TopLeft);
                    break;

                case "HTML_CONTENT":
                    var htmlContentFont = new PdfSharpDrawing.XFont(PdfPlaceholderTextFontFamily, Math.Max(pos.FontSize - 1, 8));
                    gfx.DrawString(noteContent, htmlContentFont, PdfSharpDrawing.XBrushes.Black,
                        whiteRect, PdfSharpDrawing.XStringFormats.CenterLeft);
                    break;

                case "CURRENT_DAY":
                    var dayFont = new PdfSharpDrawing.XFont(PdfPlaceholderTextFontFamily, Math.Max(pos.FontSize, 8));
                    gfx.DrawString(currentDate?.ToString("dd") ?? string.Empty, dayFont, PdfSharpDrawing.XBrushes.Black,
                        whiteRect, PdfSharpDrawing.XStringFormats.CenterLeft);
                    break;

                case "CURRENT_MONTH":
                    var monthFont = new PdfSharpDrawing.XFont(PdfPlaceholderTextFontFamily, Math.Max(pos.FontSize, 8));
                    gfx.DrawString(currentDate?.ToString("MM") ?? string.Empty, monthFont, PdfSharpDrawing.XBrushes.Black,
                        whiteRect, PdfSharpDrawing.XStringFormats.CenterLeft);
                    break;

                case "CURRENT_YEAR":
                    var yearFont = new PdfSharpDrawing.XFont(PdfPlaceholderTextFontFamily, Math.Max(pos.FontSize, 8));
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

        // Validate PDF header - PdfPig throws if bytes are not valid PDF (e.g. .docx from wrong file)
        if (pdfBytes == null || pdfBytes.Length < 8)
        {
            return (positions, Array.Empty<double>());
        }
        var header = System.Text.Encoding.ASCII.GetString(pdfBytes.AsSpan(0, Math.Min(8, pdfBytes.Length)));
        if (!header.StartsWith("%PDF", StringComparison.OrdinalIgnoreCase))
        {
            return (positions, Array.Empty<double>());
        }

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
