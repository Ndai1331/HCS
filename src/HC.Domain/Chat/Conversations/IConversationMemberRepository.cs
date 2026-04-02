using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories;

namespace HC.Chat.Conversations;

public interface IConversationMemberRepository : IBasicRepository<ConversationMember, Guid>
{
    /// <summary>
    /// One query: members for the given user across multiple conversations (avoids N+1 in contact list).
    /// </summary>
    Task<IReadOnlyDictionary<Guid, ConversationMember>> GetDictionaryByConversationIdsAndUserIdAsync(
        IReadOnlyCollection<Guid> conversationIds,
        Guid userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// One query: active member count per conversation (group conversations only).
    /// </summary>
    Task<IReadOnlyDictionary<Guid, int>> GetActiveMemberCountsByConversationIdsAsync(
        IReadOnlyCollection<Guid> conversationIds,
        CancellationToken cancellationToken = default);

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
