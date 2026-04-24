using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;

namespace HC.BackgroundJobWorker;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        System.Console.OutputEncoding = System.Text.Encoding.UTF8;

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .MinimumLevel.Override("Volo.Abp", LogEventLevel.Warning)
#if DEBUG
            .MinimumLevel.Override("HC", LogEventLevel.Debug)
#else
            .MinimumLevel.Override("HC", LogEventLevel.Information)
#endif
            .Enrich.FromLogContext()
            .WriteTo.Async(c => c.File("Logs/logs.txt"))
            .WriteTo.Async(c => c.Console())
            .CreateLogger();

        try
        {
            Log.Information("Starting HC.BackgroundJobWorker host.");
            var builder = Host.CreateApplicationBuilder(args);
            builder.Configuration.AddJsonFile("appsettings.secrets.json", optional: true, reloadOnChange: false);
            builder.Logging.ClearProviders();
            builder.Services.AddHostedService<BackgroundJobWorkerHostedService>();
            builder.Logging.AddSerilog();

            var host = builder.Build();
            await host.RunAsync();
            return 0;
        }
        catch (System.Exception ex)
        {
            Log.Fatal(ex, "HC.BackgroundJobWorker terminated unexpectedly.");
            return 1;
        }
        finally
        {
            await Log.CloseAndFlushAsync();
        }
    }
}
