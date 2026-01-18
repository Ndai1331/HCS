using HC.Shared;
using Asp.Versioning;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;
using Volo.Abp.Application.Dtos;
using HC.NotificationReceivers;
using Volo.Abp.Content;

namespace HC.Controllers.NotificationReceivers;

[RemoteService]
[Area("app")]
[ControllerName("NotificationReceiver")]
[Route("api/app/notification-receivers")]
public abstract class NotificationReceiverControllerBase : AbpController
{
    protected INotificationReceiversAppService _notificationReceiversAppService;

    public NotificationReceiverControllerBase(INotificationReceiversAppService notificationReceiversAppService)
    {
        _notificationReceiversAppService = notificationReceiversAppService;
    }

    [HttpGet]
    public virtual Task<PagedResultDto<NotificationReceiverWithNavigationPropertiesDto>> GetListAsync(GetNotificationReceiversInput input)
    {
        return _notificationReceiversAppService.GetListAsync(input);
    }

    [HttpGet]
    [Route("with-navigation-properties/{id}")]
    public virtual Task<NotificationReceiverWithNavigationPropertiesDto> GetWithNavigationPropertiesAsync(Guid id)
    {
        return _notificationReceiversAppService.GetWithNavigationPropertiesAsync(id);
    }

    [HttpGet]
    [Route("{id}")]
    public virtual Task<NotificationReceiverDto> GetAsync(Guid id)
    {
        return _notificationReceiversAppService.GetAsync(id);
    }

    [HttpGet]
    [Route("notification-lookup")]
    public virtual Task<PagedResultDto<LookupDto<Guid>>> GetNotificationLookupAsync(LookupRequestDto input)
    {
        return _notificationReceiversAppService.GetNotificationLookupAsync(input);
    }

    [HttpGet]
    [Route("identity-user-lookup")]
    public virtual Task<PagedResultDto<LookupDto<Guid>>> GetIdentityUserLookupAsync(LookupRequestDto input)
    {
        return _notificationReceiversAppService.GetIdentityUserLookupAsync(input);
    }

    [HttpPost]
    public virtual Task<NotificationReceiverDto> CreateAsync(NotificationReceiverCreateDto input)
    {
        return _notificationReceiversAppService.CreateAsync(input);
    }

    [HttpPut]
    [Route("{id}")]
    public virtual Task<NotificationReceiverDto> UpdateAsync(Guid id, NotificationReceiverUpdateDto input)
    {
        return _notificationReceiversAppService.UpdateAsync(id, input);
    }

    [HttpDelete]
    [Route("{id}")]
    public virtual Task DeleteAsync(Guid id)
    {
        return _notificationReceiversAppService.DeleteAsync(id);
    }

    [HttpGet]
    [Route("as-excel-file")]
    public virtual Task<IRemoteStreamContent> GetListAsExcelFileAsync(NotificationReceiverExcelDownloadDto input)
    {
        return _notificationReceiversAppService.GetListAsExcelFileAsync(input);
    }

    [HttpGet]
    [Route("download-token")]
    public virtual Task<HC.Shared.DownloadTokenResultDto> GetDownloadTokenAsync()
    {
        return _notificationReceiversAppService.GetDownloadTokenAsync();
    }

    [HttpDelete]
    [Route("")]
    public virtual Task DeleteByIdsAsync(List<Guid> notificationreceiverIds)
    {
        return _notificationReceiversAppService.DeleteByIdsAsync(notificationreceiverIds);
    }

    [HttpDelete]
    [Route("all")]
    public virtual Task DeleteAllAsync(GetNotificationReceiversInput input)
    {
        return _notificationReceiversAppService.DeleteAllAsync(input);
    }

    [HttpPost]
    [Route("mark-all-as-read")]
    public virtual Task MarkAllAsReadAsync([FromQuery] string? sourceType = null)
    {
        return _notificationReceiversAppService.MarkAllAsReadAsync(sourceType);
    }

    [HttpPost]
    [Route("mark-as-read")]
    public virtual Task MarkAsReadAsync(Guid notificationId)
    {
        return _notificationReceiversAppService.MarkAsReadAsync(notificationId);
    }
}