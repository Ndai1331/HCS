using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;
using Volo.Abp.Data;

namespace HC.DocumentFiles;

public abstract class DocumentFileManagerBase : DomainService
{
    protected IDocumentFileRepository _documentFileRepository;

    public DocumentFileManagerBase(IDocumentFileRepository documentFileRepository)
    {
        _documentFileRepository = documentFileRepository;
    }

    public virtual async Task<DocumentFile> CreateAsync(Guid? documentId, string name, bool isSigned, DateTime uploadedAt, string? path = null, string? hash = null)
    {
        Check.NotNullOrWhiteSpace(name, nameof(name));
        Check.NotNull(uploadedAt, nameof(uploadedAt));
        var documentFile = new DocumentFile(GuidGenerator.Create(), documentId, name, isSigned, uploadedAt, path, hash);
        return await _documentFileRepository.InsertAsync(documentFile);
    }

    public virtual async Task<DocumentFile> UpdateAsync(Guid id, Guid? documentId, string name, bool isSigned, DateTime uploadedAt, string? path = null, string? hash = null, [CanBeNull] string? concurrencyStamp = null)
    {
        Check.NotNullOrWhiteSpace(name, nameof(name));
        Check.NotNull(uploadedAt, nameof(uploadedAt));
        var documentFile = await _documentFileRepository.GetAsync(id);
        documentFile.DocumentId = documentId;
        documentFile.Name = name;
        documentFile.IsSigned = isSigned;
        documentFile.UploadedAt = uploadedAt;
        documentFile.Path = path;
        documentFile.Hash = hash;
        documentFile.SetConcurrencyStampIfNotNull(concurrencyStamp);
        return await _documentFileRepository.UpdateAsync(documentFile);
    }
}