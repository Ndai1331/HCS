using HC.WorkflowStepAssignments;
using HC.WorkflowStepTemplates;
using Volo.Abp.Identity;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;
using JetBrains.Annotations;
using Volo.Abp;

namespace HC.WorkflowStepAssignments;

public abstract class WorkflowStepAssignmentBase : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public virtual Guid? TenantId { get; set; }

    public virtual bool IsPrimary { get; set; }

    public virtual bool IsActive { get; set; }

    public Guid? StepId { get; set; }

    public Guid? DefaultUserId { get; set; }

    [NotNull]
    public virtual string AssigneeType { get; set; } = WorkflowStepAssigneeTypeNames.SpecificUser;

    public virtual Guid? RoleId { get; set; }

    protected WorkflowStepAssignmentBase()
    {
    }

    public WorkflowStepAssignmentBase(Guid id, Guid? stepId, Guid? defaultUserId, bool isPrimary, bool isActive, string? assigneeType = null, Guid? roleId = null)
    {
        Id = id;
        IsPrimary = isPrimary;
        IsActive = isActive;
        StepId = stepId;
        DefaultUserId = defaultUserId;
        AssigneeType = assigneeType ?? WorkflowStepAssigneeTypeNames.SpecificUser;
        RoleId = roleId;
    }
}