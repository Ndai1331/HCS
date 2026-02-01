using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Microsoft.Extensions.Logging;
using System.Linq;
using System.Threading;
using HC.Chat.Messages;

namespace HC.Blazor.Services
{
    /// <summary>
    /// Service for managing Chat SignalR connection lifecycle
    /// Provides proper disposal and prevents memory leaks
    /// </summary>
    public interface IChatHubConnectionService
    {
        Task InitializeAsync(string hubUrl, string context);
        Task RegisterAsync<T>(T component) where T : class;
        Task UnregisterAsync<T>(T component) where T : class;
        Task OnReceiveMessageAsync(Func<ChatMessageRdto, Task> callback);
        Task OnDeletedMessageAsync(Func<Guid, Task> callback);
        Task OnDeletedConversationAsync(Func<Guid, Task> callback);
        Task OnConversationCreatedAsync(Func<object, Task> callback);
        Task CleanupAsync();
    }

    /// <summary>
    /// Implementation of Chat SignalR connection service
    /// Handles connection lifecycle, event registration, and proper cleanup
    /// </summary>
    public class ChatHubConnectionService : IChatHubConnectionService, IAsyncDisposable
    {
        private readonly IJSRuntime _jsRuntime;
        private readonly ILogger<ChatHubConnectionService> _logger;
        private readonly DotNetObjectReference<ChatHubConnectionService> _dotNetReference;
        
        private Func<ChatMessageRdto, Task> _onReceiveMessageCallback;
        private Func<Guid, Task> _onDeletedMessageCallback;
        private Func<Guid, Task> _onDeletedConversationCallback;
        private Func<object, Task> _onConversationCreatedCallback;
        
        private bool _isInitialized = false;
        private bool _isDisposed = false;
        private readonly SemaphoreSlim _lock = new SemaphoreSlim(1, 1);

        public ChatHubConnectionService(IJSRuntime jsRuntime, ILogger<ChatHubConnectionService> logger)
        {
            _jsRuntime = jsRuntime;
            _logger = logger;
            _dotNetReference = DotNetObjectReference.Create(this);
        }

        /// <summary>
        /// Initialize the SignalR connection
        /// </summary>
        public async Task InitializeAsync(string hubUrl, string context)
        {
            if (_isDisposed)
            {
                _logger.LogWarning("ChatHubConnectionService is disposed, cannot initialize");
                return;
            }

            await _lock.WaitAsync();
            try
            {
                if (_isInitialized)
                {
                    _logger.LogDebug("ChatHubConnectionService already initialized");
                    return;
                }

                _logger.LogInformation("Initializing ChatHubConnectionService for {Context}", context);

                // Initialize the chat hub via JavaScript
                await _jsRuntime.InvokeVoidAsync("chatHub.start", _dotNetReference);

                _isInitialized = true;
                _logger.LogInformation("ChatHubConnectionService initialized successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize ChatHubConnectionService");
                throw;
            }
            finally
            {
                _lock.Release();
            }
        }

        /// <summary>
        /// Register a component to receive messages (legacy method for backward compatibility)
        /// </summary>
        public Task RegisterAsync<T>(T component) where T : class
        {
            _logger.LogDebug("Registering component of type {Type}", typeof(T).Name);
            // This is a no-op in the new architecture since components are passed directly to JS
            return Task.CompletedTask;
        }

        /// <summary>
        /// Unregister a component from receiving messages
        /// </summary>
        public async Task UnregisterAsync<T>(T component) where T : class
        {
            if (_isDisposed)
            {
                return;
            }

            _logger.LogDebug("Unregistering component of type {Type}", typeof(T).Name);
            
            // Cleanup will be handled in CleanupAsync()
            await Task.CompletedTask;
        }

        /// <summary>
        /// Register callback for receiving messages
        /// </summary>
        public Task OnReceiveMessageAsync(Func<ChatMessageRdto, Task> callback)
        {
            _onReceiveMessageCallback = callback;
            return Task.CompletedTask;
        }

        /// <summary>
        /// Register callback for deleted messages
        /// </summary>
        public Task OnDeletedMessageAsync(Func<Guid, Task> callback)
        {
            _onDeletedMessageCallback = callback;
            return Task.CompletedTask;
        }

        /// <summary>
        /// Register callback for deleted conversations
        /// </summary>
        public Task OnDeletedConversationAsync(Func<Guid, Task> callback)
        {
            _onDeletedConversationCallback = callback;
            return Task.CompletedTask;
        }

        /// <summary>
        /// Register callback for created conversations
        /// </summary>
        public Task OnConversationCreatedAsync(Func<object, Task> callback)
        {
            _onConversationCreatedCallback = callback;
            return Task.CompletedTask;
        }

        /// <summary>
        /// JS Invokable method to handle SignalR messages
        /// </summary>
        [JSInvokable]
        public async Task HandleSignalRMessageJson(object messageData)
        {
            if (_isDisposed || _onReceiveMessageCallback == null)
            {
                return;
            }

            try
            {
                var message = ParseMessageData(messageData);
                await _onReceiveMessageCallback(message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling SignalR message");
            }
        }

        /// <summary>
        /// JS Invokable method to handle cross-tab messages
        /// </summary>
        [JSInvokable]
        public async Task HandleCrossTabMessageJson(object messageData)
        {
            if (_isDisposed || _onReceiveMessageCallback == null)
            {
                return;
            }

            try
            {
                var message = ParseMessageData(messageData);
                message.IsCrossTabMessage = true;
                await _onReceiveMessageCallback(message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling cross-tab message");
            }
        }

        /// <summary>
        /// Parse message data from dynamic object
        /// </summary>
        private ChatMessageRdto ParseMessageData(object messageData)
        {
            // Parse the dynamic object
            var properties = messageData.GetType().GetProperties();
            
            var getId = () => Guid.TryParse(properties.FirstOrDefault(p => p.Name == "Id")?.GetValue(messageData)?.ToString() ?? Guid.Empty.ToString(), out var id) ? id : Guid.Empty;
            var getConversationId = () => Guid.TryParse(properties.FirstOrDefault(p => p.Name == "ConversationId")?.GetValue(messageData)?.ToString(), out var convId) ? convId : (Guid?)null;
            var getSenderUserId = () => Guid.Parse(properties.FirstOrDefault(p => p.Name == "SenderUserId")?.GetValue(messageData)?.ToString() ?? Guid.Empty.ToString());
            var getSenderUsername = () => properties.FirstOrDefault(p => p.Name == "SenderUsername")?.GetValue(messageData)?.ToString();
            var getSenderName = () => properties.FirstOrDefault(p => p.Name == "SenderName")?.GetValue(messageData)?.ToString();
            var getSenderSurname = () => properties.FirstOrDefault(p => p.Name == "SenderSurname")?.GetValue(messageData)?.ToString();
            var getText = () => properties.FirstOrDefault(p => p.Name == "Text")?.GetValue(messageData)?.ToString();

            return new ChatMessageRdto
            {
                Id = getId(),
                ConversationId = getConversationId(),
                SenderUserId = getSenderUserId(),
                SenderUsername = getSenderUsername(),
                SenderName = getSenderName(),
                SenderSurname = getSenderSurname(),
                Text = getText(),
                IsCrossTabMessage = false
            };
        }

        /// <summary>
        /// Cleanup resources and stop connection
        /// </summary>
        public async Task CleanupAsync()
        {
            if (_isDisposed)
            {
                return;
            }

            await _lock.WaitAsync();
            try
            {
                _logger.LogInformation("Cleaning up ChatHubConnectionService");

                // Clear callbacks
                _onReceiveMessageCallback = null;
                _onDeletedMessageCallback = null;
                _onDeletedConversationCallback = null;
                _onConversationCreatedCallback = null;

                // Stop the JavaScript connection
                try
                {
                    await _jsRuntime.InvokeVoidAsync("chatHub.stop");
                }
                catch (JSDisconnectedException)
                {
                    // Expected during disposal
                    _logger.LogDebug("JS disconnected during cleanup");
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error stopping chat hub during cleanup");
                }

                _isInitialized = false;
                _logger.LogInformation("ChatHubConnectionService cleanup completed");
            }
            finally
            {
                _lock.Release();
            }
        }

        /// <summary>
        /// Dispose of the service and cleanup resources
        /// </summary>
        public async ValueTask DisposeAsync()
        {
            if (_isDisposed)
            {
                return;
            }

            _logger.LogInformation("Disposing ChatHubConnectionService");

            await CleanupAsync();

            // Dispose the DotNetObjectReference
            try
            {
                _dotNetReference?.Dispose();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error disposing DotNetObjectReference");
            }

            // Dispose the semaphore
            try
            {
                _lock?.Dispose();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error disposing semaphore");
            }

            _isDisposed = true;
            _logger.LogInformation("ChatHubConnectionService disposed");
        }
    }
}
