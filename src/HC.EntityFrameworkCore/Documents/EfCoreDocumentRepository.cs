using HC.Workflows;
using HC.Units;
using HC.MasterDatas;
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

namespace HC.Documents;

public abstract class EfCoreDocumentRepositoryBase : EfCoreRepository<HCDbContext, Document, Guid>
{
    public EfCoreDocumentRepositoryBase(IDbContextProvider<HCDbContext> dbContextProvider) : base(dbContextProvider)
    {
    }

    public virtual async Task DeleteAllAsync(string? filterText = null, string? no = null,
     string? title = null, string? currentStatus = null, DateTime? completedTimeMin = null,
      DateTime? completedTimeMax = null, string? storageNumber = null,
       DateTime? incommingDateMin = null, DateTime? incommingDateMax = null, 
       Guid? fieldId = null, Guid? unitId = null, Guid? workflowId = null, Guid? statusId = null, 
       Guid? typeId = null, Guid? urgencyLevelId = null, Guid? secrecyLevelId = null, Guid? creatorId = null, CancellationToken cancellationToken = default)
    {
        var query = await GetQueryForNavigationPropertiesAsync();
        query = ApplyFilter(query, filterText, no, title, currentStatus, completedTimeMin, completedTimeMax, storageNumber, incommingDateMin, incommingDateMax, fieldId, unitId, workflowId, statusId, typeId, urgencyLevelId, secrecyLevelId, creatorId);
        var ids = query.Select(x => x.Document.Id);
        await DeleteManyAsync(ids, cancellationToken: GetCancellationToken(cancellationToken));
    }

    public virtual async Task<DocumentWithNavigationProperties> GetWithNavigationPropertiesAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var dbContext = await GetDbContextAsync();
        return (await GetDbSetAsync()).Where(b => b.Id == id).Select(document => new DocumentWithNavigationProperties { Document = document, Field = dbContext.Set<MasterData>().FirstOrDefault(c => c.Id == document.FieldId), Unit = dbContext.Set<Unit>().FirstOrDefault(c => c.Id == document.UnitId), Workflow = dbContext.Set<Workflow>().FirstOrDefault(c => c.Id == document.WorkflowId), Status = dbContext.Set<MasterData>().FirstOrDefault(c => c.Id == document.StatusId), Type = dbContext.Set<MasterData>().FirstOrDefault(c => c.Id == document.TypeId), UrgencyLevel = dbContext.Set<MasterData>().FirstOrDefault(c => c.Id == document.UrgencyLevelId), SecrecyLevel = dbContext.Set<MasterData>().FirstOrDefault(c => c.Id == document.SecrecyLevelId) }).FirstOrDefault();
    }

    public virtual async Task<List<DocumentWithNavigationProperties>> GetListWithNavigationPropertiesAsync(string? filterText = null, string? no = null, string? title = null, string? currentStatus = null, DateTime? completedTimeMin = null, DateTime? completedTimeMax = null, string? storageNumber = null, DateTime? incommingDateMin = null, DateTime? incommingDateMax = null, Guid? fieldId = null, Guid? unitId = null, Guid? workflowId = null, Guid? statusId = null, Guid? typeId = null, Guid? urgencyLevelId = null, Guid? secrecyLevelId = null, Guid? creatorId = null, string? sorting = null, int maxResultCount = int.MaxValue, int skipCount = 0, CancellationToken cancellationToken = default)
    {
        var query = await GetQueryForNavigationPropertiesAsync();
        query = ApplyFilter(query, filterText, no, title, currentStatus, completedTimeMin, completedTimeMax, storageNumber, incommingDateMin, incommingDateMax, fieldId, unitId, workflowId, statusId, typeId, urgencyLevelId, secrecyLevelId, creatorId);
        query = query.OrderBy(string.IsNullOrWhiteSpace(sorting) ? DocumentConsts.GetDefaultSorting(true) : sorting);
        return await query.PageBy(skipCount, maxResultCount).ToListAsync(cancellationToken);
    }

    protected virtual async Task<IQueryable<DocumentWithNavigationProperties>> GetQueryForNavigationPropertiesAsync()
    {
        return from document in (await GetDbSetAsync())
               join field in (await GetDbContextAsync()).Set<MasterData>() on document.FieldId equals field.Id into masterDatas
               from field in masterDatas.DefaultIfEmpty()
               join unit in (await GetDbContextAsync()).Set<Unit>() on document.UnitId equals unit.Id into units
               from unit in units.DefaultIfEmpty()
               join workflow in (await GetDbContextAsync()).Set<Workflow>() on document.WorkflowId equals workflow.Id into workflows
               from workflow in workflows.DefaultIfEmpty()
               join status in (await GetDbContextAsync()).Set<MasterData>() on document.StatusId equals status.Id into masterDatas1
               from status in masterDatas1.DefaultIfEmpty()
               join type in (await GetDbContextAsync()).Set<MasterData>() on document.TypeId equals type.Id into masterDatas2
               from type in masterDatas2.DefaultIfEmpty()
               join urgencyLevel in (await GetDbContextAsync()).Set<MasterData>() on document.UrgencyLevelId equals urgencyLevel.Id into masterDatas3
               from urgencyLevel in masterDatas3.DefaultIfEmpty()
               join secrecyLevel in (await GetDbContextAsync()).Set<MasterData>() on document.SecrecyLevelId equals secrecyLevel.Id into masterDatas4
               from secrecyLevel in masterDatas4.DefaultIfEmpty()
               select new DocumentWithNavigationProperties
               {
                   Document = document,
                   Field = field,
                   Unit = unit,
                   Workflow = workflow,
                   Status = status,
                   Type = type,
                   UrgencyLevel = urgencyLevel,
                   SecrecyLevel = secrecyLevel
               };
    }

    protected virtual IQueryable<DocumentWithNavigationProperties> ApplyFilter(IQueryable<DocumentWithNavigationProperties> query, string? filterText, string? no = null, string? title = null, string? currentStatus = null, DateTime? completedTimeMin = null, DateTime? completedTimeMax = null, string? storageNumber = null, DateTime? incommingDateMin = null, DateTime? incommingDateMax = null, Guid? fieldId = null, Guid? unitId = null, Guid? workflowId = null, Guid? statusId = null, Guid? typeId = null, Guid? urgencyLevelId = null, Guid? secrecyLevelId = null, Guid? creatorId = null)
    {
        return query.WhereIf(!string.IsNullOrWhiteSpace(filterText), e => e.Document.No!.Contains(filterText!) || e.Document.Title!.Contains(filterText!) || e.Document.CurrentStatus!.Contains(filterText!) || e.Document.StorageNumber!.Contains(filterText!)).WhereIf(!string.IsNullOrWhiteSpace(no), e => e.Document.No.Contains(no)).WhereIf(!string.IsNullOrWhiteSpace(title), e => e.Document.Title.Contains(title)).WhereIf(!string.IsNullOrWhiteSpace(currentStatus), e => e.Document.CurrentStatus.Contains(currentStatus)).WhereIf(completedTimeMin.HasValue, e => e.Document.CompletedTime >= completedTimeMin!.Value).WhereIf(completedTimeMax.HasValue, e => e.Document.CompletedTime <= completedTimeMax!.Value).WhereIf(!string.IsNullOrWhiteSpace(storageNumber), e => e.Document.StorageNumber.Contains(storageNumber)).WhereIf(incommingDateMin.HasValue, e => e.Document.IncommingDate >= incommingDateMin!.Value).WhereIf(incommingDateMax.HasValue, e => e.Document.IncommingDate <= incommingDateMax!.Value).WhereIf(fieldId != null && fieldId != Guid.Empty, e => e.Field != null && e.Field.Id == fieldId).WhereIf(unitId != null && unitId != Guid.Empty, e => e.Unit != null && e.Unit.Id == unitId).WhereIf(workflowId != null && workflowId != Guid.Empty, e => e.Workflow != null && e.Workflow.Id == workflowId).WhereIf(statusId != null && statusId != Guid.Empty, e => e.Status != null && e.Status.Id == statusId).WhereIf(typeId != null && typeId != Guid.Empty, e => e.Type != null && e.Type.Id == typeId).WhereIf(urgencyLevelId != null && urgencyLevelId != Guid.Empty, e => e.UrgencyLevel != null && e.UrgencyLevel.Id == urgencyLevelId).WhereIf(secrecyLevelId != null && secrecyLevelId != Guid.Empty, e => e.SecrecyLevel != null && e.SecrecyLevel.Id == secrecyLevelId).WhereIf(creatorId != null && creatorId != Guid.Empty, e => e.Document.CreatorId == creatorId);
    }

    public virtual async Task<List<Document>> GetListAsync(string? filterText = null, string? no = null, string? title = null, string? currentStatus = null, DateTime? completedTimeMin = null, DateTime? completedTimeMax = null, string? storageNumber = null, DateTime? incommingDateMin = null, DateTime? incommingDateMax = null, Guid? fieldId = null, Guid? unitId = null, Guid? workflowId = null, Guid? statusId = null, Guid? typeId = null, Guid? urgencyLevelId = null, Guid? secrecyLevelId = null, Guid? creatorId = null, string? sorting = null, int maxResultCount = int.MaxValue, int skipCount = 0, CancellationToken cancellationToken = default)
    {
        var query = ApplyFilter((await GetQueryableAsync()), filterText, no, title, currentStatus, completedTimeMin, completedTimeMax, storageNumber, incommingDateMin, incommingDateMax, fieldId, unitId, workflowId, statusId, typeId, urgencyLevelId, secrecyLevelId, creatorId);
        query = query.OrderBy(string.IsNullOrWhiteSpace(sorting) ? DocumentConsts.GetDefaultSorting(false) : sorting);
        return await query.PageBy(skipCount, maxResultCount).ToListAsync(cancellationToken);
    }

    public virtual async Task<long> GetCountAsync(string? filterText = null, string? no = null, string? title = null, string? currentStatus = null, DateTime? completedTimeMin = null, DateTime? completedTimeMax = null, string? storageNumber = null, DateTime? incommingDateMin = null, DateTime? incommingDateMax = null, Guid? fieldId = null, Guid? unitId = null, Guid? workflowId = null, Guid? statusId = null, Guid? typeId = null, Guid? urgencyLevelId = null, Guid? secrecyLevelId = null, Guid? creatorId = null, CancellationToken cancellationToken = default)
    {
        var query = await GetQueryForNavigationPropertiesAsync();
        query = ApplyFilter(query, filterText, no, title, currentStatus, completedTimeMin, completedTimeMax, storageNumber, incommingDateMin, incommingDateMax, fieldId, unitId, workflowId, statusId, typeId, urgencyLevelId, secrecyLevelId, creatorId);
        return await query.LongCountAsync(GetCancellationToken(cancellationToken));
    }

    protected virtual IQueryable<Document> ApplyFilter(IQueryable<Document> query, string? filterText = null, string? no = null, string? title = null, string? currentStatus = null, DateTime? completedTimeMin = null, DateTime? completedTimeMax = null, string? storageNumber = null, DateTime? incommingDateMin = null, DateTime? incommingDateMax = null, Guid? fieldId = null, Guid? unitId = null, Guid? workflowId = null, Guid? statusId = null, Guid? typeId = null, Guid? urgencyLevelId = null, Guid? secrecyLevelId = null, Guid? creatorId = null)
    {
        return query.WhereIf(!string.IsNullOrWhiteSpace(filterText), e => e.No!.Contains(filterText!) || e.Title!.Contains(filterText!) || e.CurrentStatus!.Contains(filterText!) || e.StorageNumber!.Contains(filterText!)).WhereIf(!string.IsNullOrWhiteSpace(no), e => e.No.Contains(no)).WhereIf(!string.IsNullOrWhiteSpace(title), e => e.Title.Contains(title)).WhereIf(!string.IsNullOrWhiteSpace(currentStatus), e => e.CurrentStatus.Contains(currentStatus)).WhereIf(completedTimeMin.HasValue, e => e.CompletedTime >= completedTimeMin!.Value).WhereIf(completedTimeMax.HasValue, e => e.CompletedTime <= completedTimeMax!.Value).WhereIf(!string.IsNullOrWhiteSpace(storageNumber), e => e.StorageNumber.Contains(storageNumber)).WhereIf(incommingDateMin.HasValue, e => e.IncommingDate >= incommingDateMin!.Value).WhereIf(incommingDateMax.HasValue, e => e.IncommingDate <= incommingDateMax!.Value).WhereIf(fieldId != null && fieldId != Guid.Empty, e => e.FieldId == fieldId).WhereIf(unitId != null && unitId != Guid.Empty, e => e.UnitId == unitId).WhereIf(workflowId != null && workflowId != Guid.Empty, e => e.WorkflowId == workflowId).WhereIf(statusId != null && statusId != Guid.Empty, e => e.StatusId == statusId).WhereIf(typeId != null && typeId != Guid.Empty, e => e.TypeId == typeId).WhereIf(urgencyLevelId != null && urgencyLevelId != Guid.Empty, e => e.UrgencyLevelId == urgencyLevelId).WhereIf(secrecyLevelId != null && secrecyLevelId != Guid.Empty, e => e.SecrecyLevelId == secrecyLevelId).WhereIf(creatorId != null && creatorId != Guid.Empty, e => e.CreatorId == creatorId);
    }
}