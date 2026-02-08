using HC.DocumentAssignments;
using Volo.Abp.Identity;
using System;
using Volo.Abp.Application.Dtos;
using System.Collections.Generic;

namespace HC.DocumentWorkflowInstanceLogss;

public abstract class DocumentWorkflowInstanceLogsWithNavigationPropertiesDtoBase
{
    public DocumentWorkflowInstanceLogsDto DocumentWorkflowInstanceLogs { get; set; } = null!;
    public DocumentAssignmentDto? DocumentAssignment { get; set; }

    public IdentityUserDto? ActorUser { get; set; }
}