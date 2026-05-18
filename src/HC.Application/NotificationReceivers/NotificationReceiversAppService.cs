using HC.Shared;
using Volo.Abp.Identity;
using HC.Notifications;
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
using HC.NotificationReceivers;
using MiniExcelLibs;
using Volo.Abp.Content;
using Volo.Abp.Authorization;
using Volo.Abp.Caching;
using Microsoft.Extensions.Caching.Distributed;
using Volo.Abp.Users;
using Microsoft.EntityFrameworkCore;

namespace HC.NotificationReceivers;

[RemoteService(IsEnabled=false)]
[Authorize(HCPermissions.NotificationReceivers.Default)]
public abstract class NotificationReceiversAppServiceBase : HCAppService
{
    protected IDistributedCache<NotificationReceiverDownloadTokenCacheItem, string> _downloadTokenCache;
    protected INotificationReceiverRepository _notificationReceiverRepository;
    protected NotificationReceiverManager _notificationReceiverManager;
    protected IRepository<HC.Notifications.Notification, Guid> _notificationRepository;
    protected IRepository<Volo.Abp.Identity.IdentityUser, Guid> _identityUserRepository;

    public NotificationReceiversAppServiceBase(INotificationReceiverRepository notificationReceiverRepository, NotificationReceiverManager notificationReceiverManager, IDistributedCache<NotificationReceiverDownloadTokenCacheItem, string> downloadTokenCache, IRepository<HC.Notifications.Notification, Guid> notificationRepository, IRepository<Volo.Abp.Identity.IdentityUser, Guid> identityUserRepository)
    {
        _downloadTokenCache = downloadTokenCache;
        _notificationReceiverRepository = notificationReceiverRepository;
        _notificationReceiverManager = notificationReceiverManager;
        _notificationRepository = notificationRepository;
        _identityUserRepository = identityUserRepository;
    }

    public virtual async Task<PagedResultDto<NotificationReceiverWithNavigationPropertiesDto>> GetListAsync(GetNotificationReceiversInput input)
    {
        var totalCount = await _notificationReceiverRepository.GetCountAsync(input.FilterText, input.IsRead, input.ReadAtMin, input.ReadAtMax, input.NotificationId, input.IdentityUserId, input.SourceType, input.CreationTimeMin, input.CreationTimeMax);
        var items = await _notificationReceiverRepository.GetListWithNavigationPropertiesAsync(input.FilterText, input.IsRead, input.ReadAtMin, input.ReadAtMax, input.NotificationId, input.IdentityUserId, input.Sorting, input.MaxResultCount, input.SkipCount, input.SourceType, input.CreationTimeMin, input.CreationTimeMax);
        return new PagedResultDto<NotificationReceiverWithNavigationPropertiesDto>
        {
            TotalCount = totalCount,
            Items = ObjectMapper.Map<List<NotificationReceiverWithNavigationProperties>, List<NotificationReceiverWithNavigationPropertiesDto>>(items)
        };
    }

    public virtual async Task<NotificationReceiverWithNavigationPropertiesDto> GetWithNavigationPropertiesAsync(Guid id)
    {
        return ObjectMapper.Map<NotificationReceiverWithNavigationProperties, NotificationReceiverWithNavigationPropertiesDto>(await _notificationReceiverRepository.GetWithNavigationPropertiesAsync(id));
    }

    public virtual async Task<NotificationReceiverDto> GetAsync(Guid id)
    {
        return ObjectMapper.Map<NotificationReceiver, NotificationReceiverDto>(await _notificationReceiverRepository.GetAsync(id));
    }

    public virtual async Task<PagedResultDto<LookupDto<Guid>>> GetNotificationLookupAsync(LookupRequestDto input)
    {
        var query = (await _notificationRepository.GetQueryableAsync()).WhereIf(!string.IsNullOrWhiteSpace(input.Filter), x => x.Title != null && x.Title.Contains(input.Filter));
        var lookupData = await query.PageBy(input.SkipCount, input.MaxResultCount).ToDynamicListAsync<HC.Notifications.Notification>();
        var totalCount = await query.CountAsync();
        return new PagedResultDto<LookupDto<Guid>>
        {
            TotalCount = totalCount,
            Items = ObjectMapper.Map<List<HC.Notifications.Notification>, List<LookupDto<Guid>>>(lookupData)
        };
    }

    public virtual async Task<PagedResultDto<LookupDto<Guid>>> GetIdentityUserLookupAsync(LookupRequestDto input)
    {
        var query = (await _identityUserRepository.GetQueryableAsync()).WhereIf(!string.IsNullOrWhiteSpace(input.Filter), x => x.Name != null && x.Name.Contains(input.Filter));
        var lookupData = await query.PageBy(input.SkipCount, input.MaxResultCount).ToDynamicListAsync<Volo.Abp.Identity.IdentityUser>();
        var totalCount = await query.CountAsync();
        return new PagedResultDto<LookupDto<Guid>>
        {
            TotalCount = totalCount,
            Items = ObjectMapper.Map<List<Volo.Abp.Identity.IdentityUser>, List<LookupDto<Guid>>>(lookupData)
        };
    }

    [Authorize(HCPermissions.NotificationReceivers.Delete)]
    public virtual async Task DeleteAsync(Guid id)
    {
        await _notificationReceiverRepository.DeleteAsync(id);
    }

    [Authorize(HCPermissions.NotificationReceivers.Create)]
    public virtual async Task<NotificationReceiverDto> CreateAsync(NotificationReceiverCreateDto input)
    {
        if (input.NotificationId == default)
        {
            throw new UserFriendlyException(L["The {0} field is required.", L["Notification"]]);
        }

        if (input.IdentityUserId == default)
        {
            throw new UserFriendlyException(L["The {0} field is required.", L["IdentityUser"]]);
        }

        var notificationReceiver = await _notificationReceiverManager.CreateAsync(input.NotificationId, input.IdentityUserId, input.IsRead, input.ReadAt);
        return ObjectMapper.Map<NotificationReceiver, NotificationReceiverDto>(notificationReceiver);
    }

    [Authorize(HCPermissions.NotificationReceivers.Edit)]
    public virtual async Task<NotificationReceiverDto> UpdateAsync(Guid id, NotificationReceiverUpdateDto input)
    {
        if (input.NotificationId == default)
        {
            throw new UserFriendlyException(L["The {0} field is required.", L["Notification"]]);
        }

        if (input.IdentityUserId == default)
        {
            throw new UserFriendlyException(L["The {0} field is required.", L["IdentityUser"]]);
        }

        var notificationReceiver = await _notificationReceiverManager.UpdateAsync(id, input.NotificationId, input.IdentityUserId, input.IsRead, input.ReadAt, input.ConcurrencyStamp);
        return ObjectMapper.Map<NotificationReceiver, NotificationReceiverDto>(notificationReceiver);
    }

    [AllowAnonymous]
    public virtual async Task<IRemoteStreamContent> GetListAsExcelFileAsync(NotificationReceiverExcelDownloadDto input)
    {
        await HC.ExcelDownloadAnonymousTokenHelper.ValidateAndConsumeOneTimeExportTokenAsync(_downloadTokenCache, input.DownloadToken, x => x.Token);

        var notificationReceivers = await _notificationReceiverRepository.GetListWithNavigationPropertiesAsync(input.FilterText, input.IsRead, input.ReadAtMin, input.ReadAtMax, input.NotificationId, input.IdentityUserId);
        var items = notificationReceivers.Select(item => new { IsRead = item.NotificationReceiver.IsRead, ReadAt = item.NotificationReceiver.ReadAt, Notification = item.Notification?.Title, IdentityUser = item.IdentityUser?.Name, });
        var memoryStream = new MemoryStream();
        await memoryStream.SaveAsAsync(items);
        memoryStream.Seek(0, SeekOrigin.Begin);
        return new RemoteStreamContent(memoryStream, "NotificationReceivers.xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
    }

    [Authorize(HCPermissions.NotificationReceivers.Delete)]
    public virtual async Task DeleteByIdsAsync(List<Guid> notificationreceiverIds)
    {
        await _notificationReceiverRepository.DeleteManyAsync(notificationreceiverIds);
    }

    [Authorize(HCPermissions.NotificationReceivers.Delete)]
    public virtual async Task DeleteAllAsync(GetNotificationReceiversInput input)
    {
        await _notificationReceiverRepository.DeleteAllAsync(input.FilterText, input.IsRead, input.ReadAtMin, input.ReadAtMax, input.NotificationId, input.IdentityUserId, input.CreationTimeMin, input.CreationTimeMax);
    }

    public virtual async Task<HC.Shared.DownloadTokenResultDto> GetDownloadTokenAsync()
    {
        var token = Guid.NewGuid().ToString("N");
        await _downloadTokenCache.SetAsync(token, new NotificationReceiverDownloadTokenCacheItem { Token = token }, new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(30) });
        return new HC.Shared.DownloadTokenResultDto
        {
            Token = token
        };
    }
    // public virtual async Task<PagedResultDto<NotificationReceiverWithNavigationPropertiesDto>> GetReadNotificationsAsync(GetUserNotificationsInput input)
    // {
    //     var currentUserId = CurrentUser.GetId();
    //     var totalCount = await _notificationReceiverRepository.GetCountByUserAndReadStatusAsync(currentUserId, true, input.FilterText);
    //     var items = await _notificationReceiverRepository.GetNotificationsByUserAndReadStatusAsync(
    //         currentUserId,
    //         true,
    //         input.FilterText,
    //         input.Sorting,
    //         input.MaxResultCount,
    //         input.SkipCount);

    //     return new PagedResultDto<NotificationReceiverWithNavigationPropertiesDto>
    //     {
    //         TotalCount = totalCount,
    //         Items = ObjectMapper.Map<List<NotificationReceiverWithNavigationProperties>, List<NotificationReceiverWithNavigationPropertiesDto>>(items)
    //     };
    // }

    // public virtual async Task<PagedResultDto<NotificationReceiverWithNavigationPropertiesDto>> GetUnreadNotificationsAsync(GetUserNotificationsInput input)
    // {
    //     var currentUserId = CurrentUser.GetId();
    //     var totalCount = await _notificationReceiverRepository.GetCountByUserAndReadStatusAsync(currentUserId, false, input.FilterText);
    //     var items = await _notificationReceiverRepository.GetNotificationsByUserAndReadStatusAsync(
    //         currentUserId,
    //         false,
    //         input.FilterText,
    //         input.Sorting,
    //         input.MaxResultCount,
    //         input.SkipCount);

    //     return new PagedResultDto<NotificationReceiverWithNavigationPropertiesDto>
    //     {
    //         TotalCount = totalCount,
    //         Items = ObjectMapper.Map<List<NotificationReceiverWithNavigationProperties>, List<NotificationReceiverWithNavigationPropertiesDto>>(items)
    //     };
    // }
   
}