using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Volo.Abp.Domain.Repositories.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore;
using HC.Chat.Messages;

namespace HC.Chat.EntityFrameworkCore.Messages;

public class EfCoreMessageRepository : EfCoreRepository<IChatDbContext, Message, Guid>, IMessageRepository
{
    public EfCoreMessageRepository(IDbContextProvider<IChatDbContext> dbContextProvider) : base(dbContextProvider)
    {
    }

    public async Task DeleteALlMessagesAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken = default)
    {
         await (await GetDbSetAsync()).Where(message => ids.Contains(message.Id)).ExecuteDeleteAsync(GetCancellationToken(cancellationToken));
    }
    
    // New methods
    public virtual async Task<List<Message>> GetPinnedMessagesAsync(Guid conversationId, CancellationToken cancellationToken = default)
    {
        // Get pinned messages by ConversationId - much simpler and more accurate
        return await (await GetDbSetAsync())
            .Where(m => m.ConversationId == conversationId && m.IsPinned)
            // .OrderByDescending(m => m.PinnedDate)
            .OrderByDescending(m => m.CreationTime)
            .ToListAsync(GetCancellationToken(cancellationToken));
    }
    
    public virtual async Task<Message> GetWithReplyAsync(Guid messageId, CancellationToken cancellationToken = default)
    {
        return await (await GetQueryableAsync())
            .Include(x => x.ReplyToMessage)
            .Include(x => x.ForwardedFromMessage)
            .FirstOrDefaultAsync(x => x.Id == messageId, GetCancellationToken(cancellationToken));
    }
    
    public virtual async Task<List<Message>> GetRepliesAsync(Guid messageId, CancellationToken cancellationToken = default)
    {
        return await (await GetDbSetAsync())
            .Where(x => x.ReplyToMessageId == messageId)
            .OrderBy(x => x.CreationTime)
            .ToListAsync(GetCancellationToken(cancellationToken));
    }

    public virtual async Task<List<Message>> GetMessagesInConversationAsync(Guid conversationId, string messageText, int maxResultCount = 10, int skipCount = 0, CancellationToken cancellationToken = default)
    {
        return await (await GetDbSetAsync())
            .Where(x => x.ConversationId == conversationId && x.Text.Contains(messageText))
            .OrderByDescending(x => x.CreationTime)
            .PageBy(skipCount, maxResultCount)
            .ToListAsync(GetCancellationToken(cancellationToken));
    }
}
