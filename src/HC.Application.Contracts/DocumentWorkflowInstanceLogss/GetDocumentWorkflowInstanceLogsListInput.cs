using Volo.Abp.Application.Dtos;
using System;

namespace HC.DocumentWorkflowInstanceLogss;

public class GetDocumentWorkflowInstanceLogsListInput : PagedAndSortedResultRequestDto
{
    public Guid DocumentWorkflowInstanceId { get; set; }
}