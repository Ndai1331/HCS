using HC.DocumentFiles;
using System;
using Volo.Abp.Application.Dtos;
using System.Collections.Generic;

namespace HC.DocumentWorkflowInstanceFiles;

public abstract class DocumentWorkflowInstanceFileWithNavigationPropertiesDtoBase
{
    public DocumentWorkflowInstanceFileDto DocumentWorkflowInstanceFile { get; set; } = null!;
    public DocumentFileDto DocumentFile { get; set; } = null!;
}