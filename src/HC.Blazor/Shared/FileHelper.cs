using System;
namespace HC.Blazor.Shared;
public class FileHelper
{
    public static string GetImageUrl(string apiBaseUrl,string imagePath)
    {
       if (string.IsNullOrEmpty(imagePath))
            return string.Empty;
            
        var baseUrl = apiBaseUrl ?? string.Empty;
        return $"{baseUrl}api/app/blob-files/file?path={Uri.EscapeDataString(imagePath)}";
    }

    public static bool IsPdfFileExtension(string fileName)
    {
        if (string.IsNullOrEmpty(fileName))
            return false;
            
        var extension = System.IO.Path.GetExtension(fileName).ToLowerInvariant();
        return extension == ".pdf";
    }
}