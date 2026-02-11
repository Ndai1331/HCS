using System;
using System.ComponentModel.DataAnnotations;
using System.Collections.Generic;

namespace HC.WorkflowTemplates;

public abstract class WorkflowTemplateCreateDtoBase
{
    [Required]
    [StringLength(WorkflowTemplateConsts.CodeMaxLength, MinimumLength = WorkflowTemplateConsts.CodeMinLength)]
    public string Code { get; set; } = null!;
    [Required]
    public string Name { get; set; } = null!;
    public string? WordTemplatePath { get; set; }

    public string? PdfTemplatePath { get; set; }

    public string? ContentSchema { get; set; }

    [StringLength(WorkflowTemplateConsts.OutputFormatMaxLength)]
    public string? OutputFormat { get; set; }

    [StringLength(WorkflowTemplateConsts.SignModeMaxLength)]
    public string? SignMode { get; set; }

    public Guid WorkflowId { get; set; }
}