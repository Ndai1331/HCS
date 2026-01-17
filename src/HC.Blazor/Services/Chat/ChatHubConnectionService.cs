using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.JSInterop;
using Microsoft.Extensions.Logging;
using Volo.Abp.DependencyInjection;
using HC.Chat.Messages;

namespace HC.Blazor.Components.Chat;

public class ChatHubConnectionService : IChatHubConnectionService, IScopedDependency, IAsyncDisposable
{
     private readonly List<Func<ChatMessageRdto, Task>> _messageReceived;
     private readonly List<Func<Guid, Task>> _messageDeleted;
     private readonly List<Func<Guid, Task>> _conversationDeleted;
     private readonly List<Func<object, Task>> _conversationCreated;
     private readonly ILogger<ChatHubConnectionService> _logger;
     private readonly IJSRuntime _jsRuntime;

     private DotNetObjectReference<ChatHubConnectionService>? _objRef;

     public ChatHubConnectionService(ILogger<ChatHubConnectionService> logger, IJSRuntime jsRuntime)
     {
          _messageReceived = new List<Func<ChatMessageRdto, Task>>();
          _messageDeleted = new List<Func<Guid, Task>>();
          _conversationDeleted = new List<Func<Guid, Task>>();
          _conversationCreated = new List<Func<object, Task>>();
          _logger = logger;
          _jsRuntime = jsRuntime;
     }
     
     public Guid LastNotificationMessageId { get; set; }

     public async Task ReceivedMessageAsync(ChatMessageRdto message)
     {
          _logger.LogInformation($"ChatHubConnectionService: ReceivedMessageAsync called with {message.Id}, calling {_messageReceived.Count} registered callbacks");

          foreach (var func in _messageReceived)
          {
               _logger.LogInformation("ChatHubConnectionService: Calling callback...");
               await func(message);
               _logger.LogInformation("ChatHubConnectionService: Callback completed");
          }

          _logger.LogInformation("ChatHubConnectionService: All callbacks completed");
     }

     public Task OnReceiveMessageAsync(Func<ChatMessageRdto, Task> func)
     {
          _logger.LogInformation("ChatHubConnectionService: Registering OnReceiveMessageAsync callback");
          _messageReceived.Add(func);
          _logger.LogInformation($"ChatHubConnectionService: Total callbacks registered: {_messageReceived.Count}");
          return Task.CompletedTask;
     }

     public async Task DeletedMessageAsync(Guid messageId)
     {
          foreach (var func in _messageDeleted)
          {
               await func(messageId);
          }
     }

     public Task OnDeletedMessageAsync(Func<Guid, Task> func)
     {
          _messageDeleted.Add(func);
          return Task.CompletedTask;
     }

     public async Task DeletedConversationAsync(Guid userId)
     {
          foreach (var func in _conversationDeleted)
          {
               await func(userId);
          }
     }

     public Task OnDeletedConversationAsync(Func<Guid, Task> func)
     {
          _conversationDeleted.Add(func);
          return Task.CompletedTask;
     }

     public async Task ConversationCreatedAsync(object conversationData)
     {
          _logger.LogInformation($"ChatHubConnectionService: ConversationCreatedAsync called, calling {_conversationCreated.Count} registered callbacks");

          foreach (var func in _conversationCreated)
          {
               _logger.LogInformation("ChatHubConnectionService: Calling conversation created callback...");
               await func(conversationData);
               _logger.LogInformation("ChatHubConnectionService: Conversation created callback completed");
          }

          _logger.LogInformation("ChatHubConnectionService: All conversation created callbacks completed");
     }

     public Task OnConversationCreatedAsync(Func<object, Task> func)
     {
          _logger.LogInformation("ChatHubConnectionService: Registering OnConversationCreatedAsync callback");
          _conversationCreated.Add(func);
          _logger.LogInformation($"ChatHubConnectionService: Total conversation created callbacks registered: {_conversationCreated.Count}");
          return Task.CompletedTask;
     }

     public async Task InitializeAsync(string hubUrl, string accessToken)
     {
          try
          {
               _logger.LogInformation("ChatHubConnectionService: Initializing SignalR connection...");

               // Create object reference for JavaScript callbacks
               _objRef = DotNetObjectReference.Create(this);
               _logger.LogInformation($"ChatHubConnectionService: Created DotNetObjectReference: {_objRef != null}");

               // Start the JavaScript SignalR connection
               _logger.LogInformation("ChatHubConnectionService: Calling chatHub.start...");
               await _jsRuntime.InvokeVoidAsync("chatHub.start", _objRef);
               _logger.LogInformation("ChatHubConnectionService: chatHub.start completed");

               _logger.LogInformation("ChatHubConnectionService: Chat SignalR connection initialized successfully");
               _logger.LogInformation("Chat SignalR connection initialized via JavaScript");
          }
          catch (Exception ex)
          {
               _logger.LogError(ex, $"ChatHubConnectionService: Failed to initialize SignalR connection: {ex.Message}");
               _logger.LogError(ex, "Failed to initialize chat SignalR connection");
               throw;
          }
     }

    // Test method for JS interop
    [JSInvokable("TestJSInterop")]
    public void TestJSInterop()
    {
        _logger.LogInformation("ChatHubConnectionService: TestJSInterop called successfully!");
    }

    // Simple test with string parameter
    [JSInvokable("TestWithString")]
    public void TestWithString(string message)
    {
        _logger.LogInformation($"ChatHubConnectionService: TestWithString called with: {message}");
    }

    // Handler using JsonElement for SignalR messages
    [JSInvokable("HandleSignalRMessageJson")]
    public async Task HandleSignalRMessageJson(JsonElement messageData)
    {
        _logger.LogInformation("ChatHubConnectionService: HandleSignalRMessageJson ENTRY with JsonElement!");
        _logger.LogInformation($"ChatHubConnectionService: HandleSignalRMessageJson called with: {messageData.GetRawText()}");

        try
        {
            // Deserialize JsonElement to ChatMessageRdto
            var message = JsonSerializer.Deserialize<ChatMessageRdto>(messageData.GetRawText(), new JsonSerializerOptions 
            { 
                PropertyNameCaseInsensitive = true 
            });

            if (message != null)
            {
                message.IsCrossTabMessage = false; // This is from SignalR, not cross-tab
                _logger.LogInformation($"ChatHubConnectionService: Deserialized message - Id: {message.Id}, Sender: {message.SenderUsername}, Text: {message.Text}");
                await ReceivedMessageAsync(message);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"ChatHubConnectionService: Error processing message: {ex.Message}");
        }
    }

    // Handler using JsonElement for cross-tab messages
    [JSInvokable("HandleCrossTabMessageJson")]
    public async Task HandleCrossTabMessageJson(JsonElement messageData)
    {
        _logger.LogInformation("ChatHubConnectionService: HandleCrossTabMessageJson ENTRY with JsonElement!");
        _logger.LogInformation($"ChatHubConnectionService: HandleCrossTabMessageJson called with: {messageData.GetRawText()}");

        try
        {
            // Deserialize JsonElement to ChatMessageRdto
            var message = JsonSerializer.Deserialize<ChatMessageRdto>(messageData.GetRawText(), new JsonSerializerOptions 
            { 
                PropertyNameCaseInsensitive = true 
            });

            if (message != null)
            {
                message.IsCrossTabMessage = true; // Mark as cross-tab message
                _logger.LogInformation($"ChatHubConnectionService: Deserialized cross-tab message - Id: {message.Id}, Sender: {message.SenderUsername}, Text: {message.Text}");
                await ReceivedMessageAsync(message);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"ChatHubConnectionService: Error processing cross-tab message: {ex.Message}");
        }
    }

    // Direct SignalR message handler
    [JSInvokable("HandleSignalRMessage")]
    public async Task HandleSignalRMessage(object messageData)
    {
        _logger.LogInformation("ChatHubConnectionService: HandleSignalRMessage ENTRY POINT!");
        _logger.LogInformation($"ChatHubConnectionService: HandleSignalRMessage called with: {System.Text.Json.JsonSerializer.Serialize(messageData)}");

        try
        {
            // Convert dynamic object to ChatMessageRdto
            var message = new ChatMessageRdto
            {
                Id = Guid.Parse(messageData.GetType().GetProperty("Id")?.GetValue(messageData)?.ToString() ?? Guid.Empty.ToString()),
                ConversationId = Guid.TryParse(messageData.GetType().GetProperty("ConversationId")?.GetValue(messageData)?.ToString(), out var convId) ? convId : null,
                SenderUserId = Guid.Parse(messageData.GetType().GetProperty("SenderUserId")?.GetValue(messageData)?.ToString() ?? Guid.Empty.ToString()),
                SenderUsername = messageData.GetType().GetProperty("SenderUsername")?.GetValue(messageData)?.ToString(),
                SenderName = messageData.GetType().GetProperty("SenderName")?.GetValue(messageData)?.ToString(),
                SenderSurname = messageData.GetType().GetProperty("SenderSurname")?.GetValue(messageData)?.ToString(),
                Text = messageData.GetType().GetProperty("Text")?.GetValue(messageData)?.ToString()
            };

            _logger.LogInformation($"ChatHubConnectionService: Forwarding message to registered callbacks - Id: {message.Id}, Sender: {message.SenderUsername}, ConversationId: {message.ConversationId}");
            await ReceivedMessageAsync(message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"ChatHubConnectionService: Error processing SignalR message: {ex.Message}");
        }
    }

    // Handle cross-tab messages (from BroadcastChannel)
    [JSInvokable]
    public async Task HandleCrossTabMessage(object messageData)
    {
        _logger.LogInformation($"ChatHubConnectionService: HandleCrossTabMessage called with: {System.Text.Json.JsonSerializer.Serialize(messageData)}");

        try
        {
            // Convert dynamic object to ChatMessageRdto
            var message = new ChatMessageRdto
            {
                Id = Guid.Parse(messageData.GetType().GetProperty("Id")?.GetValue(messageData)?.ToString() ?? Guid.Empty.ToString()),
                ConversationId = Guid.TryParse(messageData.GetType().GetProperty("ConversationId")?.GetValue(messageData)?.ToString(), out var convId) ? convId : null,
                SenderUserId = Guid.Parse(messageData.GetType().GetProperty("SenderUserId")?.GetValue(messageData)?.ToString() ?? Guid.Empty.ToString()),
                SenderUsername = messageData.GetType().GetProperty("SenderUsername")?.GetValue(messageData)?.ToString(),
                SenderName = messageData.GetType().GetProperty("SenderName")?.GetValue(messageData)?.ToString(),
                SenderSurname = messageData.GetType().GetProperty("SenderSurname")?.GetValue(messageData)?.ToString(),
                Text = messageData.GetType().GetProperty("Text")?.GetValue(messageData)?.ToString()
            };

            // Mark as cross-tab message to avoid duplicate processing
            message.IsCrossTabMessage = true;

            _logger.LogInformation($"ChatHubConnectionService: Forwarding cross-tab message to registered callbacks - Id: {message.Id}, Sender: {message.SenderUsername}, ConversationId: {message.ConversationId}");
            await ReceivedMessageAsync(message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"ChatHubConnectionService: Error processing cross-tab message: {ex.Message}");
        }
    }

    // Methods called by JavaScript
    [JSInvokable]
    public void OnMessageReceivedSync(object messageData)
    {
        _logger.LogInformation($"ChatHubConnectionService: OnMessageReceivedSync called with: {System.Text.Json.JsonSerializer.Serialize(messageData)}");
    }

    [JSInvokable]
    public async Task OnMessageReceived(object messageData)
    {
          try
          {
               _logger.LogInformation($"ChatHubConnectionService: OnMessageReceived called with data: {System.Text.Json.JsonSerializer.Serialize(messageData)}");

               // Convert dynamic object to ChatMessageRdto
               var message = new ChatMessageRdto
               {
                    Id = Guid.Parse(messageData.GetType().GetProperty("Id")?.GetValue(messageData)?.ToString() ?? Guid.Empty.ToString()),
                    ConversationId = Guid.TryParse(messageData.GetType().GetProperty("ConversationId")?.GetValue(messageData)?.ToString(), out var convId) ? convId : null,
                    SenderUserId = Guid.Parse(messageData.GetType().GetProperty("SenderUserId")?.GetValue(messageData)?.ToString() ?? Guid.Empty.ToString()),
                    SenderUsername = messageData.GetType().GetProperty("SenderUsername")?.GetValue(messageData)?.ToString(),
                    SenderName = messageData.GetType().GetProperty("SenderName")?.GetValue(messageData)?.ToString(),
                    SenderSurname = messageData.GetType().GetProperty("SenderSurname")?.GetValue(messageData)?.ToString(),
                    Text = messageData.GetType().GetProperty("Text")?.GetValue(messageData)?.ToString()
               };

               _logger.LogInformation($"ChatHubConnectionService: Converted to ChatMessageRdto - Id: {message.Id}, Sender: {message.SenderUsername}, Text: {message.Text}, ConversationId: {message.ConversationId}");

               // Use Task.Run to ensure we're not blocking the JS interop thread
               Task.Run(async () =>
               {
                    _logger.LogInformation("ChatHubConnectionService: Calling ReceivedMessageAsync in Task.Run...");
                    try
                    {
                         await ReceivedMessageAsync(message);
                         _logger.LogInformation("ChatHubConnectionService: ReceivedMessageAsync completed");
                    }
                    catch (Exception ex)
                    {
                         _logger.LogError(ex, $"ChatHubConnectionService: Error in Task.Run: {ex.Message}");
                    }
               });
          }
          catch (Exception ex)
          {
               _logger.LogError(ex, $"ChatHubConnectionService: Error processing message: {ex.Message}");
          }
     }

     [JSInvokable]
     public async Task OnMessageDeleted(Guid messageId)
     {
          await DeletedMessageAsync(messageId);
     }

     [JSInvokable]
     public async Task OnConversationDeleted(Guid userId)
     {
          await DeletedConversationAsync(userId);
     }

     [JSInvokable]
     public async Task OnConversationCreated(object conversationData)
     {
          _logger.LogInformation($"ChatHubConnectionService: OnConversationCreated called with: {System.Text.Json.JsonSerializer.Serialize(conversationData)}");
          await ConversationCreatedAsync(conversationData);
     }

     public async ValueTask DisposeAsync()
     {
          // IMPORTANT: Don't stop chatHub connection here!
          // The connection is shared between Chat page and NotificationToast
          // Let the connection stay alive for notifications
          _logger.LogInformation("ChatHubConnectionService: Disposing (keeping connection alive for notifications)");

          // Clear callbacks
          _messageReceived.Clear();
          _messageDeleted.Clear();
          _conversationDeleted.Clear();
          _conversationCreated.Clear();

          // Dispose object reference
          _objRef?.Dispose();
          _objRef = null;
          
          await Task.CompletedTask;
     }

     public bool IsConnected => true; // For now, assume connected since we can't check JS connection status easily
}
