using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using Volo.Abp;

namespace HC.BackgroundJobWorker;

public class BackgroundJobWorkerHostedService : IHostedService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<BackgroundJobWorkerHostedService> _logger;
    private IAbpApplicationWithInternalServiceProvider? _application;

    public BackgroundJobWorkerHostedService(
        IConfiguration configuration,
        ILogger<BackgroundJobWorkerHostedService> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _application = await AbpApplicationFactory.CreateAsync<HCBackgroundJobWorkerModule>(options =>
        {
            options.Services.ReplaceConfiguration(_configuration);
            options.UseAutofac();
            options.Services.AddLogging(c => c.AddSerilog());
        });

        await _application.InitializeAsync();
        _logger.LogInformation("HC Background Job Worker started — processing ABP background jobs from queue.");
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
