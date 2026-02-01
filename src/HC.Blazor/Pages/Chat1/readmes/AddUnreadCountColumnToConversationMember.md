LOGIC:Có bảng ChatConversationMembers (1 conversation có nhiều member) nhưng chưa biết tổng số tin nhắn chưa đọc của member đó là bao nhiêu.

==== tầng APPLICATION ====
src/HC.Application/
src/HC.Application/Contracts
src/HC.Domain/
src/HC.Domain/Shared
src/HC.EntityFrameworkCore/
src/HC.Api/

1. Thêm cột UnreadMessageCount vào bảng (conversation - member ) để biết member đó với conversation này đang có bao nhiêu tin nhắn chưa đọc 
2. API Update UnreadMessageCount 
3. API tổng số tin nhắn chưa đọc tất cả conversation của member đó
4. Run db migration HCDbcontext để tạo cột

===Tầng UI ==
1. Khi vào menu chat load UnreadMessageCount theo conversation kèm theo
2. Khi có tin nhắn mới update số UnreadMessageCount (conversation và member) 
3. HC.Blazor/Pages/Chat Thêm logic click vào Conversation thì update lại số UnreadMessageCount = 0 đối với member và conversation đó, nêu đang ở conversation đó thì tự cập nhật lại UnreadMessageCount = 0 của conversation 
4. Làm HC.Blazor/Components/Pages/Notification.razor thêm hiển thị số tin nhắn chưa đọc tổng các conversation của member đó giống thông báo



== Bổ sung thêm 
1. Khi gửi tin nhắn update số tin nhắn chưa đọc HC.Blazor/Components/Pages/Notification.razor realtime
2. Api /api/chat/contact chưa load được unreadMessageCount
