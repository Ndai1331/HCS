using Volo.Abp.Application.Dtos;
using System;

namespace HC.DocumentWorkflowInstanceLogss;

public abstract class GetDocumentWorkflowInstanceLogssInputBase : PagedAndSortedResultRequestDto
{
    public string? FilterText { get; set; }

    public string? Action { get; set; }

    public string? ActorRole { get; set; }

    public string? FromStatus { get; set; }

    public string? ToStatus { get; set; }

    public string? Note { get; set; }

    public Guid? DocumentAssignmentId { get; set; }

    public Guid? ActorUserId { get; set; }

    public GetDocumentWorkflowInstanceLogssInputBase()
    {
    }
}