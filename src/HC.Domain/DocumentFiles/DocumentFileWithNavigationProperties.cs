using HC.Documents;
using System;
using System.Collections.Generic;
using HC.DocumentFiles;

namespace HC.DocumentFiles;

public abstract class DocumentFileWithNavigationPropertiesBase
{
    public DocumentFile DocumentFile { get; set; } = null!;
    public Document? Document { get; set; }
}