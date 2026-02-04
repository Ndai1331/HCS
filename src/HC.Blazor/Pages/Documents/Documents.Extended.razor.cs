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
    /// Set filter to Personal documents (Văn bản của tôi)
    /// Shows documents where SourceType = Personal AND (CreatorId = CurrentUserId OR assigned via DocumentAssignments)
    /// </summary>
    protected async Task SetPersonalFilterAsync()
    {
        SelectedSourceType = DocumentSourceType.Personal;
        Filter.SourceType = DocumentSourceType.Personal;
        
        // For personal documents, also filter by current user as creator
        Filter.CreatorId = CurrentUser.Id;
        
        await SearchAsync();
        await InvokeAsync(StateHasChanged);
    }

    /// <summary>
    /// Clear all filters including SourceType
    /// </summary>
    protected async Task ClearAllFiltersAsync()
    {
        SelectedSourceType = null;
        Filter.SourceType = null;
        Filter.CreatorId = null;
        await SearchAsync();
        await InvokeAsync(StateHasChanged);
    }
}