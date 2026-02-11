using System;

namespace HC.WorkflowTemplates;

public abstract class WorkflowTemplateExcelDtoBase
{
    public string Code { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string? WordTemplatePath { get; set; }

    public string? PdfTemplatePath { get; set; }

    public string? ContentSchema { get; set; }

    public string? OutputFormat { get; set; }

    public string? SignMode { get; set; }
}