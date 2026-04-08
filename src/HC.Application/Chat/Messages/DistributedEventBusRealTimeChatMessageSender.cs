using System;
using System.Threading.Tasks;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EventBus.Distributed;

namespace HC.Chat.Messages;

[Dependency(TryRegister = true)]
public class DistributedEventBusRealTimeChatMessageSender : IRealTimeChatMessageSender, ITransientDependency
{
    protected IDistributedEventBus DistributedEventBus { get; }

    public DistributedEventBusRealTimeChatMessageSender(IDistributedEventBus distributedEventBus)
    {
        DistributedEventBus = distributedEventBus;
    }

    public virtual async Task SendAsync(Guid targetUserId, ChatMessageRdto message)
    {
        await DistributedEventBus.PublishAsync(
            new ChatMessageEto
            {
                SenderUserId = message.SenderUserId,
                SenderUserName = message.SenderUsername,
                SenderName = message.SenderName,
                SenderSurname = message.SenderSurname,
                TargetUserId = targetUserId,
                ConversationId = message.ConversationId,
                ConversationType = message.ConversationType,
                ConversationName = message.ConversationName,
                Message = message.Text,
                MessageId = message.Id
            }
        );
    }
    
    public async Task DeleteMessageAsync(Guid targetUserId, Guid messageId)
    {
        await DistributedEventBus.PublishAsync(
            new ChatDeletedMessageEto
            {
                TargetUserId = targetUserId,
                MessageId = messageId
            }
        );
    }
    
    public async Task DeleteConversationAsync(Guid targetUserId, Guid userId, Guid? conversationId)
    {
        await DistributedEventBus.PublishAsync(
            new ChatDeletedConversationEto
            {
                TargetUserId = targetUserId,
                UserId = userId,
                ConversationId = conversationId
            }
        );
    }
}
