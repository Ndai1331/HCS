using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;
using Volo.Abp.Data;

namespace HC.DocumentAssignments;

public abstract class DocumentAssignmentManagerBase : DomainService
{
    protected IDocumentAssignmentRepository _documentAssignmentRepository;

    public DocumentAssignmentManagerBase(IDocumentAssignmentRepository documentAssignmentRepository)
    {
        _documentAssignmentRepository = documentAssignmentRepository;
    }

    public virtual async Task<DocumentAssignment> CreateAsync(Guid documentId, Guid? workflowStepTemplateId, Guid receiverUserId, int stepOrder, string actionType, string status, DateTime assignedAt, DateTime processedAt, bool isCurrent, Guid? documentFileResultId = null)
    {
        Check.NotNull(documentId, nameof(documentId));
        Check.NotNull(receiverUserId, nameof(receiverUserId));
        Check.Range(stepOrder, nameof(stepOrder), DocumentAssignmentConsts.StepOrderMinLength, DocumentAssignmentConsts.StepOrderMaxLength);
        Check.NotNullOrWhiteSpace(actionType, nameof(actionType));
        Check.Length(actionType, nameof(actionType), DocumentAssignmentConsts.ActionTypeMaxLength);
        Check.NotNullOrWhiteSpace(status, nameof(status));
        Check.Length(status, nameof(status), DocumentAssignmentConsts.StatusMaxLength);
        Check.NotNull(assignedAt, nameof(assignedAt));
        Check.NotNull(processedAt, nameof(processedAt));
        var documentAssignment = new DocumentAssignment(GuidGenerator.Create(), documentId, workflowStepTemplateId, receiverUserId, stepOrder, actionType, status, assignedAt, processedAt, isCurrent);
        documentAssignment.DocumentFileResultId = documentFileResultId;
        return await _documentAssignmentRepository.InsertAsync(documentAssignment);
    }

    public virtual async Task<DocumentAssignment> UpdateAsync(Guid id, Guid documentId, Guid? workflowStepTemplateId, Guid receiverUserId, int stepOrder, string actionType, string status, DateTime assignedAt, DateTime processedAt, bool isCurrent, Guid? documentFileResultId = null, [CanBeNull] string? concurrencyStamp = null)
    {
        Check.NotNull(documentId, nameof(documentId));
        Check.NotNull(receiverUserId, nameof(receiverUserId));
        Check.Range(stepOrder, nameof(stepOrder), DocumentAssignmentConsts.StepOrderMinLength, DocumentAssignmentConsts.StepOrderMaxLength);
        Check.NotNullOrWhiteSpace(actionType, nameof(actionType));
        Check.Length(actionType, nameof(actionType), DocumentAssignmentConsts.ActionTypeMaxLength);
        Check.NotNullOrWhiteSpace(status, nameof(status));
        Check.Length(status, nameof(status), DocumentAssignmentConsts.StatusMaxLength);
        Check.NotNull(assignedAt, nameof(assignedAt));
        Check.NotNull(processedAt, nameof(processedAt));
        var documentAssignment = await _documentAssignmentRepository.GetAsync(id);
        documentAssignment.DocumentId = documentId;
        documentAssignment.WorkflowStepTemplateId = workflowStepTemplateId;
        documentAssignment.ReceiverUserId = receiverUserId;
        documentAssignment.StepOrder = stepOrder;
        documentAssignment.ActionType = actionType;
        documentAssignment.Status = status;
        documentAssignment.AssignedAt = assignedAt;
        documentAssignment.ProcessedAt = processedAt;
        documentAssignment.IsCurrent = isCurrent;
        documentAssignment.DocumentFileResultId = documentFileResultId;
        documentAssignment.SetConcurrencyStampIfNotNull(concurrencyStamp);
        return await _documentAssignmentRepository.UpdateAsync(documentAssignment);
    }
}