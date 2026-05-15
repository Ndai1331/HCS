using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq.Dynamic.Core;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using HC.Permissions;
using HC.Notifications;
using MiniExcelLibs;
using Volo.Abp.Content;
using Volo.Abp.Authorization;
using Volo.Abp.Caching;
using Microsoft.Extensions.Caching.Distributed;
using HC.Shared;

namespace HC.Notifications;

[RemoteService(IsEnabled = false)]
[Authorize(HCPermissions.Notifications.Default)]
public abstract class NotificationsAppServiceBase : HCAppService
{
    protected IDistributedCache<NotificationDownloadTokenCacheItem, string> _downloadTokenCache;
    protected INotificationRepository _notificationRepository;
    protected NotificationManager _notificationManager;

    public NotificationsAppServiceBase(INotificationRepository notificationRepository, NotificationManager notificationManager, IDistributedCache<NotificationDownloadTokenCacheItem, string> downloadTokenCache)
    {
        _downloadTokenCache = downloadTokenCache;
        _notificationRepository = notificationRepository;
        _notificationManager = notificationManager;
    }

    public virtual async Task<PagedResultDto<NotificationDto>> GetListAsync(GetNotificationsInput input)
    {
        var totalCount = await _notificationRepository.GetCountAsync(input.FilterText, input.Title, input.Content, input.SourceType, input.EventType, input.RelatedType, input.RelatedId, input.Priority);
        var items = await _notificationRepository.GetListAsync(input.FilterText, input.Title, input.Content, input.SourceType, input.EventType, input.RelatedType, input.RelatedId, input.Priority, input.Sorting, input.MaxResultCount, input.SkipCount);
        return new PagedResultDto<NotificationDto>
        {
            TotalCount = totalCount,
            Items = ObjectMapper.Map<List<Notification>, List<NotificationDto>>(items)
        };
    }

    public virtual async Task<NotificationDto> GetAsync(Guid id)
    {
        return ObjectMapper.Map<Notification, NotificationDto>(await _notificationRepository.GetAsync(id));
    }

    [Authorize(HCPermissions.Notifications.Delete)]
    public virtual async Task DeleteAsync(Guid id)
    {
        await _notificationRepository.DeleteAsync(id);
    }

    [Authorize(HCPermissions.Notifications.Create)]
    public virtual async Task<NotificationDto> CreateAsync(NotificationCreateDto input)
    {
        var notification = await _notificationManager.CreateAsync(input.Title, input.Content, input.SourceType, input.EventType, input.RelatedType, input.Priority, input.RelatedId);
        return ObjectMapper.Map<Notification, NotificationDto>(notification);
    }

    [Authorize(HCPermissions.Notifications.Edit)]
    public virtual async Task<NotificationDto> UpdateAsync(Guid id, NotificationUpdateDto input)
    {
        var notification = await _notificationManager.UpdateAsync(id, input.Title, input.Content, input.SourceType, input.EventType, input.RelatedType, input.Priority, input.RelatedId, input.ConcurrencyStamp);
        return ObjectMapper.Map<Notification, NotificationDto>(notification);
    }

    [AllowAnonymous]
    public virtual async Task<IRemoteStreamContent> GetListAsExcelFileAsync(NotificationExcelDownloadDto input)
    {
        await HC.ExcelDownloadAnonymousTokenHelper.ValidateAndConsumeOneTimeExportTokenAsync(_downloadTokenCache, input.DownloadToken, x => x.Token);

        var items = await _notificationRepository.GetListAsync(input.FilterText, input.Title, input.Content, input.SourceType, input.EventType, input.RelatedType, input.RelatedId, input.Priority);
        var memoryStream = new MemoryStream();
        await memoryStream.SaveAsAsync(ObjectMapper.Map<List<Notification>, List<NotificationExcelDto>>(items));
        memoryStream.Seek(0, SeekOrigin.Begin);
        return new RemoteStreamContent(memoryStream, "Notifications.xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
    }

    [Authorize(HCPermissions.Notifications.Delete)]
    public virtual async Task DeleteByIdsAsync(List<Guid> notificationIds)
    {
        await _notificationRepository.DeleteManyAsync(notificationIds);
    }

    [Authorize(HCPermissions.Notifications.Delete)]
    public virtual async Task DeleteAllAsync(GetNotificationsInput input)
    {
        await _notificationRepository.DeleteAllAsync(input.FilterText, input.Title, input.Content, input.SourceType, input.EventType, input.RelatedType, input.RelatedId, input.Priority);
    }

    public virtual async Task<HC.Shared.DownloadTokenResultDto> GetDownloadTokenAsync()
    {
        var token = Guid.NewGuid().ToString("N");
        await _downloadTokenCache.SetAsync(token, new NotificationDownloadTokenCacheItem { Token = token }, new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(30) });
        return new HC.Shared.DownloadTokenResultDto
        {
            Token = token
        };
    }
}