# Chat1.razor.cs Integration Guide

## Overview

Guide này mô tả cách gradual integration (tích hợp dần dần) các handlers mới vào `Chat1.razor.cs` mà không làm gián đoạn chức năng hiện tại.

## Approach: Parallel Implementation

Thay vì replace code hiện tại, chúng ta sẽ:
1. Thêm handlers bên cạnh code hiện tại
2. Test handlers với từng feature riêng
3. Migration dần dần khi đã confident
4. Remove code cũ sau khi validation hoàn tất

---

## Step 1: Add Factory Injection (5 phút)

Thêm vào cuối phần `[Inject]` statements trong `Chat1.razor.cs`:

```csharp
[Inject]
private IRemoteServiceConfigurationProvider RemoteServiceConfigurationProvider { get; set; } = default!;

// === NEW: Handler Factory for Refactored Code ===
[Inject]
private Pages.Chat1.Handlers.IChatHandlerFactory HandlerFactory { get; set; }

private string? _apiBaseUrl;
```

---

## Step 2: Initialize State & Handlers (10 phút)

Thêm vào cuối `OnInitializedAsync()`:

```csharp
protected override async Task OnInitializedAsync()
{
    // ... existing initialization code ...
    
    // === NEW: Initialize ChatState and Handlers ===
    // Create state object if not exists
    if (_state == null)
    {
        _state = new ChatState();
    }
    
    // Set up state change notification
    _state.OnChange = async () =>
    {
        await InvokeAsync(StateHasChanged);
    };
    
    // Initialize state with existing data
    _state.CurrentConversation = ChatConversationDto;
    _state.CurrentConversationId = CurrentConversationId;
    _state.MessageText = Message;
    _state.MessageTextArea = MessageTextArea;
    _state.SendOnEnter = SendOnEnter;
    _state.ShowInfoBox = ShowInfoBox;
    _state.SearchValue = SearchValue;
    _state.ReplyingToMessage = ReplyingToMessage;
    _state.UploadedFiles = UploadedFiles;
    
    // Create handlers
    _messageHandler = HandlerFactory.CreateMessageHandler(_state);
    _fileHandler = HandlerFactory.CreateFileHandler(_state);
    _paginationHandler = HandlerFactory.CreatePaginationHandler(_state, _pagination);
    _optimizationHandler = HandlerFactory.CreateOptimizationHandler(_state);
    
    // ... continue with existing code ...
}
```

---

## Step 3: Add Private Fields (5 phút)

Thêm vào đầu class với các private fields khác:

```csharp
public partial class Chat1 : HCComponentBase, IAsyncDisposable
{
    // ... existing properties ...
    
    // === NEW: Refactored Components ===
    private ChatState _state = new ChatState();
    private PaginationState _pagination = new PaginationState();
    
    private IChatMessageHandler _messageHandler;
    private IChatFileHandler _fileHandler;
    private IChatPaginationHandler _paginationHandler;
    private IChatOptimizationHandler _optimizationHandler;
    
    [Parameter]
    public Guid? RedirectToConversationId { get; set; }
    // ... rest of existing code ...
}
```

---

## Step 4: Migrate Features One by One

### 4.1 Message Sending (Priority: HIGH)

**Current code** (giữ nguyên):
```csharp
private async Task SendMessage()
{
    // ... existing implementation ...
}
```

**New code** (thêm song song để test):
```csharp
private async Task SendMessageWithHandler()
{
    try
    {
        await _messageHandler.SendMessageAsync(Message, UploadedFiles, ReplyingToMessage);
        
        // Update existing properties to stay in sync
        Message = _state.MessageText;
        UploadedFiles = _state.UploadedFiles;
        ReplyingToMessage = _state.ReplyingToMessage;
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error sending message via handler");
        // Fallback to existing implementation
        await SendMessage();
    }
}
```

**Usage in razor file** (`Chat1.razor`):
```razor
<!-- Temporary: use new handler method -->
<button @onclick="SendMessageWithHandler">Send</button>
```

---

### 4.2 File Handling (Priority: HIGH)

**Current code**:
```csharp
private async Task OnFileSelected(InputFileChangeEventArgs e)
{
    // ... existing implementation ...
}
```

**New code**:
```csharp
private async Task OnFileSelectedWithHandler(InputFileChangeEventArgs e)
{
    try
    {
        await _fileHandler.OnFileSelectedAsync(e);
        
        // Sync with existing properties
        UploadedFiles = _state.UploadedFiles;
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error handling file via handler");
        await OnFileSelected(e);
    }
}
```

---

### 4.3 Pagination (Priority: MEDIUM)

**New code**:
```csharp
private async Task LoadMoreMessagesWithHandler()
{
    try
    {
        await _paginationHandler.LoadMoreMessagesAsync();
        
        // Sync pagination state
        _messagesSkipCount = _pagination.MessagesSkipCount;
        _isLoadingMoreMessages = _pagination.IsLoadingMoreMessages;
        _hasMoreMessages = _pagination.HasMoreMessages;
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error loading messages via handler");
        await LoadMoreMessages(); // Fallback to existing
    }
}
```

---

### 4.4 Message Reception (Priority: MEDIUM)

Update `ProcessReceivedMessage` để sử dụng optimization handler:

```csharp
private async Task ProcessReceivedMessage(ChatMessageRdto message)
{
    try
    {
        // Use optimization handler to append message without full refresh
        await _optimizationHandler.AppendMessageAsync(message);
        
        // Check if we need full refresh
        var shouldRefresh = await _optimizationHandler.ShouldRefreshConversationAsync(message);
        
        if (shouldRefresh)
        {
            // Fall back to existing refresh logic
            await LoadMessagesAsync();
        }
        
        await InvokeAsync(StateHasChanged);
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error processing message via handler");
        // Fallback to existing logic
        // ... existing code ...
    }
}
```

---

## Step 5: Update Dispose Method

```csharp
public async ValueTask DisposeAsync()
{
    // Dispose new handlers
    if (_messageHandler is IAsyncDisposable disposableMessageHandler)
    {
        await disposableMessageHandler.DisposeAsync();
    }
    
    // Dispose state (if it has disposable resources)
    // ChatState doesn't currently implement IAsyncDisposable
    
    // ... existing disposal code ...
}
```

---

## Testing Strategy

### Phase 1: Unit Testing (từng handler riêng)
1. Test message sending handler only
2. Test file upload handler only
3. Test pagination handler only
4. Test optimization handler only

### Phase 2: Integration Testing
1. Test complete flow: send → receive → display
2. Test error scenarios (network failure, server error)
3. Test concurrent operations

### Phase 3: Migration
1. Replace UI event handlers one by one
2. Monitor for errors
3. Roll back if issues found

### Phase 4: Cleanup
1. Remove old implementation methods
2. Remove fallback logic
3. Remove "WithHandler" suffix
4. Update documentation

---

## Rollback Plan

Nếu có vấn đề, có thể rollback nhanh bằng cách:
1. Comment out handler calls
2. Use original methods directly
3. Remove handler injection

---

## Benefits of Gradual Migration

1. ✅ No breaking changes
2. ✅ Test in production with feature flags
3. ✅ Easy rollback
4. ✅ Lower risk
5. ✅ Learn from real usage

---

## Next Actions

1. ✅ Add factory injection to `Chat1.razor.cs`
2. ✅ Initialize state and handlers in `OnInitializedAsync()`
3. ✅ Create `SendMessageWithHandler()` method
4. ⏳ Test message sending with handler
5. ⏳ Migrate file handling
6. ⏳ Migrate pagination
7. ⏳ Migrate message reception
8. ⏳ Remove old code after validation

---

## Configuration

Không cần thay đổi cấu hình - handlers sử dụng cùng services đã được register trong `HCBlazorModule.cs`.

---

## Monitoring

Theo dõi metrics sau khi integration:
- Message success rate (via `IChatMetrics`)
- Circuit breaker trips
- Retry attempts
- Handler execution time

Xem metrics: `/admin/metrics` endpoint (nếu có expose)
