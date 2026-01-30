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




=====31/01/2026

- Sửa logic Gửi văn bản đang lưu lưu history -> lưu thêm vào bảng DocumentAssignments

1. DocumentHistories.Action = TRINH

2. Record mới lưu các giá trị 
DocumentAssignments.StepOrder = ORIGINAL
DocumentAssignments.ActionType = VIEW (tạo thêm 1 enum view để input gửi lên ) 
DocumentAssignments.Status = DONE
DocumentAssignments.AssignedAt  = Now
DocumentAssignments.ProcessedAt  = Now
DocumentAssignments.IsCurrent = true  
DocumentAssignments.StepId = null
DocumentAssignments.ReceiverUserId = user chỉ định

3. Blazor page DocumentAssignments.razor chỉ load ra các document ReceiverUserId = currentuser.Id  
4. Xem document thì tạo 1 blazor page mới làm giống trang DocumentDetail -> ViewDocumentDetail (chỉ xem thông tin và file ko cho phép sửa xoá)


