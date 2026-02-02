# Refactor UI for Mobile - Facebook Messenger Style

## Mô tả
Chuyển đổi UI chat sang phong cách Facebook Messenger cho màn hình mobile với 3 view chính:
- List conversation
- Chat conversation
- Conversation information

## Các thay đổi đã thực hiện

### 1. Backend (Chat1.razor.cs)
- ✅ Thêm `MobileViewType` enum với 3 giá trị: `ConversationList`, `ChatConversation`, `ConversationInfo`
- ✅ Thêm property `CurrentMobileView` để quản lý view hiện tại
- ✅ Thêm property `IsMobileMode` để detect mobile mode
- ✅ Thêm các methods:
  - `SetMobileMode(bool isMobile)` - Được gọi từ JS để set mobile mode
  - `ShowConversationListAsync()` - Hiển thị list conversation (mobile)
  - `ShowChatConversationAsync()` - Hiển thị chat conversation (mobile)
  - `ShowConversationInfoAsync()` - Hiển thị conversation info (mobile)
  - `HideConversationInfoAsync()` - Ẩn conversation info, quay về chat (mobile)
- ✅ Cập nhật `SetActiveAsync()` để tự động chuyển sang chat view trên mobile khi click conversation
- ✅ Thêm mobile detection trong `OnAfterRenderAsync()` để detect screen size

### 2. Frontend (Chat1.razor)
- ✅ Thêm JavaScript để detect mobile mode dựa trên screen width (< 768px)
- ✅ Thêm CSS cho mobile view (`.mobile-view-hidden`, `.mobile-view-visible`)
- ✅ Thêm conditional rendering cho 3 sections:
  - **Conversation List**: Ẩn trên mobile khi không ở view `ConversationList`
  - **Chat Conversation**: Ẩn trên mobile khi không ở view `ChatConversation`, thêm nút back button
  - **Conversation Info**: Ẩn trên mobile khi không ở view `ConversationInfo`
- ✅ Thêm back button ở Chat conversation header (chỉ hiển thị trên mobile)
- ✅ Cập nhật InfoBox parameters để hỗ trợ mobile navigation

### 3. InfoBox Component
- ✅ Thêm parameter `IsMobileMode` để biết có đang ở mobile mode không
- ✅ Thêm parameter `HideInfoBoxAsync` callback để quay về chat
- ✅ Thêm back button ở đầu InfoBox (chỉ hiển thị trên mobile)

## Hành vi UI

### Khi vào trang chat không có param conversation id (mobile)
- ✅ Hiển thị **List conversation**
- Click conversation nào → Hiển thị **Chat conversation** của conversation đó
- Ẩn **List conversation**, hiển thị nút back để quay lại

### Khi vào trang chat có param conversation id (mobile)
- ✅ Hiển thị **Chat conversation** của conversation đó
- Ẩn **List conversation** và **Conversation Info**
- Hiển thị nút back để quay lại **List conversation**

### Khi click vào Info icon (mobile)
- ✅ Ẩn **Chat conversation**
- Hiển thị **Conversation Info**
- Hiển thị nút back để quay lại **Chat conversation**

### Desktop mode
- ✅ Hiển thị cả 3 sections như trước (List | Chat | Info)
- Không có back buttons
- Không có conditional hiding

## Responsive Breakpoints
- **Mobile**: < 768px (Bootstrap `col-md` breakpoint)
- **Desktop**: ≥ 768px

## Kỹ thuật sử dụng
- Blazor conditional rendering với `@if` và CSS classes
- JavaScript interop để detect screen size changes
- State management với `CurrentMobileView` enum
- CSS media queries với Bootstrap grid system

## Testing cần thực hiện
- [ ] Test trên mobile real device (iOS/Android)
- [ ] Test responsive trên tablet (768px - 991px)
- [ ] Test navigation flow giữa các view
- [ ] Test back buttons functionality
- [ ] Test landscape/portrait orientation changes
- [ ] Test với conversation có params URL
- [ ] Test tạo conversation mới trên mobile

---

## Yêu cầu ban đầu
Bây giờ mọi thứ hoàn thiện cho Windows desktop rồi nhưng chưa làm ổn cho mobile, tôi muốn làm UI mobile giống messenger facebook

UI sẽ có 3 mục chính sau: List conversation -> chat conversation -> infomation conversation

1. Khi chế độ màn hình mobile vào trang chat ko có param conversation id
   - Hiển thị danh List conversation
   - Click conversation nào thì hiển thị chat conversation đó và ẩn list conversation (thêm nút quay lại conversation)

2. Khi chế độ màn hình mobile vào trang chat có param conversation id
   - Hiển thị danh chat của conversation đó và ẩn list conversation & infomation conversation (thêm nút quay lại conversation)

3. infomation conversation chỉ có ở chat conversation và khi click vào infomation conversation thì ẩn chat hiển thị inforconversation và có button quay lại chat conversation

Tại 1 thời điểm chỉ show 1 mục duy nhất (tham khảo messenger facebook mobile)
