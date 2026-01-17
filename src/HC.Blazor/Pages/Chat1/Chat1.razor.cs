using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HC.Blazor;
using HC.Blazor.Components.Chat;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using Volo.Abp.Localization;
using HC.Chat.Authorization;
using HC.Chat.Conversations;
using HC.Chat.Messages;
using HC.Chat.Settings;
using HC.Chat.Users;
using HC.Projects;
using HC.ProjectTasks;
using HC.ProjectMembers;
using HC.Shared;
using Microsoft.Extensions.Caching.Memory;
using Volo.Abp.Application.Dtos;
using HC.Blazor.Extensions;
using Microsoft.Extensions.Logging;


namespace HC.Blazor.Pages.Chat1;

public partial class Chat1 : HCComponentBase, IAsyncDisposable
{
    [Inject]
    public IContactAppService ContactAppService { get; set; }

    [Inject]
    public IConversationAppService ConversationAppService { get; set; }

    [Inject]
    public IJSRuntime JsRuntime { get; set; }

    public ISettingsAppService SettingsAppService { get; set; }
    
    [Inject]
    public new IAuthorizationService AuthorizationService { get; set; }

    public List<ChatContactDto> ChatContactDtos { get; set; } = new List<ChatContactDto>();

    public Dictionary<ChatContactDto, string> ChatContactsActive { get; set; } = new Dictionary<ChatContactDto, string>();

    public Dictionary<ChatContactDto, ElementReference> CanvasElementReferences { get; set; } = new Dictionary<ChatContactDto, ElementReference>();

    public string SearchValue { get; set; } = string.Empty;

    public ChatContactDto CurrentChatContact { get; set; }

    public ElementReference CurrentChatContactCanvas { get; set; }

    public ChatConversationDto ChatConversationDto { get; set; }

    public new string Message { get; set; }

    public ElementReference MessageTextArea { get; set; }
    public ElementReference ConversationContainerRef { get; set; }

    public bool SendOnEnter { get; set; } = true; // Default: Enter to send
    
    // Loading state
    public bool IsLoadingMessages { get; set; }
    public bool IsSendingMessage { get; set; } // Loading state for send button (shows spinner but doesn't block)
    private int _pendingMessagesCount = 0; // Track pending messages for optimistic updates
    private bool _isSendingMessage = false; // Prevent duplicate sends
    
    // Pagination for messages
    private int _messagesSkipCount = 0;
    private const int MessagesPageSize = 10;
    private bool _isLoadingMoreMessages = false;
    private bool _hasMoreMessages = true;
    
    // Pagination for conversations
    private int _conversationsSkipCount = 0;
    private const int ConversationsPageSize = 15;
    private bool _isLoadingMoreConversations = false;
    private bool _hasMoreConversations = true;
    
    // Flag to update avatar after render
    private bool _shouldUpdateAvatar = false;
    
    // New properties for expanded features
    public ChatMessageDto ReplyingToMessage { get; set; }
    
    public List<MessageFileDto> UploadedFiles { get; set; } = new List<MessageFileDto>();
    
    public Guid? CurrentConversationId { get; set; }
    
    // Modal states
    public bool ShowCreateDirectModal { get; set; }
    public bool ShowCreateGroupModal { get; set; }
    public bool ShowCreateProjectModal { get; set; }
    public bool ShowCreateTaskModal { get; set; }
    
    // Form inputs
    public string NewGroupName { get; set; }
    public string NewGroupDescription { get; set; }
    public List<LookupDto<Guid>> SelectedMembers { get; set; } = new List<LookupDto<Guid>>();
    public List<LookupDto<Guid>> SelectedDirectUser { get; set; } = new List<LookupDto<Guid>>();
    private IReadOnlyList<LookupDto<Guid>> IdentityUsersCollection { get; set; } = new List<LookupDto<Guid>>();
    
    public List<LookupDto<Guid>> SelectedProject { get; set; } = new List<LookupDto<Guid>>();
    private IReadOnlyList<LookupDto<Guid>> ProjectsCollection { get; set; } = new List<LookupDto<Guid>>();
    public string NewProjectName { get; set; }
    
    public List<LookupDto<Guid>> SelectedTask { get; set; } = new List<LookupDto<Guid>>();
    private IReadOnlyList<LookupDto<Guid>> ProjectTasksCollection { get; set; } = new List<LookupDto<Guid>>();
    public string NewTaskName { get; set; }

    [Inject]
    protected NavigationManager Navigation { get; set; }

    [Inject]
    protected IChatHubConnectionService ChatHubConnectionService { get; set; }

    [Inject]
    protected ILogger<Chat1> _logger { get; set; }

    [Inject]
    protected IJSRuntime JSRuntime { get; set; }

    private DotNetObjectReference<Chat1>? _objRef;

    [JSInvokable]
    public async Task HandleSignalRMessage(object messageData)
    {
        try
        {
            _logger.LogInformation($"Chat1: HandleSignalRMessage called");

            // Convert dynamic object to ChatMessageRdto
            var message = new ChatMessageRdto
            {
                Id = Guid.Parse(messageData.GetType().GetProperty("Id")?.GetValue(messageData)?.ToString() ?? Guid.Empty.ToString()),
                ConversationId = Guid.TryParse(messageData.GetType().GetProperty("ConversationId")?.GetValue(messageData)?.ToString(), out var convId) ? convId : null,
                SenderUserId = Guid.Parse(messageData.GetType().GetProperty("SenderUserId")?.GetValue(messageData)?.ToString() ?? Guid.Empty.ToString()),
                SenderUsername = messageData.GetType().GetProperty("SenderUsername")?.GetValue(messageData)?.ToString(),
                SenderName = messageData.GetType().GetProperty("SenderName")?.GetValue(messageData)?.ToString(),
                SenderSurname = messageData.GetType().GetProperty("SenderSurname")?.GetValue(messageData)?.ToString(),
                Text = messageData.GetType().GetProperty("Text")?.GetValue(messageData)?.ToString(),
                IsCrossTabMessage = false
            };

            await ProcessReceivedMessage(message);
        }
        catch (Exception ex)
        {
            _logger.LogInformation($"Chat1: Error in HandleSignalRMessage: {ex.Message}");
            _logger.LogError(ex, "Error processing SignalR message");
        }
    }

    [JSInvokable]
    public async Task HandleCrossTabMessage(object messageData)
    {
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
                Text = messageData.GetType().GetProperty("Text")?.GetValue(messageData)?.ToString(),
                IsCrossTabMessage = true
            };

            await ProcessReceivedMessage(message);
        }
        catch (Exception ex)
        {
            _logger.LogInformation($"Chat1: Error in HandleCrossTabMessage: {ex.Message}");
            _logger.LogError(ex, "Error processing cross-tab message");
        }
    }

    private async Task ProcessReceivedMessage(ChatMessageRdto message)
    {
        _logger.LogInformation("CHAT1: ProcessReceivedMessage CALLED from service!");

        try
        {
            if (CurrentUser == null)
            {
                _logger.LogInformation("Chat1: CurrentUser is null, ignoring message");
                return;
            }

            _logger.LogInformation($"Chat1: DEBUG - Message details: Id={message.Id}, SenderUserId={message.SenderUserId}, SenderUsername={message.SenderUsername}, Text={message.Text}, ConversationId={message.ConversationId}");
            _logger.LogInformation($"Chat1: DEBUG - Current user details: CurrentUser.Id={CurrentUser.Id}, CurrentUser.UserName={CurrentUser.UserName}");
            _logger.LogInformation($"Chat1: DEBUG - CurrentChatContact: {(CurrentChatContact != null ? $"Type={CurrentChatContact.Type}, UserId={CurrentChatContact.UserId}, ConversationId={CurrentConversationId}" : "NULL")}");

            // Skip messages from current user in same tab (avoid duplicate)
            if (message.SenderUserId == CurrentUser.Id && !message.IsCrossTabMessage)
            {
                _logger.LogInformation("Chat1: Skipping SignalR message from current user in same tab to avoid duplicate processing");
                return;
            }

            // Check if message is for current conversation
            bool isForCurrentConversation = false;

            if (CurrentChatContact != null)
            {
                if (CurrentChatContact.Type == ConversationType.User)
                {
                    // IMPORTANT: Check ConversationId FIRST if both message and current contact have it
                    // This prevents Group messages from same sender being treated as User conversation messages
                    if (message.ConversationId.HasValue && CurrentChatContact.ConversationId.HasValue)
                    {
                        isForCurrentConversation = message.ConversationId.Value == CurrentChatContact.ConversationId.Value;
                        _logger.LogInformation($"Chat1: User chat check by ConversationId - message.ConversationId: {message.ConversationId.Value}, CurrentChatContact.ConversationId: {CurrentChatContact.ConversationId.Value}, isForCurrentConversation: {isForCurrentConversation}");
                    }
                    else
                    {
                        // Fallback: For old User conversations without ConversationId, check based on sender
                        bool isFromOtherUser = CurrentChatContact.UserId == message.SenderUserId;
                        bool isFromCurrentUser = message.SenderUserId == CurrentUser.Id;
                        isForCurrentConversation = isFromOtherUser || isFromCurrentUser;
                        _logger.LogInformation($"Chat1: User chat check by sender (fallback) - CurrentChatContact.UserId: {CurrentChatContact.UserId}, message.SenderUserId: {message.SenderUserId}, CurrentUser.Id: {CurrentUser.Id}, isFromOtherUser: {isFromOtherUser}, isFromCurrentUser: {isFromCurrentUser}, isForCurrentConversation: {isForCurrentConversation}");
                    }
                }
                else if (CurrentChatContact.Type != ConversationType.User && CurrentConversationId.HasValue)
                {
                    // For group conversations: check if message belongs to current conversation
                    isForCurrentConversation = message.ConversationId.HasValue &&
                                             message.ConversationId.Value == CurrentConversationId.Value;

                    _logger.LogInformation($"Chat1: Group chat check - message.ConversationId: {message.ConversationId}, CurrentConversationId: {CurrentConversationId}, SenderUserId: {message.SenderUserId}, CurrentUser.Id: {CurrentUser.Id}, isCrossTab: {message.IsCrossTabMessage}, isForCurrentConversation: {isForCurrentConversation}");

                    if (message.ConversationId.HasValue && CurrentConversationId.HasValue)
                    {
                        _logger.LogInformation($"Chat1: DEBUG - Conversation match check: message.ConversationId == CurrentConversationId? {message.ConversationId.Value == CurrentConversationId.Value}");
                    }
                }
                else
                {
                    _logger.LogInformation($"Chat1: WARNING - Cannot determine if message is for current conversation. Type: {CurrentChatContact.Type}, CurrentConversationId: {CurrentConversationId}, message.ConversationId: {message.ConversationId}");
                }
            }
            else
            {
                // No current conversation selected - always update badge
                _logger.LogInformation("Chat1: CurrentChatContact is null, will update unread badge");
                isForCurrentConversation = false;
            }

            if (isForCurrentConversation)
            {
                _logger.LogInformation("Chat1: Message is for current conversation, refreshing...");

                // Refresh conversation
                if (CurrentChatContact.Type == ConversationType.User)
                {
                    _logger.LogInformation("Chat1: Refreshing direct conversation...");
                    ChatConversationDto = await ConversationAppService.GetConversationAsync(
                        new GetConversationInput { TargetUserId = CurrentChatContact.UserId, MaxResultCount = 100 });
                    _logger.LogInformation($"Chat1: Direct conversation refreshed, messages count: {ChatConversationDto?.Messages?.Count ?? 0}");
                }
                else if (CurrentChatContact.ConversationId.HasValue)
                {
                    _logger.LogInformation("Chat1: Refreshing group conversation...");
                    ChatConversationDto = await ConversationAppService.GetConversationAsync(
                        new GetConversationInput { ConversationId = CurrentChatContact.ConversationId.Value, TargetUserId = Guid.Empty, MaxResultCount = 100 });
                    _logger.LogInformation($"Chat1: Group conversation refreshed, messages count: {ChatConversationDto?.Messages?.Count ?? 0}");
                }

                if (CurrentChatContact.UnreadMessageCount > 0 && CurrentChatContact.Type == ConversationType.User)
                {
                    _logger.LogInformation("Chat1: Marking conversation as read...");
                    await ConversationAppService.MarkConversationAsReadAsync(
                        new MarkConversationAsReadInput { TargetUserId = CurrentChatContact.UserId });
                }

                if (ChatConversationDto != null)
                {
                    _logger.LogInformation("Chat1: Processing conversation messages...");
                    ChatConversationDto.Messages.Reverse();
                    var lastMessage = ChatConversationDto.Messages.LastOrDefault();
                    CurrentChatContact.LastMessage = lastMessage?.Message;
                    CurrentChatContact.LastMessageDate = lastMessage?.MessageDate;
                    _logger.LogInformation($"Chat1: Updated last message: {lastMessage?.Message}");
                }

                // Auto scroll to bottom when receiving new message
                _logger.LogInformation("Chat1: Calling StateHasChanged and scrolling to bottom...");
                await InvokeAsync(async () =>
                {
                    _logger.LogInformation("Chat1: Invoking StateHasChanged...");
                    await InvokeAsync(StateHasChanged);
                    await Task.Delay(100); // Wait for DOM to update
                    _logger.LogInformation("Chat1: Scrolling to bottom...");
                    await ScrollToBottomAsync();
                    _logger.LogInformation("Chat1: UI update completed");
                });
            }
            else
            {
                _logger.LogInformation("Chat1: Message is NOT for current conversation, updating unread badge...");
                _logger.LogInformation("Chat1: Message is NOT for current conversation, updating unread badge...");
                
                // Debug: Log message details
                _logger.LogInformation($"Chat1 DEBUG: Incoming message.ConversationId = {message.ConversationId}");
                _logger.LogInformation($"Chat1 DEBUG: Total conversations in list = {ChatContactDtos.Count}");
                _logger.LogInformation($"Chat1 DEBUG: Conversations in list:");
                foreach (var c in ChatContactDtos)
                {
                    _logger.LogInformation($"  - Type={c.Type}, Name={c.Name}, ConversationId={c.ConversationId}, UserId={c.UserId}");
                }
                
                // Find the conversation in the list and update unread count + last message
                ChatContactDto targetContact = null;
                
                // For User (1-1) conversation: find by SenderUserId
                if (message.ConversationId.HasValue)
                {
                    _logger.LogInformation($"Chat1 DEBUG: Searching by ConversationId = {message.ConversationId.Value}");
                    // Try to find by ConversationId first (works for all types)
                    targetContact = ChatContactDtos.FirstOrDefault(c => 
                        c.ConversationId.HasValue && c.ConversationId.Value == message.ConversationId.Value);
                    
                    if (targetContact != null)
                    {
                        _logger.LogInformation($"Chat1 DEBUG: FOUND by ConversationId! Name={targetContact.Name}");
                    }
                    else
                    {
                        _logger.LogInformation($"Chat1 DEBUG: NOT FOUND by ConversationId");
                    }
                }
                
                // Fallback: For User type, find by UserId (sender)
                if (targetContact == null && message.SenderUserId != CurrentUser.Id)
                {
                    _logger.LogInformation($"Chat1 DEBUG: Fallback - Searching by SenderUserId = {message.SenderUserId}");
                    targetContact = ChatContactDtos.FirstOrDefault(c => 
                        c.Type == ConversationType.User && c.UserId == message.SenderUserId);
                    
                    if (targetContact != null)
                    {
                        _logger.LogInformation($"Chat1 DEBUG: FOUND by UserId! Name={targetContact.Name}");
                    }
                    else
                    {
                        _logger.LogInformation($"Chat1 DEBUG: NOT FOUND by UserId");
                    }
                }
                
                if (targetContact != null)
                {
                    _logger.LogInformation($"Chat1: Found conversation '{targetContact.Name}', updating unread count. Current: {targetContact.UnreadMessageCount}");
                    _logger.LogInformation($"Chat1: Found conversation in list, updating unread count. Current: {targetContact.UnreadMessageCount}");
                    
                    // Update unread count (only if message is from someone else)
                    if (message.SenderUserId != CurrentUser.Id)
                    {
                        targetContact.UnreadMessageCount++;
                        _logger.LogInformation($"Chat1: Incremented UnreadMessageCount to {targetContact.UnreadMessageCount}");
                    }
                    else
                    {
                        _logger.LogInformation($"Chat1: Not incrementing unread count (message from self)");
                    }
                    
                    // Update last message info
                    targetContact.LastMessage = message.Text;
                    targetContact.LastMessageDate = DateTime.Now;
                    
                    // Move conversation to top of list for better UX
                    ChatContactDtos.Remove(targetContact);
                    ChatContactDtos.Insert(0, targetContact);
                    
                    // Update active dictionary
                    if (ChatContactsActive.ContainsKey(targetContact))
                    {
                        var activeState = ChatContactsActive[targetContact];
                        ChatContactsActive.Remove(targetContact);
                        ChatContactsActive[targetContact] = activeState;
                    }
                    
                    _logger.LogInformation($"Chat1: Calling StateHasChanged to update UI...");
                    _logger.LogInformation($"Chat1: Updated conversation - UnreadCount: {targetContact.UnreadMessageCount}, LastMessage: {message.Text?.Substring(0, Math.Min(20, message.Text?.Length ?? 0))}");
                    
                    await InvokeAsync(StateHasChanged);
                    _logger.LogInformation($"Chat1: StateHasChanged completed");
                }
                else
                {
                    _logger.LogInformation("Chat1 ERROR: Could not find conversation in list to update unread badge!");
                    _logger.LogInformation("Chat1: Could not find conversation in list to update unread badge");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogInformation($"Chat1: ERROR in ProcessReceivedMessage: {ex.Message}");
            _logger.LogInformation($"Chat1: Stack trace: {ex.StackTrace}");
        }
    }
    
    protected ChatSettingsDto ChatSettings { get; set; }
    public string ChatMessagesContainerStyle { get; set; }
    public bool HasSearchingPermission { get; set; }

    protected async override Task OnInitializedAsync()
    {
        await GetChatSettingsAsync();

        HasSearchingPermission = await AuthorizationService.IsGrantedAsync(ChatPermissions.Searching);

        // Initialize SignalR connection for real-time chat
        try
        {
            _logger.LogInformation("Chat1: Initializing SignalR connection...");

            // Register callback with ChatHubConnectionService FIRST
            _logger.LogInformation("Chat1: Registering SignalR message callback...");
            await ChatHubConnectionService.OnReceiveMessageAsync(async message =>
            {
                _logger.LogInformation($"Chat1: SignalR message received - Id: {message.Id}, Sender: {message.SenderUsername}, Text: {message.Text}, ConversationId: {message.ConversationId}");
                await ProcessReceivedMessage(message);
            });

            // Register delete callbacks
            await ChatHubConnectionService.OnDeletedMessageAsync(async messageId =>
            {
                ChatConversationDto.Messages.RemoveAll(message => message.Id == messageId);
                var lastMessage = ChatConversationDto.Messages.LastOrDefault();
                CurrentChatContact.LastMessage = lastMessage?.Message;
                CurrentChatContact.LastMessageDate = lastMessage?.MessageDate;
                await InvokeAsync(StateHasChanged);
            });
            
            await ChatHubConnectionService.OnDeletedConversationAsync(async userId =>
            {
                if (userId == CurrentChatContact.UserId)
                {
                    CanvasElementReferences.Clear();
                    ChatConversationDto = null;
                    ChatContactDtos.RemoveAll(contact => contact.UserId == userId);
                }
                await InvokeAsync(StateHasChanged);
            });

            // Initialize SignalR with service reference
            await ChatHubConnectionService.InitializeAsync("/chatHub", string.Empty);
            _logger.LogInformation("Chat1: Chat SignalR connection initialized successfully");
            _logger.LogInformation("Chat SignalR connection initialized successfully");
        }
        catch (Exception ex)
        {
            _logger.LogInformation($"Chat1: Failed to initialize chat SignalR connection: {ex.Message}");
            _logger.LogError(ex, "Failed to initialize chat SignalR connection");
        }
    }

    protected async override Task OnAfterRenderAsync(bool firstRender)
    {
        if(firstRender)
        {
            await GetContactsAsync();
        }

        foreach (var contactDto in ChatContactDtos)
        {
            if (CanvasElementReferences.ContainsKey(contactDto))
            {
                await JsRuntime.SafeInvokeVoidAsync("VoloChatAvatarManager.createCanvasForUser", CanvasElementReferences[contactDto], contactDto.Username, GetName(contactDto));
            }
        }
        
        // Update avatar for current chat contact after canvas is rendered in DOM
        if (_shouldUpdateAvatar && CurrentChatContact?.Type == ConversationType.User)
        {
            _shouldUpdateAvatar = false;
            try
            {
                // Only call if canvas reference is valid (check if it has been rendered)
                // Wait a bit more to ensure canvas is in DOM
                await Task.Delay(100);
                if (CurrentChatContactCanvas.Id != null)
                {
                    await JsRuntime.SafeInvokeVoidAsync("VoloChatAvatarManager.createCanvasForUser", CurrentChatContactCanvas, CurrentChatContact?.Username, GetName(CurrentChatContact));
                }
            }
            catch
            {
                // Ignore errors - canvas might not be ready or component disposed
            }
        }
        
        // Draw avatars for sender messages in Group/Project/Task conversations
        if (CurrentChatContact != null && CurrentChatContact.Type != ConversationType.User && ChatConversationDto?.Messages != null)
        {
            await Task.Delay(100); // Wait for DOM to be ready
            foreach (var message in ChatConversationDto.Messages.Where(m => m.SenderUserId.HasValue && m.Side == ChatMessageSide.Receiver))
            {
                try
                {
                    var canvasId = $"sender-avatar-{message.Id}";
                    var senderName = !string.IsNullOrEmpty(message.SenderName) || !string.IsNullOrEmpty(message.SenderSurname)
                        ? $"{message.SenderName} {message.SenderSurname}".Trim()
                        : message.SenderUsername ?? "";
                    await JsRuntime.SafeInvokeVoidAsync("VoloChatAvatarManager.createCanvasForUserById", canvasId, message.SenderUsername, senderName);
                }
                catch
                {
                    // Silently ignore JS errors
                }
            }
        }

        await JsRuntime.SafeInvokeVoidAsync("eval", "document.getElementById('chat-messages-container')?.scrollTo({ top: document.getElementById('chat-messages-container').scrollHeight, behavior: 'smooth' })");
    }

    public static string GetName(ChatContactDto contact)
    {
        var name = "";

        if (!string.IsNullOrEmpty(contact.Name))
        {
            name += contact.Name + ' ';
        }

        if (!string.IsNullOrEmpty(contact.Surname))
        {
            name += contact.Surname;
        }

        if (name == string.Empty)
        {
            name = contact.Username;
        }

        return name;
    }
    
    public static string GetContactDisplayName(ChatContactDto contact)
    {
        // For group/project/task, use ConversationName if available
        if (contact.Type != ConversationType.User && !string.IsNullOrWhiteSpace(contact.ConversationName))
        {
            return contact.ConversationName;
        }
        
        return GetName(contact);
    }

    public async Task GetContactsAsync(bool includeOtherContacts = false, bool preserveCurrentContact = false, bool loadMore = false)
    {
        if (!loadMore)
        {
            // Reset pagination when loading fresh
            _conversationsSkipCount = 0;
            _hasMoreConversations = true;
            CanvasElementReferences.Clear();
        }
        
        // Preserve current contact selection if requested (e.g., when refreshing after sending message)
        var currentContactId = preserveCurrentContact && CurrentChatContact != null 
            ? (CurrentChatContact.Type == ConversationType.User 
                ? CurrentChatContact.UserId 
                : CurrentChatContact.ConversationId)
            : (Guid?)null;

        if (!loadMore)
        {
            ChatContactsActive.Clear();
        }

        var input = new GetContactsInput
        {
            Filter = SearchValue ?? string.Empty,
            IncludeOtherContacts = includeOtherContacts,
            SkipCount = _conversationsSkipCount,
            MaxResultCount = ConversationsPageSize
        };
        
        var newContacts = await ContactAppService.GetContactsAsync(input);

        if (loadMore)
        {
            // Append new contacts to existing list
            ChatContactDtos.AddRange(newContacts);
            
            // Check if there are more conversations
            if (newContacts.Count < ConversationsPageSize)
            {
                _hasMoreConversations = false;
            }
            else
            {
                _conversationsSkipCount += newContacts.Count;
            }
        }
        else
        {
            // Replace with new contacts
            ChatContactDtos = newContacts;
            
            // Check if there are more conversations
            if (newContacts.Count < ConversationsPageSize)
            {
                _hasMoreConversations = false;
            }
            else
            {
                _conversationsSkipCount = newContacts.Count;
            }
        }

        foreach (var contactDto in newContacts)
        {
            if (!ChatContactsActive.ContainsKey(contactDto))
            {
                ChatContactsActive[contactDto] = "";
            }
        }

        // Restore current contact if preserving
        if (preserveCurrentContact && currentContactId.HasValue)
        {
            CurrentChatContact = ChatContactDtos.FirstOrDefault(c => 
                (c.Type == ConversationType.User && c.UserId == currentContactId.Value) ||
                (c.Type != ConversationType.User && c.ConversationId == currentContactId.Value));
        }
        else
        {
            CurrentChatContact = ChatContactDtos.FirstOrDefault();
            if (CurrentChatContact != null)
            {
                await SetActiveAsync(CurrentChatContact);
            }
            else
            {
                // Don't call createCanvasForUser if there's no current contact or canvas is not ready
                ChatConversationDto = null;
            }
        }

        
        await InvokeAsync(StateHasChanged);
    }
    
    /// <summary>
    /// Refresh contacts list without triggering SetActiveAsync (used after sending message)
    /// </summary>
    private async Task RefreshContactsListAsync()
    {
        // Preserve current contact selection
        var currentContactId = CurrentChatContact != null 
            ? (CurrentChatContact.Type == ConversationType.User 
                ? CurrentChatContact.UserId 
                : CurrentChatContact.ConversationId)
            : (Guid?)null;

        var input = new GetContactsInput
        {
            Filter = SearchValue ?? string.Empty,
            IncludeOtherContacts = false
        };
        
        var refreshedContacts = await ContactAppService.GetContactsAsync(input);
        
        // Update contacts list while preserving active state
        foreach (var refreshedContact in refreshedContacts)
        {
            var existingContact = ChatContactDtos.FirstOrDefault(c => 
                (c.Type == ConversationType.User && c.UserId == refreshedContact.UserId) ||
                (c.Type != ConversationType.User && c.ConversationId == refreshedContact.ConversationId));
            
            if (existingContact != null)
            {
                // Update last message info
                existingContact.LastMessage = refreshedContact.LastMessage;
                existingContact.LastMessageDate = refreshedContact.LastMessageDate;
                existingContact.UnreadMessageCount = refreshedContact.UnreadMessageCount;
            }
            else
            {
                // Add new contact
                ChatContactDtos.Add(refreshedContact);
                ChatContactsActive[refreshedContact] = "";
            }
        }
        
        // Restore current contact if it still exists
        if (currentContactId.HasValue)
        {
            CurrentChatContact = ChatContactDtos.FirstOrDefault(c => 
                (c.Type == ConversationType.User && c.UserId == currentContactId.Value) ||
                (c.Type != ConversationType.User && c.ConversationId == currentContactId.Value));
        }
    }
    
    protected virtual bool IsDeletingMessageEnabled(ChatMessageDto message)
    {
        if (ChatSettings.DeletingMessages == ChatDeletingMessages.Disabled)
        {
            return false;
        }

        if (ChatSettings.DeletingMessages == ChatDeletingMessages.EnabledWithDeletionPeriod)
        {
            if(message.MessageDate.AddSeconds(ChatSettings.MessageDeletionPeriod) < Clock.Now)
            {
                return false;
            }
        }

        return true;
    }
    
    protected virtual bool IsDeletingConversationEnabled()
    {
        if (ChatSettings.DeletingMessages != ChatDeletingMessages.Enabled)
        {
            return false;
        }
        
        if (ChatSettings.DeletingConversations == ChatDeletingConversations.Disabled)
        {
            return false;
        }
        
        // User chỉ xoá conversation (Direct), Admin mới có quyền xoá GROUP | PROJECT | TASK
        if (CurrentChatContact != null)
        {
            // Chỉ cho phép xóa Direct conversations cho tất cả users
            if (CurrentChatContact.Type == ConversationType.User)
            {
                return true;
            }
            
            // GROUP | PROJECT | TASK chỉ admin mới xóa được
            // Check if current user is admin of the conversation
            if (CurrentChatContact.Type != ConversationType.User)
            {
                // Check if user has ADMIN role in the conversation
                return CurrentChatContact.MemberRole == "ADMIN";
            }
        }
        
        return false;
    }
    
    protected virtual async Task DeleteMessageAsync(ChatMessageDto message)
    {
        try
        {
            await ConversationAppService.DeleteMessageAsync(new DeleteMessageInput
            {
                MessageId = message.Id,
                TargetUserId = CurrentChatContact.UserId
            });
            
            ChatConversationDto.Messages.Remove(message);
            var lastMessage = ChatConversationDto.Messages.LastOrDefault();
            CurrentChatContact.LastMessage = lastMessage?.Message;
            CurrentChatContact.LastMessageDate = lastMessage?.MessageDate;
        }
        catch (Exception ex)
        {
            await HandleErrorAsync(ex);
        }
    }
    
    protected virtual async Task DeleteConversationAsync()
    {
        try
        {
            await ConversationAppService.DeleteConversationAsync(new DeleteConversationInput
            {
                TargetUserId = CurrentChatContact.UserId
            });

            ChatConversationDto = null;
            await GetContactsAsync();
        }
        catch (Exception ex)
        {
            await HandleErrorAsync(ex);
        }
    }

    private async Task SetActiveAsync(ChatContactDto contactDto)
    {
        try
        {
            // Show loading spinner
            IsLoadingMessages = true;
            ChatConversationDto = null; // Clear previous messages
            await InvokeAsync(StateHasChanged);
            
            // Get chat settings safely
            try
            {
                await GetChatSettingsAsync();
            }
            catch
            {
                // If settings fail, continue with default behavior
            }
            
            CurrentChatContact = contactDto;
            
            // For group conversations, always allow chat (permission check not needed)
            // For direct conversations, check HasChatPermission
            var hasPermission = contactDto.Type != ConversationType.User || contactDto.HasChatPermission;
            ChatMessagesContainerStyle = !hasPermission ? "pointer-events: none; opacity: 0.5;" : "";
            
            ChatContactsActive[contactDto] = "active";
            foreach (var dto in ChatContactsActive.Where(x => x.Key != contactDto))
            {
                ChatContactsActive[dto.Key] = "";
            }

            // Update avatar/icon based on conversation type
            // For Group/Project/Task, icon will be shown in UI instead of canvas
            // Canvas update will be done after messages are loaded

            // ALL conversations now have ConversationId (User, Group, Project, Task)
            if (CurrentChatContact.ConversationId.HasValue)
            {
                // Reset pagination when switching conversations
                _messagesSkipCount = 0;
                _hasMoreMessages = true;
                
                ChatConversationDto = await ConversationAppService.GetConversationAsync(new GetConversationInput
                {
                    ConversationId = CurrentChatContact.ConversationId.Value,
                    TargetUserId = CurrentChatContact.UserId, // For backward compatibility
                    SkipCount = 0,
                    MaxResultCount = MessagesPageSize // Load 10 messages initially
                });
                CurrentConversationId = CurrentChatContact.ConversationId.Value;
                
                // Check if there are more messages
                if (ChatConversationDto?.Messages != null && ChatConversationDto.Messages.Count < MessagesPageSize)
                {
                    _hasMoreMessages = false;
                }
                else
                {
                    _messagesSkipCount = MessagesPageSize;
                }
            }
            else if (CurrentChatContact.Type == ConversationType.User && CurrentChatContact.UserId != Guid.Empty)
            {
                // Backward compatibility: User conversation without ConversationId yet
                // Use TargetUserId to find conversation, then update ConversationId
                _messagesSkipCount = 0;
                _hasMoreMessages = true;
                
                ChatConversationDto = await ConversationAppService.GetConversationAsync(new GetConversationInput
                {
                    TargetUserId = CurrentChatContact.UserId,
                    SkipCount = 0,
                    MaxResultCount = MessagesPageSize
                });
                
                // After loading, get ConversationId from contact if available
                // Or create new conversation for this user
                if (CurrentChatContact.ConversationId.HasValue)
                {
                    CurrentConversationId = CurrentChatContact.ConversationId.Value;
                }
                else
                {
                    // Need to create conversation first - for now set to null
                    // User must create conversation before sending message
                    CurrentConversationId = null;
                    _logger.LogWarning($"Chat1: User conversation without ConversationId - UserId: {CurrentChatContact.UserId}");
                }
                
                // Check if there are more messages
                if (ChatConversationDto?.Messages != null && ChatConversationDto.Messages.Count < MessagesPageSize)
                {
                    _hasMoreMessages = false;
                }
                else
                {
                    _messagesSkipCount = MessagesPageSize;
                }
            }
            else
            {
                // No conversation ID for group, create empty conversation
                ChatConversationDto = new ChatConversationDto
                {
                    Messages = new List<ChatMessageDto>()
                };
                CurrentConversationId = null;
            }
            
            // Hide loading spinner and update UI
            IsLoadingMessages = false;
            
            // Set flag to update avatar after render
            if (CurrentChatContact?.Type == ConversationType.User)
            {
                _shouldUpdateAvatar = true;
            }
            
            await InvokeAsync(StateHasChanged);

            // Reset unread count when opening conversation
            if (contactDto.UnreadMessageCount > 0)
            {
                // Reset UI immediately
                contactDto.UnreadMessageCount = 0;
                await InvokeAsync(StateHasChanged);
                
                // Call server to mark as read (for User type - TODO: implement for other types)
                if (CurrentChatContact.Type == ConversationType.User)
                {
                    await ConversationAppService.MarkConversationAsReadAsync(new MarkConversationAsReadInput
                    {
                        TargetUserId = contactDto.UserId
                    });
                }
                // TODO: Implement MarkConversationAsReadAsync for Group/Project/Task conversations
            }

            if (ChatConversationDto?.Messages != null)
            {
                ChatConversationDto.Messages.Reverse();
                var lastMessage = ChatConversationDto.Messages.LastOrDefault();
                CurrentChatContact.LastMessage = lastMessage?.Message;
                CurrentChatContact.LastMessageDate = lastMessage?.MessageDate;
            }

            Message = "";
            
            // Update UI first
            await InvokeAsync(StateHasChanged);
            
            // Scroll to bottom after messages load
            await Task.Delay(150); // Wait for DOM to update with messages
            await ScrollToBottomAsync();
        }
        catch (Exception ex)
        {
            await HandleErrorAsync(ex);
        }
    }

    private async Task OnSearchChangeAsync(string value)
    {
        SearchValue = value;
        await GetContactsAsync(!SearchValue.IsNullOrEmpty());
    }

    private async Task OnSearchKeyupAsync(KeyboardEventArgs e)
    {
        await InvokeAsync(StateHasChanged);
        await GetContactsAsync(!SearchValue.IsNullOrEmpty());
    }


    private async Task StartConversationAsync()
    {
        await OnSearchChangeAsync(" ");
    }

    private async Task OnMessageEntryAsync(KeyboardEventArgs e)
    {
        // Send on Enter if enabled and not already sending
        // Check flag first to prevent duplicate sends
        if (e.Code == "Enter" && !e.ShiftKey && SendOnEnter && !_isSendingMessage)
        {
            // Prevent default Enter behavior (new line) and send message instead
            await JsRuntime.InvokeVoidAsync("eval", "event.preventDefault()");
            await SendMessageAsync();
        }
        // If Shift+Enter, allow default behavior (new line)
        // If SendOnEnter is false, allow default Enter behavior (new line)
    }
    
    private async Task GetChatSettingsAsync()
    {
        ChatSettings = await SettingsAppService.GetAsync();
    }
    
    // New methods for expanded features
    private async Task ShowCreateDirectModalAsync()
    {
        ShowCreateDirectModal = true;
        SelectedDirectUser.Clear();
        await GetIdentityUserCollectionLookupAsync();
        await InvokeAsync(StateHasChanged);
    }
    
    private async Task CreateDirectConversationAsync()
    {
        try
        {
            if (!SelectedDirectUser.Any())
            {
                // TODO: Show warning message
                return;
            }
            
            var targetUserId = SelectedDirectUser.First().Id;
            var currentUserId = CurrentUser.Id ?? Guid.Empty;
            
            if (targetUserId == currentUserId)
            {
                // Cannot chat with yourself
                // TODO: Show warning message
                return;
            }
            
            // Check if conversation already exists in current list (now using User type instead of Direct)
            var existingContact = ChatContactDtos.FirstOrDefault(c => c.Type == ConversationType.User && c.UserId == targetUserId);
            if (existingContact != null)
            {
                // Conversation already exists, just set it active
                await SetActiveAsync(existingContact);
                ShowCreateDirectModal = false;
                SelectedDirectUser.Clear();
                return;
            }
            
            // Create new User conversation using API
            _logger.LogInformation($"Creating User conversation with target user: {targetUserId}");
            var conversation = await ConversationAppService.CreateUserConversationAsync(new CreateUserConversationInput
            {
                TargetUserId = targetUserId,
                Name = SelectedDirectUser.First().DisplayName // Optional custom name
            });
            
            _logger.LogInformation($"User conversation created: {conversation.Id}");
            
            // Refresh contacts to get the newly created conversation
            await GetContactsAsync(false);
            
            // Find and activate the new conversation
            var newContact = ChatContactDtos.FirstOrDefault(c => c.ConversationId == conversation.Id);
            if (newContact != null)
            {
                await SetActiveAsync(newContact);
            }
            
            ShowCreateDirectModal = false;
            SelectedDirectUser.Clear();
            await InvokeAsync(StateHasChanged);
        }
        catch (Exception ex)
        {
            await HandleErrorAsync(ex);
        }
    }
    
    private async Task ShowCreateGroupModalAsync()
    {
        ShowCreateGroupModal = true;
        NewGroupName = "";
        NewGroupDescription = "";
        SelectedMembers.Clear();
        await GetIdentityUserCollectionLookupAsync();
        await InvokeAsync(StateHasChanged);
    }
    
    private async Task ShowCreateProjectModalAsync()
    {
        ShowCreateProjectModal = true;
        SelectedProject.Clear();
        NewProjectName = "";
        SelectedMembers.Clear();
        await GetProjectCollectionLookupAsync();
        await GetIdentityUserCollectionLookupAsync();
        await InvokeAsync(StateHasChanged);
    }
    
    private async Task ShowCreateTaskModalAsync()
    {
        ShowCreateTaskModal = true;
        SelectedTask.Clear();
        NewTaskName = "";
        SelectedMembers.Clear();
        await GetProjectTaskCollectionLookupAsync();
        await GetIdentityUserCollectionLookupAsync();
        await InvokeAsync(StateHasChanged);
    }
    
    private async Task CreateGroupConversationAsync()
    {
        try
        {
            if (string.IsNullOrWhiteSpace(NewGroupName) || !SelectedMembers.Any())
            {
                // TODO: Show warning message
                return;
            }
            
            var memberIds = SelectedMembers.Select(m => m.Id).ToList();
            var result = await ConversationAppService.CreateGroupConversationAsync(new CreateGroupConversationInput
            {
                Name = NewGroupName,
                Description = NewGroupDescription,
                MemberUserIds = memberIds
            });
            
            ShowCreateGroupModal = false;
            NewGroupName = "";
            NewGroupDescription = "";
            SelectedMembers.Clear();
            await GetContactsAsync();
            await InvokeAsync(StateHasChanged);
        }
        catch (Exception ex)
        {
            await HandleErrorAsync(ex);
        }
    }
    
    private async Task CreateProjectConversationAsync()
    {
        try
        {
            if (!SelectedProject.Any())
            {
                // TODO: Show warning message
                return;
            }
            
            var projectId = SelectedProject.First().Id;
            var memberIds = SelectedMembers?.Select(m => m.Id).ToList() ?? new List<Guid>();
            
            var result = await ConversationAppService.CreateProjectConversationAsync(new CreateProjectConversationInput
            {
                ProjectId = projectId,
                Name = NewProjectName,
                MemberUserIds = memberIds
            });
            
            ShowCreateProjectModal = false;
            SelectedProject.Clear();
            NewProjectName = "";
            SelectedMembers.Clear();
            await GetContactsAsync();
            await InvokeAsync(StateHasChanged);
        }
        catch (Exception ex)
        {
            await HandleErrorAsync(ex);
        }
    }
    
    private async Task CreateTaskConversationAsync()
    {
        try
        {
            if (!SelectedTask.Any())
            {
                // TODO: Show warning message
                return;
            }
            
            var taskId = SelectedTask.First().Id;
            var memberIds = SelectedMembers?.Select(m => m.Id).ToList() ?? new List<Guid>();
            
            var result = await ConversationAppService.CreateTaskConversationAsync(new CreateTaskConversationInput
            {
                TaskId = taskId,
                Name = NewTaskName,
                MemberUserIds = memberIds
            });
            
            ShowCreateTaskModal = false;
            SelectedTask.Clear();
            NewTaskName = "";
            SelectedMembers.Clear();
            await GetContactsAsync();
            await InvokeAsync(StateHasChanged);
        }
        catch (Exception ex)
        {
            await HandleErrorAsync(ex);
        }
    }
    
    // Select2 lookup methods
    private async Task GetIdentityUserCollectionLookupAsync(string? newValue = null)
    {
        var allUsers = (await ((IProjectMembersAppService)ProjectMembersAppService).GetIdentityUserLookupAsync(new LookupRequestDto { Filter = newValue })).Items;
        var currentUserId = CurrentUser.Id ?? Guid.Empty;
        // Filter out current user
        IdentityUsersCollection = allUsers.Where(u => u.Id != currentUserId).ToList();
    }
    
    private async Task<List<LookupDto<Guid>>> GetIdentityUserCollectionLookupAsync(IReadOnlyList<LookupDto<Guid>> dbset, string filter, CancellationToken token)
    {
        var allUsers = (await ProjectMembersAppService.GetIdentityUserLookupAsync(new LookupRequestDto { Filter = filter })).Items;
        var currentUserId = CurrentUser.Id ?? Guid.Empty;
        // Filter out current user
        IdentityUsersCollection = allUsers.Where(u => u.Id != currentUserId).ToList();
        return IdentityUsersCollection.ToList();
    }
    
    private async Task GetProjectCollectionLookupAsync(string? newValue = null)
    {
        ProjectsCollection = (await ProjectTasksAppService.GetProjectLookupAsync(new LookupRequestDto { Filter = newValue })).Items;
    }
    
    private async Task<List<LookupDto<Guid>>> GetProjectCollectionLookupAsync(IReadOnlyList<LookupDto<Guid>> dbset, string filter, CancellationToken token)
    {
        ProjectsCollection = (await ProjectTasksAppService.GetProjectLookupAsync(new LookupRequestDto { Filter = filter })).Items;
        return ProjectsCollection.ToList();
    }
    
    private async Task GetProjectTaskCollectionLookupAsync(string? newValue = null)
    {
        var input = new GetProjectTasksInput
        {
            FilterText = newValue,
            MaxResultCount = 20,
            SkipCount = 0,
        };
        
        var result = await ProjectTasksAppService.GetListAsync(input);
        ProjectTasksCollection = result.Items
            .Select(x => new LookupDto<Guid>
            {
                Id = x.ProjectTask.Id,
                DisplayName = $"{x.ProjectTask.Code} - {x.ProjectTask.Title}"
            })
            .ToList();
    }
    
    private async Task<List<LookupDto<Guid>>> GetProjectTaskCollectionLookupAsync(IReadOnlyList<LookupDto<Guid>> dbset, string filter, CancellationToken token)
    {
        var input = new GetProjectTasksInput
        {
            FilterText = filter,
            MaxResultCount = 20,
            SkipCount = 0,
        };
        
        var result = await ProjectTasksAppService.GetListAsync(input);
        ProjectTasksCollection = result.Items
            .Select(x => new LookupDto<Guid>
            {
                Id = x.ProjectTask.Id,
                DisplayName = $"{x.ProjectTask.Code} - {x.ProjectTask.Title}"
            })
            .ToList();
        
        return ProjectTasksCollection.ToList();
    }
    
    private async Task TogglePinConversationAsync(ChatContactDto contact)
    {
        try
        {
            if (!contact.ConversationId.HasValue)
            {
                return; // Direct conversations don't support pinning via this method
            }
            
            if (contact.IsPinned)
            {
                await ConversationAppService.UnpinConversationAsync(contact.ConversationId.Value);
            }
            else
            {
                await ConversationAppService.PinConversationAsync(contact.ConversationId.Value);
            }
            
            await GetContactsAsync();
        }
        catch (Exception ex)
        {
            await HandleErrorAsync(ex);
        }
    }
    
    private async Task ReplyToMessageAsync(ChatMessageDto message)
    {
        ReplyingToMessage = message;
        await MessageTextArea.FocusAsync();
        await InvokeAsync(StateHasChanged);
    }
    
    private async Task TogglePinMessageAsync(ChatMessageDto message)
    {
        try
        {
            if (message.IsPinned)
            {
                await ConversationAppService.UnpinMessageAsync(message.Id);
                message.IsPinned = false;
            }
            else
            {
                await ConversationAppService.PinMessageAsync(message.Id);
                message.IsPinned = true;
            }
            
            // Update pin status in all messages (including reply previews)
            if (ChatConversationDto?.Messages != null)
            {
                // Update the message itself
                var msg = ChatConversationDto.Messages.FirstOrDefault(m => m.Id == message.Id);
                if (msg != null)
                {
                    msg.IsPinned = message.IsPinned;
                }
                
                // Update reply previews that reference this message
                foreach (var m in ChatConversationDto.Messages.Where(m => m.ReplyToMessage?.Id == message.Id))
                {
                    if (m.ReplyToMessage != null)
                    {
                        m.ReplyToMessage.IsPinned = message.IsPinned;
                    }
                }
            }
            
            await InvokeAsync(StateHasChanged);
        }
        catch (Exception ex)
        {
            await HandleErrorAsync(ex);
        }
    }
    
    private async Task OnFileSelected(InputFileChangeEventArgs e)
    {
        try
        {
            foreach (var file in e.GetMultipleFiles(int.MaxValue))
            {
                // Validate file size (100MB max)
                if (file.Size > 100 * 1024 * 1024)
                {
                    // TODO: Show error message
                    continue;
                }
                
                // Read file content
                using var memoryStream = new MemoryStream();
                await file.OpenReadStream(long.MaxValue).CopyToAsync(memoryStream);
                memoryStream.Position = 0;
                var fileBytes = memoryStream.ToArray();
                
                // Upload file
                var uploadedFile = await ConversationAppService.UploadFileAsync(new UploadFileInput
                {
                    FileContent = fileBytes,
                    FileName = file.Name,
                    ContentType = file.ContentType,
                    ConversationId = CurrentConversationId
                });
                
                UploadedFiles.Add(uploadedFile);
            }
            
            await InvokeAsync(StateHasChanged);
        }
        catch (Exception ex)
        {
            await HandleErrorAsync(ex);
        }
    }
    
    private async Task DownloadFileAsync(Guid fileId)
    {
        try
        {
            var file = await ConversationAppService.DownloadFileAsync(fileId);
            
            // Create download link using JavaScript
            var base64 = Convert.ToBase64String(file.Content);
            var dataUrl = $"data:{file.ContentType};base64,{base64}";
            
            // Using SafeInvokeVoidAsync to automatically handle JSDisconnectedException
            await JsRuntime.SafeInvokeVoidAsync("eval", $@"
                var link = document.createElement('a');
                link.href = '{dataUrl}';
                link.download = '{file.FileName}';
                document.body.appendChild(link);
                link.click();
                document.body.removeChild(link);
            ");
        }
        catch (Exception ex)
        {
            await HandleErrorAsync(ex);
        }
    }
    
    private async Task SendMessageAsync()
    {
        // Prevent duplicate sends
        if (_isSendingMessage)
        {
            return;
        }
        
        if (Message.IsNullOrWhiteSpace() && (UploadedFiles == null || !UploadedFiles.Any()))
        {
            return;
        }

        if (CurrentChatContact == null)
        {
            return;
        }
        
        // Set flag to prevent duplicate sends - must be set before any async operations
        _isSendingMessage = true;

        // Store message content and files before clearing
        var messageText = Message;
        var uploadedFiles = UploadedFiles?.ToList() ?? new List<MessageFileDto>();
        var replyingTo = ReplyingToMessage;
        var targetUserId = CurrentChatContact.UserId;
        var conversationId = CurrentConversationId;

        // Clear input immediately for better UX (non-blocking)
        // Clear textarea directly via JavaScript FIRST to ensure immediate clearing
        // This must be done before clearing Message property to prevent text from reappearing
        try
        {
            await JsRuntime.SafeInvokeVoidAsync("eval", 
                "const textarea = document.querySelector('textarea.form-control'); " +
                "if (textarea) { " +
                "  textarea.value = ''; " +
                "  textarea.dispatchEvent(new Event('input', { bubbles: true })); " +
                "}");
        }
        catch
        {
            // Ignore errors
        }
        
        // Now clear the property
        Message = "";
        if (ReplyingToMessage != null)
        {
            ReplyingToMessage = null;
        }
        if (UploadedFiles != null)
        {
            UploadedFiles.Clear();
        }
        
        // Force immediate UI update
        await InvokeAsync(StateHasChanged);

        // Create optimistic message and add to UI immediately
        var optimisticMessage = CreateOptimisticMessage(messageText, uploadedFiles, replyingTo);
        optimisticMessage.IsSending = true; // Mark as sending to show spinner
        if (ChatConversationDto?.Messages == null)
        {
            ChatConversationDto = new ChatConversationDto { Messages = new List<ChatMessageDto>() };
        }
        ChatConversationDto.Messages.Add(optimisticMessage);
        
        // Update UI immediately
        CurrentChatContact.LastMessage = messageText;
        CurrentChatContact.LastMessageDate = DateTime.UtcNow;
        
        // Update contact in list
        var contactInList = ChatContactDtos.FirstOrDefault(c => 
            (c.Type == ConversationType.User && c.UserId == CurrentChatContact.UserId) ||
            (c.Type != ConversationType.User && c.ConversationId == CurrentChatContact.ConversationId));
        
        if (contactInList != null)
        {
            contactInList.LastMessage = messageText;
            contactInList.LastMessageDate = DateTime.UtcNow;
        }
        
        // Increment pending count (no spinner on button, spinner is on message)
        Interlocked.Increment(ref _pendingMessagesCount);
        
        // Update UI immediately
        await InvokeAsync(StateHasChanged);
        
        // Auto scroll to bottom to show new message
        await Task.Delay(100); // Wait for DOM to update with new message
        await ScrollToBottomAsync();
        
        // Focus textarea immediately for next message
        await Task.Delay(50); // Small delay to ensure DOM is updated
        await MessageTextArea.FocusAsync();

        // Send to server in background (fire-and-forget pattern)
        _ = Task.Run(async () =>
        {
            try
            {
                ChatMessageDto serverMessage = null;
                
                if (replyingTo != null)
                {
                    // Send reply message
                    serverMessage = await ConversationAppService.SendReplyMessageAsync(new SendReplyMessageInput
                    {
                        TargetUserId = targetUserId,
                        ConversationId = conversationId,
                        ReplyToMessageId = replyingTo.Id,
                        Message = messageText ?? string.Empty
                    });
                }
                else if (uploadedFiles.Any())
                {
                    // Send message with files
                    serverMessage = await ConversationAppService.SendMessageWithFilesAsync(new SendMessageWithFilesInput
                    {
                        TargetUserId = targetUserId,
                        ConversationId = conversationId,
                        Message = messageText,
                        FileIds = uploadedFiles.Select(f => f.Id).ToList()
                    });
                }
                else
                {
                    // Send normal message
                    serverMessage = await ConversationAppService.SendMessageAsync(new SendMessageInput
                    {
                        Message = messageText,
                        ConversationId = conversationId ?? throw new InvalidOperationException("ConversationId is required")
                    });
                }

                // Update optimistic message with server response on UI thread
                await InvokeAsync(async () =>
                {
                    if (serverMessage != null && ChatConversationDto?.Messages != null)
                    {
                        // Mark server message as sent (no spinner)
                        serverMessage.IsSending = false;
                        
                        // Replace optimistic message with server message
                        var index = ChatConversationDto.Messages.FindIndex(m => m.Id == optimisticMessage.Id);
                        if (index >= 0)
                        {
                            ChatConversationDto.Messages[index] = serverMessage;
                        }
                        else
                        {
                            // If not found, add server message
                            ChatConversationDto.Messages.Add(serverMessage);
                        }
                        
                        // Update last message from server
                        var lastMessage = ChatConversationDto.Messages.LastOrDefault();
                        if (lastMessage != null)
                        {
                            CurrentChatContact.LastMessage = lastMessage.Message;
                            CurrentChatContact.LastMessageDate = lastMessage.MessageDate;
                            
                            if (contactInList != null)
                            {
                                contactInList.LastMessage = lastMessage.Message;
                                contactInList.LastMessageDate = lastMessage.MessageDate;
                            }
                        }
                        
                        // Refresh contacts list
                        await RefreshContactsListAsync();
                        
                        // Auto scroll to bottom after server message is updated
                        await Task.Delay(100); // Wait for DOM to update
                        await ScrollToBottomAsync();
                    }
                    
                    // Decrement pending count (no spinner on button, spinner is on message)
                    var remaining = Interlocked.Decrement(ref _pendingMessagesCount);
                    
                    // Reset sending flag to allow next send
                    _isSendingMessage = false;
                    
                    await InvokeAsync(StateHasChanged);
                });
            }
            catch (Exception ex)
            {
                // Handle error on UI thread
                await InvokeAsync(async () =>
                {
                    // Remove optimistic message on error
                    if (ChatConversationDto?.Messages != null)
                    {
                        ChatConversationDto.Messages.RemoveAll(m => m.Id == optimisticMessage.Id);
                    }
                    
                    // Decrement pending count (no spinner on button)
                    var remaining = Interlocked.Decrement(ref _pendingMessagesCount);
                    
                    // Reset sending flag to allow next send
                    _isSendingMessage = false;
                    
                    await InvokeAsync(StateHasChanged);
                    await HandleErrorAsync(ex);
                });
            }
        });
    }
    
    private async Task ScrollToBottomAsync()
    {
        try
        {
            await JsRuntime.SafeInvokeVoidAsync("eval", 
                "const container = document.getElementById('chat_conversation_wrapper'); " +
                "if (container) { " +
                "  container.scrollTop = container.scrollHeight; " +
                "}");
        }
        catch
        {
            // Ignore errors
        }
    }
    
    private async Task OnConversationScroll(EventArgs e)
    {
        if (_isLoadingMoreMessages || !_hasMoreMessages || ChatConversationDto?.Messages == null)
        {
            return;
        }

        try
        {
            // Check if scrolled to top (within 100px)
            var scrollTop = await JsRuntime.SafeInvokeAsync<double>("eval", 
                "document.getElementById('chat_conversation_wrapper')?.scrollTop || 0");
            
            if (scrollTop <= 100) // Near top, load more messages
            {
                await LoadMoreMessagesAsync();
            }
        }
        catch
        {
            // Ignore errors
        }
    }
    
    private async Task LoadMoreMessagesAsync()
    {
        if (_isLoadingMoreMessages || !_hasMoreMessages || CurrentChatContact == null)
        {
            return;
        }

        _isLoadingMoreMessages = true;
        try
        {
            List<ChatMessageDto> newMessages;
            
            if (CurrentChatContact.Type == ConversationType.User)
            {
                var conversation = await ConversationAppService.GetConversationAsync(new GetConversationInput
                {
                    TargetUserId = CurrentChatContact.UserId,
                    SkipCount = _messagesSkipCount,
                    MaxResultCount = MessagesPageSize
                });
                newMessages = conversation?.Messages ?? new List<ChatMessageDto>();
            }
            else if (CurrentConversationId.HasValue)
            {
                var conversation = await ConversationAppService.GetConversationAsync(new GetConversationInput
                {
                    ConversationId = CurrentConversationId.Value,
                    TargetUserId = Guid.Empty,
                    SkipCount = _messagesSkipCount,
                    MaxResultCount = MessagesPageSize
                });
                newMessages = conversation?.Messages ?? new List<ChatMessageDto>();
            }
            else
            {
                return;
            }

            if (newMessages.Any())
            {
                // Reverse to maintain chronological order (oldest first)
                newMessages.Reverse();
                
                // Insert at beginning
                ChatConversationDto.Messages.InsertRange(0, newMessages);
                
                _messagesSkipCount += newMessages.Count;
                
                // Check if there are more messages
                if (newMessages.Count < MessagesPageSize)
                {
                    _hasMoreMessages = false;
                }
                
                // Maintain scroll position
                await Task.Delay(50); // Wait for DOM update
                await JsRuntime.SafeInvokeVoidAsync("eval", 
                    "const container = document.getElementById('chat_conversation_wrapper'); " +
                    "if (container) { " +
                    "  const oldScroll = container.scrollHeight; " +
                    "  setTimeout(() => { " +
                    "    const newScroll = container.scrollHeight; " +
                    "    container.scrollTop = newScroll - oldScroll; " +
                    "  }, 10); " +
                    "}");
            }
            else
            {
                _hasMoreMessages = false;
            }
        }
        catch (Exception ex)
        {
            await HandleErrorAsync(ex);
        }
        finally
        {
            _isLoadingMoreMessages = false;
            await InvokeAsync(StateHasChanged);
        }
    }
    
    private async Task LoadMoreConversationsAsync()
    {
        if (_isLoadingMoreConversations || !_hasMoreConversations)
        {
            return;
        }

        _isLoadingMoreConversations = true;
        try
        {
            await GetContactsAsync(includeOtherContacts: false, preserveCurrentContact: true, loadMore: true);
        }
        catch (Exception ex)
        {
            await HandleErrorAsync(ex);
        }
        finally
        {
            _isLoadingMoreConversations = false;
            await InvokeAsync(StateHasChanged);
        }
    }


    private ChatMessageDto CreateOptimisticMessage(string messageText, List<MessageFileDto> files, ChatMessageDto replyingTo)
    {
        var currentUserId = CurrentUser.Id ?? Guid.Empty;
        var now = DateTime.UtcNow;
        
        return new ChatMessageDto
        {
            Id = Guid.NewGuid(), // Temporary ID
            Message = messageText,
            MessageDate = now,
            Side = ChatMessageSide.Sender,
            IsRead = false,
            ReadDate = default(DateTime),
            ReplyToMessageId = replyingTo?.Id,
            ReplyToMessage = replyingTo != null ? new ChatMessageDto
            {
                Id = replyingTo.Id,
                Message = replyingTo.Message,
                MessageDate = replyingTo.MessageDate,
                Side = replyingTo.Side
            } : null,
            Files = files?.Select(f => new MessageFileDto
            {
                Id = f.Id,
                MessageId = f.MessageId,
                FileName = f.FileName,
                ContentType = f.ContentType,
                FileSize = f.FileSize,
                FileExtension = f.FileExtension,
                DownloadUrl = f.DownloadUrl,
                CreationTime = f.CreationTime
            }).ToList() ?? new List<MessageFileDto>(),
            // Sender info for group chats
            SenderUserId = currentUserId,
            SenderName = CurrentUser.Name,
            SenderSurname = CurrentUser.SurName,
            SenderUsername = CurrentUser.UserName
        };
    }

    public async ValueTask DisposeAsync()
    {
        // Cleanup SignalR connection when component is disposed
        try
        {
            await JSRuntime.InvokeVoidAsync("chatHub.stop");
        }
        catch (Exception ex)
        {
            _logger.LogInformation($"Chat1: Error stopping SignalR: {ex.Message}");
        }

        // Dispose object reference
        _objRef?.Dispose();
        _objRef = null;

        // Cleanup ChatHubConnectionService if needed
        if (ChatHubConnectionService is IAsyncDisposable asyncDisposable)
        {
            await asyncDisposable.DisposeAsync();
        }
    }
}
