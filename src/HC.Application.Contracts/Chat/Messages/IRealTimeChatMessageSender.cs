using System;
using System.Threading.Tasks;

namespace HC.Chat.Messages;

public interface IRealTimeChatMessageSender
{
    Task SendAsync(Guid targetUserId, ChatMessageRdto message);
    
    Task DeleteMessageAsync(Guid targetUserId, Guid messageId);

    Task DeleteConversationAsync(Guid targetUserId, Guid userId, Guid? conversationId);
}
