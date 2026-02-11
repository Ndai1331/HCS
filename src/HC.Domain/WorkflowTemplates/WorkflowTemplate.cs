using HC.Workflows;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;
using JetBrains.Annotations;
using Volo.Abp;

namespace HC.WorkflowTemplates;

public abstract class WorkflowTemplateBase : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public virtual Guid? TenantId { get; set; }

    [NotNull]
    public virtual string Code { get; set; }

    [NotNull]
    public virtual string Name { get; set; }

    [CanBeNull]
    public virtual string? WordTemplatePath { get; set; }

    [CanBeNull]
    public virtual string? PdfTemplatePath { get; set; }

    [CanBeNull]
    public virtual string? ContentSchema { get; set; }

    [CanBeNull]
    public virtual string? OutputFormat { get; set; }

    [CanBeNull]
    public virtual string? SignMode { get; set; }

    public Guid WorkflowId { get; set; }

    protected WorkflowTemplateBase()
    {
    }

    public WorkflowTemplateBase(Guid id, Guid workflowId, string code, string name, string? wordTemplatePath = null, string? pdfTemplatePath = null, string? contentSchema = null, string? outputFormat = null, string? signMode = null)
    {
        Id = id;
        Check.NotNull(code, nameof(code));
        Check.Length(code, nameof(code), WorkflowTemplateConsts.CodeMaxLength, WorkflowTemplateConsts.CodeMinLength);
        Check.NotNull(name, nameof(name));
        Check.Length(outputFormat, nameof(outputFormat), WorkflowTemplateConsts.OutputFormatMaxLength, 0);
        Check.Length(signMode, nameof(signMode), WorkflowTemplateConsts.SignModeMaxLength, 0);
        Code = code;
        Name = name;
        WordTemplatePath = wordTemplatePath;
        PdfTemplatePath = pdfTemplatePath;
        ContentSchema = contentSchema;
        OutputFormat = outputFormat;
        SignMode = signMode;
        WorkflowId = workflowId;
    }
}