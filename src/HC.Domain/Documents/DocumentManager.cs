using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;
using Volo.Abp.Data;

namespace HC.Documents;

public abstract class DocumentManagerBase : DomainService
{
    protected IDocumentRepository _documentRepository;

    public DocumentManagerBase(IDocumentRepository documentRepository)
    {
        _documentRepository = documentRepository;
    }

    public virtual async Task<Document> CreateAsync(Guid? fieldId, Guid? unitId, Guid? workflowId, Guid? statusId, Guid typeId, Guid urgencyLevelId, Guid secrecyLevelId, string title, DateTime completedTime, string storageNumber, DateTime incommingDate, string? no = null, string? currentStatus = null, DocumentSourceType sourceType = DocumentSourceType.Archive)
    {
        Check.NotNull(typeId, nameof(typeId));
        Check.NotNull(urgencyLevelId, nameof(urgencyLevelId));
        Check.NotNull(secrecyLevelId, nameof(secrecyLevelId));
        Check.NotNullOrWhiteSpace(title, nameof(title));
        Check.NotNull(completedTime, nameof(completedTime));
        Check.NotNullOrWhiteSpace(storageNumber, nameof(storageNumber));
        Check.Length(storageNumber, nameof(storageNumber), DocumentConsts.StorageNumberMaxLength);
        Check.NotNull(incommingDate, nameof(incommingDate));
        Check.Length(no, nameof(no), DocumentConsts.NoMaxLength);
        Check.Length(currentStatus, nameof(currentStatus), DocumentConsts.CurrentStatusMaxLength);
        var document = new Document(GuidGenerator.Create(), fieldId, unitId, workflowId, statusId, typeId, urgencyLevelId, secrecyLevelId, title, completedTime, storageNumber, incommingDate, no, currentStatus, sourceType);
        return await _documentRepository.InsertAsync(document);
    }

    public virtual async Task<Document> UpdateAsync(Guid id, Guid? fieldId, Guid? unitId, Guid? workflowId, Guid? statusId, Guid typeId, Guid urgencyLevelId, Guid secrecyLevelId, string title, DateTime completedTime, string storageNumber, DateTime incommingDate, string? no = null, string? currentStatus = null, [CanBeNull] string? concurrencyStamp = null)
    {
        Check.NotNull(typeId, nameof(typeId));
        Check.NotNull(urgencyLevelId, nameof(urgencyLevelId));
        Check.NotNull(secrecyLevelId, nameof(secrecyLevelId));
        Check.NotNullOrWhiteSpace(title, nameof(title));
        Check.NotNull(completedTime, nameof(completedTime));
        Check.NotNullOrWhiteSpace(storageNumber, nameof(storageNumber));
        Check.Length(storageNumber, nameof(storageNumber), DocumentConsts.StorageNumberMaxLength);
        Check.NotNull(incommingDate, nameof(incommingDate));
        Check.Length(no, nameof(no), DocumentConsts.NoMaxLength);
        Check.Length(currentStatus, nameof(currentStatus), DocumentConsts.CurrentStatusMaxLength);
        var document = await _documentRepository.GetAsync(id);
        document.FieldId = fieldId;
        document.UnitId = unitId;
        document.WorkflowId = workflowId;
        document.StatusId = statusId;
        document.TypeId = typeId;
        document.UrgencyLevelId = urgencyLevelId;
        document.SecrecyLevelId = secrecyLevelId;
        document.Title = title;
        document.CompletedTime = completedTime;
        document.StorageNumber = storageNumber;
        document.IncommingDate = incommingDate;
        document.No = no;
        document.CurrentStatus = currentStatus;
        document.SetConcurrencyStampIfNotNull(concurrencyStamp);
        return await _documentRepository.UpdateAsync(document);
    }
}