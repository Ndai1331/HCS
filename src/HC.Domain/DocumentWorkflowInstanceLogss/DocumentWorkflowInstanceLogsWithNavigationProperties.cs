using HC.DocumentAssignments;
using Volo.Abp.Identity;
using System;
using System.Collections.Generic;
using HC.DocumentWorkflowInstanceLogss;

namespace HC.DocumentWorkflowInstanceLogss;

public abstract class DocumentWorkflowInstanceLogsWithNavigationPropertiesBase
{
    public DocumentWorkflowInstanceLogs DocumentWorkflowInstanceLogs { get; set; } = null!;
    public DocumentAssignment? DocumentAssignment { get; set; }

    public IdentityUser? ActorUser { get; set; }
}