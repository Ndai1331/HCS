# Chức năng Trình ký văn bản (Document Signing / Submit to Workflow)

> Tài liệu kỹ thuật đầy đủ mô tả toàn bộ chức năng đã triển khai, bao gồm các bảng liên quan, files đã thay đổi/tạo mới, logic nghiệp vụ, và giao diện.

---

## 1. Tổng quan

Chức năng **Trình ký** cho phép người dùng:
- Xem danh sách văn bản liên quan đến quy trình trình ký (đã gửi, đã nhận, đang theo dõi)
- Tạo mới trình ký: chọn văn bản hoặc dùng file mẫu từ WorkflowTemplate, chọn quy trình, nhập nội dung trình ký, đính kèm file
- Xử lý trình ký: Duyệt (Approve), Trả về (Return), Từ chối (Reject)
- Ghi lịch sử (DocumentHistory) khi gửi trình ký
- Gửi thông báo (Notification) cho người nhận

---

## 2. Các bảng/entity liên quan

### 2.1 Document (Văn bản)
| Cột | Kiểu | Mô tả |
|-----|------|-------|
| Id | Guid | PK |
| No | string? | Số văn bản |
| Title | string | Tiêu đề (bắt buộc) |
| CurrentStatus | string? | Trạng thái hiện tại |
| CompletedTime | DateTime | Thời gian hoàn thành |
| StorageNumber | string | Số lưu trữ (bắt buộc, max 50) |
| IncommingDate | DateTime | Ngày đến |
| FieldId | Guid? | FK -> MasterData (Lĩnh vực) |
| UnitId | Guid? | FK -> Unit |
| WorkflowId | Guid? | FK -> Workflow |
| StatusId | Guid? | FK -> MasterData (Trạng thái) |
| TypeId | Guid | FK -> MasterData (Loại VB, bắt buộc) |
| UrgencyLevelId | Guid | FK -> MasterData (Mức độ khẩn, bắt buộc) |
| SecrecyLevelId | Guid | FK -> MasterData (Mức độ mật, bắt buộc) |
| **SourceType** | **DocumentSourceType** | **0=Archive, 1=Personal, 3=Workflow** |
| TenantId | Guid? | Multi-tenant |

> **Enum `DocumentSourceType`** (file: `HC.Domain.Shared/Documents/DocumentConsts.cs`):
> - `Archive = 0` — Văn thư lưu trữ
> - `Personal = 1` — Văn bản của tôi
> - `Workflow = 3` — Văn bản tạo từ quy trình trình ký (MỚI)

### 2.2 DocumentFile (File văn bản)
| Cột | Kiểu | Mô tả |
|-----|------|-------|
| Id | Guid | PK |
| DocumentId | Guid? | FK -> Document |
| Name | string | Tên file |
| Path | string? | Đường dẫn blob storage |
| Hash | string? | SHA256 hash |
| IsSigned | bool | Đã ký chưa |
| UploadedAt | DateTime | Thời gian upload |
| TenantId | Guid? | Multi-tenant |

### 2.3 DocumentWorkflowInstance (Instance quy trình trình ký)
| Cột | Kiểu | Mô tả |
|-----|------|-------|
| Id | Guid | PK |
| DocumentId | Guid | FK -> Document |
| WorkflowId | Guid | FK -> Workflow |
| WorkflowTemplateId | Guid | FK -> WorkflowTemplate |
| CurrentStepId | Guid | FK -> WorkflowStepTemplate (bước hiện tại) |
| Status | string | IN_PROGRESS, COMPLETED, REJECTED, RETURNED, CANCELLED |
| StartedAt | DateTime | Thời gian bắt đầu |
| CompletedAt | DateTime | Thời gian hoàn thành |
| TenantId | Guid? | Multi-tenant |

### 2.4 DocumentWorkflowInstanceFile (File đính kèm quy trình)
| Cột | Kiểu | Mô tả |
|-----|------|-------|
| Id | Guid | PK |
| DocumentWorkflowInstanceId | Guid | FK -> DocumentWorkflowInstance |
| DocumentFileId | Guid | FK -> DocumentFile |
| TenantId | Guid? | Multi-tenant |

### 2.5 DocumentAssignment (Phân công xử lý)
| Cột | Kiểu | Mô tả |
|-----|------|-------|
| Id | Guid | PK |
| DocumentId | Guid | FK -> Document |
| StepId | Guid | FK -> WorkflowStepTemplate |
| ReceiverUserId | Guid | User nhận xử lý |
| StepOrder | int | Thứ tự bước |
| ActionType | string | PROCESS / SIGN |
| Status | string | PENDING, DONE, REJECTED, REVOKE |
| AssignedAt | DateTime | Thời gian phân công |
| CompletedAt | DateTime | Thời gian hoàn thành |
| IsCurrent | bool | Đang ở bước hiện tại |
| DocumentFileId | Guid? | FK -> DocumentFile (file ký) |
| CreatorId | Guid? | User tạo (ABP audit) |
| TenantId | Guid? | Multi-tenant |

### 2.6 DocumentWorkflowInstanceLogs (Log hành động)
| Cột | Kiểu | Mô tả |
|-----|------|-------|
| Id | Guid | PK |
| DocumentWorkflowInstanceId | Guid | FK |
| DocumentAssignmentId | Guid? | FK |
| UserId | Guid? | User thực hiện |
| Action | string | START_WORKFLOW, APPROVED, RETURNED, REJECTED |
| Role | string | Initiator, Processor |
| Note | string? | Ghi chú |
| NewStatus | string | Trạng thái mới |
| PreviousStatus | string? | Trạng thái trước |

### 2.7 DocumentHistory (Lịch sử văn bản)
| Cột | Kiểu | Mô tả |
|-----|------|-------|
| Id | Guid | PK |
| DocumentId | Guid | FK -> Document |
| **FromUser** | **Guid?** | **User gửi (current user khi trình ký)** |
| **ToUser** | **Guid** | **User nhận (= DocumentAssignment.ReceiverUserId)** |
| **Action** | **string** | **SUBMIT_SIGNING (khi gửi trình ký)** |
| **Comment** | **string?** | **Nội dung trình ký (SigningContent)** |
| TenantId | Guid? | Multi-tenant |

### 2.8 Workflow, WorkflowTemplate, WorkflowStepTemplate, WorkflowStepAssignment
| Entity | Mô tả |
|--------|-------|
| Workflow | Quy trình (Name, v.v.) |
| WorkflowTemplate | Mẫu quy trình (Name, WorkflowId, **WordTemplatePath**, IsActive) |
| WorkflowStepTemplate | Bước trong mẫu (Name, Order, Type, SLADays, AllowReturn, IsActive) |
| WorkflowStepAssignment | User mặc định cho bước (StepId, DefaultUserId, IsPrimary, IsActive) |

### 2.9 MasterData
| Field | Mô tả |
|-------|-------|
| Type (string) | LOAI_VB, MUC_DO_KHAN, MUC_DO_MAT, LINH_VUC_VB, TRANG_THAI_VB |
| Dùng cho | Default values khi tạo Document từ workflow template |

### 2.10 Notification / NotificationReceiver
- Dùng `IDistributedEventBus` để gửi `NotificationCreatedEto`
- Thông báo gửi cho user nhận assignment khi bắt đầu workflow, chuyển bước, hoàn thành, trả về, từ chối

---

## 3. Enum & Constants

### DocumentSourceType (`HC.Domain.Shared/Documents/DocumentConsts.cs`)
```csharp
public enum DocumentSourceType
{
    Archive = 0,   // Văn thư lưu trữ
    Personal = 1,  // Văn bản của tôi
    Workflow = 3   // Văn bản tạo từ quy trình trình ký
}
```

### DocumentSigningFilterMode (`HC.Application.Contracts/DocumentWorkflowInstances/`)
```csharp
public enum DocumentSigningFilterMode
{
    All = 0,       // Tất cả = Union(SentToMe, SentByMe, Following)
    SentToMe = 1,  // DocumentAssignment.ReceiverUserId = currentUserId
    SentByMe = 2,  // DocumentAssignment.CreatorId = currentUserId
    Following = 3  // Chưa có logic
}
```

### DocumentAssignmentStatus
```csharp
public enum DocumentAssignmentStatus { PENDING, DONE, REJECTED, REVOKE }
```

### EventType (Notification)
```csharp
WORKFLOW_RETURNED, WORKFLOW_REJECTED, WORKFLOW_ACTION
```

---

## 4. DTOs

### SubmitToWorkflowInput (`HC.Application.Contracts/DocumentWorkflowInstances/`)
```csharp
public class SubmitToWorkflowInput
{
    public Guid? DocumentId { get; set; }              // Nullable khi dùng template file
    [Required] public Guid WorkflowId { get; set; }
    public bool UseWorkflowTemplateFile { get; set; }  // Dùng file mẫu từ WorkflowTemplate
    public bool UseTemplateFile { get; set; }
    public Guid? DocumentFileId { get; set; }
    public List<Guid>? AttachedFileIds { get; set; }   // DocumentFile IDs đính kèm
    public string? SigningContent { get; set; }         // Nội dung trình ký -> DocumentHistory.Comment
}
```

### WorkflowSubmitInfoDto
```csharp
public class WorkflowSubmitInfoDto
{
    public Guid WorkflowId { get; set; }
    public string WorkflowName { get; set; }
    public Guid WorkflowTemplateId { get; set; }
    public string WorkflowTemplateName { get; set; }
    public string? WordTemplatePath { get; set; }       // File path mẫu quy trình
    public bool HasTemplateFile { get; set; }           // = !IsNullOrWhiteSpace(WordTemplatePath)
    public List<WorkflowStepDetailDto> Steps { get; set; }
}
```

### WorkflowActionInput
```csharp
public class WorkflowActionInput
{
    public Guid DocumentWorkflowInstanceId { get; set; }
    public Guid DocumentAssignmentId { get; set; }
    public string Action { get; set; }  // APPROVED, RETURNED, REJECTED
    public string? Note { get; set; }
}
```

### DocumentSigningItemDto
```csharp
public class DocumentSigningItemDto
{
    public Guid DocumentId { get; set; }
    public string DocumentTitle { get; set; }
    public string? DocumentNo { get; set; }
    public string StorageNumber { get; set; }
    public DateTime IncommingDate { get; set; }
    public string? StatusName { get; set; }
    public string? WorkflowName { get; set; }
    public string? WorkflowStatus { get; set; }
    public string? CurrentStepName { get; set; }
    public int CurrentStepOrder { get; set; }
    public int TotalSteps { get; set; }
    public string? MyAssignmentStatus { get; set; }
    public Guid? MyAssignmentId { get; set; }
    public Guid? WorkflowInstanceId { get; set; }
    public bool CanAct { get; set; }
}
```

### GetDocumentSigningListInput
```csharp
public class GetDocumentSigningListInput : PagedAndSortedResultRequestDto
{
    public string? FilterText { get; set; }
    public DocumentSigningFilterMode FilterMode { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
}
```

### DocumentSigningPageResultDto
```csharp
public class DocumentSigningPageResultDto
{
    public List<DocumentSigningItemDto> Items { get; set; }
    public long TotalCount { get; set; }
    public int AllCount { get; set; }
    public int SentToMeCount { get; set; }
    public int SentByMeCount { get; set; }
    public int FollowingCount { get; set; }
}
```

---

## 5. API / AppService Methods

### IDocumentWorkflowInstancesAppService
| Method | Mô tả |
|--------|-------|
| `GetWorkflowSubmitInfoAsync(Guid workflowId)` | Lấy thông tin workflow + steps + assigned users cho modal |
| `SubmitToWorkflowAsync(SubmitToWorkflowInput)` | Gửi trình ký (tạo instance, assignments, history, files, notification) |
| `ProcessWorkflowActionAsync(WorkflowActionInput)` | Xử lý hành động: Approve/Return/Reject |
| `GetDocumentSigningListAsync(GetDocumentSigningListInput)` | Lấy danh sách trình ký với filter |
| `GetWorkflowLookupAsync(LookupRequestDto)` | Lookup danh sách workflow |

---

## 6. Logic nghiệp vụ chi tiết

### 6.1 Gửi trình ký (`SubmitToWorkflowAsync`)

**Flow:**

1. **Validate** WorkflowId bắt buộc
2. **Lấy workflow info** (steps, template, assigned users)
3. **Xử lý Document:**
   - **Nếu `UseWorkflowTemplateFile = true`:**
     - Kiểm tra WorkflowTemplate có `WordTemplatePath`
     - Lấy default MasterData (DocumentType, UrgencyLevel, SecrecyLevel)
     - Tạo **Document mới** với `SourceType = Workflow (3)`, title = tên template, storageNumber = `WF-yyyyMMddHHmmss`
     - Tạo **DocumentFile** từ `WordTemplatePath`
   - **Nếu `UseWorkflowTemplateFile = false`:**
     - `DocumentId` bắt buộc (chọn từ văn bản của tôi, SourceType=Personal)
4. **Kiểm tra** không có workflow instance đang hoạt động cho document
5. **Tạo `DocumentWorkflowInstance`** (status = IN_PROGRESS, currentStep = step 1)
6. **Tạo `DocumentAssignment`** cho mỗi user ở step 1 (status = PENDING)
7. **Tạo `DocumentHistory`** cho mỗi assignment:
   - `FromUser` = current user (người gửi trình)
   - `ToUser` = assignment.ReceiverUserId (người nhận)
   - `Action` = "SUBMIT_SIGNING"
   - `Comment` = input.SigningContent (nội dung trình ký)
8. **Tạo log** (DocumentWorkflowInstanceLogs: START_WORKFLOW)
9. **Tạo `DocumentWorkflowInstanceFile`** cho các file đính kèm (AttachedFileIds)
10. **Attach template file** nếu tạo từ template
11. **Gửi notification** cho users ở step 1

### 6.2 Xử lý trình ký (`ProcessWorkflowActionAsync`)

**Actions:**
- **APPROVED**: Đánh dấu assignment = DONE, nếu tất cả assignment step hiện tại = DONE → chuyển sang step tiếp theo (tạo assignments mới) hoặc hoàn thành workflow
- **RETURNED**: Đánh dấu assignment = REJECTED, revoke các assignment khác cùng step, chuyển workflow status = RETURNED
- **REJECTED**: Tương tự RETURNED, workflow status = REJECTED

### 6.3 Danh sách trình ký (`GetDocumentSigningListAsync`)

**Filter logic:**
| Mode | Logic |
|------|-------|
| **All** | Union(SentToMe, SentByMe, Following) |
| **SentToMe** | `DocumentAssignment.ReceiverUserId = currentUserId` |
| **SentByMe** | `DocumentAssignment.CreatorId = currentUserId` |
| **Following** | Chưa implement (count = 0) |

**Thêm filter:**
- `FromDate` / `ToDate` theo `Document.IncommingDate`
- `FilterText` tìm theo `Document.Title` hoặc `Document.StorageNumber`
- Paging + Sorting

---

## 7. Giao diện (Blazor)

### 7.1 Trang `/document-signing` (`DocumentSigning.razor` + `.razor.cs`)

**Route:** `/document-signing`
**Permission:** `HCPermissions.Documents.SubmitForSigning`
**Menu:** "Trình ký" (Menu:DocumentSigning)

**Layout:**
```
┌─────────────────────────────────────────────────┐
│ PageHeader: "Trình ký văn bản" [Tạo trình ký]  │
├─────────────────────────────────────────────────┤
│ Filter: FromDate(60d ago) | ToDate(now) | Search│
├──────────┬──────────────────────────────────────┤
│ Left 3   │ Right 9                              │
│          │                                      │
│ ○ Tất cả │ DataGrid:                            │
│   (All)  │  - Actions (Xử lý / Xem)            │
│ ○ Gửi    │  - Title + No + StorageNumber        │
│   đến tôi│  - Workflow Status (badge)           │
│ ○ Tôi    │  - Current Step (x/y)                │
│   gửi đi │  - My Status (badge)                 │
│ ○ Đang   │  - Status                            │
│   theo dõi│  - IncommingDate                    │
│          │  - Workflow Name                      │
└──────────┴──────────────────────────────────────┘
```

**Default values:**
- `FromDate` = 60 ngày trước
- `ToDate` = ngày hiện tại

### 7.2 Modal "Tạo trình ký" (Submit to Workflow)

**Flow trong modal:**

```
1. Chọn Workflow (dropdown, bắt buộc)
      ↓
2. Nếu WorkflowTemplate có file path:
   ┌─────────────────────────────────────┐
   │ ☐ Sử dụng file mẫu quy trình      │
   │ 📄 template-file.docx              │
   │ /path/to/template                  │
   └─────────────────────────────────────┘
      ↓
3. Nếu KHÔNG tick template file:
   [Chọn văn bản từ văn bản của tôi] (Autocomplete)
   Hiển thị info văn bản đã chọn
      ↓
4. Hiển thị Workflow Steps:
   [1] Bước 1 (Xử lý) - User A ⭐, User B
   [2] Bước 2 (Ký) - User C ⭐
      ↓
5. Nội dung trình ký (textarea)
      ↓
6. Đính kèm file (FilePicker multiple, Upload button)
   ✅ file1.pdf
   ✅ file2.docx
      ↓
7. [Hủy]  [Trình]
```

**Button "Trình" disabled khi:**
- Chưa chọn workflow HOẶC
- Chưa chọn văn bản (khi không dùng template file) HOẶC
- WorkflowSubmitInfo chưa load xong

**Reset khi mở modal:**
- Clear tất cả: document selection, workflow, template file checkbox, signing content, uploaded files
- Increment `ModalResetKey` (force re-render Autocomplete via `@key`)
- Clear FilePicker

### 7.3 Modal "Xử lý trình ký" (Workflow Action)

```
┌──────────────────────────────────┐
│ Xử lý trình ký                  │
│                                  │
│ 📄 Văn bản: Title - #No         │
│ Bước hiện tại: Step X (x/y)     │
│                                  │
│ Chọn hành động: *                │
│ ○ ✅ Duyệt                       │
│ ○ ↩️ Trả về                      │
│ ○ ❌ Từ chối                      │
│                                  │
│ Ghi chú: [textarea]             │
│                                  │
│ [Hủy]  [Xác nhận]               │
└──────────────────────────────────┘
```

---

## 8. Files đã tạo/thay đổi

### 8.1 Domain.Shared
| File | Thay đổi |
|------|----------|
| `Documents/DocumentConsts.cs` | Thêm `Workflow = 3` vào enum `DocumentSourceType` |
| `Notifications/EventType.cs` | Thêm `WORKFLOW_RETURNED`, `WORKFLOW_REJECTED`, `WORKFLOW_ACTION` |
| `Localization/HC/vi.json` | Thêm ~60 keys dịch Việt (Menu, filter, modal, status, action, v.v.) |
| `Localization/HC/en.json` | Thêm ~60 keys dịch Anh tương ứng |

### 8.2 Application.Contracts
| File | Thay đổi |
|------|----------|
| `DocumentWorkflowInstances/SubmitToWorkflowInput.cs` | `DocumentId` nullable, thêm `UseWorkflowTemplateFile`, `SigningContent` |
| `DocumentWorkflowInstances/WorkflowStepDetailDto.cs` | Tạo mới: `WorkflowStepDetailDto`, `WorkflowStepUserDto`, `WorkflowSubmitInfoDto`, `DocumentWorkflowStatusDto`, `DocumentAssignmentInfoDto` |
| `DocumentWorkflowInstances/WorkflowActionInput.cs` | Tạo mới |
| `DocumentWorkflowInstances/DocumentSigningItemDto.cs` | Tạo mới |
| `DocumentWorkflowInstances/GetDocumentSigningListInput.cs` | Tạo mới |
| `DocumentWorkflowInstances/DocumentSigningPageResultDto.cs` | Tạo mới |
| `DocumentWorkflowInstances/DocumentSigningFilterMode.cs` | Tạo mới (enum) |

### 8.3 Application
| File | Thay đổi |
|------|----------|
| `DocumentWorkflowInstances/DocumentWorkflowInstancesAppService.Extended.cs` | Logic chính: GetWorkflowSubmitInfoAsync, SubmitToWorkflowAsync, ProcessWorkflowActionAsync, GetDocumentSigningListAsync, helper methods |

**Dependencies injected:**
- `IRepository<WorkflowStepAssignment>`, `IDocumentAssignmentRepository`, `DocumentAssignmentManager`
- `IDocumentWorkflowInstanceLogsRepository`, `DocumentWorkflowInstanceLogsManager`
- `INotificationRepository`, `INotificationReceiverRepository`, `IDistributedEventBus`
- `IRepository<IdentityUser>`, `IRepository<MasterData>`
- `IRepository<DocumentWorkflowInstanceFile>`, `IRepository<DocumentFile>`
- `DocumentManager`, `DocumentHistoryManager`

### 8.4 HttpApi
| File | Thay đổi |
|------|----------|
| `Controllers/DocumentWorkflowInstances/DocumentWorkflowInstanceController.Extended.cs` | API endpoints cho các methods trên |

### 8.5 Blazor
| File | Thay đổi |
|------|----------|
| `Menus/HCMenuContributor.cs` | Thêm menu "Trình ký" → `/document-signing` |
| `Pages/Documents/DocumentSigning.razor` | Trang chính: filter, datagrid, 2 modals |
| `Pages/Documents/DocumentSigning.razor.cs` | Code-behind: state, data loading, events, submit/action logic |

---

## 9. Localization Keys (đầy đủ)

### vi.json
```
Menu:DocumentSigning = "Trình ký"
DocumentSigning = "Trình ký văn bản"
FromDate = "Từ ngày"
ToDate = "Đến ngày"
SearchByTitleOrNumber = "Tìm theo tiêu đề hoặc số văn bản"
AllDocuments = "Tất cả văn bản"
SentToMe = "Văn bản gửi đến tôi"
SentByMe = "Tôi gửi đi"
Following = "Đang theo dõi"
SubmitForSigning = "Trình ký"
ProcessSigning = "Xử lý trình ký"
WorkflowStatus = "Trạng thái quy trình"
NotStarted = "Chưa bắt đầu"
MyStatus = "Trạng thái của tôi"
CurrentStep = "Bước hiện tại"
Days = "ngày"
SelectWorkflow = "Chọn quy trình"
WorkflowSteps = "Các bước quy trình"
NoUsersAssigned = "Chưa phân công người xử lý"
SelectAction = "Chọn hành động"
Approve = "Duyệt"
ApproveDescription = "Duyệt văn bản và chuyển sang bước tiếp theo"
Return = "Trả về"
ReturnDescription = "Trả văn bản về người gửi để chỉnh sửa"
Reject = "Từ chối"
RejectDescription = "Từ chối văn bản, kết thúc quy trình"
ConfirmApprove = "Bạn có chắc chắn muốn duyệt văn bản này?"
ConfirmReturn = "Bạn có chắc chắn muốn trả về văn bản này?"
ConfirmReject = "Bạn có chắc chắn muốn từ chối văn bản này?"
ConfirmSubmitForSigning = "Bạn có chắc chắn muốn trình ký văn bản này?"
EnterNoteOptional = "Nhập ghi chú (không bắt buộc)"
PleaseSelectWorkflow = "Vui lòng chọn quy trình"
NoActiveAssignment = "Không có phân công đang hoạt động"
WorkflowSubmittedSuccessfully = "Trình ký thành công"
DocumentApprovedSuccessfully = "Duyệt văn bản thành công"
DocumentReturnedSuccessfully = "Trả về văn bản thành công"
DocumentRejectedSuccessfully = "Từ chối văn bản thành công"
ViewDetail = "Xem chi tiết"
WorkflowAssigned = "Phân công trình ký"
WorkflowAssignedMessage = "...đã được phân công..."
WorkflowStepCompleted = "Bước quy trình hoàn thành"
WorkflowCompleted = "Quy trình đã hoàn thành"
WorkflowReturned = "Văn bản đã bị trả về"
WorkflowRejected = "Văn bản đã bị từ chối"
CreateSigning = "Tạo trình ký"
SelectDocument = "Chọn văn bản"
AttachFiles = "Đính kèm file"
Submit = "Trình"
FirstStepMustHaveAssignedUsers = "Bước đầu tiên phải có người xử lý"
UseWorkflowTemplateFile = "Sử dụng file mẫu quy trình"
WorkflowTemplateFile = "File mẫu quy trình"
WorkflowTemplateHasNoFile = "Mẫu quy trình không có file đính kèm"
NoDefaultMasterDataFound = "Không tìm thấy dữ liệu mặc định"
OrSelectFromMyDocuments = "Hoặc chọn từ văn bản của tôi"
SigningContent = "Nội dung trình ký"
EnterSigningContent = "Nhập nội dung trình ký..."
```

---

## 10. Permissions

| Permission | Mô tả |
|-----------|-------|
| `HCPermissions.Documents.SubmitForSigning` | Trình ký văn bản (trang + API) |
| `HCPermissions.DocumentAssignments.Default` | Xử lý trình ký (Approve/Return/Reject) |
| `HCPermissions.DocumentHistories.Create` | Tạo DocumentHistory (internal) |

---

## 11. Luồng dữ liệu tổng thể

```
[User mở trang /document-signing]
       │
       ▼
[GetDocumentSigningListAsync] ──► Hiển thị danh sách trình ký
       │                          (filter: All/SentToMe/SentByMe/Following)
       │
[User click "Tạo trình ký"]
       │
       ▼
[Modal hiển thị]
       │
       ├── Chọn Workflow ──► [GetWorkflowSubmitInfoAsync]
       │                          │
       │                          ├── HasTemplateFile? → Hiện checkbox
       │                          └── Hiện steps + assigned users
       │
       ├── Tick "Dùng file mẫu" HOẶC Chọn văn bản (Autocomplete)
       │
       ├── Nhập nội dung trình ký (textarea)
       │
       ├── Upload files (FilePicker → BlobStorage → DocumentFile)
       │
       └── Click "Trình" ──► [SubmitToWorkflowAsync]
                                   │
                                   ├── (Nếu dùng template) Tạo Document (SourceType=3)
                                   │                        Tạo DocumentFile từ template path
                                   │
                                   ├── Tạo DocumentWorkflowInstance (IN_PROGRESS)
                                   ├── Tạo DocumentAssignment (PENDING) cho step 1 users
                                   ├── Tạo DocumentHistory (SUBMIT_SIGNING, Comment=SigningContent)
                                   │   - FromUser = currentUser
                                   │   - ToUser = each assignment ReceiverUserId
                                   ├── Tạo DocumentWorkflowInstanceLogs (START_WORKFLOW)
                                   ├── Tạo DocumentWorkflowInstanceFile (attached files)
                                   └── Send Notification → step 1 users

[User nhận thông báo, click "Xử lý"]
       │
       ▼
[Modal "Xử lý trình ký"]
       │
       ├── Chọn: Duyệt / Trả về / Từ chối
       ├── Nhập ghi chú
       └── Click "Xác nhận" ──► [ProcessWorkflowActionAsync]
                                      │
                                      ├── APPROVED: assignment=DONE
                                      │   ├── All done? → Next step (tạo assignments mới)
                                      │   │              → HOẶC Complete workflow
                                      │   └── Send Notification
                                      │
                                      ├── RETURNED: workflow=RETURNED, revoke assignments
                                      │   └── Send Notification to initiator
                                      │
                                      └── REJECTED: workflow=REJECTED, revoke assignments
                                          └── Send Notification to initiator
```

---

## 12. Ghi chú phát triển

1. **Filter "Following"**: Chưa có logic, count luôn = 0. Có thể implement sau bằng cách thêm bảng `DocumentFollower` hoặc dùng field trên `DocumentAssignment`.

2. **File upload**: Sử dụng Blazorise `FilePicker` với event `Upload` (official pattern cho Blazor Server). Files được upload lên BlobStorage, tạo record `DocumentFile`, rồi link vào `DocumentWorkflowInstanceFile`.

3. **Template file**: Khi WorkflowTemplate có `WordTemplatePath`, user có thể tick checkbox để dùng file đó thay vì chọn văn bản. Hệ thống tự tạo Document mới (SourceType=Workflow) + DocumentFile.

4. **DocumentHistory**: Mỗi lần gửi trình ký, hệ thống tạo DocumentHistory record cho **mỗi** user nhận ở step 1, với `Comment` = nội dung trình ký mà user nhập.

5. **Default MasterData**: Khi tạo Document từ template, lấy record đầu tiên (theo CreationTime) của mỗi MasterDataType làm giá trị mặc định cho TypeId, UrgencyLevelId, SecrecyLevelId.

6. **Modal reset**: Mỗi lần mở modal, tất cả fields được reset. Autocomplete dùng `@key={ModalResetKey}` (increment mỗi lần mở) để force re-render và clear text.





=====WORKFLOW 08/02/2026 update

Khi người dùng từ chối hoặc là người cuối cùng xử lý file đồng ý/ ký => Update status của Document.cs là "var statusList = await _masterDataRepository.GetListAsync(x=>x.Code == "HT" && x.Type == MasterDataType.Status.GetTypeValue());""


Khi người dùng xử lý file đồng ý/ ký => Update status của Document.cs là "var statusList = await _masterDataRepository.GetListAsync(x=>x.Code == "DANG_XU_LY" && x.Type == MasterDataType.Status.GetTypeValue());"




=====WORKFLOW 08/02/2026 update Continue

1. Khi gửi trình tới step nào thì update :  DocumentWorkflowInstances.cs StartedAt = ngày giờ hiện tại.  FinishedAt = Ngày giờ hiện tại .AddDay số ngày WorkflowStepTemplates.SLADAys

2. Hiển thị hành động trả về: Mở modal hiên tại ở bước WorkflowStepTemplates AllowReturn = True thì mới hiển thị nút trả về ở Modal (Logic xử lý sau)

3. Quá hạn xử lý tài liệu: => Lúc Mở modal kiểm tra kiểm tra xem task đã quá hạn chưa
DocumentWorkflowInstances.FinishedAt <= ngày giờ hiện tại (phải tính cả giờ nha)
và trạng thái  DocumentWorkflowInstances.Status khác 3 trạng thái sau COMPLETED / REJECTED / CANCELLED
thì báo warning đỏ đã quá hạn ko được phép xử lý gì nữa disabled hết  

- Update Document.cs Status = "var statusList = await _masterDataRepository.GetListAsync(x=>x.Code == "DA_HUY" && x.Type == MasterDataType.Status.GetTypeValue());"
- Update DocumentHistory.cs  => Comment "Hết hạn xử lý tài liệu"
- Update DocumentWorkflowInstances.cs  => Status CANCELLED
- Update DocumentWorkflowInstanceLogs.cs  => Note  "Hết hạn xử lý tài liệu",Action=WORKFLOW_COMPLETED 


=> Viết thêm 1 hàm chung để update các mục sau:
Document.cs status => Null thì ko update
DocumentHistory.cs Comment =>string empty thì ko upadte (khác thì update Comment  = param truyền vào)
DocumentWorkflowInstances.cs Status  => Null thì ko update 
Update DocumentWorkflowInstanceLogs.cs  =>  string empty thì ko upadte (khác thì update Note  = param truyền vào)


===========WORKFLOW 09/02/2026 
1. Modal ký chọn phương pháp ký (SigningMethods.cs)
- Thêm select chọn phương pháp ký.
- Bắt buộc chọn 1 option mới cho ký

2. Fix Filter 
- Tôi có thay đổi ở ngày giờ, filter text nhưng các số ở mục tất cả tài liệu, văn bản đến, văn bản đã gửi chưa thay đổi

3. Đổi vị trí của Lịch sử văn bản và lịch sử quy trình
- tab lịch sử quy trình đổi tên => Lịch sử văn bản 
- Đưa lịch sử quy trình qua bên thông tin chung
- Đưa lịch sử văn bản qua bên tab mới Lịch sử văn bản


4. Modal WorkflowActionModal / Modal trình ký cho phép tải văn bản cuối cùng của workflow.
- Bổ sung tab Xem tài liệu (Thông tin chung | Tài liệu trình ký | Lịch sử văn bản)
- Ở tab "Tài liệu trình ký" Tài liệu cho phép tải hoặc xem file pdf cuối cùng hiển thị DocumentFile tại bước DocumentAssignments (mới nhất theo ngày tạo) lấy DocumentFiles từ DocumentAssignments.DocumentFileResultId  hiển thị lên 


======= 11/02/2026 logic mới : Ký điện tử ELECTRONIC (MasterData.Type = LOAI_KY  && MasterData.Code = ELECTRONIC)
 <!-- KÝ SỐ Digital (MasterData Type = LOAI_KY  && Code = DIGITAL)  Chưa áp dụng logic  -->
Logic ký tuần tự (WorkflowTemplate.SignMode = SEQUENTIAL)

1.File PDF cần ký sẽ có <<Sign01>> ,  <<FullName01>>,  <<NoteContent01>>  theo thứ tự step 

2. Khi chọn ký điện tử ở modal ký 
- Kiểm tra user đã cấu hình chữ ký điện tử chưa (Chữ ký của user bảng UserSignature.cs)
- SignType = ELECTRONIC
- SignatureImage 
- Còn hiệu lực hay ko
- Kích hoạt chưa
- FullName = Surname + " " + Name 

3. Thoả mãn các diều kiện trên băt đầu ký điện tử 
(viết hàm riêng sau này dùng cho ký số luôn vì params là như nhau)
- Nếu thứ tự là 1 thì replace biến ở file pdf 01 <<Sign01>> là ảnh chữ ký (SignatureImage) của user   <<FullName01>> là Surname + " " + Name  <<NoteContent01>>  là nội dung SigningContent ở modal, tương tự cho vị trí 02, 03.....
- Lưu file đã ký thành file mới upload lên minio + lưu kết quả file đã ký vào (AppDocumentAssignments.DocumentFileResultId) 
- Logic gửi thông báo và next step tiếp theo vẫn giữ như cũ

=> 
1. Yêu cầu code ràng buộc rõ ràng, try catch hiển thị UI service message cho Frontend để biết đang stuck ở đâu tránh lỗi hệ thống 
2. Thiết kế các hàm phải dễ maintain và reuse 
3. Gợi ý thêm kết quả file lưu như nào nếu ký song song (PARALLEL)

======= ĐÃ TRIỂN KHAI (11/02/2026) =======

## Implementation Details: Electronic Signing (SEQUENTIAL)

### Files đã thay đổi:

| File | Thay đổi |
|------|----------|
| `HC.Application/DocumentWorkflowInstances/DocumentWorkflowInstancesAppService.Extended.cs` | Thêm `IUserSignatureRepository`, `ApplyElectronicSignatureAsync()`, `ReplacePdfPlaceholders()`, `ResolveSignatureImageBytesAsync()`. Sửa `ProcessWorkflowActionAsync` gọi ký trước approve |
| `HC.Domain.Shared/Localization/HC/vi.json` | Thêm ~12 keys cho ký điện tử (error messages, validation) |
| `HC.Domain.Shared/Localization/HC/en.json` | Thêm ~12 keys tương ứng tiếng Anh |
| `HC.Blazor/Pages/Documents/DocumentSigning.razor.cs` | Thêm thông báo "Đang ký điện tử..." khi chọn phương pháp ELECTRONIC |

### Flow ký điện tử tuần tự (SEQUENTIAL):

```
[User chọn Approve + Signing Method = ELECTRONIC]
       │
       ▼
[ProcessWorkflowActionAsync]
       │
       ├── Kiểm tra SigningMethodId → MasterData Code = "ELECTRONIC"
       │
       ▼
[ApplyElectronicSignatureAsync] ── BEFORE HandleApproveAsync
       │
       ├── STEP 1: Validate UserSignature
       │   ├── Tìm UserSignature (SignType=ELECTRONIC, IsActive=true, IdentityUserId=currentUser)
       │   ├── Check IsActive
       │   ├── Check SignatureImage not empty
       │   ├── Check ValidFrom <= now
       │   └── Check ValidTo >= now
       │   → UserFriendlyException nếu bất kỳ check nào fail
       │
       ├── STEP 2: Get FullName = IdentityUser.Surname + " " + IdentityUser.Name
       │
       ├── STEP 3: Read PDF from DocumentAssignment.DocumentFileResultId → BlobStorage
       │
       ├── STEP 4: Resolve SignatureImage bytes (base64 data URI / plain base64 / blob path)
       │
       ├── STEP 5: ReplacePdfPlaceholders (PdfPig + PDFsharp)
       │   ├── PdfPig: Scan letters → find <<Sign{NN}>>, <<FullName{NN}>>, <<NoteContent{NN}>>
       │   ├── PDFsharp: White-out placeholder text
       │   ├── <<Sign{NN}>> → Draw signature image (scaled, maintain aspect ratio)
       │   ├── <<FullName{NN}>> → Draw full name text (Helvetica font)
       │   └── <<NoteContent{NN}>> → Draw note content text
       │
       ├── STEP 6: Upload signed PDF → Minio (path: electronic-signed/{guid}.pdf)
       │
       ├── STEP 7: Create DocumentFile (IsSigned=true, newBlobPath, SHA256 hash)
       │
       └── STEP 8: Update assignment.DocumentFileResultId = signedFile.Id
              │
              ▼
[HandleApproveAsync] ── Logic gửi thông báo và next step giữ nguyên
       │
       └── CopyDocumentFileForNextStepAsync copies the SIGNED file to next step
```

### Error Messages (UserFriendlyException → UI):

| Điều kiện | Error Key | Message (VI) |
|-----------|-----------|-------------|
| Không có UserSignature ELECTRONIC | UserHasNoElectronicSignature | Bạn chưa cấu hình chữ ký điện tử |
| IsActive = false | SignatureNotActivated | Chữ ký điện tử chưa được kích hoạt |
| SignatureImage empty | SignatureImageNotConfigured | Ảnh chữ ký điện tử chưa được cấu hình |
| ValidFrom > now | SignatureNotYetValid | Chữ ký điện tử chưa đến ngày hiệu lực |
| ValidTo < now | SignatureExpired | Chữ ký điện tử đã hết hạn |
| Không có file PDF | NoFileToSign | Không tìm thấy file PDF để ký |
| Lỗi đọc ảnh chữ ký | ErrorReadingSignatureImage | Lỗi đọc ảnh chữ ký điện tử |
| Lỗi xử lý PDF | ErrorProcessingPdf | Lỗi xử lý file PDF khi ký |

### Libraries sử dụng:
- **PdfPig 0.1.13**: Đọc PDF, tìm vị trí placeholder text (letter-level extraction)
- **PDFsharp 6.2.4**: Sửa PDF, vẽ white box + overlay image/text tại vị trí placeholder

### ĐÃ TRIỂN KHAI: Ký song song (PARALLEL) - 11/02/2026

#### Thay đổi bổ sung:

| File | Thay đổi |
|------|----------|
| `HC.Application.Contracts/.../WorkflowStepDetailDto.cs` | Thêm `SignMode` vào `WorkflowSubmitInfoDto` |
| `HC.Application/.../DocumentWorkflowInstancesAppService.Extended.cs` | Sửa `SubmitToWorkflowAsync` (tạo tất cả assignments khi PARALLEL), sửa `HandleApproveAsync` (parallel complete + merge), thêm `HandleParallelCompleteAsync`, thêm `MergeSignedPdfsForParallelAsync` |
| `HC.Domain.Shared/Localization/HC/vi.json` | Thêm key `AllStepsMustHaveAssignedUsers` |
| `HC.Domain.Shared/Localization/HC/en.json` | Thêm key tương ứng |
| `HC.Blazor/Pages/Documents/DocumentSigning.razor.cs` | Thay `"ELECTRONIC"` → `nameof(SignType.ELECTRONIC)` |

#### Thay thế Hardcoded bằng Enum:
- `"ELECTRONIC"` → `nameof(SignType.ELECTRONIC)` (`HC.SignatureSettings.SignType`)
- `"DIGITAL"` → `nameof(SignType.DIGITAL)` (`HC.SignatureSettings.SignType`)
- `"PARALLEL"` / `"SEQUENTIAL"` → `nameof(SignMode.PARALLEL)` / `nameof(SignMode.SEQUENTIAL)` (`HC.WorkflowTemplates.SignMode`)

#### Flow ký song song (PARALLEL):

```
[SubmitToWorkflowAsync - PARALLEL detected]
       │
       ├── Validate: TẤT CẢ steps phải có assigned users
       │
       ├── FinishedAt = now + MAX(SLADays) across all steps
       │
       ├── Tạo DocumentAssignments cho TẤT CẢ steps cùng lúc
       │   ├── Step 1 users: IsCurrent=true, file = original
       │   ├── Step 2 users: IsCurrent=true, file = copy of original
       │   ├── Step 3 users: IsCurrent=true, file = copy of original
       │   └── (mỗi step > 1 nhận bản copy riêng qua CopyDocumentFileForNextStepAsync)
       │
       ├── DocumentHistory cho TẤT CẢ users
       │
       └── Notification cho TẤT CẢ users (distinct)

[User X ký (ProcessWorkflowActionAsync → ApplyElectronicSignatureAsync)]
       │
       ├── Ký trên bản copy riêng (replace <<Sign{NN}>> etc.)
       │
       └── HandleApproveAsync:
           ├── Mark assignment DONE
           ├── Check remaining PENDING (IsCurrent=true)
           │
           ├── Nếu CÒN pending → log + return (chờ user khác)
           │
           └── Nếu TẤT CẢ done → HandleParallelCompleteAsync:
               │
               ├── MergeSignedPdfsForParallelAsync:
               │   ├── Đọc file GỐC (original template từ instance files)
               │   ├── Cho mỗi completed assignment (order by stepOrder):
               │   │   ├── Lấy UserSignature + FullName + Note từ log
               │   │   └── ReplacePdfPlaceholders (step's placeholders)
               │   │       → Các step có placeholder KHÁC NHAU nên không conflict
               │   │
               │   ├── Upload merged PDF → Minio (electronic-signed/parallel-merged-{guid}.pdf)
               │   ├── Tạo DocumentFile (IsSigned=true, hash SHA256)
               │   ├── Update TẤT CẢ assignments → DocumentFileResultId = merged file
               │   └── Attach merged file vào workflow instance files
               │
               ├── Complete workflow (status = COMPLETED)
               ├── Log
               ├── Notify initiator
               └── Update document status = HT
```

#### So sánh SEQUENTIAL vs PARALLEL:

| Tiêu chí | SEQUENTIAL | PARALLEL |
|-----------|-----------|----------|
| Tạo assignments | Chỉ step 1, các step sau tạo khi step trước done | TẤT CẢ steps cùng lúc |
| IsCurrent | Chỉ step hiện tại | TẤT CẢ assignments |
| File signing | Chuỗi: step 1 ký → copy → step 2 ký | Mỗi step ký bản copy riêng |
| Completion check | Per step → next step or complete | ALL assignments done → merge → complete |
| SLA/Deadline | Per step (reset mỗi step) | Max SLA across all steps |
| File kết quả | File cuối cùng = đã qua tất cả steps | Merged file = overlay tất cả signatures |
| Merge | Không cần (đã nối tiếp) | MergeSignedPdfsForParallelAsync |

#### Gợi ý mở rộng:
1. **Parallel per-step**: Nếu muốn parallel CHỈ trong cùng step (vẫn sequential giữa steps), thêm field `StepSignMode` cho mỗi step
2. **Placeholder naming**: Hiện dùng `<<Sign{stepOrder:D2}>>` cho cả 2 mode. Nếu cần parallel trong cùng step với nhiều user, dùng `<<Sign{stepOrder}_{userOrder}>>`
3. **Conflict resolution**: Nếu 2 user cùng step ký song song, hiện mỗi user ký bản copy riêng → merge sẽ lấy cả 2 signatures



