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
    Task<List<Message>> GetMessagesInConversationAsync(Guid conversationId, string messageText, int maxResultCount = 10, int skipCount = 0, CancellationToken cancellationToken = default);



}
