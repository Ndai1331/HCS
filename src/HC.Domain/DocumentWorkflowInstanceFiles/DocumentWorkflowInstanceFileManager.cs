using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;

namespace HC.DocumentWorkflowInstanceFiles;

public abstract class DocumentWorkflowInstanceFileManagerBase : DomainService
{
    protected IDocumentWorkflowInstanceFileRepository _documentWorkflowInstanceFileRepository;

    public DocumentWorkflowInstanceFileManagerBase(IDocumentWorkflowInstanceFileRepository documentWorkflowInstanceFileRepository)
    {
        _documentWorkflowInstanceFileRepository = documentWorkflowInstanceFileRepository;
    }

    public virtual async Task<DocumentWorkflowInstanceFile> CreateAsync(Guid documentWorkflowInstanceId, Guid documentFileId)
    {
        Check.NotNull(documentFileId, nameof(documentFileId));
        var documentWorkflowInstanceFile = new DocumentWorkflowInstanceFile(GuidGenerator.Create(), documentWorkflowInstanceId, documentFileId);
        return await _documentWorkflowInstanceFileRepository.InsertAsync(documentWorkflowInstanceFile);
    }

    public virtual async Task<DocumentWorkflowInstanceFile> UpdateAsync(Guid id, Guid documentWorkflowInstanceId, Guid documentFileId)
    {
        Check.NotNull(documentFileId, nameof(documentFileId));
        var documentWorkflowInstanceFile = await _documentWorkflowInstanceFileRepository.GetAsync(id);
        documentWorkflowInstanceFile.DocumentWorkflowInstanceId = documentWorkflowInstanceId;
        documentWorkflowInstanceFile.DocumentFileId = documentFileId;
        return await _documentWorkflowInstanceFileRepository.UpdateAsync(documentWorkflowInstanceFile);
    }
}