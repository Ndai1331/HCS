# Chat SignalR integration cho mobile

## 1) SignalR endpoint

- **Hub URL:** `/chatHub`
- Hub được map trong Blazor service (`HC.Blazor`), ví dụ local: `https://localhost:44302/chatHub`
- Transport bật: `WebSockets` và `LongPolling`

## 2) Authentication hiện tại

- `ChatHub` có `[Authorize]` nên bắt buộc user đã xác thực.
- Hệ thống hiện tại đang cấu hình authentication chính bằng **Cookie + OpenIdConnect** cho Blazor host.
- Server dùng `ClaimTypes.NameIdentifier` làm `UserIdentifier` để route message theo user.

> Lưu ý cho mobile:
> - Nếu mobile gọi SignalR trực tiếp, cần đảm bảo request hub có context xác thực hợp lệ.
> - Với code hiện tại, luồng chuẩn nhất là xác thực theo session/cookie của Blazor host.
> - Nếu muốn mobile dùng access token Bearer thuần (không cookie), cần bổ sung cấu hình JwtBearer cho Hub handshake.

## 3) Event mobile cần subscribe

Server hiện phát các event chính sau qua `Clients.User(targetUserId)`:

1. `ReceiveMessage`
   - Payload:
     - `id` (Guid)
     - `conversationId` (Guid?)
     - `senderUserId` (Guid)
     - `senderUsername` (string)
     - `senderName` (string)
     - `senderSurname` (string)
     - `text` (string)
     - `messageDate` (UTC datetime)

2. `MessageDeleted`
   - Payload: `messageId` (Guid)

3. `ConversationDeleted`
   - Payload: `userId` (Guid)

4. `ConversationCreated`
   - Payload:
     - `conversationId` (Guid)
     - `type` (User/Group/Project/Task)
     - `conversationName` (string?)
     - `creatorUserId` (Guid)
     - `creatorUserName` (string)
     - `creatorName` (string)
     - `creatorSurname` (string)
     - `createdDate` (UTC datetime)

## 4) Gửi tin nhắn từ mobile

Hiện tại Hub **không expose method** để client gửi message lên server.

Mobile gửi tin bằng REST API:

- `POST /api/chat/conversation/send-message`
  - body:
    ```json
    {
      "conversationId": "GUID",
      "message": "Noi dung"
    }
    ```

Sau khi API ghi DB thành công, backend publish distributed event và đẩy realtime qua SignalR cho user nhận.

## 5) Quy trình tích hợp mobile đề xuất

1. Login lấy context xác thực hợp lệ (theo cơ chế server hỗ trợ).
2. Mở SignalR connection tới `/chatHub`.
3. Subscribe 4 event: `ReceiveMessage`, `MessageDeleted`, `ConversationDeleted`, `ConversationCreated`.
4. Khi user gửi tin: gọi REST `POST /api/chat/conversation/send-message`.
5. Đồng bộ UI theo event nhận được từ hub.

## 6) Checklist backend nếu muốn hỗ trợ mobile-native tốt hơn

- [ ] Bổ sung JwtBearer auth cho `chatHub`/`notificationHub` (nhận access token trong handshake).
- [ ] Cho phép `[Authorize(AuthenticationSchemes = "Cookies,Bearer")]` cho Hub.
- [ ] Public tài liệu contract payload (OpenAPI + SignalR event schema) cho mobile.
- [ ] Thêm endpoint health/check cho hub negotiation để QA test nhanh.
