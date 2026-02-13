namespace HC.DocumentWorkflowInstances;

/// <summary>
/// ISSUE-16 FIX: Constants for hardcoded strings used across workflow services.
/// Centralizes magic strings for maintainability and consistency.
/// </summary>
public static class WorkflowConstants
{
    // Assignment roles
    public const string RoleInitiator = "Initiator";
    public const string RoleProcessor = "Processor";
    public const string RoleSystem = "System";

    // Notification priorities
    public const string PriorityNormal = "NORMAL";
    public const string PriorityHigh = "HIGH";

    // Blob storage paths
    public const string BlobPathSigningSteps = "signing-steps/";
    public const string BlobPathElectronicSigned = "electronic-signed/";
}
