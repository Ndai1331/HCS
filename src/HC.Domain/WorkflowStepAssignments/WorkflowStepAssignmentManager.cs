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
        Guid? roleId = null)
    {
        ValidateAssignee(assigneeType, defaultUserId, roleId);
        var workflowStepAssignment = new WorkflowStepAssignment(
            GuidGenerator.Create(),
            stepId,
            defaultUserId,
            isPrimary,
            isActive,
            assigneeType,
            roleId);
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
        [CanBeNull] string? concurrencyStamp = null)
    {
        ValidateAssignee(assigneeType, defaultUserId, roleId);
        var workflowStepAssignment = await _workflowStepAssignmentRepository.GetAsync(id);
        workflowStepAssignment.StepId = stepId;
        workflowStepAssignment.DefaultUserId = defaultUserId;
        workflowStepAssignment.IsPrimary = isPrimary;
        workflowStepAssignment.IsActive = isActive;
        workflowStepAssignment.AssigneeType = assigneeType ?? WorkflowStepAssigneeTypeNames.SpecificUser;
        workflowStepAssignment.RoleId = roleId;
        workflowStepAssignment.SetConcurrencyStampIfNotNull(concurrencyStamp);
        return await _workflowStepAssignmentRepository.UpdateAsync(workflowStepAssignment);
    }

    protected virtual void ValidateAssignee(string? assigneeType, Guid? defaultUserId, Guid? roleId)
    {
        var type = assigneeType ?? WorkflowStepAssigneeTypeNames.SpecificUser;
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

        if (!defaultUserId.HasValue || defaultUserId == Guid.Empty)
        {
            throw new BusinessException("HC:WorkflowStepAssignment:DefaultUserRequired");
        }
    }
}