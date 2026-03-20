using System.Threading.Tasks;
using HC.Documents;

namespace HC.Blazor.Pages.Documents;

public partial class Documents
{
    /// <summary>
    /// Set filter to Archive documents (Văn thư lưu trữ)
    /// </summary>
    protected async Task SetArchiveFilterAsync()
    {
        SelectedSourceType = DocumentSourceType.Archive;
        Filter.SourceType = DocumentSourceType.Archive;
        await SearchAsync();
        await InvokeAsync(StateHasChanged);
    }

    /// <summary>
    /// Set filter to Personal documents (Văn bản của tôi): SourceType = 1 AND CreatorId = current user.
    /// </summary>
    protected async Task SetPersonalFilterAsync()
    {
        SelectedSourceType = DocumentSourceType.Personal;
        Filter.SourceType = DocumentSourceType.Personal;
        Filter.CreatorId = CurrentUser.Id;
        await SearchAsync();
        await InvokeAsync(StateHasChanged);
    }

    /// <summary>
    /// Set filter to documents sent to current user (SourceType = 2 tab / inbox logic).
    /// </summary>
    protected async Task SetSentToMeFilterAsync()
    {
        SelectedSourceType = DocumentSourceType.SentToMe;
        Filter.SourceType = DocumentSourceType.SentToMe;
        Filter.CreatorId = null;
        await SearchAsync();
        await InvokeAsync(StateHasChanged);
    }

    /// <summary>
    /// Clear all filters including SourceType
    /// </summary>
    protected async Task ClearAllFiltersAsync()
    {
        SelectedSourceType = DocumentSourceType.Archive;
        Filter.SourceType = DocumentSourceType.Archive;
        Filter.CreatorId = null;
        await SearchAsync();
        await InvokeAsync(StateHasChanged);
    }
}