using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HC.Blazor.Components.Chat;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using HC.Chat.Authorization;
using HC.Chat.Conversations;
using HC.Chat.Messages;
using HC.Chat.Settings;
using HC.Chat.Users;
using HC.Projects;
using HC.ProjectTasks;
using HC.ProjectMembers;
using HC.ProjectTaskAssignments;
using HC.Shared;
using HC.Blazor.Extensions;
using Microsoft.Extensions.Logging;
using Volo.Abp.Http.Client;
using HC.Blazor.Pages.Chat1.Handlers;
using Volo.Abp.AspNetCore.Components.Messages;
using HC.Blazor.BlobStoring;

namespace HC.Blazor.Pages.Chat1;

public partial class Chat1 : HCComponentBase, IAsyncDisposable
{

    [Parameter]
    public Guid? RedirectToConversationId { get; set; }


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

    public bool SendOnEnter { get; set; } = true; // Default: Enter to send
    
    public bool ShowInfoBox { get; set; } = false;

    // Mobile view state management
    public enum MobileViewType
    {
        ConversationList,
        ChatConversation,
        ConversationInfo
    }

    public MobileViewType CurrentMobileView { get; set; } = MobileViewType.ConversationList;
    public bool IsMobileMode { get; set; } = false;


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
    private bool _isJumpingToMessage = false;
    
    // Pagination for conversations
    private int _conversationsSkipCount = 0;
    private const int ConversationsPageSize = 10;
    private bool _isLoadingMoreConversations = false;
    private bool _hasMoreConversations = true;
    private bool _hasActivatedRouteConversation;
    
    // Flag to update avatar after render
    private bool _shouldUpdateAvatar = false;
    
    // Track processed message IDs to prevent duplicate UnreadMessageCount increments
    private HashSet<Guid> _processedMessageIds = new HashSet<Guid>();
    private const int MaxProcessedIdsCacheSize = 200; // Cleanup when cache gets too large

    private CancellationTokenSource? _refreshContactsDebounceCts;
    
    // New properties for expanded features
    public ChatMessageDto ReplyingToMessage { get; set; }
    
    public List<MessageFileDto> UploadedFiles { get; set; } = new List<MessageFileDto>();
    
    public Guid? CurrentConversationId { get; set; }
    
    // Modal states
    public bool ShowCreateDirectModal { get; set; }
    public bool ShowDirectUserRequiredMessage { get; set; }
    public bool ShowCreateGroupModal { get; set; }
    public bool ShowGroupNameRequiredMessage { get; set; }
    public bool ShowGroupMembersRequiredMessage { get; set; }
    public bool IsCreatingGroupConversation { get; set; }
    
    // Image Viewer Modal
    public bool ShowImageViewerModal { get; set; }
    public string ImageViewerUrl { get; set; } = string.Empty;
    public string ImageViewerFilePath { get; set; } = string.Empty;
    public string ImageViewerFileName { get; set; } = string.Empty;
    private Guid _currentViewingImageFileId { get; set; }
    
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
    protected IProjectTaskAssignmentsAppService ProjectTaskAssignmentsAppService { get; set; } = default!;

    [Inject]
    protected IChatHubConnectionService ChatHubConnectionService { get; set; }

    [Inject]
    protected ILogger<Chat1> _logger { get; set; }

    [Inject]
    protected IJSRuntime JSRuntime { get; set; }

    [Inject]
    private IBlobDisplayUrlProvider BlobDisplayUrlProvider { get; set; } = default!;

    // === NEW: Handler Factory for Refactored Code ===
    [Inject]
    private Pages.Chat1.Handlers.IChatHandlerFactory HandlerFactory { get; set; }

    private IChatPaginationHandler _paginationHandler;
    private ChatState _state = new ChatState();
    private PaginationState _pagination = new PaginationState();
    
    private IChatMessageHandler _messageHandler;
    private IChatFileHandler _fileHandler;
    private IChatOptimizationHandler _optimizationHandler;
    [JSInvokable]
    public async Task HandleSignalRMessage(object messageData)
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
                IsCrossTabMessage = false
            };

            await ProcessReceivedMessage(message);
        }
        catch (Exception ex)
        {
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
            _logger.LogError(ex, "Error processing cross-tab message");
        }
    }

    private async Task ProcessReceivedMessage(ChatMessageRdto message)
    {
        try
        {
            if (CurrentUser == null)
            {
                return;
            }

#if DEBUG
            _logger.LogInformation($"Chat1: DEBUG - Message details: Id={message.Id}, SenderUserId={message.SenderUserId}, SenderUsername={message.SenderUsername}, Text={message.Text}, ConversationId={message.ConversationId}");
            _logger.LogInformation($"Chat1: DEBUG - Current user details: CurrentUser.Id={CurrentUser.Id}, CurrentUser.UserName={CurrentUser.UserName}");
            _logger.LogInformation($"Chat1: DEBUG - CurrentChatContact: {(CurrentChatContact != null ? $"Type={CurrentChatContact.Type}, UserId={CurrentChatContact.UserId}, ConversationId={CurrentConversationId}" : "NULL")}");
#endif

            // Skip messages from current user in same tab (avoid duplicate)
            if (message.SenderUserId == CurrentUser.Id && !message.IsCrossTabMessage)
            {
                return;
            }

            // Check if this message was already processed (prevent duplicate UnreadMessageCount increments)
            bool isFirstProcessing = false;
            if (!_processedMessageIds.Contains(message.Id))
            {
                lock (_processedMessageIds)
                {
                    if (!_processedMessageIds.Contains(message.Id))
                    {
                        _processedMessageIds.Add(message.Id);
                        isFirstProcessing = true;
                        
                        // Cleanup cache if it gets too large
                        if (_processedMessageIds.Count > MaxProcessedIdsCacheSize)
                        {
                            var oldIds = _processedMessageIds.Take(MaxProcessedIdsCacheSize / 2).ToList();
                            foreach (var oldId in oldIds)
                            {
                                _processedMessageIds.Remove(oldId);
                            }
                        }
                    }
                }
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
                    }
                    else
                    {
                        // Fallback: For old User conversations without ConversationId, check based on sender
                        bool isFromOtherUser = CurrentChatContact.UserId == message.SenderUserId;
                        bool isFromCurrentUser = message.SenderUserId == CurrentUser.Id;
                        isForCurrentConversation = isFromOtherUser || isFromCurrentUser;
                    }
                }
                else if (CurrentChatContact.Type != ConversationType.User && CurrentConversationId.HasValue)
                {
                    // For group conversations: check if message belongs to current conversation
                    isForCurrentConversation = message.ConversationId.HasValue &&
                                             message.ConversationId.Value == CurrentConversationId.Value;
                }
            }
            else
            {
                // No current conversation selected - always update badge
                isForCurrentConversation = false;
            }

            if (isForCurrentConversation)
            {
                // OPTIMIZATION: If message is for the currently active conversation,
                // reset the unread count to avoid showing badge on active conversation
                if (isFirstProcessing && CurrentChatContact != null && CurrentChatContact.UnreadMessageCount > 0 && CurrentChatContact.ConversationId.HasValue)
                {
                    try
                    {
                        await ConversationAppService.ResetUnreadCountAsync(new ResetUnreadCountInput
                        {
                            ConversationId = CurrentChatContact.ConversationId.Value
                        });
                        CurrentChatContact.UnreadMessageCount = 0;

                        // Broadcast to update notification icon (since unread count decreased)
                        try
                        {
                            await JSRuntime.InvokeVoidAsync("chatHub.broadcastUnreadCountChanged");
                        }
                        catch (Exception ex2)
                        {
                            _logger.LogWarning(ex2, "Failed to broadcast unread count changed event");
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to reset unread count for active conversation {ConversationId}", CurrentChatContact.ConversationId);
                    }
                }

                // Refresh conversation
                if (CurrentChatContact != null && CurrentChatContact.Type == ConversationType.User)
                {
                    ChatConversationDto = await ConversationAppService.GetConversationAsync(
                        new GetConversationInput { TargetUserId = CurrentChatContact.UserId, MaxResultCount = 100 });
                }
                else if (CurrentChatContact != null && CurrentChatContact.ConversationId.HasValue)
                {
                    ChatConversationDto = await ConversationAppService.GetConversationAsync(
                        new GetConversationInput { ConversationId = CurrentChatContact.ConversationId.Value, TargetUserId = Guid.Empty, MaxResultCount = 100 });
                }

                if (CurrentChatContact != null && CurrentChatContact.UnreadMessageCount > 0 && CurrentChatContact.Type == ConversationType.User)
                {
                    await ConversationAppService.MarkConversationAsReadAsync(
                        new MarkConversationAsReadInput { TargetUserId = CurrentChatContact.UserId });
                }

                if (ChatConversationDto != null)
                {
                    ChatConversationDto.Messages.Reverse();
                    var lastMessage = ChatConversationDto.Messages.LastOrDefault();
                    if (CurrentChatContact != null)
                    {
                        CurrentChatContact.LastMessage = lastMessage?.Message;
                    }
                    if (CurrentChatContact != null)
                    {
                        CurrentChatContact.LastMessageDate = lastMessage?.MessageDate;
                    }
                }

                // Auto scroll to bottom when receiving new message
                await InvokeAsync(async () =>
                {
                    await InvokeAsync(StateHasChanged);
                    await Task.Delay(100); // Wait for DOM to update
                    await ScrollToBottomAsync();
                });
            }
            else
            {
                // Find the conversation in the list and update unread count + last message
                ChatContactDto targetContact = null;
                
                // For User (1-1) conversation: find by SenderUserId
                if (ChatContactDtos != null && message.ConversationId.HasValue)
                {
                    // Try to find by ConversationId first (works for all types)
                    targetContact = ChatContactDtos.FirstOrDefault(c => c != null && 
                        c.ConversationId.HasValue && c.ConversationId.Value == message.ConversationId.Value);
                }
                
                // Fallback: For User type, find by UserId (sender)
                if (targetContact == null && message.SenderUserId != CurrentUser.Id)
                {
                    targetContact = ChatContactDtos.FirstOrDefault(c => c != null && 
                        c.Type == ConversationType.User && c.UserId == message.SenderUserId);
                }
                
                if (targetContact != null)
                {
                    // Update unread count (only if message is from someone else AND this is first processing)
                    if (message.SenderUserId != CurrentUser.Id && isFirstProcessing)
                    {
                        targetContact.UnreadMessageCount++;
                    }
                    
                    // Update last message info
                    targetContact.LastMessage = message.Text;
                    targetContact.LastMessageDate = DateTime.Now;
                    
                    if (ChatContactDtos != null)
                    {
                        ChatContactDtos.Remove(targetContact);
                    }
                    ChatContactDtos = ChatContactDtos == null ? new List<ChatContactDto>() : ChatContactDtos;
                    var pinnedCount = ChatContactDtos.Count(c => c.IsPinned);
                    ChatContactDtos.Insert(pinnedCount, targetContact);
                    
                    // Update active dictionary
                    if (ChatContactsActive.ContainsKey(targetContact))
                    {
                        var activeState = ChatContactsActive[targetContact];
                        ChatContactsActive.Remove(targetContact);
                        ChatContactsActive[targetContact] = activeState;
                    }
                    
                    await InvokeAsync(StateHasChanged);
                }
                else
                {
                    _logger.LogWarning("Could not find conversation in list to update unread badge");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing received message");
        }
    }

    private static Guid GetGuidProperty(System.Text.Json.JsonElement el, string pascalName, string camelName)
    {
        if (el.TryGetProperty(pascalName, out var p)) return p.GetGuid();
        if (el.TryGetProperty(camelName, out var c)) return c.GetGuid();
        throw new KeyNotFoundException($"Property '{pascalName}'/'{camelName}' not found");
    }

    private static string? GetStringPropertyOrNull(System.Text.Json.JsonElement el, string pascalName, string camelName)
    {
        if (el.TryGetProperty(pascalName, out var p)) return p.GetString();
        if (el.TryGetProperty(camelName, out var c)) return c.GetString();
        return null;
    }

    private async Task ProcessConversationCreated(object conversationData)
    {
        try
        {
            if (CurrentUser == null)
            {
                return;
            }

            var jsonElement = (System.Text.Json.JsonElement)conversationData;
            var conversationId = GetGuidProperty(jsonElement, "ConversationId", "conversationId");
            var conversationType = GetStringPropertyOrNull(jsonElement, "Type", "type");
            var conversationName = GetStringPropertyOrNull(jsonElement, "ConversationName", "conversationName");

            // Refresh the conversation list to show the new conversation
            await GetContactsAsync(includeOtherContacts: false, preserveCurrentContact: true, loadMore: false);
            
            // Find the newly created conversation in the list
            var newConversation = ChatContactDtos.FirstOrDefault(c => c.ConversationId == conversationId);
            if (newConversation != null)
            {
                ChatContactDtos.Remove(newConversation);
                var insertIndex = ChatContactDtos.Count(c => c.IsPinned);
                ChatContactDtos.Insert(insertIndex, newConversation);

                await InvokeAsync(StateHasChanged);
            }
            else
            {
                _logger.LogWarning($"Could not find new conversation in list - ConversationId: {conversationId}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing conversation created event");
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
            await ChatHubConnectionService.OnReceiveMessageAsync(async message =>
            {
                await ProcessReceivedMessage(message);
            });

            await ChatHubConnectionService.OnDeletedMessageAsync(async messageId =>
            {
                ChatConversationDto.Messages.RemoveAll(message => message.Id == messageId);
                var lastMessage = ChatConversationDto.Messages.LastOrDefault();
                CurrentChatContact.LastMessage = lastMessage?.Message;
                CurrentChatContact.LastMessageDate = lastMessage?.MessageDate;
                await InvokeAsync(StateHasChanged);
            });
            
            await ChatHubConnectionService.OnDeletedConversationAsync(async deletedConversation =>
            {
                var deletedContacts = ChatContactDtos
                    .Where(contact =>
                        deletedConversation.ConversationId.HasValue
                            ? contact.ConversationId == deletedConversation.ConversationId
                            : contact.Type == ConversationType.User && contact.UserId == deletedConversation.UserId)
                    .ToList();

                var isDeletingCurrentConversation = CurrentChatContact != null &&
                    (deletedConversation.ConversationId.HasValue
                        ? CurrentChatContact.ConversationId == deletedConversation.ConversationId
                        : CurrentChatContact.Type == ConversationType.User && CurrentChatContact.UserId == deletedConversation.UserId);

                foreach (var deletedContact in deletedContacts)
                {
                    ChatContactsActive.Remove(deletedContact);
                    CanvasElementReferences.Remove(deletedContact);
                }

                if (deletedContacts.Any())
                {
                    ChatContactDtos.RemoveAll(contact => deletedContacts.Contains(contact));
                }

                if (isDeletingCurrentConversation)
                {
                    CanvasElementReferences.Clear();
                    ChatConversationDto = null;
                    CurrentChatContact = null;
                    CurrentConversationId = null;
                    ShowInfoBox = false;
                    SyncConversationUrl(null);
                }

                await InvokeAsync(StateHasChanged);
            });

            await ChatHubConnectionService.OnConversationCreatedAsync(async conversationData =>
            {
                await ProcessConversationCreated(conversationData);
            });

            await ChatHubConnectionService.OnChatUnreadCountChangedAsync(async () =>
            {
                try
                {
                    _refreshContactsDebounceCts?.Cancel();
                }
                catch (ObjectDisposedException)
                {
                    // ignore
                }

                _refreshContactsDebounceCts?.Dispose();
                _refreshContactsDebounceCts = new CancellationTokenSource();
                var token = _refreshContactsDebounceCts.Token;
                try
                {
                    await Task.Delay(350, token);
                }
                catch (OperationCanceledException)
                {
                    return;
                }

                if (token.IsCancellationRequested)
                {
                    return;
                }

                await RefreshContactsListAsync();
                await InvokeAsync(StateHasChanged);
            });

            // Initialize SignalR with service reference
            await ChatHubConnectionService.InitializeAsync("/chatHub", string.Empty);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize chat SignalR connection");
        }

        // === NEW: Initialize ChatState and Handlers ===
        try
        {
            // Set up state change notification for UI updates
            _state.OnChange = async () =>
            {
                await InvokeAsync(StateHasChanged);
            };

            // Initialize state with existing data (keep in sync)
            _state.CurrentConversation = ChatConversationDto;
            _state.CurrentConversationId = CurrentConversationId;
            _state.MessageText = Message;
            _state.MessageTextArea = MessageTextArea;
            _state.SendOnEnter = SendOnEnter;
            _state.ShowInfoBox = ShowInfoBox;
            _state.SearchValue = SearchValue;
            _state.ReplyingToMessage = ReplyingToMessage;
            _state.UploadedFiles = UploadedFiles;
            
            // Convert ICurrentUser to CurrentUserDto
            _state.CurrentUser = new CurrentUserDto
            {
                Id = CurrentUser.Id,
                UserName = CurrentUser.UserName,
                Name = CurrentUser.Name,
                SurName = CurrentUser.SurName
            };

            // Create handlers using factory
            _messageHandler = HandlerFactory.CreateMessageHandler(_state);
            _fileHandler = HandlerFactory.CreateFileHandler(_state);
            _paginationHandler = HandlerFactory.CreatePaginationHandler(_state, _pagination);
            _optimizationHandler = HandlerFactory.CreateOptimizationHandler(_state);

            _logger.LogInformation("Chat handlers initialized successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize chat handlers");
        }
    }

    protected async override Task OnAfterRenderAsync(bool firstRender)
    {
        if(firstRender)
        {
            // Initialize mobile detection using IJSRuntime
            try
            {
                // Check initial screen size
                var isMobile = await JsRuntime.InvokeAsync<bool>("eval", "window.innerWidth < 768");
                _logger.LogInformation($"Initial mobile detection: {isMobile}");
                SetMobileMode(isMobile);

                // Set up resize listener
                await JsRuntime.InvokeVoidAsync("eval", @"
                    window.addEventListener('resize', function() {
                        const isMobile = window.innerWidth < 768;
                        // Store in a global variable for polling
                        window._isMobile = isMobile;
                    });
                ");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize mobile detection");
                // Fallback
                IsMobileMode = true;
                CurrentMobileView = MobileViewType.ConversationList;
            }

            await BlockUiService.Block(selectors: "#chat_wrapper", busy: true);
            await GetContactsAsync(isSetActive: false);

            if (RedirectToConversationId.HasValue)
            {
                // Load more until conversation is in list (so user can see it), but do NOT auto-select
                while (!ChatContactDtos.Any(c => c.ConversationId == RedirectToConversationId.Value) && _hasMoreConversations)
                {
                    await GetContactsAsync(loadMore: true, isSetActive: false);
                    await Task.Delay(100);
                }
            }
            await BlockUiService.UnBlock();

            if (RedirectToConversationId.HasValue && !_hasActivatedRouteConversation)
            {
                _hasActivatedRouteConversation = true;
                await ActivateConversationFromRouteAsync(RedirectToConversationId.Value);
            }
        }

        // Create snapshot to avoid "Collection was modified" errors
        var contactsSnapshot = ChatContactDtos.ToList();
        var userContacts = contactsSnapshot.Where(c => c.Type == ConversationType.User).ToList();
        if (userContacts.Any())
        {
            await Task.Delay(50);
            foreach (var contactDto in userContacts)
            {
                var canvasId = $"contact-avatar-{contactDto.UserId}";
                try
                {
                    await JsRuntime.SafeInvokeVoidAsync("VoloChatAvatarManager.createCanvasForUserById", canvasId, contactDto.Username, GetName(contactDto));
                }
                catch (JSException)
                {
                    // Canvas can be temporarily absent during rerender/search transitions.
                }
            }
        }
        
        // Update avatar for current chat contact after canvas is rendered in DOM
        if (_shouldUpdateAvatar && CurrentChatContact?.Type == ConversationType.User)
        {
            _shouldUpdateAvatar = false;
            try
            {
                var currentContact = CurrentChatContact;
                if (currentContact != null)
                {
                    // Only call if canvas reference is valid (check if it has been rendered)
                    // Wait a bit more to ensure canvas is in DOM
                    await Task.Delay(100);
                    if (CurrentChatContactCanvas.Id != null)
                    {
                        await JsRuntime.SafeInvokeVoidAsync("VoloChatAvatarManager.createCanvasForUser", CurrentChatContactCanvas, currentContact.Username, GetName(currentContact));
                    }
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
            var currentConversationMessages = ChatConversationDto?.Messages;
            if (currentConversationMessages != null)
            {
                // Create snapshot to avoid "Collection was modified" errors
                var messagesSnapshot = currentConversationMessages
                    .Where(m => m != null && m.SenderUserId.HasValue && m.Side == ChatMessageSide.Receiver)
                    .ToList();
                foreach (var message in messagesSnapshot)
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
        }

    }

    public static string GetName(ChatContactDto contact)
    {
        string name = "";

        if (!string.IsNullOrEmpty(contact.Surname))
        {
            name += contact.Surname +  ' ';
        }

        if (!string.IsNullOrEmpty(contact.Name))
        {
            name += contact.Name;
        }
        
        if (name == string.Empty)
        {
            name = contact?.Username ?? "unknown user";
        }

        return name;
    }
    
    /// <summary>
    /// Get a snapshot of messages to avoid "Collection was modified" errors during rendering
    /// </summary>
    public List<ChatMessageDto> GetMessagesSnapshot()
    {
        return ChatConversationDto?.Messages?.ToList() ?? new List<ChatMessageDto>();
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

    public async Task GetContactsAsync(bool includeOtherContacts = false,
     bool preserveCurrentContact = false, 
     bool loadMore = false,
     bool isSetActive = true)
    {
        try
        {
            if (!loadMore)
            {
                _conversationsSkipCount = 0;
                _hasMoreConversations = true;
                CanvasElementReferences.Clear();
            }
            
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
                ChatContactDtos.AddRange(newContacts);
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
                ChatContactDtos = newContacts;
                
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

            if (!isSetActive && CurrentChatContact != null)
            {
                var currentContactInList = ChatContactDtos.FirstOrDefault(c =>
                    (CurrentChatContact.Type == ConversationType.User && c.Type == ConversationType.User && c.UserId == CurrentChatContact.UserId) ||
                    (CurrentChatContact.Type != ConversationType.User && c.Type != ConversationType.User && c.ConversationId == CurrentChatContact.ConversationId));

                if (currentContactInList != null && ChatContactsActive.ContainsKey(currentContactInList))
                {
                    ChatContactsActive[currentContactInList] = "active";
                }
            }

            if(isSetActive)
            {
                if (preserveCurrentContact && currentContactId.HasValue)
                {
                    CurrentChatContact = ChatContactDtos.FirstOrDefault(c => 
                        (c.Type == ConversationType.User && c.UserId == currentContactId.Value) ||
                        (c.Type != ConversationType.User && c.ConversationId == currentContactId.Value));
                }
                else
                {
                    // Do NOT SetActiveAsync - user must click to select
                    CurrentChatContact = null;
                    ChatConversationDto = null;
                }
            }
            
            await InvokeAsync(StateHasChanged);
        }
        catch (AbpRemoteCallException ex)
        {
            _logger.LogError(ex, "API error when getting contacts");
            // Don't show error to user on first load - it might be a temporary issue
            // Just return empty list
            ChatContactDtos = new List<ChatContactDto>();
            await InvokeAsync(StateHasChanged);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error when getting contacts");
            await HandleErrorAsync(ex);
        }
    }
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

        if (CurrentChatContact?.Type != ConversationType.User)
        {
            var currentUserId = CurrentUser.Id;
            var isOwnMessage = (currentUserId.HasValue && message.SenderUserId.HasValue && message.SenderUserId.Value == currentUserId.Value)
                || message.Side == ChatMessageSide.Sender;
            var isAdmin = string.Equals(CurrentChatContact.MemberRole, "ADMIN", StringComparison.OrdinalIgnoreCase);

            if (!isOwnMessage && !isAdmin)
            {
                return false;
            }
        }

        return true;
    }
    
    protected virtual bool IsDeletingConversationEnabled()
    {
        return IsDeletingConversationEnabled(CurrentChatContact);
    }

    protected virtual bool IsDeletingConversationEnabled(ChatContactDto contact)
    {
        if (ChatSettings.DeletingMessages != ChatDeletingMessages.Enabled)
        {
            return false;
        }
        
        if (ChatSettings.DeletingConversations == ChatDeletingConversations.Disabled)
        {
            return false;
        }
        
        if (contact == null)
        {
            return false;
        }

        if (contact.Type == ConversationType.User)
        {
            return true;
        }
        
        return contact.MemberRole == "ADMIN";
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
    
    protected virtual async Task DeleteConversationAsync(ChatContactDto contactToDelete)
    {
        try
        {
            if (contactToDelete == null) return;

            var isDeletingCurrentConversation = CurrentChatContact != null &&
                                               ((contactToDelete.Type == ConversationType.User &&
                                                 CurrentChatContact.Type == ConversationType.User &&
                                                 CurrentChatContact.UserId == contactToDelete.UserId) ||
                                                (contactToDelete.Type != ConversationType.User &&
                                                 CurrentChatContact.Type != ConversationType.User &&
                                                 CurrentChatContact.ConversationId.HasValue &&
                                                 contactToDelete.ConversationId.HasValue &&
                                                 CurrentChatContact.ConversationId.Value == contactToDelete.ConversationId.Value));

            await BlockUiService.Block(selectors: "#chat_wrapper", busy: true);
            try
            {
                if (contactToDelete.Type == ConversationType.User)
                {
                    // Explicitly set ConversationId = null so backend uses 1-1 delete path, not group path.
                    // User ChatContactDto has both UserId and ConversationId; backend treats ConversationId.HasValue as group.
                    await ConversationAppService.DeleteConversationAsync(new DeleteConversationInput
                    {
                        TargetUserId = contactToDelete.UserId,
                        ConversationId = null
                    });
                }
                else
                {
                    await ConversationAppService.DeleteConversationAsync(new DeleteConversationInput
                    {
                        ConversationId = contactToDelete.ConversationId
                    });
                }

                if (isDeletingCurrentConversation)
                {
                    ChatConversationDto = null;
                    CurrentChatContact = null;
                    CurrentConversationId = null;
                    ShowInfoBox = false;
                }

                await GetContactsAsync(isSetActive: false);
            }
            finally
            {
                await BlockUiService.UnBlock();
            }
        }
        catch (Exception ex)
        {
            try { await BlockUiService.UnBlock(); }
            catch (Exception unblockEx) { Logger.LogDebug(unblockEx, "BlockUiService.UnBlock failed (ignored)."); }
            await HandleErrorAsync(ex);
        }
    }

    private async Task SetActiveAsync(ChatContactDto contactDto)
    {
        try
        {
            await BlockUiService.Block(selectors: "#chat_wrapper", busy: true);
            // OPTIMIZATION: If clicking the same conversation that's already active, skip reload
            bool isSameConversation = CurrentChatContact != null &&
                                     CurrentChatContact.ConversationId.HasValue &&
                                     contactDto.ConversationId.HasValue &&
                                     CurrentChatContact.ConversationId.Value == contactDto.ConversationId.Value;

            if (isSameConversation)
            {
                _logger.LogDebug("Conversation {ConversationId} is already active, skipping reload", contactDto.ConversationId);

                // Still need to reset unread count and update notification icon if there are unread messages
                if (contactDto.UnreadMessageCount > 0 && contactDto.ConversationId.HasValue)
                {
                    try
                    {
                        await ConversationAppService.ResetUnreadCountAsync(new ResetUnreadCountInput
                        {
                            ConversationId = contactDto.ConversationId.Value
                        });
                        contactDto.UnreadMessageCount = 0;

                        // Update notification icon
                        try
                        {
                            await JSRuntime.InvokeVoidAsync("chatHub.broadcastUnreadCountChanged");
                        }
                        catch (Exception ex2)
                        {
                            _logger.LogWarning(ex2, "Failed to broadcast unread count changed event");
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to reset unread count for conversation {ConversationId}", contactDto.ConversationId);
                    }
                }

                // Update active state styling
                ChatContactsActive[contactDto] = "active";
                foreach (var dto in ChatContactsActive.Where(x => x.Key != contactDto))
                {
                    ChatContactsActive[dto.Key] = "";
                }
                await InvokeAsync(StateHasChanged);

                return; // Skip full reload
            }

            ShowInfoBox = false;

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

            // Reset unread count when opening a conversation
            if (contactDto.UnreadMessageCount > 0 && contactDto.ConversationId.HasValue)
            {
                try
                {
                    await ConversationAppService.ResetUnreadCountAsync(new ResetUnreadCountInput
                    {
                        ConversationId = contactDto.ConversationId.Value
                    });
                    contactDto.UnreadMessageCount = 0;

                    // Broadcast unread count changed to update notification icon
                    try
                    {
                        await JSRuntime.InvokeVoidAsync("chatHub.broadcastUnreadCountChanged");
                    }
                    catch (Exception ex2)
                    {
                        _logger.LogWarning(ex2, "Failed to broadcast unread count changed event");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to reset unread count for conversation {ConversationId}", contactDto.ConversationId);
                }
            }

            ChatContactsActive[contactDto] = "active";
            foreach (var dto in ChatContactsActive.Where(x => x.Key != contactDto))
            {
                ChatContactsActive[dto.Key] = "";
            }
            if (CurrentChatContact.ConversationId.HasValue)
            {
                var conversationId = CurrentChatContact.ConversationId.Value;
                _messagesSkipCount = 0;
                _hasMoreMessages = true;
                
                ChatConversationDto = await ConversationAppService.GetConversationAsync(new GetConversationInput
                {
                    ConversationId = conversationId,
                    TargetUserId = CurrentChatContact.UserId,
                    SkipCount = 0,
                    MaxResultCount = MessagesPageSize
                });
                CurrentConversationId = conversationId;
                SyncConversationUrl(CurrentConversationId);
                
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
                _messagesSkipCount = 0;
                _hasMoreMessages = true;
                
                ChatConversationDto = await ConversationAppService.GetConversationAsync(new GetConversationInput
                {
                    TargetUserId = CurrentChatContact.UserId,
                    SkipCount = 0,
                    MaxResultCount = MessagesPageSize
                });
                
                if (CurrentChatContact.ConversationId.HasValue)
                {
                    CurrentConversationId = CurrentChatContact.ConversationId.Value;
                    SyncConversationUrl(CurrentConversationId);
                }
                else
                {
                    CurrentConversationId = null;
                    _logger.LogWarning($"Chat1: User conversation without ConversationId - UserId: {CurrentChatContact.UserId}");
                }
                
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
                ChatConversationDto = new ChatConversationDto
                {
                    Messages = new List<ChatMessageDto>()
                };
                CurrentConversationId = null;
                SyncConversationUrl(null);
            }
            
            IsLoadingMessages = false;

            if (CurrentChatContact?.Type == ConversationType.User)
            {
                _shouldUpdateAvatar = true;
            }

            // Mobile: switch to chat conversation view
            if (IsMobileMode)
            {
                await ShowChatConversationAsync();
            }

            await InvokeAsync(StateHasChanged);

            if (contactDto.UnreadMessageCount > 0)
            {
                contactDto.UnreadMessageCount = 0;
                await InvokeAsync(StateHasChanged);
                
                if (CurrentChatContact.Type == ConversationType.User)
                {
                    await ConversationAppService.MarkConversationAsReadAsync(new MarkConversationAsReadInput
                    {
                        TargetUserId = contactDto.UserId
                    });
                }
            }

            if (ChatConversationDto?.Messages != null)
            {
                ChatConversationDto.Messages.Reverse();
                var lastMessage = ChatConversationDto.Messages.LastOrDefault();
                CurrentChatContact.LastMessage = lastMessage?.Message;
                CurrentChatContact.LastMessageDate = lastMessage?.MessageDate;
            }

            Message = "";
            ReplyingToMessage = null;
            UploadedFiles?.Clear();
            
            await InvokeAsync(StateHasChanged);
            
            await Task.Delay(150);
            await ScrollToBottomAsync();
        }
        catch (Exception ex)
        {
            await HandleErrorAsync(ex);
        }
        finally
        {
            try
            {
                await BlockUiService.UnBlock();
            }
            catch (JSDisconnectedException)
            {
                // Ignore when circuit disconnects while leaving this method.
            }
            catch (TaskCanceledException)
            {
                // Ignore canceled JS operation during component disposal.
            }
        }
    }

    private async Task OnSearchChangeAsync(string value)
    {
        SearchValue = value;
        await GetContactsAsync(
            includeOtherContacts: false,
            preserveCurrentContact: true,
            loadMore: false,
            isSetActive: false);
    }

    private async Task OnSearchKeyupAsync(KeyboardEventArgs e)
    {
        await InvokeAsync(StateHasChanged);
        await GetContactsAsync(
            includeOtherContacts: false,
            preserveCurrentContact: true,
            loadMore: false,
            isSetActive: false);
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
    }
    
    private async Task GetChatSettingsAsync()
    {
        ChatSettings = await SettingsAppService.GetAsync();
    }
    
    // New methods for expanded features
    private async Task ShowCreateDirectModalAsync()
    {
        ShowCreateDirectModal = true;
        ShowDirectUserRequiredMessage = false;
        SelectedDirectUser.Clear();
        await InvokeAsync(StateHasChanged);
    }

    private void OnSelectedDirectUserChanged(List<LookupDto<Guid>>? value)
    {
        SelectedDirectUser = value ?? new List<LookupDto<Guid>>();
        ShowDirectUserRequiredMessage = false;
    }
    private async Task SendMessageToMemberAsync(ConversationMemberDto member)
    {
        ChatContactDto? targetContact = null;

        try
        {
            await BlockUiService.Block(selectors: "#chat_wrapper", busy: true);
            targetContact = await ResolveDirectConversationContactAsync(
                member.UserId,
                member.UserInfo?.Username,
                member.UserInfo);
        }
        catch (Exception ex)
        {
            await UiMessageService.Error(L["FailedToSendMessage"]  + " :"+  ex.Message,
            options: new Action<UiMessageOptions>(options => options.OkButtonText = L["Ok"]));
        }
        finally
        {
            await BlockUiService.UnBlock();
            await InvokeAsync(StateHasChanged);
        }

        if (targetContact != null)
        {
            await SetActiveAsync(targetContact);
        }
    }

    private async Task CreateDirectConversationAsync()
    {
        ChatContactDto? targetContact = null;

        try
        {
            if (!SelectedDirectUser.Any())
            {
                ShowDirectUserRequiredMessage = true;
                await InvokeAsync(StateHasChanged);
                return;
            }

            var targetUserId = SelectedDirectUser.First().Id;
            var currentUserId = CurrentUser.Id ?? Guid.Empty;

            if (targetUserId == currentUserId)
            {
                return;
            }

            await BlockUiService.Block(selectors: "#chat_wrapper", busy: true);
            try
            {
                var selectedUser = SelectedDirectUser.First();
                targetContact = await ResolveDirectConversationContactAsync(
                    targetUserId,
                    selectedUser.DisplayName);

                ShowCreateDirectModal = false;
                SelectedDirectUser.Clear();
            }
            finally
            {
                await BlockUiService.UnBlock();
            }
        }
        catch (Exception ex)
        {
            try { await BlockUiService.UnBlock(); }
            catch (Exception unblockEx) { Logger.LogDebug(unblockEx, "BlockUiService.UnBlock failed (ignored)."); }
            await HandleErrorAsync(ex);
        }

        if (targetContact != null)
        {
            await SetActiveAsync(targetContact);
        }
    }

    private async Task<ChatContactDto> ResolveDirectConversationContactAsync(
        Guid targetUserId,
        string? fallbackDisplayName = null,
        ChatTargetUserInfo? fallbackUserInfo = null)
    {
        var currentUserId = CurrentUser.Id ?? Guid.Empty;
        if (currentUserId == Guid.Empty)
        {
            throw new InvalidOperationException("Current user is not available.");
        }

        var conversation = await ConversationAppService.FindConversationAsync(new FindConversationInput
        {
            UserIds = new List<Guid> { targetUserId, currentUserId },
            Type = ConversationType.User,
        });

        if (conversation == null)
        {
            conversation = await ConversationAppService.CreateUserConversationAsync(new CreateUserConversationInput
            {
                TargetUserId = targetUserId,
                Name = fallbackDisplayName
            });
        }

        await GetContactsAsync(
            includeOtherContacts: false,
            preserveCurrentContact: true,
            loadMore: false,
            isSetActive: false);

        var targetContact = ChatContactDtos.FirstOrDefault(c =>
            c.Type == ConversationType.User &&
            ((c.ConversationId.HasValue && c.ConversationId.Value == conversation.Id) || c.UserId == targetUserId));

        if (targetContact != null)
        {
            return targetContact;
        }

        var userInfo = conversation.TargetUserInfo ?? fallbackUserInfo;
        targetContact = new ChatContactDto
        {
            ConversationId = conversation.Id,
            Type = ConversationType.User,
            UserId = targetUserId,
            Name = userInfo?.Name ?? fallbackDisplayName ?? conversation.Name,
            Surname = userInfo?.Surname,
            Username = userInfo?.Username ?? fallbackDisplayName,
            HasChatPermission = true,
        };

        var pinnedInsertIdx = ChatContactDtos.Count(c => c.IsPinned);
        ChatContactDtos.Insert(pinnedInsertIdx, targetContact);
        ChatContactsActive[targetContact] = "";

        return targetContact;
    }

    private async Task ActivateConversationFromRouteAsync(Guid conversationId)
    {
        var targetContact = ChatContactDtos.FirstOrDefault(c => c.ConversationId == conversationId);
        if (targetContact == null)
        {
            _logger.LogWarning("Chat1: Could not find contact for route conversation {ConversationId}", conversationId);
            return;
        }

        if (CurrentChatContact?.ConversationId == conversationId)
        {
            return;
        }

        await SetActiveAsync(targetContact);
    }

    private void SyncConversationUrl(Guid? conversationId)
    {
        var targetUrl = conversationId.HasValue
            ? $"/Chat/{conversationId.Value}"
            : "/Chat";

        var currentRelativePath = Navigation.ToBaseRelativePath(Navigation.Uri)
            .Split('?', '#')[0]
            .Trim('/');
        var targetRelativePath = targetUrl.Trim('/');

        if (string.Equals(currentRelativePath, targetRelativePath, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        Navigation.NavigateTo(targetUrl, replace: true);
    }
    
    private async Task ShowCreateGroupModalAsync()
    {
        ShowCreateGroupModal = true;
        NewGroupName = "";
        NewGroupDescription = "";
        SelectedMembers.Clear();
        ShowGroupNameRequiredMessage = false;
        ShowGroupMembersRequiredMessage = false;
        IsCreatingGroupConversation = false;
        await InvokeAsync(StateHasChanged);
    }

    private void OnGroupNameChanged(ChangeEventArgs e)
    {
        NewGroupName = e.Value?.ToString() ?? string.Empty;
        ShowGroupNameRequiredMessage = false;
    }

    private void OnSelectedGroupMembersChanged(List<LookupDto<Guid>>? value)
    {
        SelectedMembers = value ?? new List<LookupDto<Guid>>();
        ShowGroupMembersRequiredMessage = false;
    }

    private void CloseCreateGroupModal()
    {
        if (IsCreatingGroupConversation)
        {
            return;
        }

        ShowCreateGroupModal = false;
        ShowGroupNameRequiredMessage = false;
        ShowGroupMembersRequiredMessage = false;
    }

    private bool CanCreateGroupConversation()
    {
        return !IsCreatingGroupConversation
            && !string.IsNullOrWhiteSpace(NewGroupName)
            && SelectedMembers.Any();
    }
    
    
    private async Task CreateGroupConversationAsync()
    {
        if (IsCreatingGroupConversation)
        {
            return;
        }

        try
        {
            ShowGroupNameRequiredMessage = string.IsNullOrWhiteSpace(NewGroupName);
            ShowGroupMembersRequiredMessage = !SelectedMembers.Any();

            if (ShowGroupNameRequiredMessage || ShowGroupMembersRequiredMessage)
            {
                await InvokeAsync(StateHasChanged);
                return;
            }

            IsCreatingGroupConversation = true;
            await BlockUiService.Block(selectors: "#chat_wrapper", busy: true);
            try
            {
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
                ShowGroupNameRequiredMessage = false;
                ShowGroupMembersRequiredMessage = false;

                await GetContactsAsync(
                    includeOtherContacts: false,
                    preserveCurrentContact: false,
                    loadMore: false,
                    isSetActive: false);

                var newContact = ChatContactDtos.FirstOrDefault(c => c.ConversationId == result.Id);
                if (newContact == null)
                {
                    // New group may not be in first page; build minimal contact from API result
                    newContact = new ChatContactDto
                    {
                        ConversationId = result.Id,
                        ConversationName = result.Name,
                        Type = result.Type,
                        UserId = Guid.Empty
                    };
                    var insertIndex = ChatContactDtos.Count(c => c.IsPinned);
                    ChatContactDtos.Insert(insertIndex, newContact);
                    ChatContactsActive[newContact] = "";
                }
                // Do NOT SetActiveAsync - user must click to select
                await InvokeAsync(StateHasChanged);
            }
            finally
            {
                await BlockUiService.UnBlock();
            }
        }
        catch (Exception ex)
        {
            try { await BlockUiService.UnBlock(); }
            catch (Exception unblockEx) { Logger.LogDebug(unblockEx, "BlockUiService.UnBlock failed (ignored)."); }
            await HandleErrorAsync(ex);
        }
    }
   
    // Select2 lookup methods
    private async Task<List<LookupDto<System.Guid>>> GetIdentityUserCollectionLookupAsync(IReadOnlyList<LookupDto<System.Guid>> dbset, string filter, CancellationToken token)
    {
        IdentityUsersCollection = (await ContactAppService.GetUserLookupAsync(new LookupRequestDto
        {
            Filter = filter,
            MaxResultCount = 20,
            SkipCount = 0
        })).Items;

        return IdentityUsersCollection.ToList();
    }
    
    // Overload for modal/non-Select2 usage
    private async Task GetProjectCollectionLookupAsync(string? newValue = null)
    {
        ProjectsCollection = (await ProjectTasksAppService.GetProjectLookupAsync(new LookupRequestDto { Filter = newValue })).Items;
    }
    
    private async Task<List<LookupDto<Guid>>> GetProjectCollectionLookupAsync(IReadOnlyList<LookupDto<Guid>> dbset, string filter, CancellationToken token)
    {
        ProjectsCollection = (await ProjectTasksAppService.GetProjectLookupAsync(new LookupRequestDto { Filter = filter })).Items;
        return ProjectsCollection.ToList();
    }
    
    private async Task TogglePinConversationAsync(ChatContactDto contact)
    {
        try
        {
            if (!contact.ConversationId.HasValue)
            {
                return;
            }

            await BlockUiService.Block(selectors: "#chat_wrapper", busy: true);
            try
            {
                if (contact.IsPinned)
                {
                    await ConversationAppService.UnpinConversationAsync(contact.ConversationId.Value);
                }
                else
                {
                    await ConversationAppService.PinConversationAsync(contact.ConversationId.Value);
                }
                // Refresh conversation list but keep current opened conversation unchanged.
                await GetContactsAsync(isSetActive: false);
            }
            finally
            {
                await BlockUiService.UnBlock();
            }
        }
        catch (Exception ex)
        {
            try { await BlockUiService.UnBlock(); }
            catch (Exception unblockEx) { Logger.LogDebug(unblockEx, "BlockUiService.UnBlock failed (ignored)."); }
            await HandleErrorAsync(ex);
        }
    }

    private async Task RemoveMemberAsync(RemoveMemberInput input)
    {
        try
        {
            if (input.ConversationId == Guid.Empty)
            {
                await UiMessageService.Error(L["ConversationNotFound"],
                options: new Action<UiMessageOptions>(options => options.OkButtonText = L["Ok"]));
                return;
            }
            if (input.UserId == Guid.Empty)
            {
                await UiMessageService.Error(L["UserNotFound"],
                options: new Action<UiMessageOptions>(options => options.OkButtonText = L["Ok"]));
                return;
            }
            await BlockUiService.Block(selectors: "#chat_wrapper", busy: true);
            await ConversationAppService.RemoveMemberAsync(input);
            await GetContactsAsync(isSetActive: false);
            await InvokeAsync(StateHasChanged);
        }
        catch (Exception ex)
        {
            await UiMessageService.Error(ex.Message,
            options: new Action<UiMessageOptions>(options => options.OkButtonText = L["Ok"]));
        }
        finally
        {
            await BlockUiService.UnBlock();
        }
    }
    private async Task AddMembersAsync(AddMemberInput input)
    {
        try
        {
            if (input == null || input.ConversationId == Guid.Empty)
            {
                await UiMessageService.Error(L["ConversationNotFound"],
                options: new Action<UiMessageOptions>(options => options.OkButtonText = L["Ok"]));
                return;
            }
            if (input.UserIds == null || input.UserIds.Count == 0)
            {
                await UiMessageService.Error(L["NoUsersSelected"],
                options: new Action<UiMessageOptions>(options => options.OkButtonText = L["Ok"]));
                return;
            }
            await BlockUiService.Block(selectors: "#chat_wrapper", busy: true);
            var result = await ConversationAppService.AddMemberAsync(input);
            if(!string.IsNullOrEmpty(result))
            {
                await UiMessageService.Error(L[result],
                options: new Action<UiMessageOptions>(options => options.OkButtonText = L["Ok"]));
                return;
            }
            await GetContactsAsync(isSetActive: false);
            await InvokeAsync(StateHasChanged);
        }
        catch (Exception ex)
        {
            await UiMessageService.Error(ex.Message,
            options: new Action<UiMessageOptions>(options => options.OkButtonText = L["Ok"]));
        }
        finally
        {
            await BlockUiService.UnBlock();
        }
    }
    private async Task LeaveConversationAsync(RemoveMemberInput contact)
    {
        try
        {
            if (contact.ConversationId == Guid.Empty)
            {
                await UiMessageService.Error(L["ConversationNotFound"],
                options: new Action<UiMessageOptions>(options => options.OkButtonText = L["Ok"]));
                return;
            }
            await BlockUiService.Block(selectors: "#chat_wrapper", busy: true);
            await ConversationAppService.LeaveConversationAsync(new LeaveConversationInput
            {
                ConversationId = contact.ConversationId
            });

            ShowInfoBox = false;

            var leftContact = ChatContactDtos?.FirstOrDefault(c =>
                c.ConversationId.HasValue && c.ConversationId.Value == contact.ConversationId);
            if (leftContact != null)
            {
                ChatContactDtos!.Remove(leftContact);
                ChatContactsActive.Remove(leftContact);
            }

            if (CurrentChatContact?.ConversationId == contact.ConversationId)
            {
                CurrentChatContact = null;
                ChatConversationDto = null;
            }
            await InvokeAsync(StateHasChanged);
        }
        catch (Exception ex)
        {
            await HandleErrorAsync(ex);
        }
        finally
        {
            IsCreatingGroupConversation = false;
            try { await BlockUiService.UnBlock(); }
            catch (Exception unblockEx) { Logger.LogDebug(unblockEx, "BlockUiService.UnBlock failed (ignored)."); }
            await InvokeAsync(StateHasChanged);
        }
    }
    private async Task ReplyToMessageAsync(ChatMessageDto message)
    {
        ReplyingToMessage = message;
        try
        {
            await MessageTextArea.FocusAsync();
        }
        catch
        {
            // Ignore if element is not available
        }
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
    private bool ValidateMessageBeforeSend()
    {
        if (_isSendingMessage)
            return false;
        
        if (Message.IsNullOrWhiteSpace() && (UploadedFiles == null || !UploadedFiles.Any()))
            return false;

        if (CurrentChatContact == null)
            return false;

        return true;
    }
    private (string messageText, List<MessageFileDto> files, ChatMessageDto replyingTo, Guid targetUserId, Guid? conversationId) PrepareMessageContent()
    {
        var messageText = Message;
        var uploadedFiles = UploadedFiles?.ToList() ?? new List<MessageFileDto>();
        var replyingTo = ReplyingToMessage;
        var targetUserId = CurrentChatContact.UserId;
        var conversationId = CurrentConversationId;

        return (messageText, uploadedFiles, replyingTo, targetUserId, conversationId);
    }
    private async Task ClearInputAsync()
    {
        // Clear textarea via JavaScript FIRST to ensure immediate clearing
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
        
        Message = "";
        ReplyingToMessage = null;
        UploadedFiles?.Clear();
        await InvokeAsync(StateHasChanged);
    }
    private async Task SendToServerAsync(string messageText, List<MessageFileDto> uploadedFiles, ChatMessageDto replyingTo, ChatMessageDto optimisticMessage)
    {
        try
        {
            ChatMessageDto serverMessage = null;
            var targetUserId = CurrentChatContact.UserId;
            var conversationId = CurrentConversationId;
            
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
                        
                        var contactInList = ChatContactDtos.FirstOrDefault(c => 
                            (c.Type == ConversationType.User && c.UserId == CurrentChatContact.UserId) ||
                            (c.Type != ConversationType.User && c.ConversationId == CurrentChatContact.ConversationId));
                        
                        if (contactInList != null)
                        {
                            contactInList.LastMessage = lastMessage.Message;
                            contactInList.LastMessageDate = lastMessage.MessageDate;
                        }
                    }
                    
                    // Refresh contacts list
                    await RefreshContactsListAsync();
                    
                    // Auto scroll to bottom after server message is updated
                    await Task.Delay(100);
                    await ScrollToBottomAsync();
                }
                
                // Decrement pending count
                Interlocked.Decrement(ref _pendingMessagesCount);
                
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
                
                // Decrement pending count
                Interlocked.Decrement(ref _pendingMessagesCount);
                
                // Reset sending flag to allow next send
                _isSendingMessage = false;
                
                await InvokeAsync(StateHasChanged);
                await HandleErrorAsync(ex);
            });
        }
    }
    private async Task SendMessageAsync()
    {
        // Validate before attempting send
        if (!ValidateMessageBeforeSend())
            return;
        
        _isSendingMessage = true;

        // Extract message content
        var (messageText, uploadedFiles, replyingTo, targetUserId, conversationId) = PrepareMessageContent();

        // Clear input immediately for better UX
        await ClearInputAsync();

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
        
        // Increment pending count
        Interlocked.Increment(ref _pendingMessagesCount);
        
        // Update UI immediately
        await InvokeAsync(StateHasChanged);
        
        // Auto scroll to bottom to show new message
        await Task.Delay(100);
        await ScrollToBottomAsync();
        
        // Focus textarea immediately for next message
        try
        {
            await Task.Delay(50);
            await MessageTextArea.FocusAsync();
        }
        catch
        {
            // Ignore if element is not available or component disposed
        }

        // Send to server in background (fire-and-forget pattern)
        _ = SendToServerAsync(messageText, uploadedFiles, replyingTo, optimisticMessage);
    }

    private async Task JumpToMessageAsync(Guid messageId)
    {
        if (CurrentConversationId == null || ChatConversationDto?.Messages == null)
        {
            return;
        }

        if (ChatConversationDto.Messages.Any(x => x.Id == messageId))
        {
            await ScrollToMessageAsync(messageId);
            return;
        }

        _isJumpingToMessage = true;
        await InvokeAsync(StateHasChanged);

        try
        {
            var context = await ConversationAppService.GetMessageContextAsync(new GetMessageContextInput
            {
                ConversationId = CurrentConversationId.Value,
                MessageId = messageId,
                BeforeCount = 20,
                AfterCount = 20
            });

            if (context.AnchorMessage == null)
            {
                return;
            }

            var existing = ChatConversationDto.Messages.ToDictionary(x => x.Id, x => x);
            foreach (var message in context.BeforeMessages)
            {
                existing[message.Id] = message;
            }

            existing[context.AnchorMessage.Id] = context.AnchorMessage;

            foreach (var message in context.AfterMessages)
            {
                existing[message.Id] = message;
            }

            ChatConversationDto.Messages = existing.Values
                .OrderBy(x => x.MessageDate)
                .ThenBy(x => x.Id)
                .ToList();

            _messagesSkipCount = ChatConversationDto.Messages.Count;

            await InvokeAsync(StateHasChanged);
            await Task.Delay(50);
            await ScrollToMessageAsync(messageId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "JumpToMessageAsync failed for {MessageId}", messageId);
        }
        finally
        {
            _isJumpingToMessage = false;
            await InvokeAsync(StateHasChanged);
        }
    }

    private async Task ScrollToMessageAsync(Guid messageId)
    {
        var elementId = $"msg-{messageId}";
        var script =
            "(function() {" +
            "  const el = document.getElementById('" + elementId + "');" +
            "  if (!el) return false;" +
            "  el.scrollIntoView({ block: 'center', behavior: 'smooth' });" +
            "  el.classList.add('message-jump-highlight');" +
            "  setTimeout(() => el.classList.remove('message-jump-highlight'), 2000);" +
            "  return true;" +
            "})()";

        await JsRuntime.SafeInvokeAsync<object>("eval", script);
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
    private async Task ShowInfoBoxAsync()
    {
        ShowInfoBox = !ShowInfoBox;
        await InvokeAsync(StateHasChanged);
    }
    private string GetImageUrl(string imagePath)
    {
        if (string.IsNullOrEmpty(imagePath))
        {
            return string.Empty;
        }

        return BlobDisplayUrlProvider.GetDisplayUrl(imagePath);
    }

    /// <summary>
    /// Open image viewer modal
    /// </summary>
    public async Task OpenImageViewerAsync(MessageFileDto file)
    {
        ImageViewerFileName = file.FileName;
        ImageViewerUrl = file.FilePath;
        ImageViewerFilePath = file.FilePath;
        _currentViewingImageFileId = file.Id;
        ShowImageViewerModal = true;
        await InvokeAsync(StateHasChanged);
    }

    /// <summary>
    /// Close image viewer modal
    /// </summary>
    public async Task CloseImageViewerModal()
    {
        ShowImageViewerModal = false;
        ImageViewerUrl = string.Empty;
        ImageViewerFilePath = string.Empty;
        ImageViewerFileName = string.Empty;
        _currentViewingImageFileId = Guid.Empty;
        await InvokeAsync(StateHasChanged);
    }

    /// <summary>
    /// Download current viewing image
    /// </summary>
    public async Task DownloadImageAsync()
    {
        if (_currentViewingImageFileId != Guid.Empty)
        {
            await DownloadFileAsync(_currentViewingImageFileId);
        }
    }
    
    public async ValueTask DisposeAsync()
    {
        try
        {
            try
            {
                _refreshContactsDebounceCts?.Cancel();
            }
            catch (ObjectDisposedException)
            {
                // ignore
            }

            _refreshContactsDebounceCts?.Dispose();
            _refreshContactsDebounceCts = null;

            // === NEW: Dispose refactored handlers ===
            if (_messageHandler is IAsyncDisposable disposableMessageHandler)
            {
                await disposableMessageHandler.DisposeAsync();
                _logger.LogDebug("MessageHandler disposed");
            }

            // State doesn't implement IAsyncDisposable, so no need to dispose

            _logger.LogDebug("Chat1 disposal completed");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during Chat1 handler disposal");
        }

    //     if (ChatHubConnectionService is IAsyncDisposable asyncDisposable)
    //     {
    //         await asyncDisposable.DisposeAsync();
    //     }
    }

    // Mobile view management methods

    private void CheckMobileMode()
    {
        // Detect if we're in mobile mode based on screen width
        // This will be called from JavaScript
    }

    [JSInvokable]
    public void SetMobileMode(bool isMobile)
    {
        var previousMode = IsMobileMode;
        IsMobileMode = isMobile;

        // Set initial mobile view based on URL parameter
        if (isMobile && !previousMode)
        {
            // First time switching to mobile mode
            if (RedirectToConversationId.HasValue)
            {
                // Has conversation parameter -> show chat directly
                CurrentMobileView = MobileViewType.ChatConversation;
            }
            else
            {
                // No conversation parameter -> show list
                CurrentMobileView = MobileViewType.ConversationList;
            }
        }
        else if (!isMobile)
        {
            // Reset to default view when switching to desktop
            CurrentMobileView = MobileViewType.ConversationList;
        }

        _logger.LogInformation($"Mobile mode changed: {previousMode} -> {isMobile}, CurrentView: {CurrentMobileView}");
        InvokeAsync(StateHasChanged);
    }

    public async Task ShowConversationListAsync()
    {
        if (IsMobileMode)
        {
            CurrentMobileView = MobileViewType.ConversationList;
        }
        await InvokeAsync(StateHasChanged);
    }

    public async Task ShowChatConversationAsync()
    {
        if (IsMobileMode)
        {
            CurrentMobileView = MobileViewType.ChatConversation;
        }
        await InvokeAsync(StateHasChanged);
    }

    public async Task ShowConversationInfoAsync()
    {
        if (IsMobileMode)
        {
            CurrentMobileView = MobileViewType.ConversationInfo;
            ShowInfoBox = true;
        }
        else
        {
            ShowInfoBox = !ShowInfoBox;
        }

        await InvokeAsync(StateHasChanged);
    }

    public async Task HideConversationInfoAsync()
    {
        if (IsMobileMode)
        {
            CurrentMobileView = MobileViewType.ChatConversation;
        }
        ShowInfoBox = false;
        await InvokeAsync(StateHasChanged);
    }
}
