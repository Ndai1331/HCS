using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using HC.Shared;

namespace HC.Chat.Users;

public interface IContactAppService : IApplicationService
{
    Task<List<ChatContactDto>> GetContactsAsync(GetContactsInput input);

    Task<int> GetTotalUnreadMessageCountAsync();

    Task<PagedResultDto<LookupDto<Guid>>> GetUserLookupAsync(LookupRequestDto input);
}
