using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using HC.DocumentFiles;
using Volo.Abp.BlobStoring;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Volo.Abp.Timing;
using Volo.Abp.MultiTenancy;

namespace HC.DocumentWorkflowInstances;

public interface IWorkflowDocxSigningService
{
    Task<DocumentFile?> ResolveWorkingDocxFileAsync(DocumentFile file);

    Task<(DocumentFile DocxFile, DocumentFile PdfFile)> SaveDocxPdfPairAsync(
        byte[] docxBytes,
        byte[] pdfBytes,
        string docxFileName,
        string pdfFileName,
        Guid? documentId,
        bool isSigned,
        string docxBlobPrefix,
        string pdfBlobPrefix);

    Task<(DocumentFile DocxFile, DocumentFile PdfFile)> CopyDocxPdfPairAsync(
        DocumentFile sourcePdfOrDocx,
        string docxBlobPrefix,
        string pdfBlobPrefix);
}

/// <summary>
/// Manages DOCX working copies and derived PDF views for workflow signing.
/// </summary>
public sealed class WorkflowDocxSigningService : IWorkflowDocxSigningService, ITransientDependency
{
    private readonly IRepository<DocumentFile, Guid> _documentFileRepository;
    private readonly IBlobContainer _blobContainer;
    private readonly IGuidGenerator _guidGenerator;
    private readonly IClock _clock;
    private readonly ICurrentTenant _currentTenant;

    public WorkflowDocxSigningService(
        IRepository<DocumentFile, Guid> documentFileRepository,
        IBlobContainer blobContainer,
        IGuidGenerator guidGenerator,
        IClock clock,
        ICurrentTenant currentTenant)
    {
        _documentFileRepository = documentFileRepository;
        _blobContainer = blobContainer;
        _guidGenerator = guidGenerator;
        _clock = clock;
        _currentTenant = currentTenant;
    }

    public async Task<DocumentFile?> ResolveWorkingDocxFileAsync(DocumentFile file)
    {
        if (IsDocxExtension(file))
        {
            return file;
        }

        if (file.SourceDocxFileId.HasValue)
        {
            return await _documentFileRepository.FindAsync(file.SourceDocxFileId.Value);
        }

        return null;
    }

    public async Task<(DocumentFile DocxFile, DocumentFile PdfFile)> SaveDocxPdfPairAsync(
        byte[] docxBytes,
        byte[] pdfBytes,
        string docxFileName,
        string pdfFileName,
        Guid? documentId,
        bool isSigned,
        string docxBlobPrefix,
        string pdfBlobPrefix)
    {
        var now = _clock.Now;
        var docxBlobPath = $"{docxBlobPrefix}{Guid.NewGuid()}.docx";
        var pdfBlobPath = $"{pdfBlobPrefix}{Guid.NewGuid()}.pdf";

        await _blobContainer.SaveAsync(docxBlobPath, docxBytes);
        await _blobContainer.SaveAsync(pdfBlobPath, pdfBytes);

        var docxHash = Convert.ToBase64String(System.Security.Cryptography.SHA256.HashData(docxBytes));
        var pdfHash = Convert.ToBase64String(System.Security.Cryptography.SHA256.HashData(pdfBytes));

        var docxFile = new DocumentFile(
            _guidGenerator.Create(),
            documentId,
            docxFileName,
            isSigned,
            now,
            docxBlobPath,
            docxHash);
        docxFile.TenantId = _currentTenant.Id;

        var pdfFile = new DocumentFile(
            _guidGenerator.Create(),
            documentId,
            pdfFileName,
            isSigned,
            now,
            pdfBlobPath,
            pdfHash)
        {
            SourceDocxFileId = docxFile.Id
        };
        pdfFile.TenantId = _currentTenant.Id;

        docxFile.DerivedPdfFileId = pdfFile.Id;

        await _documentFileRepository.InsertAsync(docxFile, autoSave: true);
        await _documentFileRepository.InsertAsync(pdfFile, autoSave: true);

        return (docxFile, pdfFile);
    }

    public async Task<(DocumentFile DocxFile, DocumentFile PdfFile)> CopyDocxPdfPairAsync(
        DocumentFile sourcePdfOrDocx,
        string docxBlobPrefix,
        string pdfBlobPrefix)
    {
        var docxSource = await ResolveWorkingDocxFileAsync(sourcePdfOrDocx)
            ?? throw new InvalidOperationException("No working DOCX found for copy.");

        var pdfSource = sourcePdfOrDocx;
        if (IsDocxExtension(sourcePdfOrDocx))
        {
            if (!sourcePdfOrDocx.DerivedPdfFileId.HasValue)
            {
                throw new InvalidOperationException("DOCX source has no derived PDF.");
            }

            pdfSource = await _documentFileRepository.GetAsync(sourcePdfOrDocx.DerivedPdfFileId.Value);
        }

        var docxBytes = await _blobContainer.GetAllBytesAsync(docxSource.Path!);
        var pdfBytes = await _blobContainer.GetAllBytesAsync(pdfSource.Path!);

        return await SaveDocxPdfPairAsync(
            docxBytes,
            pdfBytes,
            docxSource.Name,
            pdfSource.Name,
            docxSource.DocumentId,
            docxSource.IsSigned || pdfSource.IsSigned,
            docxBlobPrefix,
            pdfBlobPrefix);
    }

    internal static bool IsDocxExtension(DocumentFile file)
    {
        var ext = Path.GetExtension(file.Name);
        if (string.IsNullOrWhiteSpace(ext) && !string.IsNullOrWhiteSpace(file.Path))
        {
            ext = Path.GetExtension(file.Path);
        }

        ext = ext?.ToLowerInvariant() ?? string.Empty;
        return ext is ".docx" or ".doc";
    }

    internal static bool IsPdfExtension(DocumentFile file)
    {
        var ext = Path.GetExtension(file.Name);
        if (string.IsNullOrWhiteSpace(ext) && !string.IsNullOrWhiteSpace(file.Path))
        {
            ext = Path.GetExtension(file.Path);
        }

        return string.Equals(ext, ".pdf", StringComparison.OrdinalIgnoreCase);
    }
}
