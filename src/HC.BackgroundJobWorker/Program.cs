using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp;

namespace HC.BackgroundJobWorker;

public class Program
{
    private static async Task Main(string[] args)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;

        using (var application = await AbpApplicationFactory.CreateAsync<HCBackgroundJobWorkerModule>())
        {
            await application.InitializeAsync();

            await application.ServiceProvider
                .GetRequiredService<HCBackgroundJobWorkerService>()
                .RunAsync(args);
            
            await application.ShutdownAsync();
        }
    }
}