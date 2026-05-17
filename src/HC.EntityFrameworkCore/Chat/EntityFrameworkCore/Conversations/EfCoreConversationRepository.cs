using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Volo.Abp.Domain.Repositories.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore;
using HC.Chat.Conversations;
using HC.Chat.Messages;
using HC.Chat.Users;

namespace HC.Chat.EntityFrameworkCore.Conversations;

public class EfCoreConversationRepository : EfCoreRepository<IChatDbContext, Conversation, Guid>, IConversationRepository
{
    public EfCoreConversationRepository(IDbContextProvider<IChatDbContext> dbContextProvider) : base(dbContextProvider)
    {
    }

    public virtual async Task<ConversationPair> FindPairAsync(Guid senderId, Guid targetId, CancellationToken cancellationToken = default)
    {
        // TODO: This method is deprecated - User conversations are now single shared conversations
        // For backward compatibility, return the same conversation for both sender and target
        var dbContext = await GetDbContextAsync();
        
        var conversation = await (from c in (await GetDbSetAsync())
            join m1 in dbContext.Set<ConversationMember>() on c.Id equals m1.ConversationId
            join m2 in dbContext.Set<ConversationMember>() on c.Id equals m2.ConversationId
            where c.Type == ConversationType.User
                && m1.UserId == senderId && m1.IsActive
                && m2.UserId == targetId && m2.IsActive
                && m1.ConversationId == m2.ConversationId
            select c).FirstOrDefaultAsync(GetCancellationToken(cancellationToken));

        if (conversation == null)
        {
            return null;
        }

        return new ConversationPair
        {
            SenderConversation = conversation,
            TargetConversation = conversation // Same conversation for both users now
        };
    }

    public virtual async Task<List<ConversationWithTargetUser>> GetListByUserIdAsync(
        Guid userId,
        string filter,
        int skipCount,
        int maxResultCount,
        CancellationToken cancellationToken = default)
    {
        var dbContext = await GetDbContextAsync();
        var memberSet = dbContext.ChatConversationMembers.AsNoTracking();
        var conversations = (await GetDbSetAsync()).AsNoTracking();

        var query =
            from m in memberSet
            join c in conversations on m.ConversationId equals c.Id
            where m.UserId == userId && m.IsActive
            select new { Member = m, Conv = c };

        if (!string.IsNullOrWhiteSpace(filter))
        {
            var pattern = $"%{filter.Trim()}%";
            query = query.Where(x =>
                (x.Conv.Type != ConversationType.User && x.Conv.Name != null && EF.Functions.ILike(x.Conv.Name, pattern))
                || (x.Conv.Type == ConversationType.User && dbContext.ChatConversationMembers
                    .Any(pm => pm.ConversationId == x.Conv.Id && pm.UserId != userId && pm.IsActive &&
                               dbContext.ChatUsers.Any(u => u.Id == pm.UserId &&
                                   (EF.Functions.ILike(u.Name, pattern) ||
                                    EF.Functions.ILike(u.Surname, pattern) ||
                                    EF.Functions.ILike(u.UserName, pattern))))));
        }

        query = query
            .OrderByDescending(x => x.Member.IsPinned)
            .ThenByDescending(x => x.Member.IsPinned
                ? x.Member.PinnedDate ?? DateTime.MinValue
                : DateTime.MinValue)
            .ThenByDescending(x => x.Conv.LastMessageDate);

        if (skipCount > 0)
        {
            query = query.Skip(skipCount);
        }

        if (maxResultCount > 0)
        {
            query = query.Take(maxResultCount);
        }

        var rows = await query.ToListAsync(GetCancellationToken(cancellationToken));

        var userDmIds = rows.Where(r => r.Conv.Type == ConversationType.User).Select(r => r.Conv.Id).Distinct().ToList();
        var targetUserByConversation = new Dictionary<Guid, ChatUser>();
        if (userDmIds.Count > 0)
        {
            var otherMemberRows = await memberSet
                .Where(m => userDmIds.Contains(m.ConversationId) && m.UserId != userId && m.IsActive)
                .Select(m => new { m.ConversationId, m.UserId })
                .ToListAsync(GetCancellationToken(cancellationToken));

            var otherUserIdByConversation = otherMemberRows
                .GroupBy(r => r.ConversationId)
                .ToDictionary(g => g.Key, g => g.First().UserId);

            var targetUserIds = otherUserIdByConversation.Values.Distinct().ToList();
            if (targetUserIds.Count > 0)
            {
                var chatUsers = await dbContext.ChatUsers.AsNoTracking()
                    .Where(u => targetUserIds.Contains(u.Id))
                    .ToListAsync(GetCancellationToken(cancellationToken));
                var userById = chatUsers.ToDictionary(u => u.Id);
                foreach (var pair in otherUserIdByConversation)
                {
                    if (userById.TryGetValue(pair.Value, out var cu))
                    {
                        targetUserByConversation[pair.Key] = cu;
                    }
                }
            }
        }

        return rows.ConvertAll(r =>
        {
            ChatUser? targetUser = null;
            if (r.Conv.Type == ConversationType.User)
            {
                targetUserByConversation.TryGetValue(r.Conv.Id, out targetUser);
            }

            return new ConversationWithTargetUser
            {
                Conversation = r.Conv,
                TargetUser = targetUser
            };
        });
    }

    public virtual async Task<int> GetTotalUnreadMessageCountAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        // TODO: Calculate unread count from ConversationMember per-user read status
        // Old logic used Conversation.UnreadMessageCount and LastMessageSide which are removed
        await Task.CompletedTask;
        return 0;
    }
    
    // New methods
    public virtual async Task<List<Conversation>> GetByTypeAsync(
        Guid userId, 
        ConversationType type, 
        bool includePinned = false,
        CancellationToken cancellationToken = default)
    {
        var dbContext = await GetDbContextAsync();
        var conversations = await GetQueryableAsync();
        
        // Get conversations by type where user is a member
        var query = from member in dbContext.ChatConversationMembers
                    join conversation in conversations on member.ConversationId equals conversation.Id
                    where member.UserId == userId && member.IsActive 
                        && conversation.Type == type
                    select conversation;
            
        return await query.ToListAsync(GetCancellationToken(cancellationToken));
    }
    
    public virtual async Task<Conversation> GetWithMembersAsync(Guid conversationId, CancellationToken cancellationToken = default)
    {
        return await (await GetQueryableAsync())
            .Include(x => x.Members)
            .FirstOrDefaultAsync(x => x.Id == conversationId, GetCancellationToken(cancellationToken));
    }
    
    public virtual async Task<bool> IsUserMemberAsync(Guid conversationId, Guid userId, CancellationToken cancellationToken = default)
    {
        var dbContext = await GetDbContextAsync();
        return await dbContext.ChatConversationMembers
            .AnyAsync(x => x.ConversationId == conversationId && x.UserId == userId && x.IsActive, GetCancellationToken(cancellationToken));
    }
    
    public virtual async Task<List<Conversation>> GetByProjectIdAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        return await (await GetQueryableAsync())
            .Where(x => x.ProjectId == projectId)
            .ToListAsync(GetCancellationToken(cancellationToken));
    }
    
    public virtual async Task<List<Conversation>> GetByTaskIdAsync(Guid taskId, CancellationToken cancellationToken = default)
    {
        return await (await GetQueryableAsync())
            .Where(x => x.TaskId == taskId)
            .ToListAsync(GetCancellationToken(cancellationToken));
    }
}
