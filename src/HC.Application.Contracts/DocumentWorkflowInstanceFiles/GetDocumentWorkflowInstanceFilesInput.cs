using Volo.Abp.Application.Dtos;
using System;

namespace HC.DocumentWorkflowInstanceFiles;

public abstract class GetDocumentWorkflowInstanceFilesInputBase : PagedAndSortedResultRequestDto
{
    public string? FilterText { get; set; }

    public Guid? DocumentFileId { get; set; }

    public GetDocumentWorkflowInstanceFilesInputBase()
    {
    }
}