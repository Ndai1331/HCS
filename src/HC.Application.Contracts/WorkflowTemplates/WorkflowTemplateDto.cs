using System;
using System.Collections.Generic;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Domain.Entities;

namespace HC.WorkflowTemplates;

public abstract class WorkflowTemplateDtoBase : FullAuditedEntityDto<Guid>, IHasConcurrencyStamp
{
    public string Code { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string? WordTemplatePath { get; set; }

    public string? PdfTemplatePath { get; set; }

    public string? ContentSchema { get; set; }

    public string? OutputFormat { get; set; }

    public string? SignMode { get; set; }

    public Guid WorkflowId { get; set; }

    public string ConcurrencyStamp { get; set; } = null!;
}