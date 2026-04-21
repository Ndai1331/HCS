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

## 2. Đợt 1 — Quick Wins (Hoàn thành 2026-04-20)

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

## 3. Đợt 2 — Mid-term (Hoàn thành 2026-04-20)


| #   | Việc                                                                                                                                                                          | File chính                                                                                                                                                                                          | Trạng thái |
| --- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ---------- |
| M1  | Endpoint **bundle** `documents/{id}/detail-bundle` gom Document + Files + Histories → 1 RTT thay vì 3                                                                         | `src/HC.Application.Contracts/Documents/DocumentDetailBundleDto.cs`, `IDocumentsAppService.Extended.cs`, `DocumentsAppService.Extended.cs`, `src/HC.Blazor/Pages/Documents/DocumentDetail.razor.cs` | ✅ Done     |
| M2  | Sửa pattern `FirstOrDefault` lồng trong `Select` ở 4 repository `GetWithNavigationPropertiesAsync` → tái dùng `GetQueryForNavigationPropertiesAsync` (LEFT JOIN) + async First | `EfCoreDocumentRepository.cs`, `EfCoreDocumentAssignmentRepository.cs`, `EfCoreDocumentHistoryRepository.cs`, `EfCoreUserSignatureRepository.cs`                                                    | ✅ Done     |
| M3  | Endpoint bundle `documents/page-bootstrap` (8 permission + 6 lookup) và `document-workflow-instances/action-bundle` (7 call modal ký) → mỗi trang cắt ~13 RTT                 | `DocumentsPageBootstrapDto.cs`, `WorkflowInstanceActionBundleDto.cs`, `DocumentsAppService.Extended.cs`, `DocumentWorkflowInstancesAppService.Extended.cs`, `Documents.razor.cs`, `DocumentSigning.razor.cs`, 2 controller | ✅ Done     |
| M4  | Cache Redis `DocumentsLookupCacheItem` cho `GetMasterDataLookupAsync`/`GetUnitLookupAsync`/`GetWorkflowLookupAsync` khi filter rỗng (first-page) + TTL 5 phút, tenant-scoped tự động | `DocumentsAppService.cs` (+ `DocumentsLookupCacheItem.cs`)                                                                                                                                          | ✅ Done     |
| M5  | Gộp `GetSentToMeDocumentIdsAsync` từ 3 query tuần tự + `Union` trong bộ nhớ thành 1 query SQL duy nhất với `EXISTS` subquery                                                 | `DocumentsAppService.Extended.cs`                                                                                                                                                                    | ✅ Done     |
| M6  | pg_trgm + GIN index cho `Documents.No`, `Documents.Title`, `Documents.StorageNumber`                                                                                          | Migration `20260420100000_AddDocumentsTextSearchAndMiscIndexes` + SQL song song (idempotent + concurrent)                                                                                           | ✅ Done     |
| M7  | Thêm index `Documents.CreatorId`, partial index `UserSignatures(IdentityUserId, IsActive) WHERE IsActive = true`                                                              | Cùng migration M6                                                                                                                                                                                    | ✅ Done     |
| M8  | `IsDocumentNumberDuplicateAsync`/`IsStorageNumberDuplicateAsync`: chuyển query xuống `IDocumentRepository.AnyByNoAsync` / `AnyByStorageNumberAsync` dùng `EF.Functions.ILike` tận dụng GIN trigram index | `IDocumentRepository.Extended.cs`, `EfCoreDocumentRepository.Extended.cs`, `DocumentsAppService.Extended.cs`                                                                                         | ✅ Done     |


### Lợi ích kỳ vọng

- **M1 — DocumentDetail 1 lần mở**: 3 HTTP request tuần tự → **1 request**, cắt thêm 2 client RTT (~200–600 ms tùy mạng). Server-side chạy tuần tự trong 1 UoW/DbContext, không thêm áp lực DB.
- **M2 — `GetWithNavigationPropertiesAsync`**: mỗi GET-by-id văn bản trước đây sinh **7 correlated subquery** (Field/Unit/Workflow/Status/Type/UrgencyLevel/SecrecyLevel). Sau refactor → **1 query LEFT JOIN** duy nhất. Tương tự: Assignment (4→1), History (3→1), UserSignature (1→1 async + AsNoTracking).
- **M3 — Trang `Documents` first paint**: 8 permission + 7 lookup = 15 HTTP call → **1 call** (page-bootstrap) + 1 call `Departments` = **2 RTT**. Modal ký: 7 parallel call → **1 call** (action-bundle) + `LoadSigningDocumentFilesAsync` chạy nền. Cả 2 có fallback path giữ nguyên logic cũ nếu bundle endpoint fail.
- **M4 — Lookup cache**: first paint của nhiều user cùng tenant chia sẻ Redis cache (TTL 5 phút), cắt ~3–6 SQL `SELECT COUNT + SELECT page` mỗi lần. Khi filter ≠ rỗng hoặc trang ≠ đầu thì fallback sang DB như cũ để giữ tính chính xác.
- **M5 — SentToMe**: trước đây 3 query (assignments, userDepartments, explicit) + `Union` in-memory → **1 query** duy nhất (EXISTS subquery + OR branch). Giảm cả latency DB lẫn áp lực GC khi inbox lớn.
- **M6 + M7 — Index**: `ILIKE '%x%'` trên `No`/`Title`/`StorageNumber` chuyển từ seq scan → GIN trigram index (10–100× với table lớn). `CreatorId` có B-tree, query "văn bản do tôi tạo" dùng index scan. `UserSignatures(IdentityUserId, IsActive) WHERE IsActive = true` partial index khớp đúng hot path của modal ký.
- **M8 — Duplicate check**: bỏ `TRIM(LOWER(x)) = y` (seq scan bắt buộc) → `ILIKE '@normalized'` khớp chính xác nhưng vẫn dùng được GIN trigram index vừa tạo ở M6.


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
- 2026-04-20: Áp migration performance indexes lên AxisHCS (host + 3 tenant), tạo SQL script cho DB song song (`docs/sql/`).
- 2026-04-20: Đợt 2 — Hoàn thành M1 (detail bundle cắt 2 RTT) và M2 (4 repository `GetWithNavigationPropertiesAsync` dùng LEFT JOIN).
- 2026-04-20: Đợt 2 — Hoàn thành M3 (2 bundle endpoint: `documents/page-bootstrap` + `document-workflow-instances/action-bundle`), M4 (Redis cache 3 lookup với TTL 5 phút, tenant-scoped), M5 (SentToMe gộp 1 query EXISTS subquery), M6 (pg_trgm + GIN index cho `No`, `Title`, `StorageNumber`), M7 (index `Documents.CreatorId` + partial index `UserSignatures(IdentityUserId, IsActive)`), M8 (duplicate check dùng `EF.Functions.ILike`). Áp migration `20260420100000_AddDocumentsTextSearchAndMiscIndexes` lên host + 3 tenant của AxisHCS, kèm 2 SQL (idempotent & `CONCURRENTLY`) tại `docs/sql/`.