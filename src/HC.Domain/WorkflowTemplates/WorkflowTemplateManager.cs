using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;
using Volo.Abp.Data;

namespace HC.WorkflowTemplates;

public abstract class WorkflowTemplateManagerBase : DomainService
{
    protected IWorkflowTemplateRepository _workflowTemplateRepository;

    public WorkflowTemplateManagerBase(IWorkflowTemplateRepository workflowTemplateRepository)
    {
        _workflowTemplateRepository = workflowTemplateRepository;
    }

    public virtual async Task<WorkflowTemplate> CreateAsync(Guid workflowId, string code, string name, string? wordTemplatePath = null, string? pdfTemplatePath = null, string? contentSchema = null, string? outputFormat = null, string? signMode = null)
    {
        Check.NotNull(workflowId, nameof(workflowId));
        Check.NotNullOrWhiteSpace(code, nameof(code));
        Check.Length(code, nameof(code), WorkflowTemplateConsts.CodeMaxLength, WorkflowTemplateConsts.CodeMinLength);
        Check.NotNullOrWhiteSpace(name, nameof(name));
        Check.Length(outputFormat, nameof(outputFormat), WorkflowTemplateConsts.OutputFormatMaxLength);
        Check.Length(signMode, nameof(signMode), WorkflowTemplateConsts.SignModeMaxLength);
        var workflowTemplate = new WorkflowTemplate(GuidGenerator.Create(), workflowId, code, name, wordTemplatePath, pdfTemplatePath, contentSchema, outputFormat, signMode);
        return await _workflowTemplateRepository.InsertAsync(workflowTemplate);
    }

    public virtual async Task<WorkflowTemplate> UpdateAsync(Guid id, Guid workflowId, string code, string name, string? wordTemplatePath = null, string? pdfTemplatePath = null, string? contentSchema = null, string? outputFormat = null, string? signMode = null, [CanBeNull] string? concurrencyStamp = null)
    {
        Check.NotNull(workflowId, nameof(workflowId));
        Check.NotNullOrWhiteSpace(code, nameof(code));
        Check.Length(code, nameof(code), WorkflowTemplateConsts.CodeMaxLength, WorkflowTemplateConsts.CodeMinLength);
        Check.NotNullOrWhiteSpace(name, nameof(name));
        Check.Length(outputFormat, nameof(outputFormat), WorkflowTemplateConsts.OutputFormatMaxLength);
        Check.Length(signMode, nameof(signMode), WorkflowTemplateConsts.SignModeMaxLength);
        var workflowTemplate = await _workflowTemplateRepository.GetAsync(id);
        workflowTemplate.WorkflowId = workflowId;
        workflowTemplate.Code = code;
        workflowTemplate.Name = name;
        workflowTemplate.WordTemplatePath = wordTemplatePath;
        workflowTemplate.PdfTemplatePath = pdfTemplatePath;
        workflowTemplate.ContentSchema = contentSchema;
        workflowTemplate.OutputFormat = outputFormat;
        workflowTemplate.SignMode = signMode;
        workflowTemplate.SetConcurrencyStampIfNotNull(concurrencyStamp);
        return await _workflowTemplateRepository.UpdateAsync(workflowTemplate);
    }
}