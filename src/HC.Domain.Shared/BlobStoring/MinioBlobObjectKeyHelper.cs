using System;

namespace HC.BlobStoring;

/// <summary>
/// Builds MinIO object keys the same way as ABP MinIO blob name calculator (tenant/host prefix).
/// </summary>
public static class MinioBlobObjectKeyHelper
{
    /// <summary>
    /// Returns the full object key stored in the bucket for a logical blob name.
    /// If <paramref name="blobName"/> already starts with tenants/ or host/, it is returned trimmed (no double prefix).
    /// </summary>
    public static string GetObjectKeyForPublicUrl(string blobName, Guid? tenantId)
    {
        if (string.IsNullOrWhiteSpace(blobName))
        {
            return string.Empty;
        }

        var name = blobName.TrimStart('/').Replace('\\', '/');

        if (name.StartsWith("tenants/", StringComparison.Ordinal) ||
            name.StartsWith("host/", StringComparison.Ordinal))
        {
            return name;
        }

        if (tenantId.HasValue)
        {
            return $"tenants/{tenantId.Value}/{name}";
        }

        return $"host/{name}";
    }
}
