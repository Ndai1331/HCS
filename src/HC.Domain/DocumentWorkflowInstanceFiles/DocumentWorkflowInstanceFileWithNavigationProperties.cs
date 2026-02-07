using HC.DocumentFiles;
using System;
using System.Collections.Generic;
using HC.DocumentWorkflowInstanceFiles;

namespace HC.DocumentWorkflowInstanceFiles;

public abstract class DocumentWorkflowInstanceFileWithNavigationPropertiesBase
{
    public DocumentWorkflowInstanceFile DocumentWorkflowInstanceFile { get; set; } = null!;
    public DocumentFile DocumentFile { get; set; } = null!;
}