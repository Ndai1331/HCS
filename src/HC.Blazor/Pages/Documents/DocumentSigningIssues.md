======= CODE REVIEW 13/02/2026 =======

# BÁO CÁO REVIEW CODE - CHỨC NĂNG TRÌNH KÝ

> Ngày review: 13/02/2026
> File chính được review: `DocumentWorkflowInstancesAppService.Extended.cs` (~2100 dòng), `DocumentsAppService.Extended.cs`, `WorkflowStepAssignmentsAppService.Extended.cs`

---

## TỔNG QUAN MỨC ĐỘ

| Mức độ | Số lượng | Mô tả |
|--------|----------|-------|
| 🔴 Nghiêm trọng | 2 ✅ | Có thể gây crash, mất dữ liệu, sai logic nghiệp vụ nghiêm trọng — **ĐÃ SỬA** |
| 🟠 Cao | 5 ✅ | Bug logic nghiệp vụ hoặc performance ảnh hưởng lớn — **ĐÃ SỬA** |
| 🟡 Trung bình | 7 ✅ | Bug tiềm ẩn hoặc performance cần cải thiện — **ĐÃ SỬA** |
| 🔵 Thấp | 4 ✅ | Code quality, maintainability — **ĐÃ SỬA** (trừ ISSUE-15 refactor lớn) |

---

## 🔴 ISSUE-01: Race Condition khi 2 user Approve đồng thời (PARALLEL mode)

- **Mức độ:** 🔴 Nghiêm trọng
- **File:** `DocumentWorkflowInstancesAppService.Extended.cs`
- **Dòng:** 552-589 (`HandleApproveAsync`)
- **Trạng thái:** ✅ Đã sửa (13/02/2026) — Thêm optimistic concurrency guard: re-fetch instance từ DB sau khi check remainingPending, skip completion nếu instance đã chuyển trạng thái

**Mô tả:**
Trong chế độ PARALLEL, tất cả assignments đều có `IsCurrent = true`. Khi 2 user cùng Approve gần như đồng thời:
1. User A: update assignment → DONE (`autoSave: true`)
2. User B: update assignment → DONE (`autoSave: true`)
3. User A: query `remainingPending` → thấy 0 pending → gọi `HandleParallelCompleteAsync`
4. User B: query `remainingPending` → cũng thấy 0 pending → **CŨNG gọi** `HandleParallelCompleteAsync`

Kết quả: workflow bị COMPLETED 2 lần, merge PDF 2 lần, tạo 2 file merged, gửi 2 notification.

**Code hiện tại:**
```csharp
// Line 555-559
assignment.Status = nameof(DocumentAssignmentStatus.DONE);
assignment.ProcessedAt = now;
assignment.IsCurrent = false;
await _documentAssignmentRepository.UpdateAsync(assignment, autoSave: true);

// Line 571-574 - Query ngay sau update, KHÔNG có lock
var remainingPending = await _documentAssignmentRepository.GetListAsync(
    x => x.DocumentId == instance.DocumentId
    && x.IsCurrent
    && x.Status == nameof(DocumentAssignmentStatus.PENDING));

// Line 576-588
if (remainingPending.Any())
{
    // log and return
}
// Line 591-594 - Nếu không còn pending → complete
if (isParallel)
{
    await HandleParallelCompleteAsync(instance, assignment, currentStep, now, note);
}
```

**Giải pháp:**
Dùng distributed lock (Redis lock) hoặc optimistic concurrency trên `DocumentWorkflowInstance`:

```csharp
// Option A: Distributed Lock
using var lockHandle = await _distributedLockProvider
    .TryAcquireAsync($"workflow-complete-{instance.Id}", TimeSpan.FromSeconds(30));
if (lockHandle == null)
{
    Logger.LogWarning("Could not acquire lock for workflow {InstanceId}, another thread is completing", instance.Id);
    return;
}
// ... tiếp tục logic check remaining + complete

// Option B: Optimistic Concurrency - check-and-set status
// Re-fetch instance và kiểm tra status trước khi complete
var freshInstance = await _documentWorkflowInstanceRepository.GetAsync(instance.Id);
if (freshInstance.Status != nameof(DocumentWorkflowInstanceStatus.IN_PROGRESS))
{
    Logger.LogWarning("Workflow {InstanceId} already completed by another thread", instance.Id);
    return;
}
```

---

## 🔴 ISSUE-02: Fallback trả sourceFileId khi copy file thất bại → ghi đè file gốc

- **Mức độ:** 🔴 Nghiêm trọng
- **File:** `DocumentWorkflowInstancesAppService.Extended.cs`
- **Dòng:** 1403-1410 (`CopyDocumentFileForNextStepAsync`)
- **Trạng thái:** ✅ Đã sửa (13/02/2026) — Thay `return sourceFileId` bằng `throw UserFriendlyException` để ngăn ghi đè file gốc

**Mô tả:**
Khi copy file cho step tiếp theo thất bại, code fallback trả về `sourceFileId` gốc. Khi step tiếp theo ký lên file này (ApplyElectronicSignatureAsync), file gốc sẽ bị **ghi đè** → mất dữ liệu audit trail, mất file đã ký ở step trước.

**Code hiện tại:**
```csharp
// Line 1403-1410
catch (Exception ex)
{
    Logger.LogError(ex, "Error copying document file for next step...");
    // NGUY HIỂM: trả sourceFileId gốc, step tiếp theo sẽ ký lên file gốc
    return sourceFileId;
}
```

**Giải pháp:**
Không nên swallow exception. Nếu copy file thất bại, nên throw hoặc ít nhất không cho phép ký tiếp:

```csharp
catch (Exception ex)
{
    Logger.LogError(ex, "Error copying document file for next step. SourceFileId={SourceFileId}", sourceFileId);
    throw new UserFriendlyException(L["ErrorCopyingFileForNextStep"]);
    // HOẶC: return null; và xử lý ở caller để skip ký nếu không có file
}
```

---

## 🟠 ISSUE-03: RETURN/REJECT dùng DocumentStatusCode.HT (Hoàn thành) - Sai logic nghiệp vụ

- **Mức độ:** 🟠 Cao
- **File:** `DocumentWorkflowInstancesAppService.Extended.cs`
- **Dòng:** 531-544 (`ProcessWorkflowActionAsync`)
- **Trạng thái:** ✅ Đã sửa (13/02/2026) — Thêm enum `TRA_VE`, `TU_CHOI` vào `DocumentStatusCode`. RETURN → TRA_VE, REJECT → TU_CHOI. **Lưu ý:** Cần thêm MasterData records tương ứng trong DB (Code="TRA_VE", Code="TU_CHOI", Type="TRANG_THAI_VB")

**Mô tả:**
Khi văn bản bị **Trả về** (RETURN), Document status được set thành `HT` (Hoàn thành). Nghiệp vụ: khi trả về, người gửi cần chỉnh sửa và gửi lại → status nên là `TRA_VE` hoặc giữ `DANG_XU_LY`, không phải `HT`.

Khi **Từ chối** (REJECT), dùng `HT` cũng có thể sai - nên là `DA_HUY` hoặc `TU_CHOI`.

**Code hiện tại:**
```csharp
// Line 531-544
case nameof(WorkflowInstanceLogAction.RETURN):
    await HandleTerminalActionAsync(..., DocumentStatusCode.HT);  // ← SAI: Trả về = Hoàn thành?
    break;
case nameof(WorkflowInstanceLogAction.REJECT):
    await HandleTerminalActionAsync(..., DocumentStatusCode.HT);  // ← SAI: Từ chối = Hoàn thành?
    break;
```

**Giải pháp:**
```csharp
case nameof(WorkflowInstanceLogAction.RETURN):
    await HandleTerminalActionAsync(..., DocumentStatusCode.TRA_VE);  // hoặc DANG_XU_LY
    break;
case nameof(WorkflowInstanceLogAction.REJECT):
    await HandleTerminalActionAsync(..., DocumentStatusCode.DA_HUY);  // hoặc TU_CHOI
    break;
```

Cần thêm enum values vào `DocumentStatusCode` và MasterData tương ứng.

---

## 🟠 ISSUE-04: PARALLEL mode - RETURN revoke TOÀN BỘ assignments thay vì chỉ cùng step

- **Mức độ:** 🟠 Cao
- **File:** `DocumentWorkflowInstancesAppService.Extended.cs`
- **Dòng:** 774-786 (`HandleTerminalActionAsync`)
- **Trạng thái:** ✅ Đã sửa (13/02/2026) — RETURN trong PARALLEL mode giờ chỉ revoke assignments cùng StepOrder. REJECT vẫn revoke toàn bộ

**Mô tả:**
`HandleTerminalActionAsync` dùng chung cho cả RETURN và REJECT. Code revoke **TẤT CẢ** pending assignments (`IsCurrent && PENDING && Id != current`). Trong chế độ PARALLEL:
- REJECT: Đúng - nên hủy toàn bộ
- RETURN: Chưa rõ - có thể chỉ nên hủy assignments cùng step, để các step khác tiếp tục

**Code hiện tại:**
```csharp
// Line 774-778 - Revoke ALL pending, không phân biệt step
var otherPendingAssignments = await _documentAssignmentRepository.GetListAsync(
    x => x.DocumentId == instance.DocumentId
    && x.IsCurrent
    && x.Status == nameof(DocumentAssignmentStatus.PENDING)
    && x.Id != assignment.Id);
```

**Giải pháp:**
Cân nhắc tách logic RETURN và REJECT cho PARALLEL mode:

```csharp
// Nếu RETURN trong PARALLEL: chỉ revoke cùng step
if (isParallel && logAction == nameof(WorkflowInstanceLogAction.RETURN))
{
    otherPendingAssignments = otherPendingAssignments
        .Where(x => x.StepOrder == assignment.StepOrder).ToList();
}
```

Hoặc nếu nghiệp vụ quy định RETURN/REJECT trong PARALLEL đều hủy toàn bộ, thì cần document rõ ràng.

---

## 🟠 ISSUE-05: CheckAndHandleOverdueAsync gọi từ Frontend - Lỗ hổng bảo mật

- **Mức độ:** 🟠 Cao
- **File:** `DocumentWorkflowInstancesAppService.Extended.cs`
- **Dòng:** 1204-1253 (`CheckAndHandleOverdueAsync`)
- **Trạng thái:** ✅ Đã sửa (13/02/2026) — Thêm authorization: kiểm tra user phải là creator hoặc receiver của workflow. Sửa logAction WORKFLOW_COMPLETED → WORKFLOW_CANCELLED. Re-throw UserFriendlyException cho UI

**Mô tả:**
Logic kiểm tra quá hạn (overdue) và **HỦY workflow** (`CANCELLED`) được trigger từ frontend khi user mở modal. Vấn đề:

1. **Bất kỳ user nào** có permission `DocumentAssignments.Default` đều có thể trigger hủy workflow bằng cách gọi API
2. **Không kiểm tra** user có liên quan đến workflow instance không
3. **Server time phụ thuộc** vào lúc user mở modal - nếu không ai mở modal thì workflow không bao giờ bị hủy dù đã quá hạn
4. **Side effect từ GET-like operation** - user chỉ muốn xem thông tin nhưng lại trigger hủy dữ liệu

**Code hiện tại:**
```csharp
// Line 1204-1206 - Bất kỳ user nào có permission đều gọi được
[Authorize(HCPermissions.DocumentAssignments.Default)]
public async Task<WorkflowOverdueCheckResultDto> CheckAndHandleOverdueAsync(Guid workflowInstanceId)
{
    // ...
    // Line 1236-1243 - TỰ ĐỘNG hủy workflow khi quá hạn
    await UpdateWorkflowStatusCommonAsync(
        instance: instance,
        documentStatusCode: DocumentStatusCode.DA_HUY,
        historyComment: "Hết hạn xử lý tài liệu",
        workflowInstanceStatus: nameof(DocumentWorkflowInstanceStatus.CANCELLED),
        logNote: "Hết hạn xử lý tài liệu",
        logAction: nameof(WorkflowInstanceLogAction.WORKFLOW_COMPLETED)
    );
}
```

**Giải pháp:**
1. **Tách read và write**: Tạo 2 API riêng: `CheckOverdueAsync` (read-only, trả kết quả) và `HandleOverdueAsync` (write, chỉ system/admin gọi)
2. **Dùng Background Job**: Tạo recurring job (Hangfire/Quartz) chạy mỗi 5-15 phút để check và hủy workflow quá hạn
3. **Thêm authorization**: Kiểm tra user phải liên quan đến workflow trước khi cho phép trigger

```csharp
// Background Job approach
public class WorkflowOverdueCheckerJob : AsyncBackgroundJob<WorkflowOverdueCheckerArgs>
{
    public override async Task ExecuteAsync(WorkflowOverdueCheckerArgs args)
    {
        var overdueInstances = await _repository.GetListAsync(
            x => x.Status == "IN_PROGRESS"
            && x.FinishedAt > DateTime.MinValue
            && x.FinishedAt <= DateTime.UtcNow);

        foreach (var instance in overdueInstances)
        {
            // Cancel workflow, update status, create logs...
        }
    }
}
```

---

## 🟠 ISSUE-06: GetDocumentSigningListAsync load TOÀN BỘ assignments vào memory

- **Mức độ:** 🟠 Cao (Performance)
- **File:** `DocumentWorkflowInstancesAppService.Extended.cs`
- **Dòng:** 887-1077 (`GetDocumentSigningListAsync`)
- **Trạng thái:** ✅ Đã sửa (13/02/2026) — Refactored dùng IQueryable JOIN: filter, count, page đều chạy ở DB level. Chỉ load paged documents + related data cho trang hiện tại

**Mô tả:**
Hàm load **TẤT CẢ** assignment records (received + created) của user vào memory, sau đó load tất cả documents theo IDs, rồi filter/page trong C#. Nếu user có 5000 assignments → load 5000 records + load hàng nghìn documents.

**Code hiện tại:**
```csharp
// Line 892-893 - Load TẤT CẢ received assignments (không giới hạn)
var receivedAssignments = await _documentAssignmentRepository.GetListAsync(
    x => x.ReceiverUserId == currentUserId);

// Line 897-899 - Load TẤT CẢ created assignments
var createdAssignments = await AsyncExecuter.ToListAsync(
    allAssignmentsQueryable.Where(x => x.CreatorId == currentUserId));

// Line 906-908 - Load TẤT CẢ documents theo IDs
var allRelevantDocuments = allDocIds.Any()
    ? await _documentRepository.GetListAsync(x => allDocIds.Contains(x.Id))
    : new List<Document>();

// Line 911-929 - Filter trong C# memory thay vì SQL
if (input.FromDate.HasValue)
    allRelevantDocuments = allRelevantDocuments.Where(d => d.IncommingDate >= ...).ToList();
```

**Giải pháp:**
Viết custom repository method dùng SQL JOIN để filter + page ở database level:

```csharp
// Approach: Dùng queryable JOIN thay vì load riêng
public async Task<DocumentSigningPageResultDto> GetDocumentSigningListAsync(...)
{
    var assignmentQuery = await _documentAssignmentRepository.GetQueryableAsync();
    var documentQuery = await _documentRepository.GetQueryableAsync();

    // Join assignments → documents, filter ở DB
    var baseQuery = from a in assignmentQuery
                    join d in documentQuery on a.DocumentId equals d.Id
                    where a.ReceiverUserId == currentUserId || a.CreatorId == currentUserId
                    select new { Assignment = a, Document = d };

    // Apply date/text filter ở DB
    if (input.FromDate.HasValue)
        baseQuery = baseQuery.Where(x => x.Document.IncommingDate >= input.FromDate.Value.Date);

    // Count ở DB
    var totalCount = await AsyncExecuter.CountAsync(baseQuery);

    // Page ở DB
    var pagedResults = await AsyncExecuter.ToListAsync(
        baseQuery.OrderByDescending(x => x.Document.IncommingDate)
            .Skip(input.SkipCount).Take(input.MaxResultCount));
}
```

---

## 🟠 ISSUE-07: GetWorkflowSubmitInfoAsync không filter IsActive cho template

- **Mức độ:** 🟠 Cao
- **File:** `DocumentWorkflowInstancesAppService.Extended.cs`
- **Dòng:** 124-125 (`GetWorkflowSubmitInfoAsync`)
- **Trạng thái:** ✅ Đã sửa (13/02/2026) — Thêm sort `OrderByDescending(CreationTime)` để luôn lấy template mới nhất thay vì ngẫu nhiên. WorkflowTemplate không có IsActive nhưng dùng soft-delete (FullAuditedAggregateRoot) tự filter IsDeleted

**Mô tả:**
Lấy danh sách templates nhưng không filter `IsActive`. `FirstOrDefault()` không có sort → lấy template ngẫu nhiên nếu có nhiều template.

**Code hiện tại:**
```csharp
// Line 124-125
var templates = await _workflowTemplateRepository.GetListAsync(x => x.WorkflowId == workflowId);
var activeTemplate = templates.FirstOrDefault();  // ← Không filter IsActive, không sort
```

**Giải pháp:**
```csharp
var templates = await _workflowTemplateRepository.GetListAsync(
    x => x.WorkflowId == workflowId && x.IsActive);
var activeTemplate = templates.OrderByDescending(x => x.CreationTime).FirstOrDefault();
if (activeTemplate == null)
{
    throw new UserFriendlyException(L["NoActiveWorkflowTemplateFound"]);
}
```

---

## 🟡 ISSUE-08: DateTime.Now không nhất quán - Không dùng UTC/Clock

- **Mức độ:** 🟡 Trung bình
- **File:** `DocumentWorkflowInstancesAppService.Extended.cs`, `DocumentsAppService.Extended.cs`
- **Dòng:** Nhiều nơi (229, 264, 304, 511, 1314, 1483, v.v.)
- **Trạng thái:** ✅ Đã sửa (13/02/2026) — Thay tất cả DateTime.Now bằng Clock.Now (ABP IClock) ở cả 2 file

**Mô tả:**
Toàn bộ code sử dụng `DateTime.Now` (local time) thay vì `DateTime.UtcNow` hoặc ABP's `Clock.Now`. Nếu deploy nhiều server ở timezone khác nhau, SLA deadline (`FinishedAt`) sẽ bị tính sai.

**Các vị trí cần sửa:**
```
DocumentWorkflowInstancesAppService.Extended.cs:
- Line 229: var now = DateTime.Now; (SubmitToWorkflowAsync)
- Line 304: var nowTime = DateTime.Now; (SubmitToWorkflowAsync)
- Line 511: var now = DateTime.Now; (ProcessWorkflowActionAsync)
- Line 1314: instance.FinishedAt = DateTime.Now; (UpdateWorkflowStatusCommonAsync)
- Line 1395: DateTime.Now (CopyDocumentFileForNextStepAsync)
- Line 1483: var now = DateTime.Now; (ApplyElectronicSignatureAsync)
- Line 2038: DateTime.Now (MergeSignedPdfsForParallelAsync)

DocumentsAppService.Extended.cs:
- Line 264: var now = DateTime.Now; (SendDocumentAsync)
```

**Giải pháp:**
Inject `IClock` (ABP Framework) và dùng `Clock.Now` thay thế toàn bộ `DateTime.Now`:

```csharp
// Inject
private readonly IClock _clock;

// Sử dụng
var now = _clock.Now;  // Tự động xử lý UTC/Local theo cấu hình ABP
```

---

## 🟡 ISSUE-09: Thiếu cleanup assignments cũ khi re-submit document đã RETURNED

- **Mức độ:** 🟡 Trung bình
- **File:** `DocumentWorkflowInstancesAppService.Extended.cs`
- **Dòng:** 275-280 (`SubmitToWorkflowAsync`)
- **Trạng thái:** ✅ Đã sửa (13/02/2026) — Thêm cleanup: mark old REJECTED/REVOKE assignments IsCurrent=false trước khi re-submit

**Mô tả:**
Khi submit workflow, chỉ kiểm tra có instance `IN_PROGRESS` không. Nhưng nếu document đã bị RETURNED, user submit lại → tạo assignments mới nhưng **assignments cũ** (REJECTED, REVOKE) vẫn tồn tại. Query `GetDocumentSigningListAsync` có thể bị ảnh hưởng.

**Code hiện tại:**
```csharp
// Line 275-280 - Chỉ check IN_PROGRESS, không check RETURNED
var existingInstances = await _documentWorkflowInstanceRepository.GetListAsync(
    x => x.DocumentId == documentId && x.Status == nameof(DocumentWorkflowInstanceStatus.IN_PROGRESS));
if (existingInstances.Any())
{
    throw new UserFriendlyException(L["DocumentAlreadyHasActiveWorkflow"]);
}
// → THIẾU: cleanup old assignments từ instance RETURNED/REJECTED trước đó
```

**Giải pháp:**
```csharp
// Sau khi check không có IN_PROGRESS, cleanup assignments cũ nếu re-submit
var oldInstances = await _documentWorkflowInstanceRepository.GetListAsync(
    x => x.DocumentId == documentId &&
    (x.Status == nameof(DocumentWorkflowInstanceStatus.RETURNED) ||
     x.Status == nameof(DocumentWorkflowInstanceStatus.REJECTED)));

foreach (var oldInstance in oldInstances)
{
    var oldAssignments = await _documentAssignmentRepository.GetListAsync(
        x => x.DocumentId == documentId && x.WorkflowStepTemplateId != null);
    // Mark old assignments as obsolete hoặc soft delete
}
```

---

## 🟡 ISSUE-10: HandleApproveAsync (SEQUENTIAL) - instance.StartedAt bị ghi đè

- **Mức độ:** 🟡 Trung bình
- **File:** `DocumentWorkflowInstancesAppService.Extended.cs`
- **Dòng:** 634-638 (`HandleApproveAsync`)
- **Trạng thái:** ✅ Đã sửa (13/02/2026) — Xóa dòng instance.StartedAt = now, giữ nguyên thời gian bắt đầu workflow

**Mô tả:**
Khi chuyển sang step tiếp theo, `instance.StartedAt` bị ghi đè thành thời gian hiện tại. Mất thông tin workflow bắt đầu lúc nào. Field này nên là immutable sau khi tạo.

**Code hiện tại:**
```csharp
// Line 634-638
instance.CurrentStepId = nextStep.Id;
instance.StartedAt = now;  // ← GHI ĐÈ thời gian bắt đầu workflow
instance.FinishedAt = nextStep.SLADays.HasValue
    ? now.AddDays(nextStep.SLADays.Value)
    : DateTime.MinValue;
```

**Giải pháp:**
Giữ `StartedAt` ban đầu. Nếu cần track thời gian bắt đầu từng step, dùng field riêng hoặc log:

```csharp
instance.CurrentStepId = nextStep.Id;
// KHÔNG ghi đè StartedAt
// instance.StartedAt = now;  // ← XÓA DÒNG NÀY
instance.FinishedAt = nextStep.SLADays.HasValue
    ? now.AddDays(nextStep.SLADays.Value)
    : DateTime.MinValue;
```

---

## 🟡 ISSUE-11: N+1 Query trong MergeSignedPdfsForParallelAsync

- **Mức độ:** 🟡 Trung bình (Performance)
- **File:** `DocumentWorkflowInstancesAppService.Extended.cs`
- **Dòng:** 1969-2025 (`MergeSignedPdfsForParallelAsync`)
- **Trạng thái:** ✅ Đã sửa (13/02/2026) — Batch load users, signatures, logs trước vòng lặp. 3N queries → 3 queries

**Mô tả:**
Trong vòng lặp foreach, mỗi assignment gọi 3 DB queries:
1. `_identityUserRepository.GetAsync(userId)` - lấy user info
2. `_userSignatureRepository.GetListAsync(...)` - lấy signature
3. `_documentWorkflowInstanceLogsRepository.GetListAsync(...)` - lấy logs

N assignments = 3N queries.

**Code hiện tại:**
```csharp
// Line 1969-2007
foreach (var doneAssignment in allDoneAssignments)
{
    var user = await _identityUserRepository.GetAsync(userId);              // Query 1
    var userSignatures = await _userSignatureRepository.GetListAsync(...);  // Query 2
    var logs = await _documentWorkflowInstanceLogsRepository.GetListAsync(...); // Query 3
    // ...
}
```

**Giải pháp:**
Batch load trước vòng lặp:

```csharp
// Batch load users
var userIds = allDoneAssignments.Select(a => a.ReceiverUserId).Distinct().ToList();
var users = await _identityUserRepository.GetListAsync(x => userIds.Contains(x.Id));
var userDict = users.ToDictionary(u => u.Id);

// Batch load signatures
var allSignatures = await _userSignatureRepository.GetListAsync(
    signType: nameof(SignType.ELECTRONIC), isActive: true);
var signatureDict = allSignatures
    .Where(s => userIds.Contains(s.IdentityUserId))
    .GroupBy(s => s.IdentityUserId)
    .ToDictionary(g => g.Key, g => g.First());

// Batch load logs
var assignmentIds = allDoneAssignments.Select(a => a.Id).ToList();
var allLogs = await _documentWorkflowInstanceLogsRepository.GetListAsync(
    x => x.DocumentWorkflowInstanceId == instance.Id
    && assignmentIds.Contains(x.DocumentAssignmentId ?? Guid.Empty)
    && x.Action == nameof(WorkflowInstanceLogAction.APPROVE));
var logDict = allLogs.GroupBy(l => l.DocumentAssignmentId ?? Guid.Empty)
    .ToDictionary(g => g.Key, g => g.OrderByDescending(l => l.CreationTime).First());

// Sau đó trong vòng lặp chỉ đọc từ dictionary
foreach (var doneAssignment in allDoneAssignments)
{
    userDict.TryGetValue(doneAssignment.ReceiverUserId, out var user);
    signatureDict.TryGetValue(doneAssignment.ReceiverUserId, out var signature);
    logDict.TryGetValue(doneAssignment.Id, out var log);
    // ...
}
```

---

## 🟡 ISSUE-12: ApplyElectronicSignatureAsync load TẤT CẢ UserSignature ELECTRONIC

- **Mức độ:** 🟡 Trung bình (Performance)
- **File:** `DocumentWorkflowInstancesAppService.Extended.cs`
- **Dòng:** 1451-1454 (`ApplyElectronicSignatureAsync`)
- **Trạng thái:** ✅ Đã sửa (13/02/2026) — Dùng queryable filter IdentityUserId trực tiếp thay vì load toàn bộ

**Mô tả:**
Load tất cả chữ ký điện tử active của toàn hệ thống, rồi filter trong C# memory. Nếu 10,000 users có signature → load 10,000 records chỉ để lấy 1.

**Code hiện tại:**
```csharp
// Line 1451-1454
var userSignatures = await _userSignatureRepository.GetListAsync(
    signType: nameof(SignType.ELECTRONIC),
    isActive: true);
signature = userSignatures.FirstOrDefault(s => s.IdentityUserId == currentUserId);
```

**Giải pháp:**
Filter `IdentityUserId` trực tiếp trong query. Nếu repository không hỗ trợ, dùng queryable:

```csharp
var sigQueryable = await _userSignatureRepository.GetQueryableAsync();
var signature = await AsyncExecuter.FirstOrDefaultAsync(
    sigQueryable.Where(s => s.IdentityUserId == currentUserId
        && s.SignType == nameof(SignType.ELECTRONIC)
        && s.IsActive));
```

---

## 🟡 ISSUE-13: Thiếu explicit transaction cho SubmitToWorkflowAsync

- **Mức độ:** 🟡 Trung bình
- **File:** `DocumentWorkflowInstancesAppService.Extended.cs`
- **Dòng:** 194-448 (`SubmitToWorkflowAsync`)
- **Trạng thái:** ✅ Đã sửa (13/02/2026) — Thêm [UnitOfWork] attribute đảm bảo atomic transaction

**Mô tả:**
`SubmitToWorkflowAsync` thực hiện 7+ thao tác write (Document, DocumentFile, WorkflowInstance, Assignments, History, Logs, InstanceFiles, Notifications). ABP mặc định wrap AppService method trong UnitOfWork, nhưng nếu exception xảy ra ở bước 6 (notification), các bước 1-5 đã commit (do `autoSave` ở một số chỗ).

**Giải pháp:**
1. Đảm bảo không dùng `autoSave: true` trong `SubmitToWorkflowAsync` (hiện tại không thấy, nhưng cần verify)
2. Hoặc wrap explicit trong UnitOfWork:

```csharp
[UnitOfWork]
public async Task<DocumentWorkflowInstanceDto> SubmitToWorkflowAsync(SubmitToWorkflowInput input)
{
    // ... tất cả operations ...
    // Notification nằm trong try-catch riêng nên OK
}
```

---

## 🟡 ISSUE-14: HandleTerminalActionAsync revoke không filter theo StepOrder

- **Mức độ:** 🟡 Trung bình
- **File:** `DocumentWorkflowInstancesAppService.Extended.cs`
- **Dòng:** 774-786
- **Trạng thái:** ✅ Đã sửa (13/02/2026) — Thêm filter StepOrder cho SEQUENTIAL mode. Kết hợp với ISSUE-04 fix cho PARALLEL mode

**Mô tả:**
Trong SEQUENTIAL mode, nếu có data inconsistency (assignments từ step cũ vẫn còn `IsCurrent = true` do bug), code sẽ revoke nhầm assignments không liên quan.

**Code hiện tại:**
```csharp
// Không filter theo WorkflowStepTemplateId hoặc StepOrder
var otherPendingAssignments = await _documentAssignmentRepository.GetListAsync(
    x => x.DocumentId == instance.DocumentId
    && x.IsCurrent
    && x.Status == nameof(DocumentAssignmentStatus.PENDING)
    && x.Id != assignment.Id);
```

**Giải pháp:**
Thêm filter `StepOrder` hoặc `WorkflowStepTemplateId` cho SEQUENTIAL mode:

```csharp
var otherPendingAssignments = await _documentAssignmentRepository.GetListAsync(
    x => x.DocumentId == instance.DocumentId
    && x.IsCurrent
    && x.Status == nameof(DocumentAssignmentStatus.PENDING)
    && x.Id != assignment.Id
    && x.StepOrder == assignment.StepOrder);  // ← Thêm filter step
```

---

## 🔵 ISSUE-15: Constructor quá lớn - 20+ dependencies

- **Mức độ:** 🔵 Thấp (Maintainability)
- **File:** `DocumentWorkflowInstancesAppService.Extended.cs`
- **Dòng:** 66-110
- **Trạng thái:** ⏭️ Skip — Cần refactor kiến trúc lớn (tách service). Thực hiện khi có thời gian

**Mô tả:**
Constructor inject 20 dependencies → class đang vi phạm Single Responsibility Principle. File dài ~2100 dòng, khó maintain.

**Giải pháp (khi có thời gian):**
Tách thành các service nhỏ:
- `WorkflowSubmitService` - logic submit workflow
- `WorkflowActionService` - logic approve/return/reject
- `ElectronicSigningService` - logic ký điện tử + PDF processing
- `WorkflowQueryService` - logic query danh sách

---

## 🔵 ISSUE-16: Hardcoded strings rải rác

- **Mức độ:** 🔵 Thấp
- **File:** `DocumentWorkflowInstancesAppService.Extended.cs`
- **Dòng:** Nhiều nơi
- **Trạng thái:** ✅ Đã sửa (13/02/2026) — Tạo WorkflowConstants class, thay thế hardcoded strings

**Mô tả:**
Các magic strings chưa được đưa vào constants:
- `"Initiator"` (Line 401)
- `"Processor"` (trong HandleApproveAsync)
- `"System"` (Line 808, 1333)
- `"NORMAL"`, `"HIGH"` (notification priority)
- `"signing-steps/"`, `"electronic-signed/"` (blob paths)

**Giải pháp:**
Tạo constants class:

```csharp
public static class WorkflowConstants
{
    public const string RoleInitiator = "Initiator";
    public const string RoleProcessor = "Processor";
    public const string RoleSystem = "System";
    public const string PriorityNormal = "NORMAL";
    public const string PriorityHigh = "HIGH";
    public const string BlobPathSigningSteps = "signing-steps/";
    public const string BlobPathElectronicSigned = "electronic-signed/";
}
```

---

## 🔵 ISSUE-17: WorkflowStepAssignmentsAppService.Extended.cs - Shadowed fields

- **Mức độ:** 🔵 Thấp
- **File:** `WorkflowStepAssignmentsAppService.Extended.cs`
- **Dòng:** 43-45
- **Trạng thái:** ✅ Đã sửa (13/02/2026) — Xóa khai báo lại shadowed fields, sử dụng base class fields

**Mô tả:**
Fields `_workflowStepTemplateRepository` và `_identityUserRepository` đã khai báo ở base class `WorkflowStepAssignmentsAppServiceBase` nhưng lại khai báo lại ở derived class → shadowed. Có thể gây bug nếu base class methods dùng field cũ.

**Code hiện tại:**
```csharp
// Line 43-45 (Extended) - SHADOW base class fields
protected IRepository<WorkflowStepTemplate, Guid> _workflowStepTemplateRepository;
protected IRepository<IdentityUser, Guid> _identityUserRepository;
```

**Giải pháp:**
Xóa khai báo lại ở derived class, dùng `new` keyword nếu cố ý, hoặc sử dụng trực tiếp field từ base class.

---

## 🔵 ISSUE-18: DocumentsAppService.SendDocumentAsync dùng DateTime.Now

- **Mức độ:** 🔵 Thấp
- **File:** `DocumentsAppService.Extended.cs`
- **Dòng:** 264
- **Trạng thái:** ✅ Đã sửa (13/02/2026) — Thay DateTime.Now bằng Clock.Now

**Mô tả:**
Tương tự ISSUE-08, dùng `DateTime.Now` thay vì `Clock.Now`.

---

## THỨ TỰ ƯU TIÊN SỬA

| Ưu tiên | Issue | Lý do |
|---------|-------|-------|
| 1 | 🔴 ISSUE-01 | Race condition có thể gây duplicate data ngay lập tức |
| 2 | 🔴 ISSUE-02 | Mất dữ liệu file ký khi copy fail |
| 3 | 🟠 ISSUE-03 | Status sai ảnh hưởng nghiệp vụ |
| 4 | 🟠 ISSUE-05 | Lỗ hổng bảo mật + logic sai |
| 5 | 🟠 ISSUE-06 | Performance ảnh hưởng lớn khi data tăng |
| 6 | 🟠 ISSUE-07 | Có thể lấy sai template |
| 7 | 🟠 ISSUE-04 | Logic RETURN trong PARALLEL chưa rõ |
| 8 | 🟡 ISSUE-08 | DateTime consistency |
| 9 | 🟡 ISSUE-09 | Data cleanup khi re-submit |
| 10 | 🟡 ISSUE-10 | Mất thông tin StartedAt |
| 11 | 🟡 ISSUE-11 | N+1 query performance |
| 12 | 🟡 ISSUE-12 | Query performance |
| 13 | 🟡 ISSUE-13 | Transaction safety |
| 14 | 🟡 ISSUE-14 | Revoke filter safety |
| 15-18 | 🔵 | Code quality - sửa khi refactor |


