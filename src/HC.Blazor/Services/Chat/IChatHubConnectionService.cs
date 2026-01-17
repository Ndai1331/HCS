using System;
using System.Threading.Tasks;
using HC.Chat.Messages;

namespace HC.Blazor.Components.Chat;

public interface IChatHubConnectionService
{
    Guid LastNotificationMessageId { get; set; }

    Task ReceivedMessageAsync(ChatMessageRdto message);

    Task OnReceiveMessageAsync(Func<ChatMessageRdto, Task> func);

    Task DeletedMessageAsync(Guid messageId);

    Task OnDeletedMessageAsync(Func<Guid, Task> func);

    Task DeletedConversationAsync(Guid userId);

    Task OnDeletedConversationAsync(Func<Guid, Task> func);

    Task ConversationCreatedAsync(object conversationData);

    Task OnConversationCreatedAsync(Func<object, Task> func);

    Task InitializeAsync(string hubUrl, string accessToken);

    bool IsConnected { get; }
}
