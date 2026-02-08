using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;

namespace HC.DocumentWorkflowInstanceLogss;

public abstract class DocumentWorkflowInstanceLogsManagerBase : DomainService
{
    protected IDocumentWorkflowInstanceLogsRepository _documentWorkflowInstanceLogsRepository;

    public DocumentWorkflowInstanceLogsManagerBase(IDocumentWorkflowInstanceLogsRepository documentWorkflowInstanceLogsRepository)
    {
        _documentWorkflowInstanceLogsRepository = documentWorkflowInstanceLogsRepository;
    }

    public virtual async Task<DocumentWorkflowInstanceLogs> CreateAsync(Guid documentWorkflowInstanceId, Guid? documentAssignmentId, Guid? actorUserId, string action, string? actorRole = null, string? fromStatus = null, string? toStatus = null, string? note = null)
    {
        Check.NotNullOrWhiteSpace(action, nameof(action));
        Check.Length(action, nameof(action), DocumentWorkflowInstanceLogsConsts.ActionMaxLength);
        Check.Length(actorRole, nameof(actorRole), DocumentWorkflowInstanceLogsConsts.ActorRoleMaxLength);
        Check.Length(fromStatus, nameof(fromStatus), DocumentWorkflowInstanceLogsConsts.FromStatusMaxLength);
        Check.Length(toStatus, nameof(toStatus), DocumentWorkflowInstanceLogsConsts.ToStatusMaxLength);
        var documentWorkflowInstanceLogs = new DocumentWorkflowInstanceLogs(GuidGenerator.Create(), documentWorkflowInstanceId, documentAssignmentId, actorUserId, action, actorRole, fromStatus, toStatus, note);
        return await _documentWorkflowInstanceLogsRepository.InsertAsync(documentWorkflowInstanceLogs);
    }

    public virtual async Task<DocumentWorkflowInstanceLogs> UpdateAsync(Guid id, Guid documentWorkflowInstanceId, Guid? documentAssignmentId, Guid? actorUserId, string action, string? actorRole = null, string? fromStatus = null, string? toStatus = null, string? note = null)
    {
        Check.NotNullOrWhiteSpace(action, nameof(action));
        Check.Length(action, nameof(action), DocumentWorkflowInstanceLogsConsts.ActionMaxLength);
        Check.Length(actorRole, nameof(actorRole), DocumentWorkflowInstanceLogsConsts.ActorRoleMaxLength);
        Check.Length(fromStatus, nameof(fromStatus), DocumentWorkflowInstanceLogsConsts.FromStatusMaxLength);
        Check.Length(toStatus, nameof(toStatus), DocumentWorkflowInstanceLogsConsts.ToStatusMaxLength);
        var documentWorkflowInstanceLogs = await _documentWorkflowInstanceLogsRepository.GetAsync(id);
        documentWorkflowInstanceLogs.DocumentWorkflowInstanceId = documentWorkflowInstanceId;
        documentWorkflowInstanceLogs.DocumentAssignmentId = documentAssignmentId;
        documentWorkflowInstanceLogs.ActorUserId = actorUserId;
        documentWorkflowInstanceLogs.Action = action;
        documentWorkflowInstanceLogs.ActorRole = actorRole;
        documentWorkflowInstanceLogs.FromStatus = fromStatus;
        documentWorkflowInstanceLogs.ToStatus = toStatus;
        documentWorkflowInstanceLogs.Note = note;
        return await _documentWorkflowInstanceLogsRepository.UpdateAsync(documentWorkflowInstanceLogs);
    }
}