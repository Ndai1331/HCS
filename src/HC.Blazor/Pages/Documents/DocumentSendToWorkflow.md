Feature 1:
Tạo 1 menu mới trình ký trong menu văn bản 
Trang này bố cục như sau
- filter từ ngày , đến ngày
body chia thành 2 card 4 - 8 
4: 
- Tất cả văn bản
- Văn bản gửi đến tôi
- Tôi gửi đi
- Đang theo dõi 
8: 
Danh sách các văn bản giống (Document.razor , sourceType=1) dựa trên filter từ ngày , đến ngày


FEATURE 2:

# 📄 ĐẶC TẢ CHỨC NĂNG: GỬI VÀ TRÌNH KÝ THEO WORKFLOW

## 1. Mục tiêu

Xây dựng chức năng **Gửi và trình ký văn bản theo Workflow**:

-   Mỗi document tại 1 thời điểm chỉ chạy **1 workflow instance**
-   Workflow gồm nhiều bước (step), mỗi bước có 1 hoặc nhiều người xử
    lý/ký
-   Ký số **chưa triển khai thật**, tạm thời:
    -   User bấm **Yes** = đồng ý ký
    -   User bấm **No** = Trả về hoặc Từ chối

------------------------------------------------------------------------

## 2. Phạm vi

-   Áp dụng cho các document có quyền `CanSubmitForSigning`
-   Hỗ trợ:
    -   File user upload
    -   Hoặc file template của workflow
-   Hỗ trợ:
    -   Ký tuần tự (theo thứ tự step)
    -   (Chưa xử lý ký song song ở version này -- để future)

------------------------------------------------------------------------

## 3. Các bảng liên quan

### 3.1. Workflows

-   Định nghĩa workflow (tên, mô tả, loại workflow...)

### 3.2. WorkflowTemplates

-   Mỗi Workflow có **nhiều template**
-   Chỉ **1 template IsActive = true** được sử dụng

### 3.3. WorkflowStepTemplates

-   Định nghĩa các bước của workflow:
    -   Thứ tự step
    -   Tên step
    -   SLA / số ngày xử lý
    -   Có cho phép trả về bước trước hay không

### 3.4. WorkflowStepAssignments

-   Định nghĩa:
    -   Ở mỗi step: ai là người xử lý / người ký
-   Chỉ lấy các record **Active**

### 3.5. DocumentWorkflowInstances

Quan hệ:

    1 Document ↔ 1 DocumentWorkflowInstance (tại 1 thời điểm)
    1 DocumentWorkflowInstance ↔ N DocumentAssignments

Dùng để: - Ghi nhận document đang chạy workflow nào - Lưu: -
CurrentStep - Status (InProgress / Completed / Rejected / Returned /
Cancelled...)

### 3.6. DocumentWorkflowInstanceFiles

-   Lưu các file đính kèm của workflow + document
-   Chỉ để user view

### 3.7. DocumentWorkflowInstanceLogs

-   Ghi log mỗi lần user thao tác:
    -   Ký
    -   Trả về
    -   Từ chối
    -   Chuyển bước

### 3.8. DocumentAssignments

-   Mỗi record = 1 user được giao xử lý tại 1 step
-   Lưu:
    -   UserId
    -   StepIndex / StepId
    -   Status (Pending / Approved / Rejected / Returned)
    -   ResultFileId
    -   ActionAt

------------------------------------------------------------------------

## 4. Flow tổng quát

### 4.1. Bắt đầu workflow

1.  User click button **CanSubmitForSigning**
2.  Hiển thị modal chọn Workflow
3.  User xác nhận
4.  Hệ thống:
    -   Lấy WorkflowTemplate IsActive
    -   Lấy WorkflowStepTemplates
    -   Lấy WorkflowStepAssignments (Active)

------------------------------------------------------------------------

### 4.2. Xử lý file đầu vào

1.  Kiểm tra Workflow có file template không?
    -   Nếu có: hỏi dùng template hay file user upload
    -   Nếu không: bắt buộc dùng file user upload
2.  Tạo **File trình ký**
3.  Lưu:
    -   DocumentWorkflowInstanceFiles
    -   Gán file vào DocumentFileResultId

------------------------------------------------------------------------

### 4.3. Khởi tạo dữ liệu workflow

1.  Tạo DocumentWorkflowInstances:
    -   DocumentId
    -   WorkflowId
    -   CurrentStep = step 1
    -   Status = InProgress
2.  Tạo DocumentAssignments cho step 1:
    -   Status = Pending
3.  Ghi DocumentWorkflowInstanceLogs: Action = StartWorkflow
4.  Gửi thông báo cho user step 1

------------------------------------------------------------------------

## 5. Flow xử lý tại mỗi bước (User N)

### 5.1. User thao tác

User có 3 lựa chọn: - Yes (Ký) - No → Trả về - No → Từ chối

------------------------------------------------------------------------

### 5.2. Trường hợp YES (Ký)

1.  Update DocumentAssignments user N:

    -   Status = Approved
    -   ActionAt = Now
    -   ResultFileId = file sau ký (hiện tại lấy 4.2 để demo)

2.  Update DocumentWorkflowInstances:

    -   Nếu còn step tiếp: CurrentStep = N+1
    -   Nếu là step cuối: Status = Completed

3.  Ghi DocumentWorkflowInstanceLogs: Action = Approved

4.  Nếu chưa phải user cuối:

    -   Tạo DocumentAssignments cho user N+1
    -   Status = Pending
    -   Gửi thông báo (đa ngôn ngữ giống logic Notification.razor đang làm)

5.  Nếu là user cuối:

    -   Kết thúc workflow

------------------------------------------------------------------------

### 5.3. Trường hợp NO → Trả về

1.  Update DocumentAssignments:
    -   Status = Returned
2.  Update DocumentWorkflowInstances:
    -   Status = Returned
3.  Ghi Log: Action = Returned
4.  Kết thúc flow

------------------------------------------------------------------------

### 5.4. Trường hợp NO → Từ chối

1.  Update DocumentAssignments:
    -   Status = Rejected
2.  Update DocumentWorkflowInstances:
    -   Status = Rejected
3.  Ghi Log: Action = Rejected
4.  Kết thúc workflow

------------------------------------------------------------------------

## 6. Trạng thái

### 6.1. DocumentWorkflowInstances.Status

DRAFT / IN_PROGRESS / COMPLETED / REJECTED / CANCELLED

### 6.2. DocumentAssignments.Status

PENDING / DONE / REJECTED/ REVOKE

------------------------------------------------------------------------

## 7. Rule nghiệp vụ

-   1 Document chỉ có 1 workflow instance active tại 1 thời điểm
-   Chỉ user có assignment Pending mới được thao tác
-   Mỗi action phải:
    -   Update dữ liệu
    -   Ghi log
-   Workflow Completed / Rejected thì không cho thao tác tiếp

------------------------------------------------------------------------

## 8. API gợi ý

-   POST /documents/{id}/submit-workflow
-   GET /documents/{id}/workflow-instance
-   POST /workflow-instances/{id}/action
    -   Body: { action: approve \| return \| reject }

------------------------------------------------------------------------

## 9. Ghi chú

-   Version hiện tại: mock ký bằng Yes / No
-   Future:
    -   Ký song song
    -   SLA / overdue
    -   Delegate / reassign





=> YÊU CẦU CHUNG

Code dễ maintain, chia ra các component nếu dùng chung được 
Sử dụng các thuộc tính modal giống các page khác

