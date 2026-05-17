using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories;

namespace HC.Chat.Conversations;

public interface IConversationRepository : IBasicRepository<Conversation, Guid>
{
    // Existing methods
    Task<ConversationPair> FindPairAsync(Guid senderId, Guid targetId, CancellationToken cancellationToken = default);

    Task<List<ConversationWithTargetUser>> GetListByUserIdAsync(
        Guid userId,
        string filter,
        int skipCount,
        int maxResultCount,
        CancellationToken cancellationToken = default);

    Task<int> GetTotalUnreadMessageCountAsync(Guid userId, CancellationToken cancellationToken = default);
    
    // New methods
    Task<List<Conversation>> GetByTypeAsync(
        Guid userId, 
        ConversationType type, 
        bool includePinned = false,
        CancellationToken cancellationToken = default
    );
    
    Task<Conversation> GetWithMembersAsync(Guid conversationId, CancellationToken cancellationToken = default);
    
    Task<bool> IsUserMemberAsync(Guid conversationId, Guid userId, CancellationToken cancellationToken = default);
    
    Task<List<Conversation>> GetByProjectIdAsync(Guid projectId, CancellationToken cancellationToken = default);
    
    Task<List<Conversation>> GetByTaskIdAsync(Guid taskId, CancellationToken cancellationToken = default);
}
