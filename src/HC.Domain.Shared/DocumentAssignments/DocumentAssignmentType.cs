namespace HC.DocumentAssignments;


public enum DocumentAssignmentStepOrder
{
    ORIGINAL,
    GENERATED,
    SIGNED
    
}
public enum DocumentAssignmentActionType
{
    PROCESS,
    SIGN,
    VIEW
    
}

public enum DocumentAssignmentStatus
{
    PENDING,
    DONE,
    REJECTED
}