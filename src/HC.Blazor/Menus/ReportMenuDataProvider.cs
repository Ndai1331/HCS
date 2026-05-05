using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HC.Reports;
using Microsoft.Extensions.Logging;

namespace HC.Blazor.Menus;

public class ReportMenuDataProvider : IReportMenuDataProvider
{
    private readonly IReportsAppService _reportsAppService;
    private readonly ILogger<ReportMenuDataProvider> _logger;
    private readonly object _lock = new();
    private List<ReportDto> _cachedReports = new();

    public ReportMenuDataProvider(
        IReportsAppService reportsAppService,
        ILogger<ReportMenuDataProvider> logger)
    {
        _reportsAppService = reportsAppService;
        _logger = logger;
    }

    public IReadOnlyList<ReportDto> GetCachedReports()
    {
        lock (_lock)
        {
            return _cachedReports.ToList();
        }
    }

    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        const int maxResultCount = 100;
        var skipCount = 0;
        var reports = new List<ReportDto>();

        while (true)
        {
            var reportsResult = await _reportsAppService.GetListAsync(new GetReportsInput
            {
                SkipCount = skipCount,
                MaxResultCount = maxResultCount,
                Sorting = $"{nameof(ReportDto.SortOrder)} asc"
            });

            if (reportsResult.Items.Count == 0)
            {
                break;
            }

            reports.AddRange(reportsResult.Items);
            skipCount += reportsResult.Items.Count;

            if (reportsResult.Items.Count < maxResultCount)
            {
                break;
            }
        }

        lock (_lock)
        {
            _cachedReports = reports;
        }

        _logger.LogDebug("Report menu cache refreshed with {Count} items.", reports.Count);
    }
}
