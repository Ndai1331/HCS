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
    private readonly IWorkflowViewScopeResolver _workflowViewScopeResolver;

    public WorkflowSubmitInfoQueryService(
        IRepository<Workflow, Guid> workflowRepository,
        IRepository<WorkflowTemplate, Guid> workflowTemplateRepository,
        IRepository<WorkflowStepTemplate, Guid> workflowStepTemplateRepository,
        IRepository<WorkflowStepAssignment, Guid> workflowStepAssignmentRepository,
        IRepository<DocumentFile, Guid> documentFileRepository,
        IRepository<IdentityUser, Guid> identityUserRepository,
        IRepository<IdentityRole, Guid> identityRoleRepository,
        IWorkflowAssigneeResolver workflowAssigneeResolver,
        IWorkflowViewScopeResolver workflowViewScopeResolver)
    {
        _workflowRepository = workflowRepository;
        _workflowTemplateRepository = workflowTemplateRepository;
        _workflowStepTemplateRepository = workflowStepTemplateRepository;
        _workflowStepAssignmentRepository = workflowStepAssignmentRepository;
        _documentFileRepository = documentFileRepository;
        _identityUserRepository = identityUserRepository;
        _identityRoleRepository = identityRoleRepository;
        _workflowAssigneeResolver = workflowAssigneeResolver;
        _workflowViewScopeResolver = workflowViewScopeResolver;
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

        var activeAssignments = stepAssignments.Where(a => a.IsActive).ToList();
        foreach (var assignment in activeAssignments)
        {
            detail.TemplateOrganizationUnitIds.AddRange(
                WorkflowStepAssignmentScopeHelper.GetOrganizationUnitIds(assignment.OrganizationUnitIdsJson));
            detail.TemplateUserIds.AddRange(
                WorkflowStepAssignmentScopeHelper.GetDefaultUserIds(
                    assignment.DefaultUserIdsJson,
                    assignment.DefaultUserId));

            if (IsRoleBasedAssignment(assignment) || IsScopedAssignment(assignment))
            {
                detail.AssigneeType = assignment.AssigneeType;
                detail.RoleId = assignment.RoleId;
                if (assignment.RoleId.HasValue)
                {
                    var role = await _identityRoleRepository.FindAsync(assignment.RoleId.Value);
                    detail.RoleName = role?.Name;
                }
            }
        }

        detail.TemplateOrganizationUnitIds = detail.TemplateOrganizationUnitIds.Distinct().ToList();
        detail.TemplateUserIds = detail.TemplateUserIds.Distinct().ToList();

        var candidateMap = new Dictionary<Guid, WorkflowStepUserDto>();
        if (WorkflowStepNavigationHelper.IsViewStep(step.Type))
        {
            var viewerIds = await _workflowViewScopeResolver.ResolveViewerUserIdsAsync(
                activeAssignments,
                null,
                submitterUserId);
            foreach (var userDto in await MapUsersToWorkflowStepUserDtosAsync(viewerIds))
            {
                candidateMap[userDto.UserId] = userDto;
            }
        }
        else
        {
            foreach (var assignment in activeAssignments.Where(IsConfiguredAssignment))
            {
                if (IsScopedAssignment(assignment))
                {
                    var ouIds = WorkflowStepAssignmentScopeHelper.GetOrganizationUnitIds(assignment.OrganizationUnitIdsJson);
                    if (assignment.RoleId.HasValue && ouIds.Count > 0)
                    {
                        var candidates = await _workflowAssigneeResolver.ResolveCandidatesByRoleInOrganizationUnitsAsync(
                            assignment.RoleId.Value,
                            ouIds,
                            assignment.IsPrimary);
                        MergeCandidates(candidateMap, candidates);
                    }

                    foreach (var userDto in await MapUsersToWorkflowStepUserDtosAsync(
                                 WorkflowStepAssignmentScopeHelper.GetDefaultUserIds(
                                     assignment.DefaultUserIdsJson,
                                     assignment.DefaultUserId)))
                    {
                        candidateMap[userDto.UserId] = userDto;
                    }
                }
                else if (IsRoleBasedAssignment(assignment))
                {
                    var candidates = await _workflowAssigneeResolver.ResolveCandidatesByRoleAsync(
                        assignment.RoleId!.Value, submitterUserId, assignment.IsPrimary);
                    MergeCandidates(candidateMap, candidates);
                }
                else
                {
                    foreach (var userDto in await MapUsersToWorkflowStepUserDtosAsync(
                                 WorkflowStepAssignmentScopeHelper.GetDefaultUserIds(
                                     assignment.DefaultUserIdsJson,
                                     assignment.DefaultUserId)))
                    {
                        candidateMap[userDto.UserId] = userDto;
                    }
                }
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

    private static bool IsScopedAssignment(WorkflowStepAssignment assignment)
    {
        return assignment.AssigneeType == WorkflowStepAssigneeTypeNames.ScopedAssignee
               || WorkflowStepAssignmentScopeHelper.HasResolvableScope(
                   assignment.AssigneeType,
                   WorkflowStepAssignmentScopeHelper.GetOrganizationUnitIds(assignment.OrganizationUnitIdsJson),
                   WorkflowStepAssignmentScopeHelper.GetDefaultUserIds(
                       assignment.DefaultUserIdsJson,
                       assignment.DefaultUserId),
                   assignment.RoleId);
    }

    private static bool IsConfiguredAssignment(WorkflowStepAssignment assignment)
    {
        if (!assignment.IsActive)
        {
            return false;
        }

        if (IsScopedAssignment(assignment))
        {
            return true;
        }

        if (IsRoleBasedAssignment(assignment))
        {
            return assignment.RoleId.HasValue;
        }

        return WorkflowStepAssignmentScopeHelper.GetDefaultUserIds(
            assignment.DefaultUserIdsJson,
            assignment.DefaultUserId).Count > 0;
    }

    private async Task<bool> StepHasResolvableAssigneesAsync(
        Guid stepId,
        IReadOnlyList<WorkflowStepAssignment> allAssignments,
        Guid submitterUserId)
    {
        var stepAssignments = allAssignments.Where(a => a.StepId == stepId && a.IsActive).ToList();
        if (!stepAssignments.Any())
        {
            return false;
        }

        var stepTemplate = await _workflowStepTemplateRepository.FindAsync(stepId);
        if (stepTemplate != null && WorkflowStepNavigationHelper.IsViewStep(stepTemplate.Type))
        {
            return true;
        }

        var viewerIds = await _workflowViewScopeResolver.ResolveViewerUserIdsAsync(
            stepAssignments,
            null,
            submitterUserId);

        return viewerIds.Count > 0;
    }

    private static void MergeCandidates(
        Dictionary<Guid, WorkflowStepUserDto> candidateMap,
        IEnumerable<WorkflowStepUserDto> candidates)
    {
        foreach (var candidate in candidates)
        {
            if (!candidateMap.TryGetValue(candidate.UserId, out var existing)
                || candidate.OrganizationUnitDepth < existing.OrganizationUnitDepth)
            {
                candidateMap[candidate.UserId] = candidate;
            }
        }
    }

    private async Task<List<WorkflowStepUserDto>> MapUsersToWorkflowStepUserDtosAsync(IEnumerable<Guid> userIds)
    {
        var ids = userIds.Where(x => x != Guid.Empty).Distinct().ToList();
        if (ids.Count == 0)
        {
            return new List<WorkflowStepUserDto>();
        }

        var users = await _identityUserRepository.GetListAsync(x => ids.Contains(x.Id));
        return users.Select(user => new WorkflowStepUserDto
        {
            UserId = user.Id,
            UserName = user.UserName ?? "Unknown",
            FullName = $"{user.Surname} {user.Name}".Trim(),
            OrganizationUnitDepth = 0
        }).ToList();
    }
}
