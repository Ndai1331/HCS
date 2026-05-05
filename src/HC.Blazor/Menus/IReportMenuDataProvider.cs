using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using HC.Reports;

namespace HC.Blazor.Menus;

public interface IReportMenuDataProvider
{
    IReadOnlyList<ReportDto> GetCachedReports();
    Task RefreshAsync(CancellationToken cancellationToken = default);
}
