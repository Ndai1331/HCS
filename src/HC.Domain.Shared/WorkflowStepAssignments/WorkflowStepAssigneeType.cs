namespace HC.WorkflowStepAssignments;

public enum WorkflowStepAssigneeType
{
    SpecificUser = 0,
    RoleInSubmitterOrganizationUnit = 1
}

public static class WorkflowStepAssigneeTypeNames
{
    public const string SpecificUser = nameof(WorkflowStepAssigneeType.SpecificUser);
    public const string RoleInSubmitterOrganizationUnit = nameof(WorkflowStepAssigneeType.RoleInSubmitterOrganizationUnit);
}
