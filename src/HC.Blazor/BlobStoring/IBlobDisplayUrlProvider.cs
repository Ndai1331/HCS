namespace HC.Blazor.BlobStoring;

/// <summary>
/// Resolves blob storage paths to URLs for &lt;img src&gt; (direct MinIO public URL or API proxy).
/// </summary>
public interface IBlobDisplayUrlProvider
{
    string GetDisplayUrl(string? path);
}
