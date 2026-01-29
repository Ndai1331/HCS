Chức năng SendDocument to personal or deparments 
I. 
1. Ở menu /manage-documents chọn gửi văn bản đến cá nhân hoặc phòng ban
2. Chọn cá nhân hoặc phòng ban cần gửi bấm gửi -> Gọi Service SendDocumentAsync và block UI
    =====SendDocumentAsync Tầng Application & Entity framework

    1.Nếu gửi đến cá nhân (1 hoặc nhiều)
    -Tạo các record DocumentHistory : FromUser là currentuser.Id , ToUser là id các User
    -Gửi thông báo realtime tới các ToUser 
    2.Nếu gửi đến các phòng ban 
    -Lấy tất cả user thuộc phòng ban Id 
    -Tạo các record DocumentHistory : FromUser là currentuser.Id , ToUser là id các User thuộc các phòng ban lấy ở bước trên
    -Gửi thông báo realtime tới các ToUser 
3. Thành công thì đóng modal, lỗi thì hiển thị lỗi hệ thống

II. Tạo 1 page mới my-document.razor 
1. Giống trang /manage-documents nhưng liệt kê các văn bản đến (Logic là bảng DocumentHistory ToUser = CurrentUser)
