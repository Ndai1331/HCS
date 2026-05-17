using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories;

namespace HC.Chat.Messages;

public interface IMessageRepository : IBasicRepository<Message, Guid>
{
    // Existing methods
    Task DeleteALlMessagesAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken = default);
    
    // New methods
    Task<List<Message>> GetPinnedMessagesAsync(Guid conversationId, CancellationToken cancellationToken = default);
    
    Task<Message> GetWithReplyAsync(Guid messageId, CancellationToken cancellationToken = default);
    
    Task<List<Message>> GetRepliesAsync(Guid messageId, CancellationToken cancellationToken = default);
    Task<List<Message>> GetByConversationIdAsync(Guid conversationId, CancellationToken cancellationToken = default);
    Task<List<Message>> GetMessagesInConversationAsync(Guid conversationId, string messageText, int maxResultCount = 10, int skipCount = 0, CancellationToken cancellationToken = default);
    Task<(Message? Anchor, List<Message> Before, List<Message> After, bool HasMoreBefore, bool HasMoreAfter)> GetMessageContextAsync(
        Guid conversationId,
        Guid messageId,
        int beforeCount,
        int afterCount,
        CancellationToken cancellationToken = default);
    Task<List<MessageSearchHit>> SearchInConversationAsync(
        Guid conversationId,
        string keyword,
        int maxResultCount = 20,
        int skipCount = 0,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Load many messages in one query (DTO mapping / reply-forward batch resolution).
    /// </summary>
    Task<List<Message>> GetListByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken = default);

    /// <summary>Bulk mark messages as all-read (single SQL round-trip).</summary>
    Task<int> BulkMarkAsAllReadAsync(IReadOnlyCollection<Guid> messageIds, DateTime readTime, CancellationToken cancellationToken = default);

}
