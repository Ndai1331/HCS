using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;
using HC.Chat.Messages;
using HC.Chat.Conversations;

namespace HC.Chat.Files;

[RemoteService(Name = ChatRemoteServiceConsts.RemoteServiceName)]
[Area(ChatRemoteServiceConsts.ModuleName)]
[Route("api/chat/files")]
public class FileController : ChatController
{
    private readonly IConversationAppService _conversationAppService;

    public FileController(IConversationAppService conversationAppService)
    {
        _conversationAppService = conversationAppService;
    }

    [HttpPost]
    [Route("upload")]
    public Task<MessageFileDto> UploadFileAsync([FromForm] UploadFileInput input)
    {
        // Convert IFormFile to UploadFileInput
        // TODO: Implement file conversion
        return _conversationAppService.UploadFileAsync(input);
    }

    [HttpGet]
    [Route("{id}/download")]
    public Task<FileDto> DownloadFileAsync(Guid id)
    {
        return _conversationAppService.DownloadFileAsync(id);
    }

    [HttpDelete]
    [Route("{id}")]
    public Task DeleteFileAsync(Guid id)
    {
        return _conversationAppService.DeleteFileAsync(id);
    }

    [HttpGet]
    [Route("message/{messageId}")]
    public Task<List<MessageFileDto>> GetMessageFilesAsync(Guid messageId)
    {
        return _conversationAppService.GetMessageFilesAsync(messageId);
    }
}
