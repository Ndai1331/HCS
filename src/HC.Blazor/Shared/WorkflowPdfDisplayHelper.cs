using System;
using System.Linq;
using System.Threading.Tasks;
using HC.DocumentFiles;
using HC.DocumentPdfViewer;
using HC.Documents;
using HC.DocumentWorkflowInstances;
using Volo.Abp.Application.Dtos;

namespace HC.Blazor.Shared;

public static class WorkflowPdfDisplayHelper
{
    public static bool IsWorkflowDocument(DocumentSourceType sourceType, Guid? workflowId)
    {
        return sourceType == DocumentSourceType.Workflow || workflowId.HasValue;
    }
    public static async Task<WorkflowDisplayPdfFileDto?> GetDisplayPdfFileAsync(
        Guid documentId,
        IDocumentWorkflowInstancesAppService documentWorkflowInstancesAppService)
    {
        return await documentWorkflowInstancesAppService.GetWorkflowDisplayPdfFileAsync(documentId);
    }

    public static async Task<string?> LoadPdfDataUrlAsync(
        Guid documentId,
        IDocumentWorkflowInstancesAppService documentWorkflowInstancesAppService,
        IDocumentPdfViewerAppService documentPdfViewerAppService,
        string watermarkAction = "view")
    {
        var pdfFile = await GetDisplayPdfFileAsync(documentId, documentWorkflowInstancesAppService);
        if (pdfFile == null || string.IsNullOrEmpty(pdfFile.Path))
        {
            return null;
        }

        var fileBytes = await documentPdfViewerAppService.GetWatermarkedPdfAsync(new GetWatermarkedPdfInput
        {
            BlobPath = pdfFile.Path,
            WatermarkAction = watermarkAction
        });

        var base64 = Convert.ToBase64String(fileBytes);
        return $"data:application/pdf;base64,{base64}";
    }

    public static async Task<bool> HasDisplayPdfAsync(
        Guid documentId,
        IDocumentWorkflowInstancesAppService documentWorkflowInstancesAppService)
    {
        var pdfFile = await GetDisplayPdfFileAsync(documentId, documentWorkflowInstancesAppService);
        return pdfFile != null
            && !string.IsNullOrEmpty(pdfFile.Path)
            && FileHelper.IsPdfFileExtension(pdfFile.Name);
    }

    public static async Task<string?> LoadPdfDataUrlWithWorkflowPreferenceAsync(
        Guid documentId,
        DocumentSourceType sourceType,
        Guid? workflowId,
        IDocumentWorkflowInstancesAppService documentWorkflowInstancesAppService,
        IDocumentFilesAppService documentFilesAppService,
        IDocumentPdfViewerAppService documentPdfViewerAppService,
        string watermarkAction = "view")
    {
        if (IsWorkflowDocument(sourceType, workflowId))
        {
            var workflowUrl = await LoadPdfDataUrlAsync(
                documentId,
                documentWorkflowInstancesAppService,
                documentPdfViewerAppService,
                watermarkAction);

            if (!string.IsNullOrEmpty(workflowUrl))
            {
                return workflowUrl;
            }
        }

        return await LoadFirstPdfDataUrlAsync(documentId, documentFilesAppService, documentPdfViewerAppService, watermarkAction);
    }

    public static async Task<bool> HasPdfWithWorkflowPreferenceAsync(
        Guid documentId,
        DocumentSourceType sourceType,
        Guid? workflowId,
        IDocumentWorkflowInstancesAppService documentWorkflowInstancesAppService,
        IDocumentFilesAppService documentFilesAppService)
    {
        if (IsWorkflowDocument(sourceType, workflowId)
            && await HasDisplayPdfAsync(documentId, documentWorkflowInstancesAppService))
        {
            return true;
        }

        return await HasFirstPdfAsync(documentId, documentFilesAppService);
    }

    private static async Task<string?> LoadFirstPdfDataUrlAsync(
        Guid documentId,
        IDocumentFilesAppService documentFilesAppService,
        IDocumentPdfViewerAppService documentPdfViewerAppService,
        string watermarkAction)
    {
        var path = await GetFirstPdfPathAsync(documentId, documentFilesAppService);
        if (string.IsNullOrEmpty(path))
        {
            return null;
        }

        var fileBytes = await documentPdfViewerAppService.GetWatermarkedPdfAsync(new GetWatermarkedPdfInput
        {
            BlobPath = path,
            WatermarkAction = watermarkAction
        });

        var base64 = Convert.ToBase64String(fileBytes);
        return $"data:application/pdf;base64,{base64}";
    }

    private static async Task<bool> HasFirstPdfAsync(Guid documentId, IDocumentFilesAppService documentFilesAppService)
    {
        var path = await GetFirstPdfPathAsync(documentId, documentFilesAppService);
        return !string.IsNullOrEmpty(path);
    }

    private static async Task<string?> GetFirstPdfPathAsync(Guid documentId, IDocumentFilesAppService documentFilesAppService)
    {
        var documentFilesResult = await documentFilesAppService.GetListAsync(new GetDocumentFilesInput
        {
            DocumentId = documentId,
            MaxResultCount = LimitedResultRequestDto.DefaultMaxResultCount,
            SkipCount = 0
        });

        var pdfFile = documentFilesResult.Items
            .Where(f => f.DocumentFile != null
                && !string.IsNullOrEmpty(f.DocumentFile.Path)
                && FileHelper.IsPdfFileExtension(f.DocumentFile.Name))
            .OrderByDescending(f => f.DocumentFile.UploadedAt)
            .FirstOrDefault();

        return pdfFile?.DocumentFile?.Path;
    }
}
