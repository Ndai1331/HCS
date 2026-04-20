# Documents Module – Performance Optimization Plan

> Phạm vi: chức năng Documents (danh sách, chi tiết, gửi/nhận, trình ký, phê duyệt, ký số) — các project:
> `src/HC.Blazor/Pages/Documents`, `src/HC.Application/Documents`,
> `src/HC.Domain/Documents`, `src/HC.EntityFrameworkCore/Documents`.
>
> Tài liệu này tổng hợp các điểm nghẽn đã rà soát, kế hoạch tối ưu chia làm 3 đợt
> (Quick Wins → Mid-term → Long-term) và **trạng thái triển khai** của từng item.

---

## 1. Bottlenecks tổng quan


| #   | Khu vực                           | Vấn đề                                                                                                                                                                            |
| --- | --------------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| A   | `DocumentDetail`                  | Sau `GetWithNavigationPropertiesAsync` còn gọi thêm 6 lookup-by-id **tuần tự**                                                                                                    |
| B   | `Documents.razor` first paint     | 8 × `IsGrantedAsync` tuần tự + ~7 lookup → ~15 RTT                                                                                                                                |
| C   | `DocumentAssignments`             | 3 lookup gọi nối tiếp ở `OnAfterRenderAsync`                                                                                                                                      |
| D   | `DocumentSigning` modal           | 7 call `Task.WhenAll` + `LoadCurrentStepDetailAsync` 2 call nối tiếp                                                                                                              |
| E   | Repositories                      | `GetWithNavigationPropertiesAsync` dùng `FirstOrDefault` lồng trong `Select` → SQL kém                                                                                            |
| F   | `EfCoreDocumentHistoryRepository` | `JsonSerializer.Serialize(query)` + `Serialize(res)` log toàn bộ list                                                                                                             |
| G   | Approval / Sign                   | PDF stamping, SHA-256, LibreOffice chạy đồng bộ trên thread HTTP                                                                                                                  |
| H   | `UpdateDocumentStatusAsync`       | `GetAsync` lại Document + query MasterData mỗi lần (không cache)                                                                                                                  |
| I   | SentToMe                          | `PopulateSentToMeDisplayNamesAsync` load full entity `IdentityUser` & `Department`                                                                                                |
| J   | DB index                          | Thiếu `(Type,Code)` MasterData, `(DocumentId,IsCurrent)` DocumentAssignment, `(IdentityUserId,IsActive)` UserSignature, `Documents.CreatorId`; `ILIKE %x%` không dùng được B-tree |


---

## 2. Đợt 1 — Quick Wins (đang triển khai)

Mục tiêu: rủi ro thấp, ROI cao, mỗi item là 1 commit nhỏ độc lập.


| #   | Việc                                                                                                       | File chính                                                                                           | Trạng thái |
| --- | ---------------------------------------------------------------------------------------------------------- | ---------------------------------------------------------------------------------------------------- | ---------- |
| QW1 | Bỏ `JsonSerializer.Serialize(query)` & `Serialize(res)` trong list lịch sử; hạ thành `LogDebug` có guard   | `src/HC.EntityFrameworkCore/DocumentHistories/EfCoreDocumentHistoryRepository.cs`                    | ✅ Done     |
| QW2 | `DocumentDetail` dùng luôn nav props của `GetWithNavigationPropertiesAsync`, **bỏ 6 lookup-by-id tuần tự** | `src/HC.Blazor/Pages/Documents/DocumentDetail.razor.cs`                                              | ✅ Done     |
| QW3 | Cache `(Type, Code) → MasterData.Id` (`IDistributedCache`) cho `UpdateDocumentStatusAsync`                 | `src/HC.Application/Documents/DocumentsAppService.Extended.cs` (+ helper cache)                      | ✅ Done     |
| QW4 | Sửa `query.Count()` đồng bộ → `await AsyncExecuter.CountAsync` + projection `LookupDto` ngay trong query   | `src/HC.Application/Documents/DocumentsAppService.cs`                                                | ✅ Done     |
| QW5 | 8 × `IsGrantedAsync` tuần tự → `Task.WhenAll` (multi-permission) ở first paint                             | `src/HC.Blazor/Pages/Documents/Documents.razor.cs`                                                   | ✅ Done     |
| QW6 | 3 lookup ở `DocumentAssignments` → `Task.WhenAll`                                                          | `src/HC.Blazor/Pages/Documents/DocumentAssignments.razor.cs`                                         | ✅ Done     |
| QW7 | Projection `IdentityUser` (Id, Surname, Name, UserName, Email) & `Department` (Id, Name)                   | `src/HC.Application/Documents/DocumentsAppService.Extended.cs` (`PopulateSentToMeDisplayNamesAsync`) | ✅ Done     |
| QW8 | Migration index: `MasterData(Type, Code)`, `DocumentAssignment(DocumentId, IsCurrent)` filtered            | `src/HC.EntityFrameworkCore/Migrations`                                                              | ✅ Done     |


### Lợi ích kỳ vọng

- **DocumentDetail mở 1 văn bản**: cắt **6 round-trip** lookup (≈ 0.6–1.2 s tùy mạng).
- **Documents first paint**: cắt **7 RTT** permission + giảm thời gian render.
- **Mỗi action ký/duyệt**: cắt query MasterData (cache hit) → ổn định p99.
- **List lịch sử**: bỏ serialize JSON → giảm CPU/RAM, có thể nhanh 2–10 lần khi nhiều dòng.
- **SentToMe list**: payload IdentityUser nhỏ hơn nhiều, EF mapping nhanh hơn.
- **Index**: tra master `(Type,Code)` và assignment "current of doc" chuyển từ seq scan → index scan.

---

## 3. Đợt 2 — Mid-term (sau Quick Wins)


| #   | Việc                                                                                                                                                                          | Ghi chú                                                      |
| --- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------ |
| M1  | Endpoint **bundle** cho 3 trang chính: `documents/page-bootstrap`, `documents/{id}/detail-bundle`, `workflow-instances/{id}/action-bundle`                                    | Giảm 5–10 RTT/lần                                            |
| M2  | Sửa pattern `FirstOrDefault` lồng trong `Select` ở 4 repository → join chuẩn                                                                                                  | Document, DocumentAssignment, DocumentHistory, UserSignature |
| M3  | Cache Redis: `MasterDataLookup`, `UnitLookup`, `WorkflowLookup`, `DepartmentLookup`, `UserDisplayName`, `UserActiveSignature`, `SignatureSettingByProvider`, `WatermarkedPdf` | Invalidate qua `IDistributedEventBus`                        |
| M4  | Gộp `GetSentToMeDocumentIdsAsync` thành 1 sub-query LINQ trong repository                                                                                                     | Tránh materialize Id list về app                             |
| M5  | pg_trgm + GIN index cho `Documents.No`, `Documents.Title`                                                                                                                     | ILIKE `%x%` tăng tốc 10–100×                                 |
| M6  | Thêm index `Documents.CreatorId`, `UserSignatures(IdentityUserId, IsActive)`                                                                                                  | Rà soát query plan                                           |
| M7  | `IsDocumentNumberDuplicateAsync`/`IsStorageNumberDuplicateAsync`: bỏ `ToLower().Trim()` → dùng `EF.Functions.ILike` hoặc cột chuẩn hóa                                        | Tận dụng index                                               |


---

## 4. Đợt 3 — Long-term (kiến trúc)

- Đưa `ApproveWithNoteAsync`, `WorkflowSigningExecutionService.ApplyDigitalSignatureAsync`, `PrepareSubmissionPlaceholdersAsync` (LibreOffice subprocess) ra **background job** (`IBackgroundJobManager`), API trả `202 Accepted` + `operationId`, UI nhận kết quả qua SignalR.
- Tách worker service riêng cho PDF/sign để scale độc lập.
- Materialized view / read-model cho inbox SentToMe.
- Outbox pattern cho notification.

---

## 5. Cách verify hiệu quả

1. Bật log thời gian (`ILogger` đã có `GetListAsync start/end`) — đo trước/sau cho từng endpoint.
2. PostgreSQL: `EXPLAIN (ANALYZE, BUFFERS)` cho các query nóng (status lookup, current assignment of document).
3. Browser DevTools → Network: đếm số request / waterfall khi mở Documents/Detail/Signing.
4. Stress test: ApacheBench/k6 trên `GET /api/documents` (100 concurrent) trước/sau index + cache MasterData.

---

## 6. Lịch sử cập nhật

- 2026-04-20: Khởi tạo tài liệu, hoàn thành Đợt 1 (8 Quick Wins).