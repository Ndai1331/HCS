using System;
using System.Linq;
using HC.BlobStoring;
using Microsoft.Extensions.Configuration;
using Volo.Abp.DependencyInjection;
using Volo.Abp.MultiTenancy;

namespace HC.Blazor.BlobStoring;

[ExposeServices(typeof(IBlobDisplayUrlProvider))]
public class BlobDisplayUrlProvider : IBlobDisplayUrlProvider, ITransientDependency
{
    private readonly IConfiguration _configuration;
    private readonly ICurrentTenant _currentTenant;

    public BlobDisplayUrlProvider(IConfiguration configuration, ICurrentTenant currentTenant)
    {
        _configuration = configuration;
        _currentTenant = currentTenant;
    }

    public string GetDisplayUrl(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return string.Empty;
        }

        if (Uri.TryCreate(path, UriKind.Absolute, out var absoluteUri)
            && (absoluteUri.Scheme == Uri.UriSchemeHttp || absoluteUri.Scheme == Uri.UriSchemeHttps))
        {
            return path;
        }

        var usePublic = _configuration.GetValue("MinIO:UseDirectPublicUrlsForImages", false);
        var publicBase = (_configuration["MinIO:PublicBaseUrl"] ?? string.Empty).TrimEnd('/');
        var bucket = _configuration["MinIO:BucketName"] ?? "hcsbucket";

        if (usePublic && !string.IsNullOrEmpty(publicBase))
        {
            var objectKey = MinioBlobObjectKeyHelper.GetObjectKeyForPublicUrl(path, _currentTenant.Id);
            if (string.IsNullOrEmpty(objectKey))
            {
                return string.Empty;
            }

            var encodedKey = string.Join("/", objectKey.Split('/', StringSplitOptions.RemoveEmptyEntries)
                .Select(Uri.EscapeDataString));
            return $"{publicBase}/{Uri.EscapeDataString(bucket)}/{encodedKey}";
        }

        var apiBase = GetBlobFilesApiBaseUrl();
        return $"{apiBase}api/app/blob-files/file?path={Uri.EscapeDataString(path)}";
    }

    private string GetBlobFilesApiBaseUrl()
    {
        var baseUrl = _configuration["RemoteServices:BlobFiles:BaseUrl"]
            ?? _configuration["RemoteServices:Default:BaseUrl"]
            ?? string.Empty;

        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            return "/";
        }

        return baseUrl.EndsWith('/') ? baseUrl : $"{baseUrl}/";
    }
}
