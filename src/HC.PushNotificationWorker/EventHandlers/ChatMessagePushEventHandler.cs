using System.Threading.Tasks;
using HC.Chat.Messages;
using Microsoft.Extensions.Logging;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EventBus.Distributed;
using HC.PushNotificationWorker.Services;

namespace HC.PushNotificationWorker.EventHandlers;

public class ChatMessagePushEventHandler : IDistributedEventHandler<ChatMessageEto>, ITransientDependency
{
    private readonly FirebaseChatPushSender _pushSender;
    private readonly ILogger<ChatMessagePushEventHandler> _logger;

    public ChatMessagePushEventHandler(
        FirebaseChatPushSender pushSender,
        ILogger<ChatMessagePushEventHandler> logger)
    {
        _pushSender = pushSender;
        _logger = logger;
    }

    public async Task HandleEventAsync(ChatMessageEto eventData)
    {
        _logger.LogDebug(
            "Chat push handler: MessageId={MessageId}, Target={TargetUserId}",
            eventData.MessageId,
            eventData.TargetUserId);

        await _pushSender.SendChatMessageAsync(eventData);
    }
}
