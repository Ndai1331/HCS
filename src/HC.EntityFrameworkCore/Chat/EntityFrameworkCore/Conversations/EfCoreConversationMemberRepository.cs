using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Volo.Abp.Domain.Repositories.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore;
using HC.Chat.Conversations;

namespace HC.Chat.EntityFrameworkCore.Conversations;

public class EfCoreConversationMemberRepository : EfCoreRepository<IChatDbContext, ConversationMember, Guid>, IConversationMemberRepository
{
    public EfCoreConversationMemberRepository(IDbContextProvider<IChatDbContext> dbContextProvider) : base(dbContextProvider)
    {
    }
    
    public virtual async Task<List<ConversationMember>> GetByConversationIdAsync(Guid conversationId, CancellationToken cancellationToken = default)
    {
        return await (await GetDbSetAsync())
            .Where(x => x.ConversationId == conversationId && x.IsActive)
            .ToListAsync(GetCancellationToken(cancellationToken));
    }
    
    public virtual async Task<List<ConversationMember>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await (await GetDbSetAsync())
            .Where(x => x.UserId == userId && x.IsActive)
            .ToListAsync(GetCancellationToken(cancellationToken));
    }

    public virtual async Task<List<ConversationMember>> GetByUserIdsAsync(List<Guid> userIds, ConversationType type, CancellationToken cancellationToken = default)
    {
        var dbSet = await GetDbSetAsync();
        var requiredCount = userIds.Count;

        // Find conversations where ALL provided users are active members
        var matchingConversationIds = await dbSet
            .Include(x => x.Conversation)
            .Where(x => userIds.Contains(x.UserId) && x.IsActive && x.Conversation.Type == type)
            .GroupBy(x => x.ConversationId)
            .Where(g => g.Select(m => m.UserId).Distinct().Count() >= requiredCount)
            .Select(g => g.Key)
            .ToListAsync(GetCancellationToken(cancellationToken));

        if (!matchingConversationIds.Any())
        {
            return new List<ConversationMember>();
        }

        return await dbSet
            .Include(x => x.Conversation)
            .Where(x => matchingConversationIds.Contains(x.ConversationId) && x.IsActive)
            .ToListAsync(GetCancellationToken(cancellationToken));
    }
    public virtual async Task<List<ConversationMember>> GetPinnedByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await (await GetDbSetAsync())
            .Where(x => x.UserId == userId && x.IsPinned && x.IsActive)
            .OrderByDescending(x => x.PinnedDate)
            .ToListAsync(GetCancellationToken(cancellationToken));
    }
    
    public virtual async Task<ConversationMember> GetByConversationAndUserAsync(
        Guid conversationId, 
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        return await (await GetDbSetAsync())
            .FirstOrDefaultAsync(x => x.ConversationId == conversationId && x.UserId == userId, GetCancellationToken(cancellationToken));
    }
    
    public virtual async Task<bool> ExistsAsync(Guid conversationId, Guid userId, CancellationToken cancellationToken = default)
    {
        return await (await GetDbSetAsync())
            .AnyAsync(x => x.ConversationId == conversationId && x.UserId == userId, GetCancellationToken(cancellationToken));
    }
    
    public virtual async Task<bool> IsPinnedAsync(Guid conversationId, Guid userId, CancellationToken cancellationToken = default)
    {
        return await (await GetDbSetAsync())
            .AnyAsync(x => x.ConversationId == conversationId && x.UserId == userId && x.IsPinned, GetCancellationToken(cancellationToken));
    }
}
