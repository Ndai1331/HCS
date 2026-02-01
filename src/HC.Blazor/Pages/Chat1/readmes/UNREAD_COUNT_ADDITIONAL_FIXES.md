# Unread Message Count - Additional Fixes

## Overview
Đã thực hiện 2 bổ sung quan trọng theo yêu cầu trong file hướng dẫn:
1. Real-time update total unread count trên Notification bar khi có tin nhắn mới
2. Fix API `/api/chat/contact` để load `UnreadMessageCount` đúng cách

## Thay đổi đã thực hiện

### 1. Fix ContactAppService - Load UnreadMessageCount từ Database

**File:** `src/HC.Application/Chat/Users/ContactAppService.cs`

**Vấn đề:** 
- Dòng 96 đang hardcode `UnreadMessageCount = 0` với TODO comment
- API `/api/chat/contact` không load unread count từ database

**Giải pháp:**
```csharp
// Trước (dòng 50-70):
// Get pin status, pinned date, and role for current user
var isPinned = false;
DateTime? pinnedDate = null;
string memberRole = null;
if (x.Conversation.Type != ConversationType.User)
{
    try
    {
        var member = await _conversationMemberRepository.GetByConversationAndUserAsync(x.Conversation.Id, currentUserId);
        isPinned = member?.IsPinned ?? false;
        pinnedDate = member?.PinnedDate;
        memberRole = member?.Role; // ADMIN or MEMBER
    }
    catch
    {
        // Ignore errors when getting member
    }
}

// Sau (fix):
// Get pin status, pinned date, role, AND UNREAD COUNT for current user
var isPinned = false;
DateTime? pinnedDate = null;
string memberRole = null;
int unreadMessageCount = 0;  // NEW
if (x.Conversation.Type != ConversationType.User)
{
    try
    {
        var member = await _conversationMemberRepository.GetByConversationAndUserAsync(x.Conversation.Id, currentUserId);
        isPinned = member?.IsPinned ?? false;
        pinnedDate = member?.PinnedDate;
        memberRole = member?.Role;
        unreadMessageCount = member?.UnreadMessageCount ?? 0;  // NEW
    }
    catch
    {
        // Ignore errors when getting member
        unreadMessageCount = 0;  // NEW
    }
}
else
{
    // For User conversations, also get unread count  // NEW
    try
    {
        var member = await _conversationMemberRepository.GetByConversationAndUserAsync(x.Conversation.Id, currentUserId);
        unreadMessageCount = member?.UnreadMessageCount ?? 0;
    }
    catch
    {
        unreadMessageCount = 0;
    }
}
```

```csharp
// Trước (dòng 88-106):
conversationContacts.Add(new ChatContactDto
{
    ...
    UnreadMessageCount = 0, // TODO: Calculate from ConversationMember per-user read status
    ...
});

// Sau (fix):
conversationContacts.Add(new ChatContactDto
{
    ...
    UnreadMessageCount = unreadMessageCount, // Get from ConversationMember
    ...
});
```

**GetTotalUnreadMessageCountAsync cũng được fix:**
```csharp
// Trước:
public virtual async Task<int> GetTotalUnreadMessageCountAsync()
{
    // TODO: Calculate from ConversationMember per-user read status
    return 0;
}

// Sau:
public virtual async Task<int> GetTotalUnreadMessageCountAsync()
{
    try
    {
        var currentUserId = CurrentUser.GetId();
        var allMembers = await _conversationMemberRepository.GetByUserIdAsync(currentUserId);
        
        var totalUnreadCount = allMembers
            .Where(m => m.IsActive)
            .Sum(m => m.UnreadMessageCount);
        
        return totalUnreadCount;
    }
    catch (Exception ex)
    {
        Logger?.LogError(ex, "Error in GetTotalUnreadMessageCountAsync");
        return 0;
    }
}
```

### 2. Real-time Update Total Unread Count trên Notification Bar

**Thay đổi SignalR để broadcast event khi có tin nhắn mới:**

**File:** `src/HC.Blazor/EventHandlers/ChatEventHandler.cs`

Thêm event `ChatUnreadCountChanged` vào `HandleEventAsync(ChatMessageEto eventData)`:
```csharp
public async Task HandleEventAsync(ChatMessageEto eventData)
{
    try
    {
        // ... existing code to send ReceiveMessage event ...
        
        // NEW: Also send ChatUnreadCountChanged event to update the badge on notification bar
        try
        {
            await _hubContext.Clients
                .User(targetUserIdString)
                .SendAsync("ChatUnreadCountChanged");
            
            _logger.LogInformation(
                "Successfully sent ChatUnreadCountChanged event: TargetUserId={TargetUserId}",
                targetUserIdString);
        }
        catch (Exception ex2)
        {
            _logger.LogError(ex2, "Error sending ChatUnreadCountChanged event");
        }
    }
    catch (Exception ex)
    {
        // ... error handling ...
    }
}
```

**File:** `src/HC.Blazor/wwwroot/chatHub.js`

Thêm event handler mới:
```javascript
// Register ChatUnreadCountChanged handler - notifies all listeners when chat unread count changes
window.baseHub.registerEventHandler("chat", "ChatUnreadCountChanged", async (helper) => {
    console.log("Chat Hub: ChatUnreadCountChanged event received");
    
    // Only call if this is a notification helper (for Notification.razor)
    if (helper === window._chatNotificationHelper || helper._isNotificationBarHelper) {
        await helper.invokeMethodAsync("OnChatUnreadCountChanged")
            .then(() => console.log("Chat Hub: OnChatUnreadCountChanged completed"))
            .catch(err => {
                console.error("Chat Hub: Error calling OnChatUnreadCountChanged:", err);
            });
    }
});
```

Thêm method mới để khởi tạo chatHub cho NotificationBar:
```javascript
/**
 * Initialize chat hub for NotificationBar component (top bar with unread badge)
 * @param {object} dotnetHelper - DotNetObjectReference for JS interop
 */
startForNotificationBar: function (dotnetHelper) {
    console.log("Chat Hub: startForNotificationBar called for Notification.razor");
    
    // Mark this helper as notification bar helper
    dotnetHelper._isNotificationBarHelper = true;
    
    // Reuse existing connection if available
    if (!window._chatConnection) {
        console.log("Chat Hub: No existing connection, creating new one for notification bar...");
        
        // Create connection with the notification bar helper
        const connection = window.baseHub.createOrReuseConnection(
            "/chatHub",
            "chat",
            dotnetHelper,
            {
                enableCrossTabSync: false
            }
        );

        window._chatConnection = connection;
        console.log("Chat Hub: Connection created for notification bar");
    } else {
        console.log("Chat Hub: Reusing existing connection for notification bar");
        // Add notification bar helper to existing connection's helper array
        const connection = window._chatConnection;
        
        // Only add if not already in array
        if (!connection._dotnetHelpers.includes(dotnetHelper)) {
            connection._dotnetHelpers.push(dotnetHelper);
            console.log("Chat Hub: Notification bar helper added to existing connection");
        }
    }
},

/**
 * Cleanup notification bar helper reference
 */
stopForNotificationBar: function () {
    console.log("Chat Hub: stopForNotificationBar called");
    
    // Remove notification bar helper from connection
    if (window._chatConnection && window._chatConnection._dotnetHelpers) {
        window._chatConnection._dotnetHelpers = window._chatConnection._dotnetHelpers.filter(
            helper => !helper._isNotificationBarHelper
        );
        console.log("Chat Hub: Notification bar helper removed from connection");
    }
    
    // Note: We don't stop the connection here because it might still be used by other components
},
```

**File:** `src/HC.Blazor/Components/Pages/Notification.razor`

Kết nối với chatHub và xử lý event:
```csharp
protected override async Task OnAfterRenderAsync(bool firstRender)
{
    if (firstRender && CurrentUser.IsAuthenticated)
    {
        _objRef = DotNetObjectReference.Create(this);
        // Initialize notification hub connection from JavaScript (browser-side)
        // This ensures cookies are sent correctly with the connection
        await JSRuntime.InvokeVoidAsync("notificationHub.start", _objRef);
        
        // NEW: Also initialize chat hub to receive chat unread count updates
        await JSRuntime.InvokeVoidAsync("chatHub.startForNotificationBar", _objRef);
    }
}

[JSInvokable]
public async Task OnChatUnreadCountChanged()  // NEW method
{
    try
    {
        await LoadChatUnreadCountAsync();
        // Force UI update - must use InvokeAsync to ensure it runs on the UI thread
        await InvokeAsync(StateHasChanged);
    }
    catch
    {
        // Log error if needed
    }
}

public void Dispose()
{
    // NEW: Stop chat hub for notification bar
    try
    {
        JSRuntime?.InvokeVoidAsync("chatHub.stopForNotificationBar");
    }
    catch
    {
        // Ignore errors during disposal
    }
    
    // Note: Don't dispose the notification hub SignalR connection here as it's shared with NotificationToast
    // The connection will be cleaned up when the page unloads
    _objRef?.Dispose();
}
```

## Cách thức hoạt động

### 1. Khi user mở trang chat
- Gọi API `/api/chat/contact`
- `ContactAppService.GetContactsAsync()` load `UnreadMessageCount` từ `ConversationMember.UnreadMessageCount`
- Badge hiển thị đúng số tin nhắn chưa đọc cho mỗi conversation

### 2. Khi có tin nhắn mới gửi đến user
**Flow:**
1. Backend increment `UnreadMessageCount` trong database (đã implement ở phần trước)
2. `ChatEventHandler.HandleEventAsync(ChatMessageEto)` được trigger
3. Gửi 2 SignalR events:
   - `ReceiveMessage` - để Chat1.razor hiển thị tin nhắn
   - `ChatUnreadCountChanged` - để Notification.razor update badge
4. Notification.razor nhận event, gọi `LoadChatUnreadCountAsync()`
5. Badge trên notification bar được update real-time

### 3. Khi user click vào conversation
- Gọi API `ResetUnreadCountAsync` để reset về 0 (đã implement ở phần trước)
- Badge được update về 0

## Kiểm tra

1. **Test API `/api/chat/contact`:**
   - Gửi tin nhắn từ user A đến user B
   - User B vào chat
   - Verify badge hiển thị đúng số unread

2. **Test Real-time update:**
   - User A vào trang bất kỳ (có Notification bar)
   - User B gửi tin nhắn cho user A
   - Verify badge trên notification bar của user A tự động update (không cần F5)

3. **Test click vào conversation:**
   - Click vào conversation có unread
   - Verify badge reset về 0

## Lưu ý

- Notification.razor giờ kết nối với 2 SignalR hubs: `notificationHub` và `chatHub`
- `chatHub` được reuse giữa nhiều components (Chat1, NotificationToast, Notification)
- Sử dụng `baseHub` để quản lý kết nối shared
- Helper được mark bằng `_isNotificationBarHelper` flag để phân biệt
