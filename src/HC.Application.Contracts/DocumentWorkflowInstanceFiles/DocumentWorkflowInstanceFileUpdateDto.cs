using System;
using System.ComponentModel.DataAnnotations;
using System.Collections.Generic;

namespace HC.DocumentWorkflowInstanceFiles;

public abstract class DocumentWorkflowInstanceFileUpdateDtoBase
{
    public Guid DocumentWorkflowInstanceId { get; set; }

    public Guid DocumentFileId { get; set; }
}