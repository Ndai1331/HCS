using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp.Uow;
using Volo.Abp.Users;

namespace HC.Chat.Users;

public class ChatUserLookupService : UserLookupService<ChatUser, IChatUserRepository>, IChatUserLookupService
{
    private readonly IChatUserRepository _chatUserRepository;
    private readonly IUnitOfWorkManager _unitOfWorkManager;

    public ChatUserLookupService(
        IChatUserRepository userRepository,
        IUnitOfWorkManager unitOfWorkManager)
        : base(
            userRepository,
            unitOfWorkManager)
    {
        _chatUserRepository = userRepository;
        _unitOfWorkManager = unitOfWorkManager;
    }

    public virtual async Task<IReadOnlyList<ChatUser>> GetListByIdsAsync(IReadOnlyCollection<Guid> userIds)
    {
        if (userIds == null || userIds.Count == 0)
        {
            return Array.Empty<ChatUser>();
        }

        var ids = userIds.Distinct().ToList();
        using (var uow = _unitOfWorkManager.Begin(requiresNew: true))
        {
            var users = await _chatUserRepository.GetListAsync(ids);
            await uow.CompleteAsync();
            return users;
        }
    }

    protected override ChatUser CreateUser(IUserData externalUser)
    {
        return new ChatUser(externalUser);
    }
}
