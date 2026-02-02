using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using HC.Chat.Conversations;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Components.Forms;
using HC.Chat.Messages;

namespace HC.Blazor.Pages.Chat1.Handlers
{
    /// <summary>
    /// Handles file-related operations for the chat system
    /// Responsible for uploading, downloading, and managing files
    /// </summary>
    public interface IChatFileHandler
    {
        Task OnFileSelectedAsync(InputFileChangeEventArgs e);
        Task DownloadFileAsync(Guid fileId);
        Task RemoveFileAsync(MessageFileDto file);
    }

    /// <summary>
    /// Implementation of chat file handler
    /// </summary>
    public class ChatFileHandler : IChatFileHandler
    {
        private readonly IConversationAppService _conversationAppService;
        private readonly ILogger<ChatFileHandler> _logger;
        private readonly IJSRuntime _jsRuntime;
        private readonly ChatState _state;

        // Configuration
        private const int MaxFileSizeBytes = 100 * 1024 * 1024; // 100MB
        private const int MaxFileCount = 10;
        private static readonly HashSet<string> AllowedFileTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp",
            ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx",
            ".txt", ".csv", ".zip", ".rar"
        };

        public ChatFileHandler(
            IConversationAppService conversationAppService,
            ILogger<ChatFileHandler> logger,
            IJSRuntime jsRuntime,
            ChatState state)
        {
            _conversationAppService = conversationAppService;
            _logger = logger;
            _jsRuntime = jsRuntime;
            _state = state;
        }

        /// <summary>
        /// Handle file selection and upload
        /// </summary>
        public async Task OnFileSelectedAsync(InputFileChangeEventArgs e)
        {
            var files = e.GetMultipleFiles(MaxFileCount);

            if (files.Count > MaxFileCount)
            {
                await ShowErrorAsync($"Maximum {MaxFileCount} files allowed");
                return;
            }

            foreach (var file in files)
            {
                try
                {
                    // Validate file
                    var validationResult = ValidateFile(file);
                    if (!validationResult.IsValid)
                    {
                        await ShowErrorAsync(validationResult.ErrorMessage);
                        continue;
                    }

                    // Upload file
                    var uploadedFile = await UploadFileAsync(file);
                    
                    if (uploadedFile != null)
                    {
                        _state.UploadedFiles.Add(uploadedFile);
                        await _state.NotifyStateChangedAsync();
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error uploading file: {FileName}", file.Name);
                    await ShowErrorAsync($"Failed to upload {file.Name}");
                }
            }
        }

        /// <summary>
        /// Download a file
        /// </summary>
        public async Task DownloadFileAsync(Guid fileId)
        {
            try
            {
                var file = await _conversationAppService.DownloadFileAsync(fileId);

                // Create download link using JavaScript
                var base64 = Convert.ToBase64String(file.Content);
                var dataUrl = $"data:{file.ContentType};base64,{base64}";

                await _jsRuntime.InvokeVoidAsync("eval", $@"
                    var link = document.createElement('a');
                    link.href = '{dataUrl}';
                    link.download = '{file.FileName}';
                    document.body.appendChild(link);
                    link.click();
                    document.body.removeChild(link);
                ");

                _logger.LogInformation("File downloaded: {FileName}", file.FileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error downloading file: {FileId}", fileId);
                await ShowErrorAsync("Failed to download file");
            }
        }

        /// <summary>
        /// Remove a file from the uploaded files list
        /// </summary>
        public async Task RemoveFileAsync(MessageFileDto file)
        {
            if (_state.UploadedFiles.Contains(file))
            {
                _state.UploadedFiles.Remove(file);
                await _state.NotifyStateChangedAsync();
            }
        }

        /// <summary>
        /// Validate a file before upload
        /// </summary>
        private (bool IsValid, string ErrorMessage) ValidateFile(IBrowserFile file)
        {
            // Check file size
            if (file.Size > MaxFileSizeBytes)
            {
                return (false, $"{file.Name} exceeds size limit ({MaxFileSizeBytes / (1024 * 1024)}MB)");
            }

            // Check file extension
            var extension = Path.GetExtension(file.Name);
            if (!AllowedFileTypes.Contains(extension))
            {
                return (false, $"File type {extension} is not allowed");
            }

            return (true, string.Empty);
        }

        /// <summary>
        /// Upload a single file to the server
        /// </summary>
        private async Task<MessageFileDto> UploadFileAsync(IBrowserFile file)
        {
            try
            {
                // Read file content
                using var memoryStream = new MemoryStream();
                await file.OpenReadStream(MaxFileSizeBytes).CopyToAsync(memoryStream);
                memoryStream.Position = 0;
                var fileBytes = memoryStream.ToArray();

                _logger.LogInformation("Uploading file: {FileName}, Size: {Size} bytes", file.Name, file.Size);

                // Upload file
                var uploadedFile = await _conversationAppService.UploadFileAsync(new UploadFileInput
                {
                    FileContent = fileBytes,
                    FileName = file.Name,
                    ContentType = file.ContentType,
                    ConversationId = _state.CurrentConversationId
                });

                _logger.LogInformation("File uploaded successfully: {FileName}, FileId: {FileId}", file.Name, uploadedFile.Id);

                return uploadedFile;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading file: {FileName}", file.Name);
                throw;
            }
        }

        /// <summary>
        /// Show error message to user
        /// TODO: Integrate with actual UI message service
        /// </summary>
        private async Task ShowErrorAsync(string message)
        {
            _logger.LogWarning("File validation error: {Message}", message);
            
            // TODO: Show error in UI
            // await _uiMessageService.Error(message);
            
            await Task.CompletedTask;
        }

        /// <summary>
        /// Get file icon based on content type
        /// </summary>
        public static string GetFileIcon(string contentType)
        {
            return contentType.ToLowerInvariant() switch
            {
                var ct when ct.StartsWith("image/") => "bi-file-image",
                var ct when ct.StartsWith("video/") => "bi-file-play",
                var ct when ct.StartsWith("audio/") => "bi-file-music",
                "application/pdf" => "bi-file-pdf",
                var ct when ct.Contains("word") => "bi-file-word",
                var ct when ct.Contains("excel") || ct.Contains("spreadsheet") => "bi-file-excel",
                var ct when ct.Contains("powerpoint") || ct.Contains("presentation") => "bi-file-ppt",
                "application/zip" or "application/x-zip-compressed" => "bi-file-zip",
                "text/plain" => "bi-file-text",
                _ => "bi-file"
            };
        }

        /// <summary>
        /// Format file size for display
        /// </summary>
        public static string FormatFileSize(long bytes)
        {
            string[] suffixes = { "B", "KB", "MB", "GB" };
            int counter = 0;
            double number = bytes;
            
            while (Math.Round(number / 1024) >= 1)
            {
                number /= 1024;
                counter++;
            }
            
            return $"{number:n1}{suffixes[counter]}";
        }

        /// <summary>
        /// Check if file is an image
        /// </summary>
        public static bool IsImage(string contentType)
        {
            return !string.IsNullOrEmpty(contentType) && contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase);
        }
    }
}
