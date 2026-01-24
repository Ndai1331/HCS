using System;
using System.Threading.Tasks;
using HC.Blazor.Extensions;
using Microsoft.JSInterop;

namespace HC.Blazor.Pages.Chat1;

/// <summary>
/// Helper class for file download operations in chat.
/// Handles safe JavaScript interop for file downloads.
/// </summary>
public class FileDownloadHelper
{
    private readonly IJSRuntime _jsRuntime;

    public FileDownloadHelper(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }

    /// <summary>
    /// Downloads a file with base64 content.
    /// </summary>
    public async Task DownloadFileAsync(string fileName, string contentType, byte[] fileContent)
    {
        try
        {
            var base64 = Convert.ToBase64String(fileContent);
            var dataUrl = $"data:{contentType};base64,{base64}";
            
            // Create download link using JS
            await _jsRuntime.SafeInvokeVoidAsync("downloadFile", dataUrl, fileName);
        }
        catch (Exception)
        {
            // Silently fail - JS module might not be available
        }
    }
}
