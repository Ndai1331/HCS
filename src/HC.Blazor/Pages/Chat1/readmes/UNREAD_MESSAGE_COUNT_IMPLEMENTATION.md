# Unread Message Count Implementation - Summary

## Overview
Đã thực hiện thành công chức năng đếm số tin nhắn chưa đọc (Unread Message Count) cho hệ thống chat.

## Thay đổi đã thực hiện

### 1. Tầng Domain (Entity)
**File:** `src/HC.Domain/Chat/Conversations/ConversationMember.cs`

- Thêm property `UnreadMessageCount` (int) vào entity ConversationMember
- Thêm các methods quản lý unread count:
  - `IncrementUnreadCount()` - Tăng số tin nhắn chưa đọc
  - `DecrementUnreadCount(int count)` - Giảm số tin nhắn chưa đọc
  - `ResetUnreadCount()` - Reset về 0
  - `SetUnreadCount(int count)` - Set giá trị cụ thể

### 2. Tầng Application Contracts (DTOs)

**Files:**
- `src/HC.Application.Contracts/Chat/Conversations/ConversationMemberDto.cs`
  - Thêm property `UnreadMessageCount`

- `src/HC.Application.Contracts/Chat/Conversations/UpdateUnreadCountInput.cs` (NEW)
  - Input DTO để update unread count

- `src/HC.Application.Contracts/Chat/Conversations/ResetUnreadCountInput.cs` (NEW)
  - Input DTO để reset unread count

- `src/HC.Application.Contracts/Chat/Conversations/TotalUnreadCountDto.cs` (NEW)
  - DTO để trả về tổng số tin nhắn chưa đọc

- `src/HC.Application.Contracts/Chat/Conversations/IConversationAppService.cs`
  - Thêm 3 methods mới:
    - `Task UpdateUnreadCountAsync(UpdateUnreadCountInput input)`
    - `Task ResetUnreadCountAsync(ResetUnreadCountInput input)`
    - `Task<TotalUnreadCountDto> GetTotalUnreadCountAsync()`

### 3. Tầng Application (Service)

**File:** `src/HC.Application/Chat/Conversations/ConversationAppService.cs`

- **SendMessageAsync**: Cập nhật để increment unread count cho tất cả recipients (trừ sender)
- **MapToConversationDtoAsync**: Cập nhật để map UnreadMessageCount từ ConversationMember entity
- **GetMembersAsync**: Cập nhật để map UnreadMessageCount vào DTO
- **UpdateUnreadCountAsync** (NEW): API để update unread count
- **ResetUnreadCountAsync** (NEW): API để reset unread count về 0
- **GetTotalUnreadCountAsync** (NEW): API để lấy tổng số unread count của user

### 4. Tầng HttpApi (Controller)

**File:** `src/HC.HttpApi/Chat/Conversations/ConversationController.cs`

Thêm 3 endpoints mới:
- `POST /api/chat/conversation/update-unread-count` - Update unread count
- `POST /api/chat/conversation/reset-unread-count` - Reset unread count
- `GET /api/chat/conversation/total-unread-count` - Get total unread count

### 5. Tầng EntityFrameworkCore (Database)

**Migration:** `AddUnreadMessageCountToConversationMember`

- Tạo cột `UnreadMessageCount` trong bảng `ChatConversationMembers`
- Default value: 0
- Cột này được add vào bảng ChatConversationMembers để track số tin nhắn chưa đọc của từng member trong từng conversation

### 6. Tầng Blazor UI

**File:** `src/HC.Blazor/Pages/Chat1/Chat1.razor.cs`

- **SetActiveAsync**: Cập nhật để gọi API reset unread count khi user click vào conversation
- **ProcessReceivedMessage**: Đã có logic increment UnreadMessageCount khi nhận tin nhắn mới
- UI đã hiển thị UnreadMessageCount dưới dạng badge (đã có sẵn)

**File:** `src/HC.Blazor/Components/Pages/Notification.razor`

- Thêm property `_totalChatUnreadCount` để track tổng số tin nhắn chưa đọc
- Thêm method `LoadChatUnreadCountAsync()` để load total unread count từ API
- Thêm badge hiển thị số unread chat icon trên thanh notification
- Cập nhật `OnUnreadCountChanged()` để reload chat unread count khi có thay đổi

## Cách thức hoạt động

### 1. Khi gửi tin nhắn mới
- Backend tự động increment `UnreadMessageCount` của tất cả recipients trong conversation
- Sender không bị increment

### 2. Khi nhận tin nhắn mới (real-time qua SignalR)
- UI tự động increment `UnreadMessageCount` của conversation tương ứng
- Badge trên chat list được update
- Total unread count trên notification bar cũng được update

### 3. Khi click vào conversation
- Gọi API `ResetUnreadCountAsync` để reset unread count về 0
- Badge trên UI được update về 0

### 4. Hiển thị tổng số unread
- Notification bar hiển thị badge với tổng số tin nhắn chưa đọc từ tất cả conversations
- Badge hiển thị dạng "99+" nếu quá 99

## Các API endpoints

```csharp
// Update unread count
POST /api/chat/conversation/update-unread-count
Body: { "conversationId": "guid", "incrementBy": 1 }

// Reset unread count
POST /api/chat/conversation/reset-unread-count
Body: { "conversationId": "guid" }

// Get total unread count
GET /api/chat/conversation/total-unread-count
Response: { "totalUnreadCount": 5 }
```

## Database Changes

Chạy migration để update database:
```bash
dotnet ef database update --project src/HC.EntityFrameworkCore --startup-project src/HC.HttpApi.Host
```

## Testing Checklist

- [ ] Send message và verify recipients' unread count được increment
- [ ] Verify UI hiển thị badge đúng số unread
- [ ] Click vào conversation và verify unread count được reset
- [ ] Verify total unread count trên notification bar
- [ ] Test với User conversations (1-1)
- [ ] Test với Group conversations
- [ ] Test với Project conversations
- [ ] Test với Task conversations

## Notes

- Unread count được lưu per-user, per-conversation trong bảng `ChatConversationMembers`
- Logic đã có sẵn từ trước nên việc implement chủ yếu là thêm backend API và connect với UI
- UI đã support hiển thị unread count badge nên chỉ cần update data source
