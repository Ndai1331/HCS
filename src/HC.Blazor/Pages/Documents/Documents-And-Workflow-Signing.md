# Tài liệu tổng hợp: Quản lý văn bản & Trình ký (Workflow)

> Gộp từ: `AddColumnForSperateFunction.md`, `DocumentSendToWorkflow.md`, `DocumentSigningFeature.md`, `DocumentSigningIssues.md` — cập nhật logic đến **03/2026**.

---

## 1. Phân hệ văn bản (`DocumentSourceType`)

Enum: `HC.Domain.Shared/Documents/DocumentConsts.cs` — `DocumentSourceType`

| Giá trị | Tên | Ý nghĩa |
|--------|-----|---------|
| **0** | `Archive` | Văn thư lưu trữ |
| **1** | `Personal` | Văn bản của tôi |
| **2** | `SentToMe` | Gửi tới tôi (hộp đến / routing) |
| **3** | `Workflow` | Bản sao dùng cho **trình ký** (workflow) |

**Menu Blazor (`HCMenuContributor` + `/manage-documents`):**

- Văn thư: `sourceType=0`
- Văn bản của tôi: `sourceType=1` (filter `CreatorId` = user hiện tại)
- Gửi tới tôi: `sourceType=2` — danh sách theo logic inbox (assignment VIEW + document `SentToMe`, xem `DocumentsAppService.Extended.cs`)

**Cột SentToMe trên grid (`Documents.razor`):**

- **FromUser**: hiển thị **họ tên đầy đủ** (`IdentityUser.Name` + `Surname`), fallback `UserName` / `Email`.
- **Không** hiển thị cột người nhận (ToUser / ReceiverUser) trên DataGrid; export Excel SentToMe cũng bỏ cột tương ứng.

---

## 2. Gửi văn bản & metadata trên `Document`

Các field phục vụ luồng “Gửi” / hiển thị inbox:

- `FromUserId`, `ReceiverUserId`, `DepartmentId` (legacy department), `OrganizationUnitId`.
- Với luồng gửi theo phòng ban, `OrganizationUnitId` lưu id từ module ABP Identity (`AbpOrganizationUnits`); không expand thành assignment theo từng user để tránh nặng.

Ứng dụng: `DocumentsAppService.Extended.cs` (`SendDocumentAsync`, `PopulateSentToMeDisplayNamesAsync`, `GetSentToMeDocumentIdsAsync`).

### 2.1 Logic inbox `SentToMe`

- Nguồn inbox gồm:
  - `DocumentAssignment` kiểu **VIEW** (`ActionType = VIEW`) và **không** gắn bước workflow (`WorkflowStepTemplateId == null`);
  - các document không phải workflow đã được gửi phòng ban (`FromUserId != null`, `OrganizationUnitId`) mà current user thuộc về.
- Query inbox **không** lấy document workflow (`SourceType = Workflow`).
- Chỉ lấy assignment inbox còn hiệu lực:
  - `IsCurrent = true`
  - `Status != REVOKE`

### 2.2 Logic gửi / thu hồi

- `SendDocumentAsync` chỉ tái sử dụng assignment thuộc luồng inbox/send:
  - `ActionType = VIEW`
  - `WorkflowStepTemplateId == null`
  - `IsCurrent = true`
  - `Status != REVOKE`
- Không update nhầm assignment của trình ký (`WorkflowStepTemplateId != null`) hay assignment nghiệp vụ khác trên cùng document.
- Khi chọn gửi theo phòng ban, UI load cây phòng ban từ `DocumentsAppService.GetOrganizationUnitTreeAsync()` / endpoint `api/app/documents/organization-unit-tree`; server chỉ lưu `OrganizationUnitId`, không tạo notification/history/assignment theo từng user.
- `RevokeDocumentAsync`:
  - chỉ revoke assignment thuộc luồng inbox/send:
    - `ActionType = VIEW`
    - `WorkflowStepTemplateId == null`
  - đổi assignment sang `REVOKE`
  - đồng thời set `IsCurrent = false`
  - nhờ đó document bị thu hồi không còn xuất hiện trong inbox `SentToMe`.

---

## 3. Trình ký — trang `/document-signing`

**Quyền:** `HCPermissions.Documents.SubmitForSigning`  
**Layout:** filter từ ngày / đến ngày + text; sidebar filter (Tất cả / Gửi đến tôi / Tôi gửi đi / Đang theo dõi); DataGrid danh sách.

### 3.1 Filter danh sách trình ký (`GetDocumentSigningListAsync`)

- Chỉ tài liệu có **`SourceType = Workflow` (3)** (bản workflow, không lẫn văn thư 0/1/2).
- **Gửi đến tôi (`SentToMe`)**: `DocumentAssignment` với  
  **`WorkflowStepTemplateId != null`** **và** **`ReceiverUserId = currentUser`**  
  (chỉ bước quy trình thật — không lấy assignment “gửi văn bản” VIEW `WorkflowStepTemplateId == null`).
- **Tôi gửi đi**: instance do user khởi tạo **và** có assignment gửi cho **người khác** (cùng điều kiện workflow assignment có step).
- **Tất cả**: union hai nhánh trên.
- **Đang theo dõi**: chưa có logic (count = 0).

### 3.2 Trình ký từ menu quản lý (0 / 1 / 2) — **bản sao + liên kết gốc**

Khi submit **không** dùng “chỉ template workflow” trên một document đang là **Archive / Personal / SentToMe**:

1. Tạo **document mới** `SourceType = Workflow`, copy metadata + **sao chép file blob** sang file mới (không sửa file gốc).
2. Gán **`ParentDocumentId`** = Id document gốc.
3. Mọi `DocumentWorkflowInstance`, `DocumentAssignment`, history, file trình ký gắn với **`documentId` của bản sao**.
4. **Document gốc** giữ nguyên `SourceType` (0/1/2), vẫn xuất hiện đúng menu.

**Sau khi trình:**

- **Parent**: cập nhật **`WorkflowId`** = quy trình đã chọn; trạng thái master data **`DANG_XU_LY`** (đồng bộ qua `UpdateDocumentStatusAsync` / `SyncParentDocumentOnWorkflowSubmitAsync`).
- Khi workflow trên **bản con** đổi trạng thái (HT, TRA_VE, TU_CHOI, DANG_XU_LY, DA_HUY, …): **`UpdateDocumentStatusAsync`** áp dụng lên **con** và **đồng bộ lên parent** nếu `ParentDocumentId` có giá trị.
- **Quá hạn (Background worker)**: hủy instance, DA_HUY trên **con**; đồng bộ **parent** khi có `ParentDocumentId`.

**Nút “Trình ký” trên grid parent (`HideSubmitForSigningButton`):**

- **Ẩn** nếu tồn tại bản con (`ParentDocumentId` trỏ về parent) có instance **`IN_PROGRESS`** hoặc **`COMPLETED`**.
- **Hiện lại** khi workflow kết thúc dạng **RETURNED / REJECTED / CANCELLED** (ví dụ hủy, trả về) — không còn instance chặn.

**Migration:** `ParentDocumentId` trên `AppDocuments` (+ FK self-reference).

Nếu document nguồn đã là **`Workflow` (3)** (ví dụ tiếp tục trên bản trình ký): **không** nhân bản thêm; dùng trực tiếp document đó.

### 3.2.1 Re-submit sau khi `RETURNED`

- Workflow instance được **reuse** để giữ nguyên log/history của cùng một vòng trình ký.
- Nếu người dùng **không đổi document**: tiếp tục trên workflow document hiện tại.
- Nếu người dùng **đổi sang document khác**:
  - nếu document mới là `Archive / Personal / SentToMe` thì hệ thống **duplicate sang `SourceType = Workflow`** trước;
  - gán `ParentDocumentId` về document nguồn mới;
  - copy file blob sang document workflow mới;
  - sync trạng thái document nguồn mới sang `DANG_XU_LY`.
- Không cho workflow instance trỏ trực tiếp sang document gốc ngoài workflow; invariant là:
  - **mọi `DocumentWorkflowInstance` chỉ chạy trên document `SourceType = Workflow`.**
- Khi re-submit đổi document, cleanup assignment cũ luôn chạy theo **document workflow cũ** để không để lại assignment treo `IsCurrent`.

### 3.2.2 Hủy trình ký do người trình ký (chưa ai ký)

- API: `POST api/app/document-workflow-instances/cancel-by-initiator` (`CancelWorkflowByInitiatorInput`).
- Chỉ **người tạo instance** (`CreatorId`) và instance ở trạng thái **`IN_PROGRESS`** hoặc **`OVERDUE`**.
- **Không** hủy được sau khi **đã ký file** (file workflow `IsSigned=true` từ lúc trình ký trở đi), hoặc đã hoàn thành bước **SIGN/PROCESS** (`assignment DONE`). Chỉ **trình ký** (submit, file copy `IsSigned=false`) vẫn được hủy. Bước **VIEW** không chặn hủy.
- Hủy mềm: instance → **`CANCELLED`**, assignment `PENDING` → **`REVOKE`**, tài liệu workflow con + parent (nếu có) → **`DA_HUY`**; log/history `WORKFLOW_CANCELLED`.
- Sau hủy: nút **Trình ký** trên document gốc hiện lại (không còn child `IN_PROGRESS`/`COMPLETED`); trình lại tạo **bản workflow con mới** như submit lần đầu.
- UI: nút hủy trên `/document-signing` khi `CanCancelWorkflow` (cột Actions, thường tab **Tôi gửi đi**).

### 3.3 Modal trình ký & file Word/PDF

- Chọn quy trình: **Select2** (lookup `GetWorkflowLookupAsync`, tìm theo tên, `MaxResultCount = 20`).
- Có thể dùng **file mẫu workflow** (`UseWorkflowTemplateFile`) → tạo document mới `SourceType = Workflow` (không có parent trừ khi sau này mở rộng).
- Hoặc chọn văn bản từ **0/1/2** → luồng **duplicate** như trên.
- File **.doc/.docx**: bắt buộc nội dung trình ký (RichText); replace placeholder trong Word rồi convert PDF (`WorkflowSigningExecutionService`, OpenXml, LibreOffice).
- Placeholders tiêu biểu: `<<DD>>`, `<<MM>>`, `<<YYYY>>`, `<<ContentToBeApproved>>`, `<<PreparedBySign>>`, `<<PreparedFullName>>`, `<<PositionName>>` / `<<ViTriLamViec>>` (chức danh đầy đủ từ `PositionId_Text`, ví dụ `KT - Kế toán`), `<<PhongBan>>` / `<<Department>>` (một OU đầu tiên của user), v.v.

### 3.3.1 Cách chọn `WorkflowTemplate` để submit

- Luôn lấy **template mới nhất theo `CreationTime`** của workflow.
- Rule hiện tại:
  - nếu template mới nhất **không có step active** -> báo lỗi;
  - nếu step đầu tiên **không có assignee active** -> báo lỗi;
  - nếu `SignMode = PARALLEL` mà có step active nào **không có assignee active** -> báo lỗi.
- Mục tiêu:
  - người dùng biết ngay template mới nhất đang cấu hình lỗi/chưa đủ dữ liệu;
  - không tự động fallback sang template cũ vì dễ che giấu lỗi cấu hình;
  - giữ tương thích với mô hình hiện tại khi entity `WorkflowTemplate` chưa có cờ “active version” riêng.

### 3.4.1 Bước VIEW (xem theo OU / người chỉ định)

- Bước `VIEW` có thể đặt ở **bất kỳ** thứ tự trong quy trình.
- Khi quy trình **tới** bước VIEW: ghi `UnlockedViewStepTemplateIds` (ExtraProperties trên instance); **không** tạo `DocumentAssignment` PENDING.
- Người được xem khi bước đã unlock:
  - `RoleInSubmitterOrganizationUnit` + role → user active có role và thuộc OU chain người trình ký;
  - `SpecificUser` → đúng user chỉ định trên assignment.
- Sau unlock, engine **auto-skip** các bước VIEW liên tiếp; **dừng** tại bước `SIGN` / `PROCESS` (assignment + ký bắt buộc).
- Danh sách `/document-signing`: UNION assignment workflow + document có quyền xem bước VIEW đã unlock (`HasViewAccess`, `CanAct` chỉ khi có assignment PENDING bước chặn).

### 3.4 Xử lý bước (Approve / Return / Reject)

- **DocumentAssignment**: `PENDING`, `DONE`, `REJECTED`, `REVOKE`; gắn `WorkflowStepTemplateId` cho bước workflow (chỉ bước chặn SIGN/PROCESS).
- **DocumentWorkflowInstance**: `IN_PROGRESS`, `COMPLETED`, `REJECTED`, `RETURNED`, `CANCELLED`, …
- Return / Reject: map trạng thái document đúng nghiệp vụ (**TRA_VE**, **TU_CHOI**, không dùng HT cho trả về/từ chối).
- **SEQUENTIAL vs PARALLEL** (`WorkflowTemplate.SignMode`): tạo assignment và copy/merge file theo logic đã triển khai; ký điện tử **ELECTRONIC** (placeholder `<<SignNN>>`, …).

### 3.5 SLA & quá hạn

- `FinishedAt` theo SLA bước (sequential) hoặc max SLA (parallel).
- Kiểm tra quá hạn khi mở modal (read); **hủy tự động** do **`WorkflowOverdueBackgroundWorker`** (periodic), có kiểm tra quyền liên quan khi API read/check.

---

## 4. Bảng / entity chính (tham chiếu nhanh)

| Entity | Vai trò |
|--------|---------|
| `Document` | Văn bản; `SourceType`, `WorkflowId`, **`ParentDocumentId`** |
| `DocumentFile` | File + path blob |
| `DocumentWorkflowInstance` | Instance quy trình theo `DocumentId` |
| `DocumentAssignment` | Phân công user/step; `WorkflowStepTemplateId`, `DocumentFileResultId` |
| `DocumentWorkflowInstanceFile` | File đính kèm instance |
| `DocumentWorkflowInstanceLogs` | Log hành động |
| `DocumentHistory` | Lịch sử (ví dụ TRINH) |
| `Workflow` / `WorkflowTemplate` / `WorkflowStepTemplate` / `WorkflowStepAssignment` | Định nghĩa quy trình |
| `MasterData` | Trạng thái VB (`TRANG_THAI_VB`), loại ký, … |
| `Notification` / `NotificationReceiver` | Thông báo workflow |

---

## 5. API / AppService chính

| Method | Mô tả ngắn |
|--------|------------|
| `GetWorkflowSubmitInfoAsync` | Thông tin workflow + bước + template |
| `SubmitToWorkflowAsync` | Trình ký (duplicate 0/1/2 → 3 + parent sync) |
| `ProcessWorkflowActionAsync` | Duyệt / trả / từ chối + ký điện tử nếu có |
| `GetDocumentSigningListAsync` | Danh sách trình ký (filter DB, Workflow + step assignment) |
| `IsDocumentSourceFileWordFormatAsync` | Kiểm tra .doc/.docx cho modal |

Controller: `DocumentWorkflowInstanceController.Extended.cs`.

---

## 6. Localization & permission

- Keys: `vi.json` / `en.json` (Menu DocumentSigning, filter, modal, lỗi ký điện tử, …).
- `SubmitForSigning`, `DocumentAssignments.Default`, lịch sử tạo nội bộ theo policy hiện có.

---

## 7. Code quality & sửa lỗi đã ghi nhận (tóm tắt)

Các mục sau đã được xử lý hoặc ghi chú trong code (chi tiết lịch sử review nằm trong git / bản gốc):

| Chủ đề | Ghi chú |
|--------|---------|
| Race PARALLEL complete | Re-fetch instance / guard trước khi complete |
| Copy file fail | Không fallback dùng lại file gốc → throw `UserFriendlyException` |
| RETURN/REJECT vs HT | Dùng `TRA_VE` / `TU_CHOI` + MasterData |
| RETURN PARALLEL | Revoke theo scope bước / nghiệp vụ đã chỉnh |
| Overdue | Worker nền + auth khi check từ client |
| `GetDocumentSigningList` | Query SQL (join), không load full assignment vào RAM |
| `Clock.Now` | Thay `DateTime.Now` trong luồng workflow/gửi |
| Cleanup assignment cũ | Trước re-submit RETURNED/REJECTED |
| `StartedAt` | Không ghi đè khi chuyển bước sequential |
| Merge parallel | Batch load user/signature/log |
| UserSignature query | Filter theo user, không load toàn bộ |
| `[UnitOfWork]` | Submit workflow atomic |
| Constants | `WorkflowConstants`, blob paths |
| Re-submit đổi document | Giữ invariant workflow chỉ chạy trên `SourceType = Workflow` |
| Inbox/revoke | Loại assignment `REVOKE`, chỉ lấy inbox assignment `IsCurrent` |
| SendDocument | Chỉ reuse assignment `VIEW` không gắn `WorkflowStepTemplateId`, còn hiệu lực hiện tại |
| RevokeDocument | Chỉ tác động assignment của luồng inbox/send |
| Chọn workflow template | Luôn lấy template mới nhất; nếu không runnable thì báo lỗi ngay |

**Chưa ưu tiên:** refactor tách `DocumentWorkflowInstancesAppService` nhỏ hơn (constructor lớn).

---

## 8. Files tham chiếu chính (code)

| Khu vực | File |
|---------|------|
| Enum SourceType | `HC.Domain.Shared/Documents/DocumentConsts.cs` |
| Entity Document | `HC.Domain/Documents/Document.cs` |
| Trình ký + duplicate + parent sync | `HC.Application/DocumentWorkflowInstances/DocumentWorkflowInstancesAppService.Extended.cs` |
| SentToMe + ẩn nút trình ký | `HC.Application/Documents/DocumentsAppService.Extended.cs` |
| UI danh sách quản lý | `HC.Blazor/Pages/Documents/Documents.razor` (+ `.cs`) |
| UI trình ký | `HC.Blazor/Pages/Documents/DocumentSigning.razor` (+ `.cs`) |
| Modal trình ký | `HC.Blazor/Components/SubmitWorkflowModal/` |
| Worker quá hạn | `HC.Domain/DocumentWorkflowInstances/WorkflowOverdueBackgroundWorker.cs` |
| Migration parent | `Migrations/20260320120000_Added_Document_ParentDocumentId.cs` |

---

## 9. Ghi chú triển khai

1. **DbMigrator**: chạy migration cho `HCDbContext` sau khi thêm `ParentDocumentId`.
2. **Regenerate client proxy** (nếu dùng): DTO có `ParentDocumentId`, `HideSubmitForSigningButton`.
3. **Filter “Đang theo dõi”**: có thể bổ sung sau (bảng follower hoặc flag assignment).

---

*Tài liệu này là nguồn đơn cho nghiệp vụ “Văn bản + Trình ký”; khi đổi logic, cập nhật song song code và mục tương ứng trong file này.*
