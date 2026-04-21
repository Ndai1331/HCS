using System.Threading.Tasks;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.DependencyInjection;

namespace HC.Documents.BackgroundJobs;

public class ApproveWithNoteBackgroundJob : AsyncBackgroundJob<ApproveWithNoteBackgroundJobArgs>, ITransientDependency
{
    private readonly ApproveWithNoteJobExecutor _executor;

    public ApproveWithNoteBackgroundJob(ApproveWithNoteJobExecutor executor)
    {
        _executor = executor;
    }

    public override async Task ExecuteAsync(ApproveWithNoteBackgroundJobArgs args)
    {
        await _executor.ExecuteAsync(args.OperationId);
    }
}
