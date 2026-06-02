using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using HC.DocumentAssignments;
using HC.DocumentFiles;
using HC.Documents;
using Volo.Abp.BlobStoring;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Microsoft.Extensions.Logging;

namespace HC.DocumentWorkflowInstances;

public class WorkflowDocumentFileService : HCAppService, IWorkflowDocumentFileService, ITransientDependency
{
    private readonly IDocumentAssignmentRepository _documentAssignmentRepository;
    private readonly IRepository<DocumentFile, Guid> _documentFileRepository;
    private readonly IBlobContainer _blobContainer;
    private readonly IWorkflowDocxSigningService _workflowDocxSigningService;

    public WorkflowDocumentFileService(
        IDocumentAssignmentRepository documentAssignmentRepository,
        IRepository<DocumentFile, Guid> documentFileRepository,
        IBlobContainer blobContainer,
        IWorkflowDocxSigningService workflowDocxSigningService)
    {
        _documentAssignmentRepository = documentAssignmentRepository;
        _documentFileRepository = documentFileRepository;
        _blobContainer = blobContainer;
        _workflowDocxSigningService = workflowDocxSigningService;
    }

    public async Task<Guid?> CopyDocumentFileForNextStepAsync(Guid? sourceFileId, Guid documentId)
    {
        if (!sourceFileId.HasValue)
        {
            var completedAssignments = await _documentAssignmentRepository.GetListAsync(
                x => x.DocumentId == documentId
                && x.Status == nameof(DocumentAssignmentStatus.DONE)
                && x.DocumentFileResultId.HasValue);
            sourceFileId = completedAssignments
                .OrderByDescending(a => a.ProcessedAt)
                .FirstOrDefault()?.DocumentFileResultId;
        }

        if (!sourceFileId.HasValue)
        {
            Logger.LogWarning("[COPY_FILE] sourceFileId is null and no fallback found. DocumentId={DocumentId}", documentId);
            return null;
        }

        var sourceFile = await _documentFileRepository.FindAsync(sourceFileId.Value);
        if (sourceFile == null || string.IsNullOrEmpty(sourceFile.Path))
        {
            Logger.LogWarning(
                "[COPY_FILE] Source file not found or has no path. SourceFileId={SourceFileId}, DocumentId={DocumentId}, Found={Found}, Path={Path}",
                sourceFileId.Value, documentId, sourceFile != null, sourceFile?.Path);
            return null;
        }

        try
        {
            if (sourceFile.SourceDocxFileId.HasValue || WorkflowDocxSigningService.IsDocxExtension(sourceFile))
            {
                var (_, pdfFile) = await _workflowDocxSigningService.CopyDocxPdfPairAsync(
                    sourceFile,
                    WorkflowConstants.BlobPathSigningSteps,
                    WorkflowConstants.BlobPathSigningSteps);
                return pdfFile.Id;
            }

            var fileBytes = await _blobContainer.GetAllBytesAsync(sourceFile.Path);
            var extension = Path.GetExtension(sourceFile.Name);
            var newBlobPath = $"{WorkflowConstants.BlobPathSigningSteps}{Guid.NewGuid()}{extension}";
            await _blobContainer.SaveAsync(newBlobPath, fileBytes);

            var newFile = new DocumentFile(
                GuidGenerator.Create(),
                null,
                sourceFile.Name,
                sourceFile.IsSigned,
                Clock.Now,
                newBlobPath,
                sourceFile.Hash);
            newFile.TenantId = CurrentTenant.Id;
            await _documentFileRepository.InsertAsync(newFile);

            return newFile.Id;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex,
                "Error copying document file for next step. SourceFileId={SourceFileId}, DocumentId={DocumentId}, SourcePath={SourcePath}",
                sourceFileId, documentId, sourceFile.Path);
            throw new Volo.Abp.UserFriendlyException(L["ErrorCopyingFileForNextStep"]);
        }
    }

    public async Task<Dictionary<Guid, Guid>> DuplicateDocumentFilesForWorkflowAsync(Guid sourceDocumentId, Guid targetDocumentId)
    {
        var map = new Dictionary<Guid, Guid>();
        var sourceFiles = await _documentFileRepository.GetListAsync(x => x.DocumentId == sourceDocumentId);
        foreach (var sourceFile in sourceFiles.OrderBy(f => f.UploadedAt))
        {
            if (string.IsNullOrEmpty(sourceFile.Path))
            {
                Logger.LogWarning(
                    "[DUPLICATE_DOC] Skipping file without path. FileId={FileId}, DocumentId={DocId}",
                    sourceFile.Id, sourceDocumentId);
                continue;
            }

            try
            {
                if (sourceFile.SourceDocxFileId.HasValue
                    || (WorkflowDocxSigningService.IsDocxExtension(sourceFile) && sourceFile.DerivedPdfFileId.HasValue))
                {
                    var (docxFile, pdfFile) = await _workflowDocxSigningService.CopyDocxPdfPairAsync(
                        sourceFile,
                        WorkflowConstants.BlobPathSigningSteps,
                        WorkflowConstants.BlobPathSigningSteps);

                    docxFile.DocumentId = targetDocumentId;
                    pdfFile.DocumentId = targetDocumentId;
                    await _documentFileRepository.UpdateAsync(docxFile, autoSave: true);
                    await _documentFileRepository.UpdateAsync(pdfFile, autoSave: true);

                    map[sourceFile.Id] = WorkflowDocxSigningService.IsDocxExtension(sourceFile)
                        ? docxFile.Id
                        : pdfFile.Id;
                    continue;
                }

                var fileBytes = await _blobContainer.GetAllBytesAsync(sourceFile.Path);
                var extension = Path.GetExtension(sourceFile.Name);
                var newBlobPath = $"{WorkflowConstants.BlobPathSigningSteps}{Guid.NewGuid()}{extension}";
                await _blobContainer.SaveAsync(newBlobPath, fileBytes);

                var newFile = new DocumentFile(
                    GuidGenerator.Create(),
                    targetDocumentId,
                    sourceFile.Name,
                    sourceFile.IsSigned,
                    Clock.Now,
                    newBlobPath,
                    sourceFile.Hash);
                newFile.TenantId = CurrentTenant.Id;
                await _documentFileRepository.InsertAsync(newFile, autoSave: true);
                map[sourceFile.Id] = newFile.Id;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex,
                    "[DUPLICATE_DOC] Failed to copy file for workflow duplicate. SourceFileId={SourceFileId}",
                    sourceFile.Id);
                throw new Volo.Abp.UserFriendlyException(L["ErrorCopyingFileForNextStep"]);
            }
        }

        if (map.Count == 0)
        {
            throw new Volo.Abp.UserFriendlyException(L["NoFilesAvailable"]);
        }

        return map;
    }
}
