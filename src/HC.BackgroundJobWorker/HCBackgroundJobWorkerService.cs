using System;
using System.Threading.Tasks;
using Volo.Abp.DependencyInjection;

namespace HC.BackgroundJobWorker;

public class HCBackgroundJobWorkerService : ITransientDependency
{
    public Task RunAsync(string[] args)
    {
        Console.WriteLine("Press enter to exit.");
        Console.ReadLine();
        
        return Task.CompletedTask;
    }
}