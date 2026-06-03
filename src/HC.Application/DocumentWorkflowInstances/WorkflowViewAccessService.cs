using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HC.DocumentAssignments;
using HC.Documents;
using HC.WorkflowStepAssignments;
using HC.WorkflowStepTemplates;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace HC.DocumentWorkflowInstances;

public class WorkflowViewAccessService : HCAppService, IWorkflowViewAccessService, ITransientDependency
{
    private static readonly string[] ActiveInstanceStatuses =
    {
        nameof(DocumentWorkflowInstanceStatus.IN_PROGRESS),
        nameof(DocumentWorkflowInstanceStatus.OVERDUE)
    };

    private readonly IDocumentWorkflowInstanceRepository _documentWorkflowInstanceRepository;
    private readonly IDocumentAssignmentRepository _documentAssignmentRepository;
    private readonly IRepository<Document, Guid> _documentRepository;
    private readonly IRepository<WorkflowStepAssignment, Guid> _workflowStepAssignmentRepository;
    private readonly IRepository<WorkflowStepTemplate, Guid> _workflowStepTemplateRepository;
    private readonly IWorkflowAssigneeResolver _workflowAssigneeResolver;

    public WorkflowViewAccessService(
        IDocumentWorkflowInstanceRepository documentWorkflowInstanceRepository,
        IDocumentAssignmentRepository documentAssignmentRepository,
        IRepository<Document, Guid> documentRepository,
        IRepository<WorkflowStepAssignment, Guid> _workflowStepAssignmentRepositoryParam,
        IRepository<WorkflowStepTemplate, Guid> workflowStepTemplateRepository,
        IWorkflowAssigneeResolver workflowAssigneeResolver)
    {
        _documentWorkflowInstanceRepository = documentWorkflowInstanceRepository;
        _documentAssignmentRepository = documentAssignmentRepository;
        _documentRepository = documentRepository;
        _workflowStepAssignmentRepository = _workflowStepAssignmentRepositoryParam;
        _workflowStepTemplateRepository = workflowStepTemplateRepository;
        _workflowAssigneeResolver = workflowAssigneeResolver;
    }

    public async Task<bool> CanUserViewWorkflowDocumentAsync(Guid workflowInstanceId, Guid userId)
    {
        var instance = await _documentWorkflowInstanceRepository.GetAsync(workflowInstanceId);
        return await CanUserViewWorkflowDocumentInternalAsync(instance, userId);
    }

    private async Task<bool> CanUserViewWorkflowDocumentInternalAsync(DocumentWorkflowInstance instance, Guid userId)
    {
        if (instance.CreatorId == userId)
        {
            return true;
        }

        var assignmentQueryable = await _documentAssignmentRepository.GetQueryableAsync();
        var hasWorkflowAssignment = await AsyncExecuter.AnyAsync(
            assignmentQueryable.Where(a =>
                a.DocumentId == instance.DocumentId
                && a.ReceiverUserId == userId
                && a.WorkflowStepTemplateId != null
                && a.CreationTime >= instance.StartedAt));

        if (hasWorkflowAssignment)
        {
            return true;
        }

        return await UserMatchesAnyUnlockedViewStepAsync(instance, userId);
    }

    public async Task<HashSet<Guid>> GetViewEligibleDocumentIdsAsync(Guid userId)
    {
        var result = new HashSet<Guid>();

        var instanceQueryable = await _documentWorkflowInstanceRepository.GetQueryableAsync();
        var documentQueryable = await _documentRepository.GetQueryableAsync();

        var activeInstances = await AsyncExecuter.ToListAsync(
            from inst in instanceQueryable
            join doc in documentQueryable on inst.DocumentId equals doc.Id
            where doc.SourceType == DocumentSourceType.Workflow
            where ActiveInstanceStatuses.Contains(inst.Status)
            select inst);

        if (activeInstances.Count == 0)
        {
            return result;
        }

        foreach (var instance in activeInstances)
        {
            if (await UserMatchesAnyUnlockedViewStepAsync(instance, userId))
            {
                result.Add(instance.DocumentId);
            }
        }

        return result;
    }

    private async Task<bool> UserMatchesAnyUnlockedViewStepAsync(DocumentWorkflowInstance instance, Guid userId)
    {
        var unlockedStepIds = WorkflowSubmissionHelper.GetUnlockedViewStepTemplateIds(instance);
        if (unlockedStepIds.Count == 0)
        {
            return false;
        }

        var submitterUserId = instance.CreatorId;
        if (!submitterUserId.HasValue)
        {
            return false;
        }

        var stepTemplates = await _workflowStepTemplateRepository.GetListAsync(x => unlockedStepIds.Contains(x.Id));
        var viewSteps = stepTemplates
            .Where(s => WorkflowStepNavigationHelper.IsViewStep(s.Type))
            .ToList();

        if (viewSteps.Count == 0)
        {
            return false;
        }

        var viewStepIds = viewSteps.Select(s => s.Id).ToList();
        var templateAssignments = await _workflowStepAssignmentRepository.GetListAsync(
            x => x.StepId.HasValue && viewStepIds.Contains(x.StepId.Value) && x.IsActive);

        foreach (var step in viewSteps)
        {
            var stepAssignments = templateAssignments.Where(a => a.StepId == step.Id).ToList();
            if (await UserMatchesViewStepAssignmentsAsync(stepAssignments, submitterUserId.Value, userId))
            {
                return true;
            }
        }

        return false;
    }

    private async Task<bool> UserMatchesViewStepAssignmentsAsync(
        IReadOnlyList<WorkflowStepAssignment> stepAssignments,
        Guid submitterUserId,
        Guid userId)
    {
        foreach (var assignment in stepAssignments)
        {
            if (!assignment.IsActive)
            {
                continue;
            }

            if (assignment.AssigneeType == WorkflowStepAssigneeTypeNames.RoleInSubmitterOrganizationUnit
                && assignment.RoleId.HasValue)
            {
                var candidates = await _workflowAssigneeResolver.ResolveCandidatesByRoleAsync(
                    assignment.RoleId.Value, submitterUserId, assignment.IsPrimary);
                if (candidates.Any(c => c.UserId == userId))
                {
                    return true;
                }
            }
            else if (assignment.DefaultUserId == userId)
            {
                return true;
            }
        }

        return false;
    }
}
