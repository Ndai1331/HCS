using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories;

namespace HC.Reports;

public partial interface IReportRepository : IRepository<Report, Guid>
{
    Task DeleteAllAsync(string? filterText = null, string? name = null, string? url = null, int? sortOrderMin = null, int? sortOrderMax = null, string? image = null, CancellationToken cancellationToken = default);
    Task<List<Report>> GetListAsync(string? filterText = null, string? name = null, string? url = null, int? sortOrderMin = null, int? sortOrderMax = null, string? image = null, string? sorting = null, int maxResultCount = int.MaxValue, int skipCount = 0, CancellationToken cancellationToken = default);
    Task<long> GetCountAsync(string? filterText = null, string? name = null, string? url = null, int? sortOrderMin = null, int? sortOrderMax = null, string? image = null, CancellationToken cancellationToken = default);
}