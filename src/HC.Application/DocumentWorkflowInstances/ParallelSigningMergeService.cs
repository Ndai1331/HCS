using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using HC.DocumentAssignments;
using HC.DocumentFiles;
using HC.DocumentWorkflowInstanceFiles;
using HC.DocumentWorkflowInstanceLogss;
using HC.Localization;
using HC.SignatureSettings;
using HC.UserSignatures;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Volo.Abp;
using Volo.Abp.BlobStoring;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Volo.Abp.Identity;
using Volo.Abp.Linq;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Timing;

namespace HC.DocumentWorkflowInstances;

public interface IParallelSigningMergeService
{
    Task MergeSignedPdfsForParallelAsync(DocumentWorkflowInstance instance);
}

public sealed class ParallelSigningMergeService : IParallelSigningMergeService, ITransientDependency
{
    private readonly IRepository<DocumentWorkflowInstanceFile, Guid> _documentWorkflowInstanceFileRepository;
    private readonly IRepository<DocumentFile, Guid> _documentFileRepository;
    private readonly IDocumentAssignmentRepository _documentAssignmentRepository;
    private readonly IDocumentWorkflowInstanceLogsRepository _documentWorkflowInstanceLogsRepository;
    private readonly IRepository<IdentityUser, Guid> _identityUserRepository;
    private readonly IUserSignatureRepository _userSignatureRepository;
    private readonly IWorkflowSigningExecutionService _workflowSigningExecutionService;
    private readonly IBlobContainer _blobContainer;
    private readonly IGuidGenerator _guidGenerator;
    private readonly ICurrentTenant _currentTenant;
    private readonly IClock _clock;
    private readonly IAsyncQueryableExecuter _asyncExecuter;
    private readonly IStringLocalizer<HCResource> _localizer;
    private readonly ILogger<ParallelSigningMergeService> _logger;

    public ParallelSigningMergeService(
        IRepository<DocumentWorkflowInstanceFile, Guid> documentWorkflowInstanceFileRepository,
        IRepository<DocumentFile, Guid> documentFileRepository,
        IDocumentAssignmentRepository documentAssignmentRepository,
        IDocumentWorkflowInstanceLogsRepository documentWorkflowInstanceLogsRepository,
        IRepository<IdentityUser, Guid> identityUserRepository,
        IUserSignatureRepository userSignatureRepository,
        IWorkflowSigningExecutionService workflowSigningExecutionService,
        IBlobContainer blobContainer,
        IGuidGenerator guidGenerator,
        ICurrentTenant currentTenant,
        IClock clock,
        IAsyncQueryableExecuter asyncExecuter,
        IStringLocalizer<HCResource> localizer,
        ILogger<ParallelSigningMergeService> logger)
    {
        _documentWorkflowInstanceFileRepository = documentWorkflowInstanceFileRepository;
        _documentFileRepository = documentFileRepository;
        _documentAssignmentRepository = documentAssignmentRepository;
        _documentWorkflowInstanceLogsRepository = documentWorkflowInstanceLogsRepository;
        _identityUserRepository = identityUserRepository;
        _userSignatureRepository = userSignatureRepository;
        _workflowSigningExecutionService = workflowSigningExecutionService;
        _blobContainer = blobContainer;
        _guidGenerator = guidGenerator;
        _currentTenant = currentTenant;
        _clock = clock;
        _asyncExecuter = asyncExecuter;
        _localizer = localizer;
        _logger = logger;
    }

    public async Task MergeSignedPdfsForParallelAsync(DocumentWorkflowInstance instance)
    {
        _logger.LogInformation("[PARALLEL_MERGE] Starting merge for instance {InstanceId}", instance.Id);

        var instanceFiles = await _documentWorkflowInstanceFileRepository.GetListAsync(
            x => x.DocumentWorkflowInstanceId == instance.Id);
        if (!instanceFiles.Any())
        {
            _logger.LogWarning("[PARALLEL_MERGE] No instance files found for merge. InstanceId={InstanceId}", instance.Id);
            return;
        }

        // Template PDF is added last (after attached files); attached files may be .docx etc.
        // Must use the PDF file - instanceFiles.First() could be a non-PDF attachment
        DocumentFile? originalFileRecord = null;
        var docFileIds = instanceFiles.Select(f => f.DocumentFileId).Distinct().ToList();
        var docFiles = await _documentFileRepository.GetListAsync(x => docFileIds.Contains(x.Id));
        foreach (var instFile in instanceFiles.OrderByDescending(f => f.CreationTime))
        {
            var docFile = docFiles.FirstOrDefault(f => f.Id == instFile.DocumentFileId);
            if (docFile != null && !string.IsNullOrEmpty(docFile.Path) && IsPdfFile(docFile))
            {
                originalFileRecord = docFile;
                break;
            }
        }

        if (originalFileRecord == null || string.IsNullOrEmpty(originalFileRecord.Path))
        {
            _logger.LogWarning("[PARALLEL_MERGE] No PDF file found in instance files. InstanceId={InstanceId}", instance.Id);
            throw new UserFriendlyException(_localizer["ErrorProcessingPdf", "No PDF file found for merge"]);
        }

        byte[] pdfBytes;
        try
        {
            pdfBytes = await _blobContainer.GetAllBytesAsync(originalFileRecord.Path);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[PARALLEL_MERGE] Error reading original PDF. Path={Path}, FileName={FileName}",
                originalFileRecord.Path, originalFileRecord.Name);
            throw new UserFriendlyException(_localizer["ErrorProcessingPdf", ex.Message]);
        }

        // Validate PDF header (blob might be wrong format if file selection was incorrect)
        if (pdfBytes.Length < 8 || !System.Text.Encoding.ASCII.GetString(pdfBytes.AsSpan(0, 8)).StartsWith("%PDF", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogError("[PARALLEL_MERGE] Blob content is not valid PDF. Path={Path}, FileName={FileName}",
                originalFileRecord.Path, originalFileRecord.Name);
            throw new UserFriendlyException(_localizer["ErrorProcessingPdf", "File is not a valid PDF"]);
        }

        var allDoneAssignments = await _documentAssignmentRepository.GetListAsync(
            x => x.DocumentId == instance.DocumentId
            && x.Status == nameof(DocumentAssignmentStatus.DONE)
            && x.CreationTime >= instance.StartedAt);
        allDoneAssignments = allDoneAssignments.OrderBy(a => a.StepOrder).ToList();

        if (!allDoneAssignments.Any())
        {
            _logger.LogWarning("[PARALLEL_MERGE] No completed assignments found. InstanceId={InstanceId}", instance.Id);
            return;
        }

        var userIds = allDoneAssignments.Select(a => a.ReceiverUserId).Distinct().ToList();
        var users = await _identityUserRepository.GetListAsync(x => userIds.Contains(x.Id));
        var userDict = users.ToDictionary(u => u.Id);

        var sigQueryable = await _userSignatureRepository.GetQueryableAsync();
        var allSignatures = await _asyncExecuter.ToListAsync(
            sigQueryable.Where(s => userIds.Contains(s.IdentityUserId)
                && s.SignType == nameof(SignType.ELECTRONIC)
                && s.IsActive));
        var signatureDict = allSignatures
            .GroupBy(s => s.IdentityUserId)
            .ToDictionary(g => g.Key, g => g.First());

        var assignmentIds = allDoneAssignments.Select(a => a.Id).ToList();
        var allLogs = await _documentWorkflowInstanceLogsRepository.GetListAsync(
            x => x.DocumentWorkflowInstanceId == instance.Id
            && assignmentIds.Contains(x.DocumentAssignmentId ?? Guid.Empty)
            && x.Action == nameof(WorkflowInstanceLogAction.APPROVE));
        var logDict = allLogs
            .GroupBy(l => l.DocumentAssignmentId ?? Guid.Empty)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(l => l.CreationTime).First());

        foreach (var doneAssignment in allDoneAssignments)
        {
            try
            {
                var userId = doneAssignment.ReceiverUserId;
                if (!userDict.TryGetValue(userId, out var user))
                {
                    _logger.LogWarning("[PARALLEL_MERGE] User {UserId} not found, skipping", userId);
                    continue;
                }
                var fullName = $"{user.Surname} {user.Name}".Trim();

                if (!signatureDict.TryGetValue(userId, out var signature)
                    || string.IsNullOrWhiteSpace(signature.SignatureImage))
                {
                    _logger.LogWarning("[PARALLEL_MERGE] Skipping merge for user {UserId} - no signature found", userId);
                    continue;
                }

                byte[] signatureImageBytes;
                try
                {
                    signatureImageBytes = await _workflowSigningExecutionService.ResolveElectronicSignatureImageBytesAsync(signature.SignatureImage);
                }
                catch
                {
                    _logger.LogWarning("[PARALLEL_MERGE] Cannot resolve signature image for user {UserId}, skipping", userId);
                    continue;
                }

                logDict.TryGetValue(doneAssignment.Id, out var log);
                var noteContent = log?.Note;

                pdfBytes = _workflowSigningExecutionService.ReplacePdfPlaceholders(
                    pdfBytes,
                    doneAssignment.StepOrder,
                    signatureImageBytes,
                    fullName,
                    noteContent ?? "");

                _logger.LogInformation("[PARALLEL_MERGE] Applied signature for step {StepOrder}, user {UserId}",
                    doneAssignment.StepOrder, userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[PARALLEL_MERGE] Error applying signature for assignment {AssignmentId}", doneAssignment.Id);
            }
        }

        var mergedBlobPath = $"{WorkflowConstants.BlobPathElectronicSigned}parallel-merged-{Guid.NewGuid()}.pdf";
        await _blobContainer.SaveAsync(mergedBlobPath, pdfBytes);

        var hashString = Convert.ToBase64String(System.Security.Cryptography.SHA256.HashData(pdfBytes));
        var mergedFile = new DocumentFile(
            _guidGenerator.Create(),
            instance.DocumentId,
            $"parallel-signed-{_clock.Now:yyyyMMddHHmmss}.pdf",
            true,
            _clock.Now,
            mergedBlobPath,
            hashString
        );
        mergedFile.TenantId = _currentTenant.Id;
        await _documentFileRepository.InsertAsync(mergedFile);

        foreach (var doneAssignment in allDoneAssignments)
        {
            doneAssignment.DocumentFileResultId = mergedFile.Id;
            await _documentAssignmentRepository.UpdateAsync(doneAssignment);
        }

        var mergedInstanceFile = new DocumentWorkflowInstanceFile(
            _guidGenerator.Create(),
            instance.Id,
            mergedFile.Id
        );
        mergedInstanceFile.TenantId = _currentTenant.Id;
        await _documentWorkflowInstanceFileRepository.InsertAsync(mergedInstanceFile);

        _logger.LogInformation("[PARALLEL_MERGE] Merge completed. MergedFileId={FileId}, BlobPath={BlobPath}",
            mergedFile.Id, mergedBlobPath);
    }

    private static bool IsPdfFile(DocumentFile file)
    {
        var ext = Path.GetExtension(file.Name ?? file.Path ?? "").ToLowerInvariant();
        return ext == ".pdf";
    }
}
