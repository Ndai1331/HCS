using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HC.DocumentFiles;
using HC.Permissions;
using HC.WorkflowStepAssignments;
using HC.WorkflowStepTemplates;
using HC.WorkflowTemplates;
using HC.Workflows;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;

namespace HC.DocumentWorkflowInstances;

[Authorize(HCPermissions.Documents.SubmitForSigning)]
public class WorkflowSubmitInfoQueryService : HCAppService, IWorkflowSubmitInfoQueryService, ITransientDependency
{
    private readonly IRepository<Workflow, Guid> _workflowRepository;
    private readonly IRepository<WorkflowTemplate, Guid> _workflowTemplateRepository;
    private readonly IRepository<WorkflowStepTemplate, Guid> _workflowStepTemplateRepository;
    private readonly IRepository<WorkflowStepAssignment, Guid> _workflowStepAssignmentRepository;
    private readonly IRepository<DocumentFile, Guid> _documentFileRepository;
    private readonly IRepository<IdentityUser, Guid> _identityUserRepository;
    private readonly IRepository<IdentityRole, Guid> _identityRoleRepository;
    private readonly IWorkflowAssigneeResolver _workflowAssigneeResolver;

    public WorkflowSubmitInfoQueryService(
        IRepository<Workflow, Guid> workflowRepository,
        IRepository<WorkflowTemplate, Guid> workflowTemplateRepository,
        IRepository<WorkflowStepTemplate, Guid> workflowStepTemplateRepository,
        IRepository<WorkflowStepAssignment, Guid> workflowStepAssignmentRepository,
        IRepository<DocumentFile, Guid> documentFileRepository,
        IRepository<IdentityUser, Guid> identityUserRepository,
        IRepository<IdentityRole, Guid> identityRoleRepository,
        IWorkflowAssigneeResolver workflowAssigneeResolver)
    {
        _workflowRepository = workflowRepository;
        _workflowTemplateRepository = workflowTemplateRepository;
        _workflowStepTemplateRepository = workflowStepTemplateRepository;
        _workflowStepAssignmentRepository = workflowStepAssignmentRepository;
        _documentFileRepository = documentFileRepository;
        _identityUserRepository = identityUserRepository;
        _identityRoleRepository = identityRoleRepository;
        _workflowAssigneeResolver = workflowAssigneeResolver;
    }

    public async Task<bool> IsDocumentSourceFileWordFormatAsync(Guid documentId)
    {
        var query = (await _documentFileRepository.GetQueryableAsync())
            .Where(f => f.DocumentId == documentId)
            .OrderBy(f => f.UploadedAt);
        var firstFile = await AsyncExecuter.FirstOrDefaultAsync(query);
        if (firstFile == null)
        {
            return false;
        }

        var path = firstFile.Path ?? firstFile.Name ?? "";
        return WorkflowSubmissionHelper.IsWordFormatPath(path);
    }

    public async Task<WorkflowSubmitInfoDto> GetWorkflowSubmitInfoAsync(Guid workflowId)
    {
        var workflow = await _workflowRepository.GetAsync(workflowId);

        var activeTemplate = await ResolveLatestWorkflowTemplateForSubmissionAsync(workflowId);
        if (activeTemplate == null)
        {
            throw new Volo.Abp.UserFriendlyException(L["NoActiveWorkflowTemplateFound"]);
        }

        var stepTemplates = await _workflowStepTemplateRepository.GetListAsync(
            x => x.WorkflowTemplateId == activeTemplate.Id && x.IsActive);
        stepTemplates = stepTemplates.OrderBy(x => x.Order).ToList();

        if (!stepTemplates.Any())
        {
            throw new Volo.Abp.UserFriendlyException(L["NoWorkflowStepsFound"]);
        }

        var stepIds = stepTemplates.Select(s => s.Id).ToList();
        var allAssignments = await _workflowStepAssignmentRepository.GetListAsync(
            x => stepIds.Contains(x.StepId!.Value) && x.IsActive);

        var submitterUserId = CurrentUser.Id!.Value;
        await EnsureWorkflowTemplateRunnableAsync(activeTemplate, stepTemplates, allAssignments, submitterUserId);

        var stepDetails = new List<WorkflowStepDetailDto>();
        foreach (var step in stepTemplates)
        {
            var stepAssignments = allAssignments.Where(a => a.StepId == step.Id && a.IsActive).ToList();
            stepDetails.Add(await BuildWorkflowStepDetailAsync(step, stepAssignments, submitterUserId));
        }

        return new WorkflowSubmitInfoDto
        {
            WorkflowId = workflow.Id,
            WorkflowName = workflow.Name,
            WorkflowTemplateId = activeTemplate.Id,
            WorkflowTemplateName = activeTemplate.Name,
            WordTemplatePath = activeTemplate.WordTemplatePath,
            PdfTemplatePath = activeTemplate.PdfTemplatePath,
            HasTemplateFile = !string.IsNullOrWhiteSpace(activeTemplate.WordTemplatePath)
                || !string.IsNullOrWhiteSpace(activeTemplate.PdfTemplatePath),
            SignMode = activeTemplate.SignMode,
            IsTemplateFileWordFormat = WorkflowSubmissionHelper.IsWordFormatPath(
                activeTemplate.WordTemplatePath ?? activeTemplate.PdfTemplatePath),
            Steps = stepDetails
        };
    }

    public async Task<WorkflowStepDetailDto> BuildWorkflowStepDetailAsync(
        WorkflowStepTemplate step,
        IReadOnlyList<WorkflowStepAssignment> stepAssignments,
        Guid submitterUserId)
    {
        var detail = new WorkflowStepDetailDto
        {
            StepId = step.Id,
            Order = step.Order,
            Name = step.Name,
            Type = step.Type,
            SLADays = step.SLADays,
            AllowReturn = step.AllowReturn
        };

        var candidateMap = new Dictionary<Guid, WorkflowStepUserDto>();

        foreach (var assignment in stepAssignments.Where(IsConfiguredAssignment))
        {
            if (IsRoleBasedAssignment(assignment))
            {
                detail.AssigneeType = assignment.AssigneeType;
                detail.RoleId = assignment.RoleId;
                if (assignment.RoleId.HasValue)
                {
                    var role = await _identityRoleRepository.FindAsync(assignment.RoleId.Value);
                    detail.RoleName = role?.Name;
                }

                var candidates = await _workflowAssigneeResolver.ResolveCandidatesByRoleAsync(
                    assignment.RoleId!.Value, submitterUserId, assignment.IsPrimary);
                foreach (var candidate in candidates)
                {
                    if (!candidateMap.TryGetValue(candidate.UserId, out var existing)
                        || candidate.OrganizationUnitDepth < existing.OrganizationUnitDepth)
                    {
                        candidateMap[candidate.UserId] = candidate;
                    }
                }
            }
            else if (assignment.DefaultUserId.HasValue)
            {
                var user = await _identityUserRepository.FindAsync(assignment.DefaultUserId.Value);
                var dto = new WorkflowStepUserDto
                {
                    UserId = assignment.DefaultUserId.Value,
                    UserName = user?.UserName ?? "Unknown",
                    FullName = user == null ? null : $"{user.Surname} {user.Name}".Trim(),
                    IsPrimary = assignment.IsPrimary,
                    OrganizationUnitDepth = 0
                };
                candidateMap[dto.UserId] = dto;
            }
        }

        detail.CandidateUsers = candidateMap.Values
            .OrderBy(x => x.OrganizationUnitDepth)
            .ThenBy(x => x.FullName)
            .ToList();
        detail.AssignedUsers = detail.CandidateUsers.ToList();

        return detail;
    }

    private async Task<WorkflowTemplate?> ResolveLatestWorkflowTemplateForSubmissionAsync(Guid workflowId)
    {
        var templates = await _workflowTemplateRepository.GetListAsync(x => x.WorkflowId == workflowId);
        return templates
            .OrderByDescending(x => x.CreationTime)
            .FirstOrDefault();
    }

    private async Task EnsureWorkflowTemplateRunnableAsync(
        WorkflowTemplate template,
        IReadOnlyList<WorkflowStepTemplate> stepTemplates,
        IReadOnlyList<WorkflowStepAssignment> allAssignments,
        Guid submitterUserId)
    {
        if (!stepTemplates.Any())
        {
            throw new Volo.Abp.UserFriendlyException(L["NoWorkflowStepsFound"]);
        }

        var orderedSteps = stepTemplates.OrderBy(x => x.Order).ToList();
        var firstBlockingStep = WorkflowStepNavigationHelper.GetFirstBlockingStepTemplate(orderedSteps);
        if (firstBlockingStep == null)
        {
            foreach (var step in orderedSteps)
            {
                if (!await StepHasResolvableAssigneesAsync(step.Id, allAssignments, submitterUserId))
                {
                    throw new Volo.Abp.UserFriendlyException(L["ViewStepMustHaveViewers"]);
                }
            }

            return;
        }

        if (!await StepHasResolvableAssigneesAsync(firstBlockingStep.Id, allAssignments, submitterUserId))
        {
            throw new Volo.Abp.UserFriendlyException(L["FirstStepMustHaveAssignedUsers"]);
        }

        foreach (var step in orderedSteps.Where(s => WorkflowStepNavigationHelper.IsViewStep(s.Type)))
        {
            if (!await StepHasResolvableAssigneesAsync(step.Id, allAssignments, submitterUserId))
            {
                throw new Volo.Abp.UserFriendlyException(L["ViewStepMustHaveViewers"]);
            }
        }

        if (template.SignMode == nameof(SignMode.PARALLEL))
        {
            foreach (var step in orderedSteps.Where(s => WorkflowStepNavigationHelper.IsBlockingStep(s.Type)))
            {
                if (!await StepHasResolvableAssigneesAsync(step.Id, allAssignments, submitterUserId))
                {
                    throw new Volo.Abp.UserFriendlyException(L["AllStepsMustHaveAssignedUsers"]);
                }
            }
        }
    }

    private static bool IsRoleBasedAssignment(WorkflowStepAssignment assignment)
    {
        return assignment.AssigneeType == WorkflowStepAssigneeTypeNames.RoleInSubmitterOrganizationUnit;
    }

    private static bool IsConfiguredAssignment(WorkflowStepAssignment assignment)
    {
        if (!assignment.IsActive)
        {
            return false;
        }

        if (IsRoleBasedAssignment(assignment))
        {
            return assignment.RoleId.HasValue;
        }

        return assignment.DefaultUserId.HasValue;
    }

    private async Task<bool> StepHasResolvableAssigneesAsync(
        Guid stepId,
        IReadOnlyList<WorkflowStepAssignment> allAssignments,
        Guid submitterUserId)
    {
        var stepAssignments = allAssignments.Where(a => a.StepId == stepId && IsConfiguredAssignment(a)).ToList();
        if (!stepAssignments.Any())
        {
            return false;
        }

        foreach (var assignment in stepAssignments.Where(IsRoleBasedAssignment))
        {
            var candidates = await _workflowAssigneeResolver.ResolveCandidatesByRoleAsync(
                assignment.RoleId!.Value, submitterUserId, assignment.IsPrimary);
            if (!candidates.Any())
            {
                return false;
            }
        }

        return true;
    }
}
