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

    public virtual async Task<List<Message>> GetByConversationIdAsync(Guid conversationId, CancellationToken cancellationToken = default)
    {
        return await (await GetDbSetAsync())
            .Where(x => x.ConversationId == conversationId)
            .OrderBy(x => x.CreationTime)
            .ToListAsync(GetCancellationToken(cancellationToken));
    }

    public virtual async Task<List<Message>> GetMessagesInConversationAsync(Guid conversationId, string messageText, int maxResultCount = 10, int skipCount = 0, CancellationToken cancellationToken = default)
    {
        messageText = (messageText ?? string.Empty).Trim();
        var query = (await GetDbSetAsync()).Where(x => x.ConversationId == conversationId);

        if (!string.IsNullOrWhiteSpace(messageText))
        {
            var searchPattern = $"%{messageText}%";
            query = query.Where(x => EF.Functions.ILike(x.Text, searchPattern));
        }

        return await query
            .OrderByDescending(x => x.CreationTime)
            .ThenByDescending(x => x.Id)
            .PageBy(skipCount, maxResultCount)
            .ToListAsync(GetCancellationToken(cancellationToken));
    }

    public virtual async Task<(Message? Anchor, List<Message> Before, List<Message> After, bool HasMoreBefore, bool HasMoreAfter)> GetMessageContextAsync(
        Guid conversationId,
        Guid messageId,
        int beforeCount,
        int afterCount,
        CancellationToken cancellationToken = default)
    {
        var token = GetCancellationToken(cancellationToken);
        var dbSet = await GetDbSetAsync();

        var anchor = await dbSet
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == messageId && x.ConversationId == conversationId, token);

        if (anchor == null)
        {
            return (null, new List<Message>(), new List<Message>(), false, false);
        }

        beforeCount = Math.Clamp(beforeCount, 1, 100);
        afterCount = Math.Clamp(afterCount, 1, 100);

        var before = await dbSet
            .AsNoTracking()
            .Where(x => x.ConversationId == conversationId && x.CreationTime < anchor.CreationTime)
            .OrderByDescending(x => x.CreationTime)
            .ThenByDescending(x => x.Id)
            .Take(beforeCount + 1)
            .ToListAsync(token);

        var hasMoreBefore = before.Count > beforeCount;
        if (hasMoreBefore)
        {
            before = before.Take(beforeCount).ToList();
        }
        before.Reverse();

        var after = await dbSet
            .AsNoTracking()
            .Where(x => x.ConversationId == conversationId && x.CreationTime > anchor.CreationTime)
            .OrderBy(x => x.CreationTime)
            .ThenBy(x => x.Id)
            .Take(afterCount + 1)
            .ToListAsync(token);

        var hasMoreAfter = after.Count > afterCount;
        if (hasMoreAfter)
        {
            after = after.Take(afterCount).ToList();
        }

        return (anchor, before, after, hasMoreBefore, hasMoreAfter);
    }

    public virtual async Task<List<MessageSearchHit>> SearchInConversationAsync(
        Guid conversationId,
        string keyword,
        int maxResultCount = 20,
        int skipCount = 0,
        CancellationToken cancellationToken = default)
    {
        keyword = (keyword ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(keyword))
        {
            return new List<MessageSearchHit>();
        }

        var pattern = $"%{keyword}%";

        return await (await GetDbSetAsync())
            .AsNoTracking()
            .Where(x => x.ConversationId == conversationId && EF.Functions.ILike(x.Text, pattern))
            .OrderByDescending(x => x.CreationTime)
            .ThenByDescending(x => x.Id)
            .PageBy(skipCount, maxResultCount)
            .Select(x => new MessageSearchHit
            {
                MessageId = x.Id,
                ConversationId = x.ConversationId ?? Guid.Empty,
                Snippet = x.Text,
                CreationTime = x.CreationTime
            })
            .ToListAsync(GetCancellationToken(cancellationToken));
    }
}
