using Volo.Abp.Application.Dtos;
using System;

namespace HC.DocumentWorkflowInstanceFiles;

public class GetDocumentWorkflowInstanceFileListInput : PagedAndSortedResultRequestDto
{
    public Guid DocumentWorkflowInstanceId { get; set; }
}