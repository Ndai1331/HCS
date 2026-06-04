using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;
using Volo.Abp.Data;

namespace HC.WorkflowStepAssignments;

public abstract class WorkflowStepAssignmentManagerBase : DomainService
{
    protected IWorkflowStepAssignmentRepository _workflowStepAssignmentRepository;

    public WorkflowStepAssignmentManagerBase(IWorkflowStepAssignmentRepository workflowStepAssignmentRepository)
    {
        _workflowStepAssignmentRepository = workflowStepAssignmentRepository;
    }

    public virtual async Task<WorkflowStepAssignment> CreateAsync(
        Guid? stepId,
        Guid? defaultUserId,
        bool isPrimary,
        bool isActive,
        string? assigneeType = null,
        Guid? roleId = null,
        IReadOnlyList<Guid>? organizationUnitIds = null,
        IReadOnlyList<Guid>? defaultUserIds = null)
    {
        var scopeFields = WorkflowStepAssignmentScopeHelper.BuildScopeFields(organizationUnitIds, defaultUserIds);
        var effectiveDefaultUserId = scopeFields.LegacyDefaultUserId ?? defaultUserId;
        ValidateAssignee(assigneeType, effectiveDefaultUserId, roleId, organizationUnitIds, defaultUserIds);
        var workflowStepAssignment = new WorkflowStepAssignment(
            GuidGenerator.Create(),
            stepId,
            effectiveDefaultUserId,
            isPrimary,
            isActive,
            assigneeType,
            roleId);
        workflowStepAssignment.OrganizationUnitIdsJson = scopeFields.OrganizationUnitIdsJson;
        workflowStepAssignment.DefaultUserIdsJson = scopeFields.DefaultUserIdsJson;
        return await _workflowStepAssignmentRepository.InsertAsync(workflowStepAssignment);
    }

    public virtual async Task<WorkflowStepAssignment> UpdateAsync(
        Guid id,
        Guid? stepId,
        Guid? defaultUserId,
        bool isPrimary,
        bool isActive,
        string? assigneeType = null,
        Guid? roleId = null,
        IReadOnlyList<Guid>? organizationUnitIds = null,
        IReadOnlyList<Guid>? defaultUserIds = null,
        [CanBeNull] string? concurrencyStamp = null)
    {
        var scopeFields = WorkflowStepAssignmentScopeHelper.BuildScopeFields(organizationUnitIds, defaultUserIds);
        var effectiveDefaultUserId = scopeFields.LegacyDefaultUserId ?? defaultUserId;
        ValidateAssignee(assigneeType, effectiveDefaultUserId, roleId, organizationUnitIds, defaultUserIds);
        var workflowStepAssignment = await _workflowStepAssignmentRepository.GetAsync(id);
        workflowStepAssignment.StepId = stepId;
        workflowStepAssignment.DefaultUserId = effectiveDefaultUserId;
        workflowStepAssignment.IsPrimary = isPrimary;
        workflowStepAssignment.IsActive = isActive;
        workflowStepAssignment.AssigneeType = assigneeType ?? WorkflowStepAssigneeTypeNames.SpecificUser;
        workflowStepAssignment.RoleId = roleId;
        workflowStepAssignment.OrganizationUnitIdsJson = scopeFields.OrganizationUnitIdsJson;
        workflowStepAssignment.DefaultUserIdsJson = scopeFields.DefaultUserIdsJson;
        workflowStepAssignment.SetConcurrencyStampIfNotNull(concurrencyStamp);
        return await _workflowStepAssignmentRepository.UpdateAsync(workflowStepAssignment);
    }

    protected virtual void ValidateAssignee(
        string? assigneeType,
        Guid? defaultUserId,
        Guid? roleId,
        IReadOnlyList<Guid>? organizationUnitIds = null,
        IReadOnlyList<Guid>? defaultUserIds = null)
    {
        var type = assigneeType ?? WorkflowStepAssigneeTypeNames.SpecificUser;

        if (type == WorkflowStepAssigneeTypeNames.ScopedAssignee)
        {
            if (!WorkflowStepAssignmentScopeHelper.HasResolvableScope(type, organizationUnitIds, defaultUserIds, roleId))
            {
                throw new BusinessException("HC:WorkflowStepAssignment:ScopeRequired");
            }

            return;
        }

        if (type == WorkflowStepAssigneeTypeNames.RoleInSubmitterOrganizationUnit)
        {
            if (!roleId.HasValue || roleId == Guid.Empty)
            {
                throw new BusinessException("HC:WorkflowStepAssignment:RoleRequired");
            }

            if (defaultUserId.HasValue)
            {
                throw new BusinessException("HC:WorkflowStepAssignment:DefaultUserMustBeEmptyForRoleAssignee");
            }

            return;
        }

        var userIds = WorkflowStepAssignmentScopeHelper.NormalizeIds(defaultUserIds);
        if (userIds.Count > 0)
        {
            return;
        }

        if (!defaultUserId.HasValue || defaultUserId == Guid.Empty)
        {
            throw new BusinessException("HC:WorkflowStepAssignment:DefaultUserRequired");
        }
    }
}