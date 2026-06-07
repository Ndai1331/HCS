using System;
using System.ComponentModel.DataAnnotations;
using System.Collections.Generic;
using Volo.Abp.Domain.Entities;

namespace HC.WorkflowStepAssignments;

public abstract class WorkflowStepAssignmentUpdateDtoBase : IHasConcurrencyStamp
{
    public bool IsPrimary { get; set; }

    public bool IsActive { get; set; }

    public Guid? StepId { get; set; }

    public Guid? DefaultUserId { get; set; }

    public string AssigneeType { get; set; } = WorkflowStepAssigneeTypeNames.SpecificUser;

    public Guid? RoleId { get; set; }

    public List<Guid> OrganizationUnitIds { get; set; } = new();

    public List<Guid> DefaultUserIds { get; set; } = new();

    public string ConcurrencyStamp { get; set; } = null!;
}