using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using Volo.Abp;

namespace HC.PushNotificationWorker;

public class PushNotificationWorkerHostedService : IHostedService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<PushNotificationWorkerHostedService> _logger;
    private IAbpApplicationWithInternalServiceProvider? _application;

    public PushNotificationWorkerHostedService(
        IConfiguration configuration,
        ILogger<PushNotificationWorkerHostedService> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _application = await AbpApplicationFactory.CreateAsync<HCPushNotificationWorkerModule>(options =>
        {
            options.Services.ReplaceConfiguration(_configuration);
            options.UseAutofac();
            options.Services.AddLogging(c => c.AddSerilog());
        });

        await _application.InitializeAsync();
        _logger.LogInformation("HC PushNotificationWorker started — consuming chat events for FCM.");
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_application != null)
        {
            await _application.ShutdownAsync();
            _application.Dispose();
            _application = null;
        }
    }
}
