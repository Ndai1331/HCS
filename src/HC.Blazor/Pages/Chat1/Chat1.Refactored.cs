using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using HC.Chat.Messages;
using HC.Blazor.Pages.Chat1.Handlers;

namespace HC.Blazor.Pages.Chat1
{
    /// <summary>
    /// Partial class for refactored chat functionality
    /// Contains new handler-based implementations alongside legacy code
    /// </summary>
    public partial class Chat1
    {
        #region Refactored Message Sending (with Handler)

        /// <summary>
        /// Send message using the new ChatMessageHandler
        /// This method runs alongside the legacy SendMessageAsync for testing
        /// </summary>
        private async Task SendMessageWithHandlerAsync()
        {
            if (_messageHandler == null)
            {
                _logger.LogWarning("MessageHandler not initialized, falling back to legacy method");
                await SendMessageAsync();
                return;
            }

            try
            {
                // Use handler to send message
                await _messageHandler.SendMessageAsync(
                    Message,
                    UploadedFiles,
                    ReplyingToMessage
                );

                // Sync state back to existing properties
                Message = _state.MessageText;
                UploadedFiles = _state.UploadedFiles;
                ReplyingToMessage = _state.ReplyingToMessage;

                _logger.LogInformation("Message sent successfully via handler");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending message via handler, falling back to legacy method");
                await SendMessageAsync();
            }
        }

        #endregion

        #region Refactored File Handling (with Handler)

        /// <summary>
        /// Handle file selection using the new ChatFileHandler
        /// </summary>
        private async Task OnFileSelectedWithHandlerAsync(Microsoft.AspNetCore.Components.Forms.InputFileChangeEventArgs e)
        {
            if (_fileHandler == null)
            {
                _logger.LogWarning("FileHandler not initialized, falling back to legacy method");
                await OnFileSelected(e);
                return;
            }

            try
            {
                // Use handler to process file selection
                await _fileHandler.OnFileSelectedAsync(e);

                // Sync state back
                UploadedFiles = _state.UploadedFiles;

                _logger.LogInformation("File selected successfully via handler");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling file via handler, falling back to legacy method");
                await OnFileSelected(e);
            }
        }

        /// <summary>
        /// Download file using the new ChatFileHandler
        /// </summary>
        private async Task DownloadFileWithHandlerAsync(Guid fileId)
        {
            if (_fileHandler == null)
            {
                _logger.LogWarning("FileHandler not initialized, falling back to legacy method");
                await DownloadFileAsync(fileId);
                return;
            }

            try
            {
                await _fileHandler.DownloadFileAsync(fileId);
                _logger.LogInformation("File downloaded successfully via handler");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error downloading file via handler, falling back to legacy method");
                await DownloadFileAsync(fileId);
            }
        }

        #endregion

        #region Refactored Pagination (with Handler)

        /// <summary>
        /// Load more messages using the new ChatPaginationHandler
        /// </summary>
        private async Task LoadMoreMessagesWithHandlerAsync()
        {
            if (_paginationHandler == null)
            {
                _logger.LogWarning("PaginationHandler not initialized, falling back to legacy method");
                await LoadMoreMessagesAsync();
                return;
            }

            try
            {
                await _paginationHandler.LoadMoreMessagesAsync();

                // Sync pagination state
                _messagesSkipCount = _pagination.MessageSkipCount;
                _isLoadingMoreMessages = _pagination.IsLoadingMoreMessages;
                _hasMoreMessages = _pagination.HasMoreMessages;

                _logger.LogInformation("More messages loaded successfully via handler");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading messages via handler, falling back to legacy method");
                await LoadMoreMessagesAsync();
            }
        }

        /// <summary>
        /// Load more conversations using the new ChatPaginationHandler
        /// </summary>
        private async Task LoadMoreConversationsWithHandlerAsync()
        {
            if (_paginationHandler == null)
            {
                _logger.LogWarning("PaginationHandler not initialized, falling back to legacy method");
                await LoadMoreConversationsAsync();
                return;
            }

            try
            {
                await _paginationHandler.LoadMoreConversationsAsync();

                // Sync pagination state
                _conversationsSkipCount = _pagination.ConversationSkipCount;
                _isLoadingMoreConversations = _pagination.IsLoadingMoreConversations;
                _hasMoreConversations = _pagination.HasMoreConversations;

                _logger.LogInformation("More conversations loaded successfully via handler");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading conversations via handler, falling back to legacy method");
                await LoadMoreConversationsAsync();
            }
        }

        #endregion

        #region Refactored Message Reception (with Handler)

        /// <summary>
        /// Process received message using optimization handler
        /// Appends message without full refresh when possible
        /// </summary>
        private async Task ProcessReceivedMessageWithHandlerAsync(ChatMessageRdto message)
        {
            if (_optimizationHandler == null || _messageHandler == null)
            {
                _logger.LogWarning("Handlers not initialized, using legacy message processing");
                await ProcessReceivedMessage(message);
                return;
            }

            try
            {
                // Use optimization handler to append message
                await _optimizationHandler.AppendMessageAsync(message);

                // Check if full refresh is needed
                var shouldRefresh = await _optimizationHandler.ShouldRefreshConversationAsync(message);

                if (shouldRefresh)
                {
                    // Fallback to legacy refresh logic
                    await ProcessReceivedMessage(message);
                }
                else
                {
                    // Just update UI with new message
                    await InvokeAsync(StateHasChanged);
                }

                _logger.LogInformation("Message processed successfully via handler");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing message via handler, falling back to legacy method");
                await ProcessReceivedMessage(message);
            }
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Check if refactored handlers are ready for use
        /// </summary>
        private bool AreHandlersReady()
        {
            return _messageHandler != null
                && _fileHandler != null
                && _paginationHandler != null
                && _optimizationHandler != null;
        }

        /// <summary>
        /// Get handler initialization status for debugging
        /// </summary>
        private string GetHandlerStatus()
        {
            return $"MessageHandler: {_messageHandler != null}, " +
                   $"FileHandler: {_fileHandler != null}, " +
                   $"PaginationHandler: {_paginationHandler != null}, " +
                   $"OptimizationHandler: {_optimizationHandler != null}";
        }

        #endregion
    }
}
