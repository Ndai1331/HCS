using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;
using HC.Chat.Messages;

namespace HC.Chat.Conversations;

[RemoteService(Name = ChatRemoteServiceConsts.RemoteServiceName)]
[Area(ChatRemoteServiceConsts.ModuleName)]
[Route("api/chat/conversation")]
public class ConversationController : ChatController, IConversationAppService
{
    private readonly IConversationAppService _conversationAppService;

    public ConversationController(IConversationAppService conversationAppService)
    {
        _conversationAppService = conversationAppService;
    }

    [HttpPost]
    [Route("send-message")]
    public Task<ChatMessageDto> SendMessageAsync(SendMessageInput input)
    {
        return _conversationAppService.SendMessageAsync(input);
    }

    [HttpDelete]
    [Route("delete-message")]
    public Task DeleteMessageAsync(DeleteMessageInput input)
    {
        return _conversationAppService.DeleteMessageAsync(input);
    }

    [HttpGet]
    [Route("conversation")]
    public Task<ChatConversationDto> GetConversationAsync(GetConversationInput input)
    {
        return _conversationAppService.GetConversationAsync(input);
    }

    [HttpPost]
    [Route("mark-conversation-as-read")]
    public Task MarkConversationAsReadAsync(MarkConversationAsReadInput input)
    {
        return _conversationAppService.MarkConversationAsReadAsync(input);
    }

    [HttpDelete]
    [Route("delete-conversation")]
    public Task DeleteConversationAsync(DeleteConversationInput input)
    {
        return _conversationAppService.DeleteConversationAsync(input);
    }
    
    // New endpoints
    [HttpPost]
    [Route("user")]
    public Task<ConversationDto> CreateUserConversationAsync(CreateUserConversationInput input)
    {
        return _conversationAppService.CreateUserConversationAsync(input);
    }
    
    [HttpPost]
    [Route("group")]
    public Task<ConversationDto> CreateGroupConversationAsync(CreateGroupConversationInput input)
    {
        return _conversationAppService.CreateGroupConversationAsync(input);
    }
    
    [HttpPost]
    [Route("project")]
    public Task<ConversationDto> CreateProjectConversationAsync(CreateProjectConversationInput input)
    {
        return _conversationAppService.CreateProjectConversationAsync(input);
    }
    
    [HttpPost]
    [Route("task")]
    public Task<ConversationDto> CreateTaskConversationAsync(CreateTaskConversationInput input)
    {
        return _conversationAppService.CreateTaskConversationAsync(input);
    }
    
    [HttpPut]
    [Route("{id}/name")]
    public Task<ConversationDto> UpdateConversationNameAsync(Guid id, [FromBody] UpdateConversationNameInput input)
    {
        input.ConversationId = id;
        return ((IConversationAppService)this).UpdateConversationNameAsync(input);
    }
    
    Task<ConversationDto> IConversationAppService.UpdateConversationNameAsync(UpdateConversationNameInput input)
    {
        return _conversationAppService.UpdateConversationNameAsync(input);
    }
    
    [HttpPost]
    [Route("{id}/pin")]
    public Task PinConversationAsync(Guid id)
    {
        return _conversationAppService.PinConversationAsync(id);
    }
    
    [HttpDelete]
    [Route("{id}/pin")]
    public Task UnpinConversationAsync(Guid id)
    {
        return _conversationAppService.UnpinConversationAsync(id);
    }
    
    [HttpPost]
    [Route("{id}/members")]
    public Task AddMemberAsync(Guid id, [FromBody] AddMemberInput input)
    {
        input.ConversationId = id;
        return ((IConversationAppService)this).AddMemberAsync(input);
    }
    
    Task IConversationAppService.AddMemberAsync(AddMemberInput input)
    {
        return _conversationAppService.AddMemberAsync(input);
    }
    
    [HttpDelete]
    [Route("{id}/members/{userId}")]
    public Task RemoveMemberAsync(Guid id, Guid userId)
    {
        return ((IConversationAppService)this).RemoveMemberAsync(new RemoveMemberInput { ConversationId = id, UserId = userId });
    }
    
    Task IConversationAppService.RemoveMemberAsync(RemoveMemberInput input)
    {
        return _conversationAppService.RemoveMemberAsync(input);
    }
    
    [HttpGet]
    [Route("{id}/members")]
    public Task<List<ConversationMemberDto>> GetMembersAsync(Guid id)
    {
        return _conversationAppService.GetMembersAsync(id);
    }
    
    [HttpGet]
    [Route("pinned")]
    public Task<List<ConversationDto>> GetPinnedConversationsAsync()
    {
        return _conversationAppService.GetPinnedConversationsAsync();
    }
    
    [HttpGet]
    [Route("type/{type}")]
    public Task<List<ConversationDto>> GetByTypeAsync(ConversationType type)
    {
        return _conversationAppService.GetByTypeAsync(type);
    }
    
    [HttpPost]
    [Route("reply-message")]
    public Task<ChatMessageDto> SendReplyMessageAsync(SendReplyMessageInput input)
    {
        return _conversationAppService.SendReplyMessageAsync(input);
    }
    
    [HttpPost]
    [Route("message/{id}/pin")]
    public Task PinMessageAsync(Guid id)
    {
        return _conversationAppService.PinMessageAsync(id);
    }
    
    [HttpDelete]
    [Route("message/{id}/pin")]
    public Task UnpinMessageAsync(Guid id)
    {
        return _conversationAppService.UnpinMessageAsync(id);
    }
    
    [HttpGet]
    [Route("{id}/messages/pinned")]
    public Task<List<ChatMessageDto>> GetPinnedMessagesAsync(Guid id)
    {
        return _conversationAppService.GetPinnedMessagesAsync(id);
    }
    
    [HttpPost]
    [Route("message-with-files")]
    public Task<ChatMessageDto> SendMessageWithFilesAsync([FromBody] SendMessageWithFilesInput input)
    {
        return _conversationAppService.SendMessageWithFilesAsync(input);
    }
    
    [HttpPost]
    [Route("files/upload")]
    public Task<MessageFileDto> UploadFileAsync([FromForm] UploadFileInput input)
    {
        return _conversationAppService.UploadFileAsync(input);
    }
    
    [HttpGet]
    [Route("files/{id}/download")]
    public Task<FileDto> DownloadFileAsync(Guid id)
    {
        return _conversationAppService.DownloadFileAsync(id);
    }
    
    [HttpDelete]
    [Route("files/{id}")]
    public Task DeleteFileAsync(Guid id)
    {
        return _conversationAppService.DeleteFileAsync(id);
    }
}
