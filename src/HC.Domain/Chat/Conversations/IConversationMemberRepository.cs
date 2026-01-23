using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories;

namespace HC.Chat.Conversations;

public interface IConversationMemberRepository : IBasicRepository<ConversationMember, Guid>
{
    Task<List<ConversationMember>> GetByConversationIdAsync(Guid conversationId, CancellationToken cancellationToken = default);
    
    Task<List<ConversationMember>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<List<ConversationMember>> GetByUserIdsAsync(List<Guid> userIds, ConversationType type, CancellationToken cancellationToken = default);
    
    Task<List<ConversationMember>> GetPinnedByUserIdAsync(Guid userId, CancellationToken cancellationToken = default); // Get user's pinned conversations
    
    Task<ConversationMember> GetByConversationAndUserAsync(
        Guid conversationId, 
        Guid userId,
        CancellationToken cancellationToken = default
    );
    
    Task<bool> ExistsAsync(Guid conversationId, Guid userId, CancellationToken cancellationToken = default);
    
    Task<bool> IsPinnedAsync(Guid conversationId, Guid userId, CancellationToken cancellationToken = default); // Check if user pinned this conversation
}
