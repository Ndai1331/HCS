using Volo.Abp.Identity;
using HC.WorkflowStepTemplates;
using HC.DocumentFiles;
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

namespace HC.DocumentAssignments;

public abstract class EfCoreDocumentAssignmentRepositoryBase : EfCoreRepository<HCDbContext, DocumentAssignment, Guid>
{
    public EfCoreDocumentAssignmentRepositoryBase(IDbContextProvider<HCDbContext> dbContextProvider) : base(dbContextProvider)
    {
    }

    public virtual async Task DeleteAllAsync(string? filterText = null, int? stepOrderMin = null, int? stepOrderMax = null, string? actionType = null, string? status = null, DateTime? assignedAtMin = null, DateTime? assignedAtMax = null, DateTime? processedAtMin = null, DateTime? processedAtMax = null, bool? isCurrent = null, Guid? documentId = null, Guid? workflowStepTemplateId = null, Guid? receiverUserId = null, CancellationToken cancellationToken = default)
    {
        var query = await GetQueryForNavigationPropertiesAsync();
        query = ApplyFilter(query, filterText, stepOrderMin, stepOrderMax, actionType, status, assignedAtMin, assignedAtMax, processedAtMin, processedAtMax, isCurrent, documentId, workflowStepTemplateId, receiverUserId);
        var ids = query.Select(x => x.DocumentAssignment.Id);
        await DeleteManyAsync(ids, cancellationToken: GetCancellationToken(cancellationToken));
    }

    public virtual async Task<DocumentAssignmentWithNavigationProperties> GetWithNavigationPropertiesAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var dbContext = await GetDbContextAsync();
        return (await GetDbSetAsync()).Where(b => b.Id == id).Select(documentAssignment => new DocumentAssignmentWithNavigationProperties { DocumentAssignment = documentAssignment, Document = dbContext.Set<Document>().FirstOrDefault(c => c.Id == documentAssignment.DocumentId), WorkflowStepTemplate = dbContext.Set<WorkflowStepTemplate>().FirstOrDefault(c => c.Id == documentAssignment.WorkflowStepTemplateId), ReceiverUser = dbContext.Set<IdentityUser>().FirstOrDefault(c => c.Id == documentAssignment.ReceiverUserId), DocumentFileResult = dbContext.Set<DocumentFile>().FirstOrDefault(c => c.Id == documentAssignment.DocumentFileResultId) }).FirstOrDefault();
    }

    public virtual async Task<List<DocumentAssignmentWithNavigationProperties>> GetListWithNavigationPropertiesAsync(string? filterText = null, int? stepOrderMin = null, int? stepOrderMax = null, string? actionType = null, string? status = null, DateTime? assignedAtMin = null, DateTime? assignedAtMax = null, DateTime? processedAtMin = null, DateTime? processedAtMax = null, bool? isCurrent = null, Guid? documentId = null, Guid? workflowStepTemplateId = null, Guid? receiverUserId = null, string? sorting = null, int maxResultCount = int.MaxValue, int skipCount = 0, CancellationToken cancellationToken = default)
    {
        var query = await GetQueryForNavigationPropertiesAsync();
        query = ApplyFilter(query, filterText, stepOrderMin, stepOrderMax, actionType, status, assignedAtMin, assignedAtMax, processedAtMin, processedAtMax, isCurrent, documentId, workflowStepTemplateId, receiverUserId);
        query = query.OrderBy(string.IsNullOrWhiteSpace(sorting) ? DocumentAssignmentConsts.GetDefaultSorting(true) : sorting);
        return await query.PageBy(skipCount, maxResultCount).ToListAsync(cancellationToken);
    }

    protected virtual async Task<IQueryable<DocumentAssignmentWithNavigationProperties>> GetQueryForNavigationPropertiesAsync()
    {
        var dbContext = await GetDbContextAsync();
        var documentAssignments = await GetDbSetAsync();
        var documents = dbContext.Set<Document>();
        var workflowStepTemplates = dbContext.Set<WorkflowStepTemplate>();
        var identityUsers = dbContext.Set<IdentityUser>();
        var documentFiles = dbContext.Set<DocumentFile>();

        return from documentAssignment in documentAssignments
               join document in documents on documentAssignment.DocumentId equals document.Id into documentJoin
               from document in documentJoin.DefaultIfEmpty()
               join workflowStepTemplate in workflowStepTemplates on documentAssignment.WorkflowStepTemplateId equals workflowStepTemplate.Id into wstJoin
               from workflowStepTemplate in wstJoin.DefaultIfEmpty()
               join receiverUser in identityUsers on documentAssignment.ReceiverUserId equals receiverUser.Id into userJoin
               from receiverUser in userJoin.DefaultIfEmpty()
               join documentFileResult in documentFiles on documentAssignment.DocumentFileResultId equals documentFileResult.Id into fileJoin
               from documentFileResult in fileJoin.DefaultIfEmpty()
               where document != null && document.IsDeleted == false
               select new DocumentAssignmentWithNavigationProperties
               {
                   DocumentAssignment = documentAssignment,
                   Document = document,
                   WorkflowStepTemplate = workflowStepTemplate,
                   ReceiverUser = receiverUser,
                   DocumentFileResult = documentFileResult
               };
    }

    protected virtual IQueryable<DocumentAssignmentWithNavigationProperties> ApplyFilter(IQueryable<DocumentAssignmentWithNavigationProperties> query, string? filterText, int? stepOrderMin = null, int? stepOrderMax = null, string? actionType = null, string? status = null, DateTime? assignedAtMin = null, DateTime? assignedAtMax = null, DateTime? processedAtMin = null, DateTime? processedAtMax = null, bool? isCurrent = null, Guid? documentId = null, Guid? workflowStepTemplateId = null, Guid? receiverUserId = null)
    {
        return query.WhereIf(!string.IsNullOrWhiteSpace(filterText), e => e.DocumentAssignment.ActionType!.Contains(filterText!) 
        || e.DocumentAssignment.Status!.Contains(filterText!))
        .WhereIf(stepOrderMin.HasValue, e => e.DocumentAssignment.StepOrder >= stepOrderMin!.Value)
        .WhereIf(stepOrderMax.HasValue, e => e.DocumentAssignment.StepOrder <= stepOrderMax!.Value)
        .WhereIf(!string.IsNullOrWhiteSpace(actionType), e => e.DocumentAssignment.ActionType.Contains(actionType))
        .WhereIf(!string.IsNullOrWhiteSpace(status), e => e.DocumentAssignment.Status.Contains(status))
        .WhereIf(assignedAtMin.HasValue, e => e.DocumentAssignment.AssignedAt >= assignedAtMin!.Value)
        .WhereIf(assignedAtMax.HasValue, e => e.DocumentAssignment.AssignedAt <= assignedAtMax!.Value).WhereIf(processedAtMin.HasValue, e => e.DocumentAssignment.ProcessedAt >= processedAtMin!.Value).WhereIf(processedAtMax.HasValue, e => e.DocumentAssignment.ProcessedAt <= processedAtMax!.Value).WhereIf(isCurrent.HasValue, e => e.DocumentAssignment.IsCurrent == isCurrent).WhereIf(documentId != null && documentId != Guid.Empty, e => e.Document != null && e.Document.Id == documentId).WhereIf(workflowStepTemplateId != null && workflowStepTemplateId != Guid.Empty, e => e.WorkflowStepTemplate != null && e.WorkflowStepTemplate.Id == workflowStepTemplateId).WhereIf(receiverUserId != null && receiverUserId != Guid.Empty, e => e.ReceiverUser != null && e.ReceiverUser.Id == receiverUserId);
    }

    public virtual async Task<List<DocumentAssignment>> GetListAsync(string? filterText = null, int? stepOrderMin = null, int? stepOrderMax = null, string? actionType = null, string? status = null, DateTime? assignedAtMin = null, DateTime? assignedAtMax = null, DateTime? processedAtMin = null, DateTime? processedAtMax = null, bool? isCurrent = null, Guid? documentId = null, Guid? workflowStepTemplateId = null, Guid? receiverUserId = null, string? sorting = null, int maxResultCount = int.MaxValue, int skipCount = 0, CancellationToken cancellationToken = default)
    {
        // Join with Document table to filter out deleted documents
        var dbContext = await GetDbContextAsync();
        var documentAssignments = await GetDbSetAsync();
        var documents = dbContext.Set<Document>();
        var query = from documentAssignment in documentAssignments
                    join document in documents on documentAssignment.DocumentId equals document.Id into documentJoin
                    from document in documentJoin.DefaultIfEmpty()
                    where document != null && document.IsDeleted == false
                    select documentAssignment;
        
        query = ApplyFilter(query, filterText, stepOrderMin, stepOrderMax, actionType, status, assignedAtMin, assignedAtMax, processedAtMin, processedAtMax, isCurrent, documentId, workflowStepTemplateId, receiverUserId);
        query = query.OrderBy(string.IsNullOrWhiteSpace(sorting) ? DocumentAssignmentConsts.GetDefaultSorting(false) : sorting);
        return await query.PageBy(skipCount, maxResultCount).ToListAsync(cancellationToken);
    }

    public virtual async Task<long> GetCountAsync(string? filterText = null, int? stepOrderMin = null, int? stepOrderMax = null, string? actionType = null, string? status = null, DateTime? assignedAtMin = null, DateTime? assignedAtMax = null, DateTime? processedAtMin = null, DateTime? processedAtMax = null, bool? isCurrent = null, Guid? documentId = null, Guid? workflowStepTemplateId = null, Guid? receiverUserId = null, CancellationToken cancellationToken = default)
    {
        var query = await GetQueryForNavigationPropertiesAsync();
        query = ApplyFilter(query, filterText, stepOrderMin, stepOrderMax, actionType, status, assignedAtMin, assignedAtMax, processedAtMin, processedAtMax, isCurrent, documentId, workflowStepTemplateId, receiverUserId);
        return await query.LongCountAsync(GetCancellationToken(cancellationToken));
    }

    protected virtual IQueryable<DocumentAssignment> ApplyFilter(IQueryable<DocumentAssignment> query, string? filterText = null, int? stepOrderMin = null, int? stepOrderMax = null, string? actionType = null, string? status = null, DateTime? assignedAtMin = null, DateTime? assignedAtMax = null, DateTime? processedAtMin = null, DateTime? processedAtMax = null, bool? isCurrent = null, Guid? documentId = null, Guid? workflowStepTemplateId = null, Guid? receiverUserId = null)
    {
        return query.WhereIf(!string.IsNullOrWhiteSpace(filterText), e => e.ActionType!.Contains(filterText!) || e.Status!.Contains(filterText!)).WhereIf(stepOrderMin.HasValue, e => e.StepOrder >= stepOrderMin!.Value).WhereIf(stepOrderMax.HasValue, e => e.StepOrder <= stepOrderMax!.Value).WhereIf(!string.IsNullOrWhiteSpace(actionType), e => e.ActionType.Contains(actionType)).WhereIf(!string.IsNullOrWhiteSpace(status), e => e.Status.Contains(status)).WhereIf(assignedAtMin.HasValue, e => e.AssignedAt >= assignedAtMin!.Value).WhereIf(assignedAtMax.HasValue, e => e.AssignedAt <= assignedAtMax!.Value).WhereIf(processedAtMin.HasValue, e => e.ProcessedAt >= processedAtMin!.Value).WhereIf(processedAtMax.HasValue, e => e.ProcessedAt <= processedAtMax!.Value).WhereIf(isCurrent.HasValue, e => e.IsCurrent == isCurrent).WhereIf(documentId != null && documentId != Guid.Empty, e => e.DocumentId == documentId).WhereIf(workflowStepTemplateId != null && workflowStepTemplateId != Guid.Empty, e => e.WorkflowStepTemplateId == workflowStepTemplateId).WhereIf(receiverUserId != null && receiverUserId != Guid.Empty, e => e.ReceiverUserId == receiverUserId);
    }
}