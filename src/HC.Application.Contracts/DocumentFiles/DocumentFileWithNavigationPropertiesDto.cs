using HC.Documents;
using System;
using Volo.Abp.Application.Dtos;
using System.Collections.Generic;

namespace HC.DocumentFiles;

public abstract class DocumentFileWithNavigationPropertiesDtoBase
{
    public DocumentFileDto DocumentFile { get; set; } = null!;
    public DocumentDto? Document { get; set; }
}