Chức năng mới "Gửi và trình ký" theo workflow: 
Chức năng ký số tạm thời chưa làm mock chức năng nếu ký bấm nút "Yes" ngược lại "No"


Bắt đầu flow
1. Người dùng click vào button CanSubmitForSigning hiển thị modal chọn workflow tương ứng bấm xác nhận
2. Dựa vào flow được chọn lấy WorkflowTemplates IsActive (mỗi workflow có nhiều WorkflowTemplates nhưng chỉ 1 template được active)
3. Hệ thống sẽ lấy ra các step từ WorkflowStepTemplates để thực hiện auto các bước. Mỗi step sẽ có WorkflowStepAssignments tương ứng (ai là người ký , người xử lý)
4. DocumentWorkflowInstances là nơi cập nhật trạng thái của workflow đang tới bước nào và status cuối cùng của workflow (hoàn thành mỗi bước thì lưu lại trạng thái bước đó)
5. Khi có các thông tin trên bắt đầu xử lý như sau 



Bảng liên quan: 
-Workflows : Định nghĩa workflow
-WorkflowTemplates : Mẫu workflow (Có thể ký tuần tự ,hay có thể ký song song)
-WorkflowStepTemplates : Các bước thực hiện workflow (Thứ tự các bước, tên bước, số ngày xử lý, có cho phép trả văn bản ngược lại hay không)
-WorkflowStepAssignments : Định nghĩa các bước ai thực hiện, ai là người ký chính (Chỉ lấy các step active)
- DocumentWorkflowInstances : 
1 document ↔ 1 document_workflow_instance (tại 1 thời điểm) ↔ N document_assignments
Bảng này dùng để:
Ghi nhận 1 văn bản đang chạy workflow nào
Biết:đang ở bước nào, trạng thái gì,đã hoàn thành chưa
- DocumentAssignments: