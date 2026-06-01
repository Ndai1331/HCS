using System.Threading;
using System.Threading.Tasks;

namespace HC.Blazor.Pages.Documents;

public partial class DocumentSigning
{
    private int _renderScheduled;

    /// <summary>
    /// Coalesces redundant InvokeAsync(StateHasChanged) calls within the same event loop tick.
    /// </summary>
    protected Task RequestRenderAsync()
    {
        if (Interlocked.CompareExchange(ref _renderScheduled, 1, 0) != 0)
        {
            return Task.CompletedTask;
        }

        return InvokeAsync(() =>
        {
            Interlocked.Exchange(ref _renderScheduled, 0);
            StateHasChanged();
        });
    }
}
