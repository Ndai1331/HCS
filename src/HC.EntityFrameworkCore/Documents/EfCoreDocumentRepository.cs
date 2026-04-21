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
       Guid? typeId = null, Guid? urgencyLevelId = null, Guid? secrecyLevelId = null, DocumentSourceType? sourceType = null, Guid? creatorId = null, CancellationToken cancellationToken = default)
    {
        var query = await GetQueryForNavigationPropertiesAsync(trackEntities: false);
        query = ApplyFilter(query, filterText, no, title, currentStatus, completedTimeMin, completedTimeMax, storageNumber, incommingDateMin, incommingDateMax, fieldId, unitId, workflowId, statusId, typeId, urgencyLevelId, secrecyLevelId, sourceType, creatorId);
        var ids = query.Select(x => x.Document.Id);
        await DeleteManyAsync(ids, cancellationToken: GetCancellationToken(cancellationToken));
    }

    public virtual async Task<DocumentWithNavigationProperties> GetWithNavigationPropertiesAsync(Guid id, CancellationToken cancellationToken = default)
    {
        // M2: reuse the LEFT JOIN query used by list-mode instead of issuing 7 correlated
        // subqueries (one per nav-property) via `FirstOrDefault` inside `Select`.
        var query = await GetQueryForNavigationPropertiesAsync(trackEntities: false);
        return await query
            .Where(x => x.Document.Id == id)
            .FirstOrDefaultAsync(GetCancellationToken(cancellationToken));
    }

    public virtual async Task<List<DocumentWithNavigationProperties>> GetListWithNavigationPropertiesAsync(string? filterText = null, string? no = null, string? title = null, string? currentStatus = null, DateTime? completedTimeMin = null, DateTime? completedTimeMax = null, string? storageNumber = null, DateTime? incommingDateMin = null, DateTime? incommingDateMax = null, Guid? fieldId = null, Guid? unitId = null, Guid? workflowId = null, Guid? statusId = null, Guid? typeId = null, Guid? urgencyLevelId = null, Guid? secrecyLevelId = null, DocumentSourceType? sourceType = null, Guid? creatorId = null, List<Guid>? documentIds = null, string? sorting = null, int maxResultCount = int.MaxValue, int skipCount = 0, CancellationToken cancellationToken = default)
    {
        var query = await GetQueryForNavigationPropertiesAsync(trackEntities: false);
        query = ApplyFilter(query, filterText, no, title, currentStatus, completedTimeMin, completedTimeMax, storageNumber, incommingDateMin, incommingDateMax, fieldId, unitId, workflowId, statusId, typeId, urgencyLevelId, secrecyLevelId, sourceType, creatorId, documentIds);
        query = query.OrderBy(string.IsNullOrWhiteSpace(sorting) ? DocumentConsts.GetDefaultSorting(true) : sorting);
        return await query.PageBy(skipCount, maxResultCount).ToListAsync(cancellationToken);
    }

    protected virtual async Task<IQueryable<DocumentWithNavigationProperties>> GetQueryForNavigationPropertiesAsync(bool trackEntities = true)
    {
        var dbContext = await GetDbContextAsync();
        var documents = await GetDbSetAsync();
        var documentQuery = trackEntities ? documents : documents.AsNoTracking();
        var masterData = dbContext.Set<MasterData>().AsNoTracking();
        var units = dbContext.Set<Unit>().AsNoTracking();
        var workflows = dbContext.Set<Workflow>().AsNoTracking();

        return from document in documentQuery
               join field in masterData on document.FieldId equals field.Id into masterDatas
               from field in masterDatas.DefaultIfEmpty()
               join unit in units on document.UnitId equals unit.Id into unitJoin
               from unit in unitJoin.DefaultIfEmpty()
               join workflow in workflows on document.WorkflowId equals workflow.Id into workflowsJoin
               from workflow in workflowsJoin.DefaultIfEmpty()
               join status in masterData on document.StatusId equals status.Id into masterDatas1
               from status in masterDatas1.DefaultIfEmpty()
               join type in masterData on document.TypeId equals type.Id into masterDatas2
               from type in masterDatas2.DefaultIfEmpty()
               join urgencyLevel in masterData on document.UrgencyLevelId equals urgencyLevel.Id into masterDatas3
               from urgencyLevel in masterDatas3.DefaultIfEmpty()
               join secrecyLevel in masterData on document.SecrecyLevelId equals secrecyLevel.Id into masterDatas4
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

    protected virtual IQueryable<DocumentWithNavigationProperties> ApplyFilter(IQueryable<DocumentWithNavigationProperties> query, string? filterText, string? no = null, string? title = null, string? currentStatus = null, DateTime? completedTimeMin = null, DateTime? completedTimeMax = null, string? storageNumber = null, DateTime? incommingDateMin = null, DateTime? incommingDateMax = null, Guid? fieldId = null, Guid? unitId = null, Guid? workflowId = null, Guid? statusId = null, Guid? typeId = null, Guid? urgencyLevelId = null, Guid? secrecyLevelId = null, DocumentSourceType? sourceType = null, Guid? creatorId = null, List<Guid>? documentIds = null)
    {
        var hasCreator = creatorId != null && creatorId != Guid.Empty;
        var hasDocumentIds = documentIds != null && documentIds.Count > 0;

        var filterPattern = string.IsNullOrWhiteSpace(filterText) ? null : "%" + filterText + "%";
        var queryWithFilters = query
            .WhereIf(filterPattern != null, e =>
                (e.Document.No != null && EF.Functions.ILike(e.Document.No, filterPattern!)) ||
                (e.Document.Title != null && EF.Functions.ILike(e.Document.Title, filterPattern!)) ||
                (e.Document.CurrentStatus != null && EF.Functions.ILike(e.Document.CurrentStatus, filterPattern!)) ||
                (e.Document.StorageNumber != null && EF.Functions.ILike(e.Document.StorageNumber, filterPattern!)))
            .WhereIf(!string.IsNullOrWhiteSpace(no), e => e.Document.No.Contains(no))
            .WhereIf(!string.IsNullOrWhiteSpace(title), e => e.Document.Title.Contains(title))
            .WhereIf(!string.IsNullOrWhiteSpace(currentStatus), e => e.Document.CurrentStatus.Contains(currentStatus))
            .WhereIf(completedTimeMin.HasValue, e => e.Document.CompletedTime >= completedTimeMin!.Value)
            .WhereIf(completedTimeMax.HasValue, e => e.Document.CompletedTime <= completedTimeMax!.Value)
            .WhereIf(!string.IsNullOrWhiteSpace(storageNumber), e => e.Document.StorageNumber.Contains(storageNumber))
            .WhereIf(incommingDateMin.HasValue, e => e.Document.IncommingDate >= incommingDateMin!.Value)
            .WhereIf(incommingDateMax.HasValue, e => e.Document.IncommingDate <= incommingDateMax!.Value)
            .WhereIf(fieldId != null && fieldId != Guid.Empty, e => e.Field != null && e.Field.Id == fieldId)
            .WhereIf(unitId != null && unitId != Guid.Empty, e => e.Unit != null && e.Unit.Id == unitId)
            .WhereIf(workflowId != null && workflowId != Guid.Empty, e => e.Workflow != null && e.Workflow.Id == workflowId)
            .WhereIf(statusId != null && statusId != Guid.Empty, e => e.Status != null && e.Status.Id == statusId)
            .WhereIf(typeId != null && typeId != Guid.Empty, e => e.Type != null && e.Type.Id == typeId)
            .WhereIf(urgencyLevelId != null && urgencyLevelId != Guid.Empty, e => e.UrgencyLevel != null && e.UrgencyLevel.Id == urgencyLevelId)
            .WhereIf(secrecyLevelId != null && secrecyLevelId != Guid.Empty, e => e.SecrecyLevel != null && e.SecrecyLevel.Id == secrecyLevelId)
            .WhereIf(sourceType.HasValue && !hasDocumentIds, e => e.Document.SourceType == sourceType!.Value);

        if (hasCreator && hasDocumentIds)
        {
            return queryWithFilters.Where(e => e.Document.CreatorId == creatorId || documentIds!.Contains(e.Document.Id));
        }

        return queryWithFilters
            .WhereIf(hasCreator, e => e.Document.CreatorId == creatorId)
            .WhereIf(hasDocumentIds, e => documentIds!.Contains(e.Document.Id));
    }

    public virtual async Task<List<Document>> GetListAsync(string? filterText = null, string? no = null, string? title = null, string? currentStatus = null, DateTime? completedTimeMin = null, DateTime? completedTimeMax = null, string? storageNumber = null, DateTime? incommingDateMin = null, DateTime? incommingDateMax = null, Guid? fieldId = null, Guid? unitId = null, Guid? workflowId = null, Guid? statusId = null, Guid? typeId = null, Guid? urgencyLevelId = null, Guid? secrecyLevelId = null, DocumentSourceType? sourceType = null, Guid? creatorId = null, List<Guid>? documentIds = null, string? sorting = null, int maxResultCount = int.MaxValue, int skipCount = 0, CancellationToken cancellationToken = default)
    {
        var query = ApplyFilter((await GetQueryableAsync()).AsNoTracking(), filterText, no, title, currentStatus, completedTimeMin, completedTimeMax, storageNumber, incommingDateMin, incommingDateMax, fieldId, unitId, workflowId, statusId, typeId, urgencyLevelId, secrecyLevelId, sourceType, creatorId, documentIds);
        query = query.OrderBy(string.IsNullOrWhiteSpace(sorting) ? DocumentConsts.GetDefaultSorting(false) : sorting);
        return await query.PageBy(skipCount, maxResultCount).ToListAsync(cancellationToken);
    }

    public virtual async Task<long> GetCountAsync(string? filterText = null, string? no = null, string? title = null, string? currentStatus = null, DateTime? completedTimeMin = null, DateTime? completedTimeMax = null, string? storageNumber = null, DateTime? incommingDateMin = null, DateTime? incommingDateMax = null, Guid? fieldId = null, Guid? unitId = null, Guid? workflowId = null, Guid? statusId = null, Guid? typeId = null, Guid? urgencyLevelId = null, Guid? secrecyLevelId = null, DocumentSourceType? sourceType = null, Guid? creatorId = null, List<Guid>? documentIds = null, CancellationToken cancellationToken = default)
    {
        var query = await GetQueryForNavigationPropertiesAsync(trackEntities: false);
        query = ApplyFilter(query, filterText, no, title, currentStatus, completedTimeMin, completedTimeMax, storageNumber, incommingDateMin, incommingDateMax, fieldId, unitId, workflowId, statusId, typeId, urgencyLevelId, secrecyLevelId, sourceType, creatorId, documentIds);
        return await query.LongCountAsync(GetCancellationToken(cancellationToken));
    }

    protected virtual IQueryable<Document> ApplyFilter(IQueryable<Document> query, string? filterText = null, string? no = null, string? title = null, string? currentStatus = null, DateTime? completedTimeMin = null, DateTime? completedTimeMax = null, string? storageNumber = null, DateTime? incommingDateMin = null, DateTime? incommingDateMax = null, Guid? fieldId = null, Guid? unitId = null, Guid? workflowId = null, Guid? statusId = null, Guid? typeId = null, Guid? urgencyLevelId = null, Guid? secrecyLevelId = null, DocumentSourceType? sourceType = null, Guid? creatorId = null, List<Guid>? documentIds = null)
    {
        var filterPattern = string.IsNullOrWhiteSpace(filterText) ? null : "%" + filterText + "%";
        var queryWithFilters = query
            .WhereIf(filterPattern != null, e =>
                (e.No != null && EF.Functions.ILike(e.No, filterPattern!)) ||
                (e.Title != null && EF.Functions.ILike(e.Title, filterPattern!)) ||
                (e.CurrentStatus != null && EF.Functions.ILike(e.CurrentStatus, filterPattern!)) ||
                (e.StorageNumber != null && EF.Functions.ILike(e.StorageNumber, filterPattern!)))
            .WhereIf(!string.IsNullOrWhiteSpace(no), e => e.No.Contains(no))
            .WhereIf(!string.IsNullOrWhiteSpace(title), e => e.Title.Contains(title))
            .WhereIf(!string.IsNullOrWhiteSpace(currentStatus), e => e.CurrentStatus.Contains(currentStatus))
            .WhereIf(completedTimeMin.HasValue, e => e.CompletedTime >= completedTimeMin!.Value)
            .WhereIf(completedTimeMax.HasValue, e => e.CompletedTime <= completedTimeMax!.Value)
            .WhereIf(!string.IsNullOrWhiteSpace(storageNumber), e => e.StorageNumber.Contains(storageNumber))
            .WhereIf(incommingDateMin.HasValue, e => e.IncommingDate >= incommingDateMin!.Value)
            .WhereIf(incommingDateMax.HasValue, e => e.IncommingDate <= incommingDateMax!.Value)
            .WhereIf(fieldId != null && fieldId != Guid.Empty, e => e.FieldId == fieldId)
            .WhereIf(unitId != null && unitId != Guid.Empty, e => e.UnitId == unitId)
            .WhereIf(workflowId != null && workflowId != Guid.Empty, e => e.WorkflowId == workflowId)
            .WhereIf(statusId != null && statusId != Guid.Empty, e => e.StatusId == statusId)
            .WhereIf(typeId != null && typeId != Guid.Empty, e => e.TypeId == typeId)
            .WhereIf(urgencyLevelId != null && urgencyLevelId != Guid.Empty, e => e.UrgencyLevelId == urgencyLevelId)
            .WhereIf(secrecyLevelId != null && secrecyLevelId != Guid.Empty, e => e.SecrecyLevelId == secrecyLevelId)
            .WhereIf(sourceType.HasValue, e => e.SourceType == sourceType!.Value);

        var hasCreator = creatorId != null && creatorId != Guid.Empty;
        var hasDocumentIds = documentIds != null && documentIds.Count > 0;

        if (hasCreator && hasDocumentIds)
        {
            return queryWithFilters.Where(e => e.CreatorId == creatorId || documentIds!.Contains(e.Id));
        }

        return queryWithFilters
            .WhereIf(hasCreator, e => e.CreatorId == creatorId)
            .WhereIf(hasDocumentIds, e => documentIds!.Contains(e.Id));
    }
}
