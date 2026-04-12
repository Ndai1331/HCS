# Tích hợp thông báo (Mobile)

Tài liệu mô tả cách backend lưu `Title` / `Content`, cách hiển thị (localization), `SourceType`, màu/icon gợi ý, và điều hướng theo `RelatedType` — tương đương logic Blazor (`Index.razor.cs`, `NotificationReceivers.razor.cs`, `NotificationToast.razor`).

**Nguồn chuỗi dịch:** `src/HC.Domain.Shared/Localization/HC/vi.json`, `src/HC.Domain.Shared/Localization/HC/en.json` (namespace localization HC).

**DTO API:** `Title`, `Content`, `SourceType`, `EventType`, `RelatedType`, `RelatedId`, `Priority` (xem `NotificationDto`).

---

## 1. `SourceType` — icon và màu gợi ý (UI web)

Mobile có thể map sang icon/màu tương đương (không bắt buộc trùng thư viện Blazor).

| `SourceType` | Icon (web: Blazor `IconName`) | Class màu (Bootstrap) |
|--------------|-------------------------------|------------------------|
| `DOCUMENT` | `File` | `bg-primary` |
| `WORKFLOW` | `ChartLine` | `bg-secondary` |
| `PROJECT` | `Folder` | `bg-success` |
| `TASK` | `CheckSquare` | `bg-info` |
| `CALENDAR` | `CalendarDay` | `bg-warning` |
| `CHAT` | `Comment` | `bg-danger` |
| *(khác / rỗng)* | `Bell` | `bg-primary` |

**Ghi chú:** Hiện tại luồng tạo `Notification` trong backend chủ yếu dùng `DOCUMENT`, `WORKFLOW`, `PROJECT`, `TASK`, `CALENDAR`. Giá trị `CHAT` có thể dùng cho tương lai hoặc dữ liệu cũ.

---

## 2. Hiển thị tiêu đề — `Title`

- `Title` là **khóa localization** (ví dụ `WorkflowAssigned`), **không** phải câu đã dịch sẵn.
- Mobile: tra bảng khóa dưới đây hoặc đồng bộ file `vi.json` / `en.json` vào app.

```csharp
// Pseudocode
localizedTitle = L[notification.Title]  // fallback: hiển thị raw Title nếu không có khóa
```

---

## 3. Hiển thị nội dung — `Content`

Hai dạng:

1. **Không có `|`:** `Content` là một khóa localization đơn → `L[Content]`.
2. **Có `|`:** định dạng `MessageKey|param0|param1|...`
   - Phần đầu: khóa template (ví dụ `WorkflowAssignedMessage`).
   - Các phần sau: tham số cho `string.Format` theo thứ tự `{0}`, `{1}`, …

```csharp
// Pseudocode
parts = Content.Split('|')
if (parts.Length == 1)
    return L[Content]
key = parts[0]
parameters = parts[1..]
template = L[key]
return string.Format(template, parameters)  // try/catch: fallback raw Content
```

**Khóa dự phòng trong tham số:** đôi khi tham số dùng literal `System` — cần dịch `System` → ví dụ tiếng Việt: **"Hệ thống"** (khóa `System` trong JSON).

---

## 4. Điều hướng — `RelatedType` + `RelatedId`

Web (Blazor) map như sau. Mobile nên map sang màn hình tương ứng; `RelatedId` là **Guid** dạng chuỗi.

| `RelatedType` (uppercase) | Đường dẫn web (tham khảo) |
|---------------------------|---------------------------|
| `WORKFLOW` | `/document-signing/{RelatedId}` |
| `TASK` | `/project-task-detail/{RelatedId}` |
| `PROJECT` | `/project-detail/{RelatedId}` |
| `DOCUMENT` | `/view-document-detail/{RelatedId}?sourceType=1` |
| `CALENDAR_EVENT` | `/calendar-event-detail/{RelatedId}` |
| *(khác / thiếu)* | `#` hoặc không điều hướng |

Nếu `RelatedId` hoặc `RelatedType` rỗng → không deep link.

---

## 5. Khóa `Title` do backend gửi (tóm tắt)

| Khóa `Title` | Ngữ cảnh ngắn |
|--------------|----------------|
| `WorkflowAssigned` | Giao workflow / trình ký / bước tiếp / trình lại |
| `WorkflowAssignRemoved` | Xóa phân công workflow |
| `WorkflowAssignUpdated` | Cập nhật phân công workflow |
| `TaskAssigned` | Giao công việc |
| `TaskAssignRemoved` | Xóa giao việc |
| `TaskAssignUpdated` | Cập nhật giao việc |
| `CalendarInvited` | Mời lịch |
| `CalendarInviteRemoved` | Xóa khỏi sự kiện |
| `CalendarInviteUpdated` | Cập nhật lời mời |
| `ProjectMemberAdded` | Thêm thành viên dự án |
| `ProjectMemberRemoved` | Xóa thành viên |
| `ProjectMemberUpdated` | Cập nhật thành viên |
| `DocumentReceived` | Nhận văn bản (gửi đến) |
| `DocumentRevoked` | Thu hồi văn bản |
| `DocumentApprovalRequested` | Trình phê duyệt |
| `DocumentApprovalRejected` | Từ chối phê duyệt |
| `DocumentApproved` | Đã phê duyệt |
| `WorkflowCompleted` | Hoàn thành quy trình ký |
| `WorkflowRejected` | Từ chối (workflow) |
| `WorkflowReturned` | Trả về |
| `WorkflowResubmitted` | Trình lại sau khi bị trả về |

*(Có khóa `WorkflowStepAssigned` trong JSON nhưng luồng gửi notification tương ứng hiện bị comment trong code.)*

---

## 6. Khóa `Content` (template) và tham số

Cột **Tham số** là thứ tự sau dấu `|` (map `{0}`, `{1}`, …).

| Khóa template | Số tham số | Ý nghĩa (thứ tự) |
|-----------------|------------|------------------|
| `WorkflowAssignedMessage` | 4 | `[0]` số lưu kho, `[1]` tiêu đề VB, `[2]` bước hoặc tên workflow (tùy luồng), `[3]` người/bước tiếp |
| `WorkflowAssignRemovedMessage` | 4 | số lưu kho, tiêu đề, tên bước, người thao tác |
| `WorkflowAssignUpdatedMessage` | 4 | số lưu kho, tiêu đề, tên bước, người thao tác |
| `TaskAssignedMessage` | 3 | mã task, tiêu đề task, người giao |
| `TaskAssignRemovedMessage` | 3 | mã task, tiêu đề, người thao tác |
| `TaskAssignUpdatedMessage` | 3 | mã task, tiêu đề, người thao tác |
| `CalendarInvitedMessage` | 3 | tiêu đề sự kiện, thời gian (format server), người mời |
| `CalendarInviteRemovedMessage` | 3 | tiêu đề sự kiện, thời gian, người thao tác |
| `CalendarInviteUpdatedMessage` | 3 | tiêu đề sự kiện, thời gian, người thao tác |
| `ProjectMemberAddedMessage` | 3 | mã dự án, tên dự án, người thao tác |
| `ProjectMemberRemovedMessage` | 3 | mã dự án, tên dự án, người thao tác |
| `ProjectMemberUpdatedMessage` | 3 | mã dự án, tên dự án, người thao tác |
| `DocumentReceivedMessage` | 3 | số lưu kho, tiêu đề, người gửi |
| `DocumentRevokedMessage` | 3 | số lưu kho, tiêu đề, người thu hồi |
| `DocumentApprovalRequestedMessage` | 3 | số lưu kho, tiêu đề, người trình |
| `DocumentApprovalRejectedMessage` | 3 | số lưu kho, tiêu đề, người từ chối |
| `DocumentApprovedMessage` | 3 | số lưu kho, tiêu đề, người phê duyệt |
| `WorkflowCompletedMessage` | 2 | số lưu kho, tiêu đề |
| `WorkflowRejectedMessage` | 3 | số lưu kho, tiêu đề, user xử lý |
| `WorkflowReturnedMessage` | 3 | số lưu kho, tiêu đề, user trả về |
| `WorkflowResubmittedMessage` | **Backend gửi 4** tham số (số lưu kho, tiêu đề, tên workflow, tên bước đầu) | **Cảnh báo:** chuỗi trong `vi.json` / `en.json` hiện **không có** placeholder `{0}`–`{3}`; cần align JSON với backend hoặc chỉ hiển thị bản dịch cố định và bỏ qua tham số phía mobile |

---

## 7. Bản dịch tham chiếu (EN / VI)

### 7.1 Nhóm task / project / workflow phân công / lịch / tài liệu (đoạn chính trong JSON)

| Key | EN | VI |
|-----|----|----|
| `TaskAssigned` | Task assigned | Công việc đã được giao |
| `TaskAssignedMessage` | You have been assigned to task '{0}: {1}' by {2} | Bạn đã được giao công việc '{0}: {1}' bởi {2} |
| `ProjectMemberAdded` | Project member added | Đã thêm thành viên dự án |
| `ProjectMemberAddedMessage` | You have been added to project '{0}: {1}' by {2} | Bạn đã được thêm vào dự án '{0}: {1}' bởi {2} |
| `WorkflowAssigned` | Workflow assigned | Workflow đã được giao |
| `WorkflowAssignedMessage` | You have been assigned workflow for document '{0}: {1}' - Step: {2} by {3} | Bạn đã được giao workflow cho văn bản '{0}: {1}' - Bước: {2} bởi {3} |
| `CalendarInvited` | Calendar event invited | Đã được mời sự kiện lịch |
| `CalendarInvitedMessage` | You have been invited to event '{0}' on {1} by {2} | Bạn đã được mời tham gia sự kiện '{0}' vào {1} bởi {2} |
| `TaskAssignRemoved` | Task assignment removed | Đã xóa giao việc |
| `TaskAssignRemovedMessage` | You have been removed from task '{0}: {1}' by {2} | Bạn đã bị xóa khỏi công việc '{0}: {1}' bởi {2} |
| `ProjectMemberRemoved` | Project member removed | Đã xóa thành viên dự án |
| `ProjectMemberRemovedMessage` | You have been removed from project '{0}: {1}' by {2} | Bạn đã bị xóa khỏi dự án '{0}: {1}' bởi {2} |
| `WorkflowAssignRemoved` | Workflow assignment removed | Đã xóa giao workflow |
| `WorkflowAssignRemovedMessage` | You have been removed from workflow for document '{0}: {1}' - Step: {2} by {3} | Bạn đã bị xóa khỏi workflow cho văn bản '{0}: {1}' - Bước: {2} bởi {3} |
| `CalendarInviteRemoved` | Calendar invite removed | Đã xóa lời mời lịch |
| `CalendarInviteRemovedMessage` | You have been removed from event '{0}' on {1} by {2} | Bạn đã bị xóa khỏi sự kiện '{0}' vào {1} bởi {2} |
| `TaskAssignUpdated` | Task assignment updated | Đã cập nhật giao việc |
| `TaskAssignUpdatedMessage` | Your assignment to task '{0}: {1}' has been updated by {2} | Giao việc của bạn cho công việc '{0}: {1}' đã được cập nhật bởi {2} |
| `ProjectMemberUpdated` | Project member updated | Đã cập nhật thành viên dự án |
| `ProjectMemberUpdatedMessage` | Your membership in project '{0}: {1}' has been updated by {2} | Thành viên của bạn trong dự án '{0}: {1}' đã được cập nhật bởi {2} |
| `WorkflowAssignUpdated` | Workflow assignment updated | Đã cập nhật giao workflow |
| `WorkflowAssignUpdatedMessage` | Your assignment to workflow for document '{0}: {1}' - Step: {2} has been updated by {3} | Giao workflow của bạn cho văn bản '{0}: {1}' - Bước: {2} đã được cập nhật bởi {3} |
| `DocumentReceived` | Document received | Đã nhận văn bản |
| `DocumentReceivedMessage` | You have received document '{0}: {1}' from {2} | Bạn đã nhận được văn bản '{0}: {1}' từ {2} |
| `DocumentApprovalRequested` | Document approval requested | Yêu cầu phê duyệt văn bản |
| `DocumentApprovalRequestedMessage` | You have received an approval request for document '{0}: {1}' from {2} | Bạn nhận được yêu cầu phê duyệt văn bản '{0}: {1}' từ {2} |
| `DocumentApprovalRejected` | Document approval rejected | Văn bản bị từ chối phê duyệt |
| `DocumentApprovalRejectedMessage` | Document '{0}: {1}' was rejected by {2} | Văn bản '{0}: {1}' đã bị từ chối phê duyệt bởi {2} |
| `DocumentApproved` | Document approved | Văn bản đã được phê duyệt |
| `DocumentApprovedMessage` | Document '{0}: {1}' was approved by {2} | Văn bản '{0}: {1}' đã được phê duyệt bởi {2} |
| `CalendarInviteUpdated` | Calendar invite updated | Đã cập nhật lời mời lịch |
| `CalendarInviteUpdatedMessage` | Your invitation to event '{0}' on {1} has been updated by {2} | Lời mời của bạn cho sự kiện '{0}' vào {1} đã được cập nhật bởi {2} |
| `System` | System | Hệ thống |

### 7.2 Nhóm workflow trình ký (cuối file localization)

| Key | EN | VI |
|-----|----|----|
| `DocumentRevoked` | Document Revoked | Văn bản đã bị thu hồi |
| `DocumentRevokedMessage` | Document '{0}: {1}' has been revoked by {2} | Văn bản '{0}: {1}' đã bị thu hồi bởi {2} |
| `WorkflowCompleted` | Workflow Completed | Quy trình đã hoàn thành |
| `WorkflowCompletedMessage` | Signing workflow for document '{0}: {1}' has been completed | Quy trình trình ký cho văn bản '{0}: {1}' đã hoàn thành |
| `WorkflowReturned` | Document Returned | Văn bản đã bị trả về |
| `WorkflowReturnedMessage` | Document '{0}: {1}' has been returned by {2} | Văn bản '{0}: {1}' đã bị trả về bởi {2} |
| `WorkflowRejected` | Document Rejected | Văn bản đã bị từ chối |
| `WorkflowRejectedMessage` | Document '{0}: {1}' has been rejected by {2} | Văn bản '{0}: {1}' đã bị từ chối bởi {2} |
| `WorkflowResubmitted` | Document resubmitted | Trình lại văn bản |
| `WorkflowResubmittedMessage` | The document has been resubmitted | Văn bản đã được trình lại |

---

## 8. Khóa dự phòng (optional)

| Key | EN | VI |
|-----|----|----|
| `WorkflowStepAssigned` | Workflow step assigned | Bước workflow đã được giao |
| `WorkflowStepAssignedMessage` | You have been assigned workflow step '{0}' by {1} | Bạn đã được giao bước workflow '{0}' bởi {1} |

---

*Tài liệu sinh để team mobile đồng bộ hiển thị với web; cập nhật khi thêm `Title`/`Content` mới trong backend.*
