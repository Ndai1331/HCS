using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;
using Volo.Abp.BlobStoring;
using HC.BlobStoring;

namespace HC.Application.BlobStoring;

/// <summary>
/// Service để tracking và quản lý blob theo tenant/host
/// </summary>
public class HCBlobTrackingService : ApplicationService
{
    private readonly IBlobContainer _blobContainer;

    public HCBlobTrackingService(IBlobContainer blobContainer)
    {
        _blobContainer = blobContainer;
    }

    /// <summary>
    /// Lấy danh sách blob của tenant hiện tại
    /// </summary>
    public async Task<List<BlobInfo>> GetTenantBlobsAsync()
    {
        // Note: MinIO không có API list trực tiếp, cần implement custom logic
        // Hoặc sử dụng MinIO client để list objects với prefix "tenants/{tenant-id}/"
        var tenantId = CurrentTenant.Id;
        
        if (!tenantId.HasValue)
        {
            return new List<BlobInfo>();
        }

        // TODO: Implement logic để list blobs từ MinIO với prefix tenants/{tenant-id}/
        // Có thể sử dụng MinIO client trực tiếp hoặc lưu metadata trong database
        
        return new List<BlobInfo>();
    }

    /// <summary>
    /// Lấy danh sách blob của host
    /// </summary>
    public async Task<List<BlobInfo>> GetHostBlobsAsync()
    {
        // TODO: Implement logic để list blobs từ MinIO với prefix "host/"
        
        return new List<BlobInfo>();
    }

    /// <summary>
    /// Parse blob name để xác định tenant/host
    /// </summary>
    public BlobNameInfo ParseBlobName(string fullBlobName)
    {
        return HCBlobNameHelper.ParseBlobName(fullBlobName);
    }

    /// <summary>
    /// Kiểm tra blob có thuộc tenant hiện tại không
    /// </summary>
    public bool IsBlobBelongsToCurrentTenant(string fullBlobName)
    {
        var info = HCBlobNameHelper.ParseBlobName(fullBlobName);
        
        if (info.IsHost)
        {
            return false;
        }

        return info.TenantId == CurrentTenant.Id;
    }
}

/// <summary>
/// Thông tin về blob
/// </summary>
public class BlobInfo
{
    public string FullBlobName { get; set; } = string.Empty;
    public string OriginalBlobName { get; set; } = string.Empty;
    public Guid? TenantId { get; set; }
    public bool IsHost { get; set; }
    public long Size { get; set; }
    public DateTime? LastModified { get; set; }
}
