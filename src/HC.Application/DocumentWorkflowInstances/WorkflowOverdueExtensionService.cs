using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HC.DocumentAssignments;
using HC.Documents;
using HC.DocumentWorkflowInstanceLogss;
using HC.Permissions;
using HC.WorkflowStepTemplates;
using HC.Workflows;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;

namespace HC.DocumentWorkflowInstances;

[Authorize(HCPermissions.DocumentAssignments.Default)]
public class WorkflowOverdueExtensionService : HCAppService, IWorkflowOverdueExtensionService, ITransientDependency
{
    private readonly IDocumentWorkflowInstanceRepository _documentWorkflowInstanceRepository;
    private readonly IDocumentAssignmentRepository _documentAssignmentRepository;
    private readonly DocumentWorkflowInstanceLogsManager _documentWorkflowInstanceLogsManager;
    private readonly IRepository<Document, Guid> _documentRepository;
    private readonly IRepository<Workflow, Guid> _workflowRepository;
    private readonly IRepository<WorkflowStepTemplate, Guid> _workflowStepTemplateRepository;
    private readonly IRepository<IdentityUser, Guid> _identityUserRepository;
    private readonly IDocumentWorkflowInstanceExtensionRepository _extensionRepository;
    private readonly IWorkflowSlaService _workflowSlaService;
    private readonly IWorkflowNotificationService _workflowNotificationService;
    private readonly WorkflowSigningOptions _workflowSigningOptions;

    public WorkflowOverdueExtensionService(
        IDocumentWorkflowInstanceRepository documentWorkflowInstanceRepository,
        IDocumentAssignmentRepository documentAssignmentRepository,
        DocumentWorkflowInstanceLogsManager documentWorkflowInstanceLogsManager,
        IRepository<Document, Guid> documentRepository,
        IRepository<Workflow, Guid> workflowRepository,
        IRepository<WorkflowStepTemplate, Guid> workflowStepTemplateRepository,
        IRepository<IdentityUser, Guid> identityUserRepository,
        IDocumentWorkflowInstanceExtensionRepository extensionRepository,
        IWorkflowSlaService workflowSlaService,
        IWorkflowNotificationService workflowNotificationService,
        IOptions<WorkflowSigningOptions> workflowSigningOptions)
    {
        _documentWorkflowInstanceRepository = documentWorkflowInstanceRepository;
        _documentAssignmentRepository = documentAssignmentRepository;
        _documentWorkflowInstanceLogsManager = documentWorkflowInstanceLogsManager;
        _documentRepository = documentRepository;
        _workflowRepository = workflowRepository;
        _workflowStepTemplateRepository = workflowStepTemplateRepository;
        _identityUserRepository = identityUserRepository;
        _extensionRepository = extensionRepository;
        _workflowSlaService = workflowSlaService;
        _workflowNotificationService = workflowNotificationService;
        _workflowSigningOptions = workflowSigningOptions.Value;
    }

    public async Task<WorkflowOverdueCheckResultDto> CheckAndHandleOverdueAsync(Guid workflowInstanceId)
    {
        if (!CurrentUser.Id.HasValue)
        {
            throw new Volo.Abp.UserFriendlyException(L["NotAuthorizedForThisAction"]);
        }

        var result = new WorkflowOverdueCheckResultDto
        {
            IsOverdue = false,
            AllowReturn = false
        };

        try
        {
            var instance = await _documentWorkflowInstanceRepository.GetAsync(workflowInstanceId);
            var currentUserId = CurrentUser.Id.Value;
            var isCreator = instance.CreatorId == currentUserId;
            if (!isCreator)
            {
                var hasAssignment = await _documentAssignmentRepository.AnyAsync(
                    x => x.DocumentId == instance.DocumentId && x.ReceiverUserId == currentUserId);
                if (!hasAssignment)
                {
                    Logger.LogWarning(
                        "[OVERDUE_AUTH] User {UserId} attempted to check overdue for workflow {InstanceId} " +
                        "but is not creator or assignment receiver.",
                        currentUserId, workflowInstanceId);
                    throw new Volo.Abp.UserFriendlyException(L["NotAuthorizedForThisAction"]);
                }
            }

            var currentStep = await _workflowStepTemplateRepository.GetAsync(instance.CurrentStepId);
            result.AllowReturn = currentStep.AllowReturn;

            var terminalStatuses = new[]
            {
                nameof(DocumentWorkflowInstanceStatus.COMPLETED),
                nameof(DocumentWorkflowInstanceStatus.REJECTED),
                nameof(DocumentWorkflowInstanceStatus.CANCELLED),
                nameof(DocumentWorkflowInstanceStatus.RETURNED)
            };

            result.WorkflowStatus = instance.Status;
            result.ExtensionCount = instance.ExtensionCount;
            result.TotalExtensionBusinessDays = instance.TotalExtensionBusinessDays;

            if (instance.Status == nameof(DocumentWorkflowInstanceStatus.CANCELLED))
            {
                result.IsOverdue = true;
            }
            else if (instance.Status == nameof(DocumentWorkflowInstanceStatus.OVERDUE)
                     && instance.OverdueAt.HasValue)
            {
                result.IsOverdue = true;
                result.GraceCancelAt = BusinessDayCalculator.GetOverdueGraceCancelAt(instance.OverdueAt.Value);
                result.CanExtend = Clock.Now < result.GraceCancelAt.Value
                    && await CanUserExtendWorkflowAsync(instance, currentUserId);
            }
            else if (instance.FinishedAt > DateTime.MinValue
                     && instance.FinishedAt <= Clock.Now
                     && !terminalStatuses.Contains(instance.Status))
            {
                result.IsOverdue = true;
                result.CanExtend = await CanUserExtendWorkflowAsync(instance, currentUserId)
                    && IsNearDeadlineForExtension(instance);
            }
            else if (instance.Status == nameof(DocumentWorkflowInstanceStatus.IN_PROGRESS)
                     && instance.FinishedAt > DateTime.MinValue)
            {
                result.CanExtend = await CanUserExtendWorkflowAsync(instance, currentUserId)
                    && IsNearDeadlineForExtension(instance);
            }
        }
        catch (Volo.Abp.UserFriendlyException)
        {
            throw;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error checking overdue for workflow instance {InstanceId}", workflowInstanceId);
            throw new Volo.Abp.UserFriendlyException(L["OverdueCheckFailed"]);
        }

        return result;
    }

    public async Task ExtendWorkflowAsync(ExtendWorkflowInput input)
    {
        if (input.WorkflowInstanceId == Guid.Empty)
        {
            throw new Volo.Abp.UserFriendlyException(L["The {0} field is required.", "WorkflowInstanceId"]);
        }

        if (input.ExtensionBusinessDays < 1)
        {
            throw new Volo.Abp.UserFriendlyException(L["ExtensionBusinessDaysMustBePositive"]);
        }

        if (string.IsNullOrWhiteSpace(input.Reason))
        {
            throw new Volo.Abp.UserFriendlyException(L["ExtensionReasonRequired"]);
        }

        var instance = await _documentWorkflowInstanceRepository.GetAsync(input.WorkflowInstanceId);
        var currentUserId = CurrentUser.Id!.Value;

        if (!await CanUserExtendWorkflowAsync(instance, currentUserId))
        {
            throw new Volo.Abp.UserFriendlyException(L["NotAuthorizedToExtendWorkflow"]);
        }

        var allowedStatuses = new[]
        {
            nameof(DocumentWorkflowInstanceStatus.IN_PROGRESS),
            nameof(DocumentWorkflowInstanceStatus.OVERDUE)
        };

        if (!allowedStatuses.Contains(instance.Status))
        {
            throw new Volo.Abp.UserFriendlyException(L["WorkflowCannotBeExtended"]);
        }

        if (instance.Status == nameof(DocumentWorkflowInstanceStatus.OVERDUE))
        {
            if (!instance.OverdueAt.HasValue
                || Clock.Now >= BusinessDayCalculator.GetOverdueGraceCancelAt(instance.OverdueAt.Value))
            {
                throw new Volo.Abp.UserFriendlyException(L["WorkflowOverdueGraceExpired"]);
            }
        }
        else if (!IsNearDeadlineForExtension(instance))
        {
            throw new Volo.Abp.UserFriendlyException(L["WorkflowExtensionNotNearDeadline"]);
        }

        var now = Clock.Now;
        var previousFinishedAt = instance.FinishedAt;
        var previousStatus = instance.Status;
        var newFinishedAt = _workflowSlaService.CalculateExtensionDeadline(now, previousFinishedAt, input.ExtensionBusinessDays);

        instance.FinishedAt = newFinishedAt;
        if (instance.Status == nameof(DocumentWorkflowInstanceStatus.OVERDUE))
        {
            instance.Status = nameof(DocumentWorkflowInstanceStatus.IN_PROGRESS);
            instance.OverdueAt = null;
        }

        instance.ExtensionCount++;
        instance.TotalExtensionBusinessDays += input.ExtensionBusinessDays;
        await _documentWorkflowInstanceRepository.UpdateAsync(instance);

        await _extensionRepository.InsertAsync(new DocumentWorkflowInstanceExtension(
            GuidGenerator.Create(),
            instance.Id,
            currentUserId,
            input.ExtensionBusinessDays,
            previousFinishedAt,
            newFinishedAt,
            input.Reason.Trim(),
            previousStatus,
            instance.Status));

        var fromText = previousFinishedAt > DateTime.MinValue
            ? previousFinishedAt.ToString("dd/MM/yyyy HH:mm")
            : "---";
        var toText = newFinishedAt > DateTime.MinValue
            ? newFinishedAt.ToString("dd/MM/yyyy HH:mm")
            : "---";
        var detail = L["WorkflowLogExtensionDetail", input.ExtensionBusinessDays, fromText, toText];
        var extensionLogNote = string.IsNullOrWhiteSpace(input.Reason)
            ? detail
            : $"{input.Reason.Trim()}{Environment.NewLine}{detail}";

        await _documentWorkflowInstanceLogsManager.CreateAsync(
            instance.Id,
            null,
            currentUserId,
            nameof(WorkflowInstanceLogAction.EXTEND_WORKFLOW),
            null,
            previousStatus,
            instance.Status,
            extensionLogNote);

        var pendingSigners = await _documentAssignmentRepository.GetListAsync(
            x => x.DocumentId == instance.DocumentId
                 && x.WorkflowStepTemplateId == instance.CurrentStepId
                 && x.Status == nameof(DocumentAssignmentStatus.PENDING)
                 && x.IsCurrent);

        if (pendingSigners.Any())
        {
            var document = await _documentRepository.GetAsync(instance.DocumentId);
            var workflow = await _workflowRepository.GetAsync(instance.WorkflowId);
            var currentStep = await _workflowStepTemplateRepository.GetAsync(instance.CurrentStepId);
            await _workflowNotificationService.SendWorkflowNotificationAsync(
                document,
                pendingSigners.Select(a => a.ReceiverUserId).Distinct().ToList(),
                "WorkflowExtended",
                $"WorkflowExtendedMessage|{document.StorageNumber}|{document.Title}|{workflow.Name}|{currentStep.Name}|{input.ExtensionBusinessDays}");
        }
    }

    public async Task<WorkflowExtensionSummaryDto> GetWorkflowExtensionSummaryAsync(Guid workflowInstanceId)
    {
        var instance = await _documentWorkflowInstanceRepository.GetAsync(workflowInstanceId);
        var extensions = await _extensionRepository.GetListByInstanceIdAsync(workflowInstanceId);

        var userIds = extensions.Select(e => e.ExtendedByUserId).Distinct().ToList();
        var users = userIds.Any()
            ? await _identityUserRepository.GetListAsync(x => userIds.Contains(x.Id))
            : new List<IdentityUser>();
        var userDict = users.ToDictionary(u => u.Id);

        return new WorkflowExtensionSummaryDto
        {
            ExtensionCount = instance.ExtensionCount,
            TotalExtensionBusinessDays = instance.TotalExtensionBusinessDays,
            History = extensions.Select(e =>
            {
                userDict.TryGetValue(e.ExtendedByUserId, out var user);
                return new WorkflowExtensionHistoryItemDto
                {
                    Id = e.Id,
                    CreationTime = e.CreationTime,
                    ExtendedByUserId = e.ExtendedByUserId,
                    ExtendedByUserName = user != null ? $"{user.Surname} {user.Name}".Trim() : user?.UserName,
                    ExtensionBusinessDays = e.ExtensionBusinessDays,
                    PreviousFinishedAt = e.PreviousFinishedAt,
                    NewFinishedAt = e.NewFinishedAt,
                    Reason = e.Reason
                };
            }).ToList()
        };
    }

    private async Task<bool> CanUserExtendWorkflowAsync(DocumentWorkflowInstance instance, Guid currentUserId)
    {
        if (IsWorkflowAdminUser())
        {
            return true;
        }

        return await _documentAssignmentRepository.AnyAsync(a =>
            a.DocumentId == instance.DocumentId
            && a.WorkflowStepTemplateId == instance.CurrentStepId
            && a.ReceiverUserId == currentUserId
            && a.Status == nameof(DocumentAssignmentStatus.PENDING)
            && a.IsCurrent);
    }

    private bool IsWorkflowAdminUser()
    {
        return CurrentUser.IsInRole("admin") || CurrentUser.IsInRole("ADMIN");
    }

    private bool IsNearDeadlineForExtension(DocumentWorkflowInstance instance)
    {
        if (instance.FinishedAt <= DateTime.MinValue)
        {
            return false;
        }

        if (instance.Status == nameof(DocumentWorkflowInstanceStatus.OVERDUE))
        {
            return true;
        }

        var threshold = Clock.Now.AddHours(_workflowSigningOptions.NearDeadlineHours);
        return instance.FinishedAt <= threshold;
    }
}
