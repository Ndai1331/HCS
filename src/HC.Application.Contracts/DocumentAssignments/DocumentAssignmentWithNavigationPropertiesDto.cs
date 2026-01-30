using HC.Documents;
using HC.WorkflowStepTemplates;
using Volo.Abp.Identity;
using System;
using Volo.Abp.Application.Dtos;
using System.Collections.Generic;

namespace HC.DocumentAssignments;

public abstract class DocumentAssignmentWithNavigationPropertiesDtoBase
{
    public DocumentAssignmentDto DocumentAssignment { get; set; } = null!;
    public DocumentDto Document { get; set; } = null!;
    public WorkflowStepTemplateDto? WorkflowStepTemplate { get; set; }

    public IdentityUserDto ReceiverUser { get; set; } = null!;
}