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
    public Task<string> AddMemberAsync([FromBody] AddMemberInput input)
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
    
    [HttpPut]
    [Route("{id}/members/{userId}/role")]
    public Task SetMemberRoleAsync(Guid id, Guid userId, [FromBody] SetMemberRoleInput input)
    {
        input.ConversationId = id;
        input.UserId = userId;
        return ((IConversationAppService)this).SetMemberRoleAsync(input);
    }
    
    Task IConversationAppService.SetMemberRoleAsync(SetMemberRoleInput input)
    {
        return _conversationAppService.SetMemberRoleAsync(input);
    }
    
    [HttpPost]
    [Route("{id}/leave")]
    public Task LeaveConversationAsync(Guid id)
    {
        return ((IConversationAppService)this).LeaveConversationAsync(new LeaveConversationInput { ConversationId = id });
    }
    
    Task IConversationAppService.LeaveConversationAsync(LeaveConversationInput input)
    {
        return _conversationAppService.LeaveConversationAsync(input);
    }
    
    [HttpPost]
    [Route("{id}/transfer-admin-and-leave")]
    public Task TransferAdminAndLeaveAsync(Guid id, [FromBody] TransferAdminAndLeaveInput input)
    {
        input.ConversationId = id;
        return ((IConversationAppService)this).TransferAdminAndLeaveAsync(input);
    }
    
    Task IConversationAppService.TransferAdminAndLeaveAsync(TransferAdminAndLeaveInput input)
    {
        return _conversationAppService.TransferAdminAndLeaveAsync(input);
    }
    
    [HttpGet]
    [Route("{id}/my-permissions")]
    public Task<ConversationPermissionDto> GetMyPermissionsAsync(Guid id)
    {
        return _conversationAppService.GetMyPermissionsAsync(id);
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

    [HttpGet]
    [Route("files/message/{messageId}")]
    public Task<List<MessageFileDto>> GetMessageFilesAsync(Guid messageId)
    {
        return _conversationAppService.GetMessageFilesAsync(messageId);
    }

    [HttpPost]
    [Route("forward-message")]
    public Task<ChatMessageDto> ForwardMessageAsync([FromBody] ForwardMessageInput input)
    {
        return _conversationAppService.ForwardMessageAsync(input);
    }

    [HttpPost]
    [Route("find-conversation")]
    public Task<ConversationDto> FindConversationAsync([FromBody] FindConversationInput input)
    {
        return _conversationAppService.FindConversationAsync(input);
    }

    [HttpPost]
    [Route("find-messages-in-conversation")]
    public Task<List<ChatMessageDto>> FindMessagesInConversationAsync([FromBody] FindMessageInConversationInput input)
    {
        return _conversationAppService.FindMessagesInConversationAsync(input);
    }

    [HttpPost]
    [Route("message-context")]
    public Task<MessageContextDto> GetMessageContextAsync([FromBody] GetMessageContextInput input)
    {
        return _conversationAppService.GetMessageContextAsync(input);
    }

    [HttpPost]
    [Route("search-messages")]
    public Task<List<MessageSearchResultDto>> SearchMessagesAsync([FromBody] SearchConversationMessagesInput input)
    {
        return _conversationAppService.SearchMessagesAsync(input);
    }

    [HttpPost]
    [Route("find-media-and-file-in-conversation")]
    public Task<List<MessageFileDto>> FindMediaAndFileInConversationAsync([FromBody] FindMediaAndFileInConversationInput input)
    {
        return _conversationAppService.FindMediaAndFileInConversationAsync(input);
    }

    [HttpGet]
    [Route("project/{projectId}")]
    public Task<ConversationDto> FindConversationByProjectIdAsync(Guid projectId)
    {
        return _conversationAppService.FindConversationByProjectIdAsync(projectId);
    }
    
    // Unread message count endpoints
    [HttpPost]
    [Route("update-unread-count")]
    public Task UpdateUnreadCountAsync([FromBody] UpdateUnreadCountInput input)
    {
        return _conversationAppService.UpdateUnreadCountAsync(input);
    }
    
    [HttpPost]
    [Route("reset-unread-count")]
    public Task ResetUnreadCountAsync([FromBody] ResetUnreadCountInput input)
    {
        return _conversationAppService.ResetUnreadCountAsync(input);
    }
    
    [HttpGet]
    [Route("total-unread-count")]
    public Task<TotalUnreadCountDto> GetTotalUnreadCountAsync()
    {
        return _conversationAppService.GetTotalUnreadCountAsync();
    }
}
