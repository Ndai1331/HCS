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
        var dbContext = await GetDbContextAsync();

        // Find conversations where ALL provided users are active members
        var matchingConversationIds = await dbSet
            .AsNoTracking()
            .Join(
                dbContext.ChatConversations.AsNoTracking(),
                member => member.ConversationId,
                conversation => conversation.Id,
                (member, conversation) => new { Member = member, ConversationType = conversation.Type })
            .Where(x => userIds.Contains(x.Member.UserId) && x.Member.IsActive && x.ConversationType == type)
            .GroupBy(x => x.Member.ConversationId)
            .Where(g => g.Select(m => m.Member.UserId).Distinct().Count() >= requiredCount)
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
    
    public virtual async Task<IReadOnlyDictionary<Guid, ConversationMember>> GetDictionaryByConversationIdsAndUserIdAsync(
        IReadOnlyCollection<Guid> conversationIds,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        if (conversationIds == null || conversationIds.Count == 0)
        {
            return new Dictionary<Guid, ConversationMember>();
        }

        var idList = conversationIds as List<Guid> ?? conversationIds.ToList();
        var members = await (await GetDbSetAsync())
            .Where(x => idList.Contains(x.ConversationId) && x.UserId == userId)
            .ToListAsync(GetCancellationToken(cancellationToken));

        return members
            .GroupBy(x => x.ConversationId)
            .ToDictionary(g => g.Key, g => g.First());
    }

    public virtual async Task<IReadOnlyDictionary<Guid, int>> GetActiveMemberCountsByConversationIdsAsync(
        IReadOnlyCollection<Guid> conversationIds,
        CancellationToken cancellationToken = default)
    {
        if (conversationIds == null || conversationIds.Count == 0)
        {
            return new Dictionary<Guid, int>();
        }

        var idList = conversationIds as List<Guid> ?? conversationIds.ToList();
        var rows = await (await GetDbSetAsync())
            .Where(x => idList.Contains(x.ConversationId) && x.IsActive)
            .GroupBy(x => x.ConversationId)
            .Select(g => new { ConversationId = g.Key, Count = g.Count() })
            .ToListAsync(GetCancellationToken(cancellationToken));

        return rows.ToDictionary(x => x.ConversationId, x => x.Count);
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
