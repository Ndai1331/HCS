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
    /// <summary>
    /// Workflow instances eligible for VIEW-step document visibility (all non-draft statuses).
    /// </summary>
    private static readonly string[] ViewEligibleInstanceStatuses =
    {
        nameof(DocumentWorkflowInstanceStatus.IN_PROGRESS),
        nameof(DocumentWorkflowInstanceStatus.OVERDUE),
        nameof(DocumentWorkflowInstanceStatus.COMPLETED),
        nameof(DocumentWorkflowInstanceStatus.REJECTED),
        nameof(DocumentWorkflowInstanceStatus.RETURNED),
        nameof(DocumentWorkflowInstanceStatus.CANCELLED)
    };

    private readonly IDocumentWorkflowInstanceRepository _documentWorkflowInstanceRepository;
    private readonly IDocumentAssignmentRepository _documentAssignmentRepository;
    private readonly IRepository<Document, Guid> _documentRepository;
    private readonly IRepository<WorkflowStepAssignment, Guid> _workflowStepAssignmentRepository;
    private readonly IRepository<WorkflowStepTemplate, Guid> _workflowStepTemplateRepository;
    private readonly IWorkflowViewScopeResolver _workflowViewScopeResolver;
    private readonly IWorkflowCommittedStepsQueryService _workflowCommittedStepsQueryService;

    public WorkflowViewAccessService(
        IDocumentWorkflowInstanceRepository documentWorkflowInstanceRepository,
        IDocumentAssignmentRepository documentAssignmentRepository,
        IRepository<Document, Guid> documentRepository,
        IRepository<WorkflowStepAssignment, Guid> _workflowStepAssignmentRepositoryParam,
        IRepository<WorkflowStepTemplate, Guid> workflowStepTemplateRepository,
        IWorkflowViewScopeResolver workflowViewScopeResolver,
        IWorkflowCommittedStepsQueryService workflowCommittedStepsQueryService)
    {
        _documentWorkflowInstanceRepository = documentWorkflowInstanceRepository;
        _documentAssignmentRepository = documentAssignmentRepository;
        _documentRepository = documentRepository;
        _workflowStepAssignmentRepository = _workflowStepAssignmentRepositoryParam;
        _workflowStepTemplateRepository = workflowStepTemplateRepository;
        _workflowViewScopeResolver = workflowViewScopeResolver;
        _workflowCommittedStepsQueryService = workflowCommittedStepsQueryService;
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

        var viewEligibleInstances = await AsyncExecuter.ToListAsync(
            from inst in instanceQueryable
            join doc in documentQueryable on inst.DocumentId equals doc.Id
            where doc.SourceType == DocumentSourceType.Workflow
            where ViewEligibleInstanceStatuses.Contains(inst.Status)
            select inst);

        if (viewEligibleInstances.Count == 0)
        {
            return result;
        }

        foreach (var instance in viewEligibleInstances)
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
        var unlockedStepIds = await GetEffectiveUnlockedViewStepIdsAsync(instance);
        if (unlockedStepIds.Count == 0)
        {
            return false;
        }

        var submitterUserId = instance.CreatorId;
        var instanceViewScopes = WorkflowSubmissionHelper.GetViewStepScopes(instance);

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
            instanceViewScopes.TryGetValue(step.Id, out var instanceScope);
            var viewerIds = await _workflowViewScopeResolver.ResolveViewerUserIdsAsync(
                stepAssignments,
                instanceScope,
                submitterUserId);

            if (viewerIds.Contains(userId))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Reads persisted unlock list, or infers VIEW steps from committed steps and workflow status (legacy instances).
    /// </summary>
    private async Task<List<Guid>> GetEffectiveUnlockedViewStepIdsAsync(DocumentWorkflowInstance instance)
    {
        var persisted = WorkflowSubmissionHelper.GetUnlockedViewStepTemplateIds(instance);
        if (persisted.Count > 0)
        {
            return persisted;
        }

        if (!ViewEligibleInstanceStatuses.Contains(instance.Status))
        {
            return persisted;
        }

        var committedSteps = await _workflowCommittedStepsQueryService.LoadCommittedWorkflowStepsOrderedAsync(instance);
        if (committedSteps.Count == 0)
        {
            return persisted;
        }

        if (string.Equals(
                instance.Status,
                nameof(DocumentWorkflowInstanceStatus.COMPLETED),
                StringComparison.OrdinalIgnoreCase))
        {
            return committedSteps
                .Where(s => WorkflowStepNavigationHelper.IsViewStep(s.Type))
                .Select(s => s.Id)
                .ToList();
        }

        var currentStep = committedSteps.FirstOrDefault(s => s.Id == instance.CurrentStepId);
        if (currentStep == null)
        {
            return persisted;
        }

        return committedSteps
            .Where(s => WorkflowStepNavigationHelper.IsViewStep(s.Type) && s.Order < currentStep.Order)
            .Select(s => s.Id)
            .ToList();
    }

}
