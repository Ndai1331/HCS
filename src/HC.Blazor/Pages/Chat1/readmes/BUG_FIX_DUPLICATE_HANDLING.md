# Bug Fix: Duplicate Message Handling & UnreadMessageCount Issues

## Ngày fix: 01/02/2026

## Vấn đề
1. **Duplicate ReceiveMessage events**: Cùng một message từ SignalR được xử lý nhiều lần
2. **UnreadMessageCount tăng sai**: Do message được xử lý trùng, count tăng nhiều lần
3. **API error**: Lỗi `AbpRemoteCallException` khi gọi `GetContactsAsync`

## Nguyên nhân

### 1. Duplicate Message Handling
**File**: `src/HC.Blazor/wwwroot/chatHub.js`

**Vấn đề**:
- `baseHub.registerEventHandler()` đăng ký 1 handler callback duy nhất cho event SignalR
- Khi có event, baseHub gọi handler này cho TẤT CẢ helpers trong `connection._dotnetHelpers` array
- Với 2 helpers (Chat1 + NotificationToast), handler được gọi 2 lần
- Handler cũ (line 49-76) lại gọi CẢ 2 methods (`HandleSignalRMessageJson` + `OnChatMessageReceived`) cho mỗi helper
- → Message được xử lý 4 lần!

**Log lỗi**:
```
chat Hub: Received event 'ReceiveMessage' {id: '3a1f26c5-49f8-75fe-c3ee-414e30e5ec05', ...}
chatHub.js:50 Chat Hub: Calling HandleSignalRMessageJson for helper
chatHub.js:157 Chat Hub: ReceiveMessage for NotificationToast
chatHub.js:175 Chat Hub: Skipping NotificationToast handler (not notification helper or wrong helper)
[REPEAT 3x for same message]
```

### 2. UnreadMessageCount tăng sai
**File**: `src/HC.Blazor/Pages/Chat1/Chat1.razor.cs`

**Vấn đề**:
- Code increment `UnreadMessageCount++` ở line 329 và 505
- Khi message được xử lý trùng, count tăng nhiều lần cho cùng 1 message
- Không có cơ chế tracking message đã xử lý

### 3. API Error GetContactsAsync
**File**: `src/HC.Blazor/Pages/Chat1/Chat1.razor.cs` line 657

**Vấn đề**:
- Lỗi `AbpRemoteCallException: Có một lỗi nội bộ xảy ra trong quá trình thực hiện yêu cầu của bạn!`
- Thiếu error handling khi API call fails

## Giải pháp

### 1. Fix Duplicate Message Handling (chatHub.js)

**Thay đổi 1**: ReceiveMessage handler
```javascript
// Trước: Luồng gọi cả 2 methods cho mọi helper
window.baseHub.registerEventHandler("chat", "ReceiveMessage", async (helper, messageData) => {
    await helper.invokeMethodAsync("HandleSignalRMessageJson", messageData);
    
    if (window._chatNotificationHelper) {
        await window._chatNotificationHelper.invokeMethodAsync("OnChatMessageReceived", messageJson);
    }
});

// Sau: Chỉ gọi method phù hợp với từng loại helper
window.baseHub.registerEventHandler("chat", "ReceiveMessage", async (helper, messageData) => {
    // Chỉ gọi HandleSignalRMessageJson nếu KHÔNG phải notification helper
    if (helper !== window._chatNotificationHelper) {
        await helper.invokeMethodAsync("HandleSignalRMessageJson", messageData);
    }

    // Chỉ gọi OnChatMessageReceived nếu CÓ phải notification helper
    if (window._chatNotificationHelper && helper === window._chatNotificationHelper) {
        await helper.invokeMethodAsync("OnChatMessageReceived", messageJson);
    }
});
```

**Thay đổi 2**: ConversationCreated handler
- Áp dụng cùng logic: chỉ gọi method phù hợp với từng helper type

**Thay đổi 3**: startForNotifications()
```javascript
// Trước: Đăng ký lại handlers (trùng lặp)
startForNotifications: function (dotnetHelper) {
    // Tạo connection mới với dummy helper
    // Đăng ký handlers ReceiveMessage, ConversationCreated
}

// Sau: Chỉ thêm helper vào connection hiện có
startForNotifications: function (dotnetHelper) {
    if (!window._chatConnection) {
        // Tạo connection với notification helper
        const connection = window.baseHub.createOrReuseConnection(
            "/chatHub", "chat", dotnetHelper,
            { enableCrossTabSync: false }
        );
    } else {
        // Thêm vào connection hiện có
        connection._dotnetHelpers.push(dotnetHelper);
    }
}
```

### 2. Fix UnreadMessageCount (Chat1.razor.cs)

**Thêm** tracking message đã xử lý:
```csharp
// Field mới
private HashSet<Guid> _processedMessageIds = new HashSet<Guid>();
private const int MaxProcessedIdsCacheSize = 1000;

// Trong ProcessReceivedMessage()
bool isFirstProcessing = false;
if (!_processedMessageIds.Contains(message.Id))
{
    lock (_processedMessageIds)
    {
        if (!_processedMessageIds.Contains(message.Id))
        {
            _processedMessageIds.Add(message.Id);
            isFirstProcessing = true;
            
            // Cleanup cache khi quá lớn
            if (_processedMessageIds.Count > MaxProcessedIdsCacheSize)
            {
                // Xóa 50% IDs cũ nhất
            }
        }
    }
}

// Chỉ increment count nếu là lần đầu xử lý message
if (message.SenderUserId != CurrentUser.Id && isFirstProcessing)
{
    targetContact.UnreadMessageCount++;
}
```

### 3. Fix API Error (Chat1.razor.cs)

**Thêm** error handling:
```csharp
public async Task GetContactsAsync(...)
{
    try
    {
        // ... existing code ...
        var newContacts = await ContactAppService.GetContactsAsync(input);
        // ... rest of code ...
    }
    catch (AbpRemoteCallException ex)
    {
        _logger.LogError(ex, "API error when getting contacts");
        // Return empty list thay vì crash
        ChatContactDtos = new List<ChatContactDto>();
        await InvokeAsync(StateHasChanged);
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Unexpected error when getting contacts");
        await HandleErrorAsync(ex);
    }
}
```

## Kết quả

### Trước fix:
- ❌ Message được xử lý 4 lần (cho 2 helpers)
- ❌ UnreadMessageCount tăng 4x actual
- ❌ App crash khi API error

### Sau fix:
- ✅ Message được xử lý chính xác 1 lần
- ✅ UnreadMessageCount tăng đúng 1 cho mỗi message mới
- ✅ App không crash, hiển thị empty list khi API error

## File đã thay đổi

1. `src/HC.Blazor/wwwroot/chatHub.js`
   - Sửa ReceiveMessage handler (line 48-94)
   - Sửa ConversationCreated handler (line 99-141)
   - Sửa startForNotifications() (line 129-175)

2. `src/HC.Blazor/Pages/Chat1/Chat1.razor.cs`
   - Thêm _processedMessageIds tracking (line 94-97)
   - Sửa ProcessReceivedMessage() để track message (line 228-260)
   - Sửa UnreadMessageCount increment (line 354-358)
   - Thêm error handling trong GetContactsAsync (line 654-787)

## Kiểm tra
- Reload trang và gửi tin nhắn
- Verify message chỉ xuất hiện 1 lần
- Verify UnreadMessageCount chỉ tăng 1 cho mỗi tin nhắn mới
- Test với user khác gửi message khi bạn không mở tab chat

## Related issues
- BUG_FIX_DUPLICATE_MESSAGES.md
- BUG_FIX_DUPLICATE_NOTIFICATIONS.md
- BUG_FIX_DOUBLE_UNREAD_COUNT.md
