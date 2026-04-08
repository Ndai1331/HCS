using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using HC.Chat.Users;
using HC.Shared;

namespace HC.Chat.Users;

[RemoteService(Name = ChatRemoteServiceConsts.RemoteServiceName)]
[Area(ChatRemoteServiceConsts.ModuleName)]
[Route("api/chat/contact")]
public class ContactController : ChatController, IContactAppService
{
    private readonly IContactAppService _contactAppService;

    public ContactController(IContactAppService contactAppService)
    {
        _contactAppService = contactAppService;
    }

    [HttpGet]
    public Task<List<ChatContactDto>> GetContactsAsync([FromQuery] GetContactsInput input)
    {
        return _contactAppService.GetContactsAsync(input);
    }

    [HttpGet]
    [Route("total-unread-message-count")]
    public Task<int> GetTotalUnreadMessageCountAsync()
    {
        return _contactAppService.GetTotalUnreadMessageCountAsync();
    }

    [HttpGet]
    [Route("user-lookup")]
    public Task<PagedResultDto<LookupDto<Guid>>> GetUserLookupAsync([FromQuery] LookupRequestDto input)
    {
        return _contactAppService.GetUserLookupAsync(input);
    }
}
