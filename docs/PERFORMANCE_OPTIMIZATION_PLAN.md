# Performance Optimization Backlog (API → Service → Blazor)

## 1) Mục tiêu
- Giảm độ trễ API ở các màn hình tải nhiều dữ liệu (Documents, Chat, Calendar).
- Giảm số round-trip DB/network không cần thiết.
- Giảm chi phí render và số lần gọi API lặp lại ở Blazor.
- Thiết lập quan sát hiệu năng để đo trước/sau tối ưu.

## 2) Ưu tiên tối ưu theo lớp

### P0 — API/Repository (ảnh hưởng lớn nhất)

#### 2.1. Tránh N+1 query trong `ContactAppService.GetContactsAsync`
**Hiện trạng**
- Với mỗi conversation, service gọi thêm:
  - `GetByConversationAndUserAsync(...)` (ít nhất 1 lần/conversation).
  - `GetByConversationIdAsync(...)` cho group/project/task.
- Pattern này tạo N+1 query khi danh sách conversation lớn.

**Tối ưu đề xuất**
- Thêm repository method dạng batch:
  - Lấy `ConversationMember` theo `conversationIds + currentUserId` trong 1 query.
  - Lấy `active member count` theo `conversationIds` bằng `GroupBy` trong 1 query.
- Map dữ liệu từ dictionary in-memory thay vì gọi DB trong vòng lặp.

**Kỳ vọng**
- Giảm mạnh số query DB (từ O(N) xuống O(1)-O(2) theo batch).

---

#### 2.2. Tối ưu lọc chuỗi `Contains` trong `EfCoreDocumentRepository`
**Hiện trạng**
- Bộ lọc search dùng nhiều `Contains` trên các cột text (`No`, `Title`, `CurrentStatus`, `StorageNumber`).
- Với PostgreSQL, pattern `%text%` có thể không dùng được index B-tree thông thường.

**Tối ưu đề xuất**
- Chuẩn hóa search strategy:
  - Nếu cần contains thực sự: dùng `ILIKE` + `pg_trgm` index (`GIN`/`GiST`) cho cột text chính.
  - Nếu có thể prefix search: đổi sang `StartsWith` ở các trường phù hợp.
- Tách filter phổ biến sang endpoint/search model chuyên dụng để kiểm soát query plan.
- Với các truy vấn read-only lớn: cân nhắc `AsNoTracking()` tại các repository query path.

**Kỳ vọng**
- Giảm full table scan, cải thiện response time ở màn hình danh sách tài liệu.

---

#### 2.3. Giảm round-trip `GetDbContextAsync()` lặp lại trong query build
**Hiện trạng**
- `GetQueryForNavigationPropertiesAsync()` gọi `GetDbContextAsync()` nhiều lần trong cùng một query.

**Tối ưu đề xuất**
- Cache local biến `var dbContext = await GetDbContextAsync();` rồi dùng lại cho tất cả join.
- Rà soát các repository tương tự để áp dụng nhất quán.

**Kỳ vọng**
- Giảm overhead không cần thiết khi build expression/query.

---

### P1 — Application Service

#### 2.4. Loại bỏ vòng lặp truy vấn theo phòng ban trong `SendDocumentAsync`
**Hiện trạng**
- Với mỗi `departmentId`, code gọi `_userDepartmentRepository.GetListAsync(...)` riêng biệt.
- Đây là N+1 query theo số phòng ban được chọn.

**Tối ưu đề xuất**
- Gộp truy vấn 1 lần:
  - Lấy toàn bộ user-department active bằng `departmentIds.Contains(ud.DepartmentId)`.
- Dùng `HashSet<Guid>` để deduplicate receiver IDs.

**Kỳ vọng**
- Tăng tốc khi gửi văn bản cho nhiều phòng ban.

---

#### 2.5. Batch insert/update cho notification + assignment + history
**Hiện trạng**
- Trong vòng lặp từng `receiverUserId`, code thực hiện nhiều thao tác insert/update tuần tự.

**Tối ưu đề xuất**
- Tạo list entity trước, dùng `InsertManyAsync` (nếu repository hỗ trợ) cho NotificationReceiver / DocumentHistory.
- Với assignment update: gom và update theo batch hoặc tối thiểu giảm SaveChanges nhiều lần (unit of work).
- Đảm bảo transaction boundary rõ ràng để giữ tính nhất quán.

**Kỳ vọng**
- Giảm transaction overhead, cải thiện throughput khi gửi cho nhiều người.

---

#### 2.6. Tránh tải full danh sách cho tác vụ lấy theo ID
**Hiện trạng**
- `GetMasterDataByIdAsync` gọi `GetListAsync(... MaxResultCount = 1000)` rồi mới `FirstOrDefault` theo ID.

**Tối ưu đề xuất**
- Thêm API chuyên dụng `GetLookupByIdAsync`/`GetAsync(id)` cho MasterData và Unit.
- Ở UI chỉ gọi đúng item cần thiết.

**Kỳ vọng**
- Cắt giảm payload và thời gian xử lý cho các màn hình chi tiết.

---

### P1 — Blazor App

#### 2.7. Dùng cache cho các lookup master data dùng lặp lại nhiều page
**Hiện trạng**
- `Documents`, `DocumentDetail`, `ViewDocumentDetail` đều gọi lại nhiều endpoint lookup với `MaxResultCount = 1000`.

**Tối ưu đề xuất**
- Tạo `LookupCacheService` scoped/singleton (TTL 5-15 phút) cho:
  - MasterData theo `Type`.
  - Unit lookup.
- Chuẩn hóa API lookup chung để tái sử dụng giữa các trang.

**Kỳ vọng**
- Giảm call API lặp lại khi user chuyển trang hoặc mở modal nhiều lần.

---

#### 2.8. Tối ưu render/alloc ở các method trả về `.ToList()` liên tục
**Hiện trạng**
- Nhiều method trả `Collection.ToList()` dù dữ liệu đã là list, gây cấp phát lặp lại.

**Tối ưu đề xuất**
- Trả trực tiếp `IReadOnlyList` hoặc chỉ clone khi thực sự cần immutable snapshot.
- Với component/filter gọi thường xuyên: hạn chế tạo list mới không cần thiết.

**Kỳ vọng**
- Giảm GC pressure, mượt hơn khi thao tác filter/search.

---

#### 2.9. Giới hạn kích thước lookup tải ban đầu
**Hiện trạng**
- Nhiều lookup request dùng cố định `MaxResultCount = 1000`.

**Tối ưu đề xuất**
- Giảm mặc định xuống 100-200 + server-side search khi user gõ.
- Chỉ prefetch dữ liệu thật sự cần cho lần render đầu.

**Kỳ vọng**
- Tăng tốc thời gian load trang ban đầu, giảm memory footprint.

## 3) Quick wins (có thể làm ngay)
1. Refactor `SendDocumentAsync`: batch query user theo department + `HashSet` dedup.
2. Refactor `ContactAppService.GetContactsAsync`: preload member info & member counts theo conversationIds.
3. Tạo shared lookup cache service cho 3 trang Documents-related.
4. Sửa `GetMasterDataByIdAsync` sang API by-id.

## 4) Đo lường trước/sau (bắt buộc)
- API metrics:
  - p50/p95 latency cho endpoints Documents, Contacts, SendDocument.
  - Số query/req và tổng thời gian DB/req.
- Blazor metrics:
  - Time-to-interactive cho Documents/DocumentDetail.
  - Số lượng API calls khi mở trang + thao tác filter.
- Hạ tầng:
  - CPU, memory, GC alloc rate ở HttpApi.Host và Blazor Server.

## 5) Checklist triển khai
- [ ] Bổ sung benchmark dữ liệu lớn (documents/conversations thực tế).
- [ ] Tối ưu query + index migration cho search text.
- [ ] Tối ưu service logic gửi văn bản theo batch.
- [ ] Tối ưu cache lookup phía Blazor.
- [ ] So sánh metrics trước/sau và chốt acceptance criteria.
