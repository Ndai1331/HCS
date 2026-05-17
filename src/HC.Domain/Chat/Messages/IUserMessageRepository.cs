using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories;

namespace HC.Chat.Messages;

public interface IUserMessageRepository : IBasicRepository<UserMessage, Guid>
{
    Task<List<MessageWithDetails>> GetMessagesAsync(Guid userId, Guid targetUserId, int skipCount, int maxResultCount, CancellationToken cancellationToken = default);
    
    // New method for group conversations
    Task<List<MessageWithDetails>> GetMessagesByConversationIdAsync(Guid conversationId, Guid userId, int skipCount, int maxResultCount, CancellationToken cancellationToken = default);
    
    Task<bool> HasConversationAsync(Guid userId, Guid targetUserId, CancellationToken cancellationToken = default);

    Task<List<UserMessage>> GetListAsync(Guid messageId, CancellationToken cancellationToken = default);
    
    Task<MessageWithDetails> GetLastMessageAsync(Guid userId, Guid targetUserId, CancellationToken cancellationToken = default);

    Task<List<Guid>> GetAllMessageIdsAsync(Guid userId, Guid targetUserId, CancellationToken cancellationToken = default);
    Task<HashSet<Guid>> GetMessageIdsByConversationIdAsync(Guid conversationId, Guid userId, CancellationToken cancellationToken = default);

    Task DeleteAllMessages(Guid userId, Guid targetUserId, CancellationToken cancellationToken = default);

    /// <summary>Bulk mark user rows read for the given chat message ids (single SQL round-trip).</summary>
    Task<int> BulkMarkAsReadForMessagesAsync(Guid userId, IReadOnlyCollection<Guid> chatMessageIds, DateTime readTime, CancellationToken cancellationToken = default);
}
