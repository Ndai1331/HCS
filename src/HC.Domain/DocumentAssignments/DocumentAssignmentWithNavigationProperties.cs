using HC.Documents;
using HC.DocumentFiles;
using HC.WorkflowStepTemplates;
using Volo.Abp.Identity;
using System;
using System.Collections.Generic;
using HC.DocumentAssignments;

namespace HC.DocumentAssignments;

public abstract class DocumentAssignmentWithNavigationPropertiesBase
{
    public DocumentAssignment DocumentAssignment { get; set; } = null!;
    public Document Document { get; set; } = null!;
    public WorkflowStepTemplate? WorkflowStepTemplate { get; set; }

    public IdentityUser ReceiverUser { get; set; } = null!;

    public DocumentFile? DocumentFileResult { get; set; }
}