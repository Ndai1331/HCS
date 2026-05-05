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

namespace HC.Reports;

public abstract class EfCoreReportRepositoryBase : EfCoreRepository<HCDbContext, Report, Guid>
{
    public EfCoreReportRepositoryBase(IDbContextProvider<HCDbContext> dbContextProvider) : base(dbContextProvider)
    {
    }

    public virtual async Task DeleteAllAsync(string? filterText = null, string? name = null, string? url = null, int? sortOrderMin = null, int? sortOrderMax = null, string? image = null, CancellationToken cancellationToken = default)
    {
        var query = await GetQueryableAsync();
        query = ApplyFilter(query, filterText, name, url, sortOrderMin, sortOrderMax, image);
        var ids = query.Select(x => x.Id);
        await DeleteManyAsync(ids, cancellationToken: GetCancellationToken(cancellationToken));
    }

    public virtual async Task<List<Report>> GetListAsync(string? filterText = null, string? name = null, string? url = null, int? sortOrderMin = null, int? sortOrderMax = null, string? image = null, string? sorting = null, int maxResultCount = int.MaxValue, int skipCount = 0, CancellationToken cancellationToken = default)
    {
        var query = ApplyFilter((await GetQueryableAsync()), filterText, name, url, sortOrderMin, sortOrderMax, image);
        query = query.OrderBy(string.IsNullOrWhiteSpace(sorting) ? ReportConsts.GetDefaultSorting(false) : sorting);
        return await query.PageBy(skipCount, maxResultCount).ToListAsync(cancellationToken);
    }

    public virtual async Task<long> GetCountAsync(string? filterText = null, string? name = null, string? url = null, int? sortOrderMin = null, int? sortOrderMax = null, string? image = null, CancellationToken cancellationToken = default)
    {
        var query = ApplyFilter((await GetDbSetAsync()), filterText, name, url, sortOrderMin, sortOrderMax, image);
        return await query.LongCountAsync(GetCancellationToken(cancellationToken));
    }

    protected virtual IQueryable<Report> ApplyFilter(IQueryable<Report> query, string? filterText = null, string? name = null, string? url = null, int? sortOrderMin = null, int? sortOrderMax = null, string? image = null)
    {
        return query.WhereIf(!string.IsNullOrWhiteSpace(filterText), e => e.Name!.Contains(filterText!) || e.Url!.Contains(filterText!) || e.Image!.Contains(filterText!)).WhereIf(!string.IsNullOrWhiteSpace(name), e => e.Name.Contains(name)).WhereIf(!string.IsNullOrWhiteSpace(url), e => e.Url.Contains(url)).WhereIf(sortOrderMin.HasValue, e => e.SortOrder >= sortOrderMin!.Value).WhereIf(sortOrderMax.HasValue, e => e.SortOrder <= sortOrderMax!.Value).WhereIf(!string.IsNullOrWhiteSpace(image), e => e.Image.Contains(image));
    }
}