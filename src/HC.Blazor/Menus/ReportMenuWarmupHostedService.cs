using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace HC.Blazor.Menus;

public class ReportMenuWarmupHostedService : BackgroundService
{
    private static readonly TimeSpan RefreshInterval = TimeSpan.FromMinutes(5);
    private readonly IReportMenuDataProvider _reportMenuDataProvider;
    private readonly ILogger<ReportMenuWarmupHostedService> _logger;

    public ReportMenuWarmupHostedService(
        IReportMenuDataProvider reportMenuDataProvider,
        ILogger<ReportMenuWarmupHostedService> logger)
    {
        _reportMenuDataProvider = reportMenuDataProvider;
        _logger = logger;
    }

    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _reportMenuDataProvider.RefreshAsync(cancellationToken);
            _logger.LogInformation("Report menu cache warmup completed at startup.");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Report menu cache warmup failed at startup.");
        }

        await base.StartAsync(cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await _reportMenuDataProvider.RefreshAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to refresh report menu cache.");
            }

            try
            {
                await Task.Delay(RefreshInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }
}
