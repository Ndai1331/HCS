using HC.WorkflowStepTemplates;
using HC.WorkflowTemplates;
using HC.Workflows;
using HC.Documents;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Dynamic.Core;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Volo.Abp.Domain.Repositories.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore;
using HC.EntityFrameworkCore;

namespace HC.DocumentWorkflowInstances;

public abstract class EfCoreDocumentWorkflowInstanceRepositoryBase : EfCoreRepository<HCDbContext, DocumentWorkflowInstance, Guid>
{
    public EfCoreDocumentWorkflowInstanceRepositoryBase(IDbContextProvider<HCDbContext> dbContextProvider) : base(dbContextProvider)
    {
    }

    public virtual async Task DeleteAllAsync(string? filterText = null, string? status = null, DateTime? startedAtMin = null, DateTime? startedAtMax = null, DateTime? finishedAtMin = null, DateTime? finishedAtMax = null, Guid? documentId = null, Guid? workflowId = null, Guid? workflowTemplateId = null, Guid? currentStepId = null, CancellationToken cancellationToken = default)
    {
        var query = await GetQueryForNavigationPropertiesAsync();
        query = ApplyFilter(query, filterText, status, startedAtMin, startedAtMax, finishedAtMin, finishedAtMax, documentId, workflowId, workflowTemplateId, currentStepId);
        var ids = query.Select(x => x.DocumentWorkflowInstance.Id);
        await DeleteManyAsync(ids, cancellationToken: GetCancellationToken(cancellationToken));
    }

    public virtual async Task<DocumentWorkflowInstanceWithNavigationProperties> GetWithNavigationPropertiesAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var query = await GetQueryForNavigationPropertiesAsync();
        return await query
            .Where(x => x.DocumentWorkflowInstance.Id == id)
            .FirstOrDefaultAsync(GetCancellationToken(cancellationToken));
    }

    public virtual async Task<List<DocumentWorkflowInstanceWithNavigationProperties>> GetListWithNavigationPropertiesAsync(string? filterText = null, string? status = null, DateTime? startedAtMin = null, DateTime? startedAtMax = null, DateTime? finishedAtMin = null, DateTime? finishedAtMax = null, Guid? documentId = null, Guid? workflowId = null, Guid? workflowTemplateId = null, Guid? currentStepId = null, string? sorting = null, int maxResultCount = int.MaxValue, int skipCount = 0, CancellationToken cancellationToken = default)
    {
        var query = await GetQueryForNavigationPropertiesAsync();
        query = ApplyFilter(query, filterText, status, startedAtMin, startedAtMax, finishedAtMin, finishedAtMax, documentId, workflowId, workflowTemplateId, currentStepId);
        query = query.OrderBy(string.IsNullOrWhiteSpace(sorting) ? DocumentWorkflowInstanceConsts.GetDefaultSorting(true) : sorting);
        return await query.PageBy(skipCount, maxResultCount).ToListAsync(cancellationToken);
    }

    protected virtual async Task<IQueryable<DocumentWorkflowInstanceWithNavigationProperties>> GetQueryForNavigationPropertiesAsync()
    {
        return from documentWorkflowInstance in (await GetDbSetAsync())
               join document in (await GetDbContextAsync()).Set<Document>() on documentWorkflowInstance.DocumentId equals document.Id into documents
               from document in documents.DefaultIfEmpty()
               join workflow in (await GetDbContextAsync()).Set<Workflow>() on documentWorkflowInstance.WorkflowId equals workflow.Id into workflows
               from workflow in workflows.DefaultIfEmpty()
               join workflowTemplate in (await GetDbContextAsync()).Set<WorkflowTemplate>() on documentWorkflowInstance.WorkflowTemplateId equals workflowTemplate.Id into workflowTemplates
               from workflowTemplate in workflowTemplates.DefaultIfEmpty()
               join currentStep in (await GetDbContextAsync()).Set<WorkflowStepTemplate>() on documentWorkflowInstance.CurrentStepId equals currentStep.Id into workflowStepTemplates
               from currentStep in workflowStepTemplates.DefaultIfEmpty()
               select new DocumentWorkflowInstanceWithNavigationProperties
               {
                   DocumentWorkflowInstance = documentWorkflowInstance,
                   Document = document,
                   Workflow = workflow,
                   WorkflowTemplate = workflowTemplate,
                   CurrentStep = currentStep
               };
    }

    protected virtual IQueryable<DocumentWorkflowInstanceWithNavigationProperties> ApplyFilter(IQueryable<DocumentWorkflowInstanceWithNavigationProperties> query, string? filterText, string? status = null, DateTime? startedAtMin = null, DateTime? startedAtMax = null, DateTime? finishedAtMin = null, DateTime? finishedAtMax = null, Guid? documentId = null, Guid? workflowId = null, Guid? workflowTemplateId = null, Guid? currentStepId = null)
    {
        return query.WhereIf(!string.IsNullOrWhiteSpace(filterText), e => e.DocumentWorkflowInstance.Status!.Contains(filterText!)).WhereIf(!string.IsNullOrWhiteSpace(status), e => e.DocumentWorkflowInstance.Status.Contains(status)).WhereIf(startedAtMin.HasValue, e => e.DocumentWorkflowInstance.StartedAt >= startedAtMin!.Value).WhereIf(startedAtMax.HasValue, e => e.DocumentWorkflowInstance.StartedAt <= startedAtMax!.Value).WhereIf(finishedAtMin.HasValue, e => e.DocumentWorkflowInstance.FinishedAt >= finishedAtMin!.Value).WhereIf(finishedAtMax.HasValue, e => e.DocumentWorkflowInstance.FinishedAt <= finishedAtMax!.Value).WhereIf(documentId != null && documentId != Guid.Empty, e => e.Document != null && e.Document.Id == documentId).WhereIf(workflowId != null && workflowId != Guid.Empty, e => e.Workflow != null && e.Workflow.Id == workflowId).WhereIf(workflowTemplateId != null && workflowTemplateId != Guid.Empty, e => e.WorkflowTemplate != null && e.WorkflowTemplate.Id == workflowTemplateId).WhereIf(currentStepId != null && currentStepId != Guid.Empty, e => e.CurrentStep != null && e.CurrentStep.Id == currentStepId);
    }

    public virtual async Task<List<DocumentWorkflowInstance>> GetListAsync(string? filterText = null, string? status = null, DateTime? startedAtMin = null, DateTime? startedAtMax = null, DateTime? finishedAtMin = null, DateTime? finishedAtMax = null, string? sorting = null, int maxResultCount = int.MaxValue, int skipCount = 0, CancellationToken cancellationToken = default)
    {
        var query = ApplyFilter((await GetQueryableAsync()), filterText, status, startedAtMin, startedAtMax, finishedAtMin, finishedAtMax);
        query = query.OrderBy(string.IsNullOrWhiteSpace(sorting) ? DocumentWorkflowInstanceConsts.GetDefaultSorting(false) : sorting);
        return await query.PageBy(skipCount, maxResultCount).ToListAsync(cancellationToken);
    }

    public virtual async Task<long> GetCountAsync(string? filterText = null, string? status = null, DateTime? startedAtMin = null, DateTime? startedAtMax = null, DateTime? finishedAtMin = null, DateTime? finishedAtMax = null, Guid? documentId = null, Guid? workflowId = null, Guid? workflowTemplateId = null, Guid? currentStepId = null, CancellationToken cancellationToken = default)
    {
        var query = await GetQueryForNavigationPropertiesAsync();
        query = ApplyFilter(query, filterText, status, startedAtMin, startedAtMax, finishedAtMin, finishedAtMax, documentId, workflowId, workflowTemplateId, currentStepId);
        return await query.LongCountAsync(GetCancellationToken(cancellationToken));
    }

    protected virtual IQueryable<DocumentWorkflowInstance> ApplyFilter(IQueryable<DocumentWorkflowInstance> query, string? filterText = null, string? status = null, DateTime? startedAtMin = null, DateTime? startedAtMax = null, DateTime? finishedAtMin = null, DateTime? finishedAtMax = null)
    {
        return query.WhereIf(!string.IsNullOrWhiteSpace(filterText), e => e.Status!.Contains(filterText!)).WhereIf(!string.IsNullOrWhiteSpace(status), e => e.Status.Contains(status)).WhereIf(startedAtMin.HasValue, e => e.StartedAt >= startedAtMin!.Value).WhereIf(startedAtMax.HasValue, e => e.StartedAt <= startedAtMax!.Value).WhereIf(finishedAtMin.HasValue, e => e.FinishedAt >= finishedAtMin!.Value).WhereIf(finishedAtMax.HasValue, e => e.FinishedAt <= finishedAtMax!.Value);
    }
}