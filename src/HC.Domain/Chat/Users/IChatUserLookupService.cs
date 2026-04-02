using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Users;

namespace HC.Chat.Users;

public interface IChatUserLookupService : IUserLookupService<ChatUser>
{
    /// <summary>
    /// Load many chat users in one repository round-trip (validation / mapping).
    /// </summary>
    Task<IReadOnlyList<ChatUser>> GetListByIdsAsync(IReadOnlyCollection<Guid> userIds);
}
