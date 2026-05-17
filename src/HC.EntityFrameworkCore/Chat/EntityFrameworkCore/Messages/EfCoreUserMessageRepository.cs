using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore;
using HC.Chat.Messages;
using Microsoft.EntityFrameworkCore;

namespace HC.Chat.EntityFrameworkCore.Messages;

public class EfCoreUserMessageRepository : EfCoreRepository<IChatDbContext, UserMessage, Guid>, IUserMessageRepository
{
    public EfCoreUserMessageRepository(IDbContextProvider<IChatDbContext> dbContextProvider) : base(dbContextProvider)
    {
    }

    public virtual async Task<List<MessageWithDetails>> GetMessagesAsync(Guid userId, Guid targetUserId, int skipCount, int maxResultCount, CancellationToken cancellationToken = default)
    {
        var query = from chatUserMessage in (await GetDbSetAsync())
                    join message in (await GetDbContextAsync()).ChatMessages on chatUserMessage.ChatMessageId equals message.Id
                    where userId == chatUserMessage.UserId && targetUserId == chatUserMessage.TargetUserId
                    select new MessageWithDetails
                    {
                        UserMessage = chatUserMessage,
                        Message = message
                    };

        return await query.OrderByDescending(x => x.Message.CreationTime).PageBy(skipCount, maxResultCount).ToListAsync(GetCancellationToken(cancellationToken));
    }
    
    public virtual async Task<MessageWithDetails> GetLastMessageAsync(Guid userId, Guid targetUserId, CancellationToken cancellationToken = default)
    {
        var query = from chatUserMessage in (await GetDbSetAsync())
                    join message in (await GetDbContextAsync()).ChatMessages on chatUserMessage.ChatMessageId equals message.Id
                    where userId == chatUserMessage.UserId && targetUserId == chatUserMessage.TargetUserId
                    select new MessageWithDetails
                    {
                        UserMessage = chatUserMessage,
                        Message = message
                    };

        return await query.OrderByDescending(x => x.Message.CreationTime).FirstOrDefaultAsync(GetCancellationToken(cancellationToken));
    }

    public virtual async Task<bool> HasConversationAsync(Guid userId, Guid targetUserId, CancellationToken cancellationToken = default)
    {
        return await (await GetDbSetAsync()).AnyAsync(p => p.UserId == userId && p.TargetUserId == targetUserId, cancellationToken);
    }

    public async Task<List<UserMessage>> GetListAsync(Guid messageId, CancellationToken cancellationToken = default)
    {
        return await (await GetDbSetAsync()).Where(message => message.ChatMessageId == messageId).ToListAsync(GetCancellationToken(cancellationToken));
    }
    
    public async Task<List<Guid>> GetAllMessageIdsAsync(Guid userId, Guid targetUserId, CancellationToken cancellationToken = default)
    {
        return await (await GetDbSetAsync()).Where(message =>
                (message.UserId == userId && message.TargetUserId == targetUserId) ||
                (message.UserId == targetUserId && message.TargetUserId == userId))
            .Select(message => message.ChatMessageId)
            .ToListAsync(GetCancellationToken(cancellationToken));
    }

    public virtual async Task<HashSet<Guid>> GetMessageIdsByConversationIdAsync(Guid conversationId, Guid userId, CancellationToken cancellationToken = default)
    {
        var dbContext = await GetDbContextAsync();

        return await (from userMessage in (await GetDbSetAsync())
                      join message in dbContext.ChatMessages on userMessage.ChatMessageId equals message.Id
                      where userMessage.UserId == userId && message.ConversationId == conversationId
                      select userMessage.ChatMessageId)
            .ToHashSetAsync(GetCancellationToken(cancellationToken));
    }
    
    public async Task DeleteAllMessages(Guid userId, Guid targetUserId, CancellationToken cancellationToken = default)
    {
        await (await GetDbSetAsync()).Where(message =>
            (message.UserId == userId && message.TargetUserId == targetUserId) || 
            (message.UserId == targetUserId && message.TargetUserId == userId))
            .ExecuteDeleteAsync(GetCancellationToken(cancellationToken));
    }
    
    public virtual async Task<List<MessageWithDetails>> GetMessagesByConversationIdAsync(Guid conversationId, Guid userId, int skipCount, int maxResultCount, CancellationToken cancellationToken = default)
    {
        var dbContext = await GetDbContextAsync();
        
        // Verify user is member of the conversation
        var conversation = await dbContext.ChatConversations
            .Include(c => c.Members)
            .FirstOrDefaultAsync(c => c.Id == conversationId, GetCancellationToken(cancellationToken));
        
        if (conversation == null)
        {
            return new List<MessageWithDetails>();
        }
        
        // Check if user is a member
        var isMember = conversation.Members.Any(m => m.UserId == userId && m.IsActive);
        if (!isMember)
        {
            return new List<MessageWithDetails>();
        }
        
        // Query messages by ConversationId - much simpler and more accurate
        var query = from chatUserMessage in (await GetDbSetAsync())
                    join message in dbContext.ChatMessages on chatUserMessage.ChatMessageId equals message.Id
                    where chatUserMessage.UserId == userId 
                        && message.ConversationId == conversationId
                    select new MessageWithDetails
                    {
                        UserMessage = chatUserMessage,
                        Message = message
                    };
        
        return await query
            .OrderByDescending(x => x.Message.CreationTime)
            .PageBy(skipCount, maxResultCount)
            .ToListAsync(GetCancellationToken(cancellationToken));
    }

    public virtual async Task<int> BulkMarkAsReadForMessagesAsync(Guid userId, IReadOnlyCollection<Guid> chatMessageIds, DateTime readTime,
        CancellationToken cancellationToken = default)
    {
        if (chatMessageIds == null || chatMessageIds.Count == 0)
        {
            return 0;
        }

        return await (await GetDbSetAsync())
            .Where(um => um.UserId == userId && chatMessageIds.Contains(um.ChatMessageId) && !um.IsRead)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(um => um.IsRead, true)
                    .SetProperty(um => um.ReadTime, readTime),
                GetCancellationToken(cancellationToken));
    }
}
