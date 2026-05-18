using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Volo.Abp.Domain.Repositories.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore;
using HC.Chat.Messages;
using HC.Chat.Conversations;
using HC.Chat.Helpers;
using Microsoft.Extensions.Logging;

namespace HC.Chat.EntityFrameworkCore.Messages;

public class EfCoreMessageFileRepository : EfCoreRepository<IChatDbContext, MessageFile, Guid>, IMessageFileRepository
{   
    private readonly ILogger<EfCoreMessageFileRepository> _logger;
    public EfCoreMessageFileRepository(
        IDbContextProvider<IChatDbContext> dbContextProvider, 
        ILogger<EfCoreMessageFileRepository> logger) : base(dbContextProvider)
    {
        _logger = logger;
    }
    
    public virtual async Task<List<MessageFile>> GetByMessageIdAsync(Guid messageId, CancellationToken cancellationToken = default)
    {
        return await (await GetDbSetAsync())
            .Where(x => x.MessageId == messageId)
            .ToListAsync(GetCancellationToken(cancellationToken));
    }

    public virtual async Task<List<MessageFile>> GetListByMessageIdsAsync(IReadOnlyCollection<Guid> messageIds, CancellationToken cancellationToken = default)
    {
        if (messageIds == null || messageIds.Count == 0)
        {
            return new List<MessageFile>();
        }

        return await (await GetDbSetAsync())
            .Where(x => x.MessageId.HasValue && messageIds.Contains(x.MessageId.Value))
            .ToListAsync(GetCancellationToken(cancellationToken));
    }
    
    public virtual async Task<List<MessageFile>> GetByConversationIdAsync(Guid conversationId, CancellationToken cancellationToken = default)
    {
        var dbContext = await GetDbContextAsync();
        
        // Get message IDs from the conversation using Message.ConversationId
        var messageIds = await dbContext.ChatMessages
            .AsNoTracking()
            .Where(m => m.ConversationId == conversationId)
            .Select(m => m.Id)
            .ToListAsync(GetCancellationToken(cancellationToken));
            
        return await (await GetDbSetAsync())
            .Where(x => x.MessageId.HasValue && messageIds.Contains(x.MessageId.Value))
            .ToListAsync(GetCancellationToken(cancellationToken));
    }
    
    public virtual async Task<MessageFile> GetWithMessageAsync(Guid fileId, CancellationToken cancellationToken = default)
    {
        return await (await GetQueryableAsync())
            .Include(x => x.Message)
            .FirstOrDefaultAsync(x => x.Id == fileId, GetCancellationToken(cancellationToken));
    }


    public virtual async Task<List<MessageFile>> GetByConversationIdAndFileTypeAsync(Guid conversationId, FileMediaType fileType, int maxResultCount = 10, int skipCount = 0, string fileName = "", CancellationToken cancellationToken = default)
    {
        List<string> fileExtensions = new();
        fileExtensions = FileHelper.GetFileExtensions(fileType);

        _logger.LogInformation($"FileExtensions: {string.Join(", ", fileExtensions)}");
        _logger.LogInformation($"FileName: {fileName}");
        _logger.LogInformation($"ConversationId: {conversationId}");
        _logger.LogInformation($"MaxResultCount: {maxResultCount}");
        _logger.LogInformation($"SkipCount: {skipCount}");


    return await (await GetQueryableAsync())
        .AsNoTracking()
        .Where(x => 
        x.Message.ConversationId == conversationId 
        && fileExtensions.Contains(x.FileExtension) 
        && (!string.IsNullOrEmpty(fileName) ? x.FileName.Contains(fileName) : true))
        .OrderBy(x => x.CreationTime)
        .PageBy(skipCount, maxResultCount)
        .ToListAsync(GetCancellationToken(cancellationToken));
    }
}
