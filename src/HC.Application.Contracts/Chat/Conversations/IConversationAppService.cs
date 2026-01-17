using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;
using HC.Chat.Messages;

namespace HC.Chat.Conversations;

public interface IConversationAppService : IApplicationService
{
    Task<ChatMessageDto> SendMessageAsync(SendMessageInput input);
    
    Task DeleteMessageAsync(DeleteMessageInput input);

    Task<ChatConversationDto> GetConversationAsync(GetConversationInput input);

    Task MarkConversationAsReadAsync(MarkConversationAsReadInput input);
    
    Task DeleteConversationAsync(DeleteConversationInput input);
    
    // New methods for expanded features
    Task<ConversationDto> CreateUserConversationAsync(CreateUserConversationInput input);
    
    Task<ConversationDto> CreateGroupConversationAsync(CreateGroupConversationInput input);
    
    Task<ConversationDto> CreateProjectConversationAsync(CreateProjectConversationInput input);
    
    Task<ConversationDto> CreateTaskConversationAsync(CreateTaskConversationInput input);
    
    Task<ConversationDto> UpdateConversationNameAsync(UpdateConversationNameInput input);
    
    Task PinConversationAsync(Guid conversationId);
    
    Task UnpinConversationAsync(Guid conversationId);
    
    Task AddMemberAsync(AddMemberInput input);
    
    Task RemoveMemberAsync(RemoveMemberInput input);
    
    Task<List<ConversationMemberDto>> GetMembersAsync(Guid conversationId);
    
    Task<List<ConversationDto>> GetPinnedConversationsAsync();
    
    Task<List<ConversationDto>> GetByTypeAsync(ConversationType type);
    
    Task<ChatMessageDto> SendReplyMessageAsync(SendReplyMessageInput input);
    
    Task PinMessageAsync(Guid messageId);
    
    Task UnpinMessageAsync(Guid messageId);
    
    Task<List<ChatMessageDto>> GetPinnedMessagesAsync(Guid conversationId);
    
    Task<ChatMessageDto> SendMessageWithFilesAsync(SendMessageWithFilesInput input);
    
    Task<MessageFileDto> UploadFileAsync(UploadFileInput input);
    
    Task<FileDto> DownloadFileAsync(Guid fileId);
    
    Task DeleteFileAsync(Guid fileId);
}
