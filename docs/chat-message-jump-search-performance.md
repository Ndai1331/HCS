# Hướng dẫn tối ưu chức năng **Reply → Jump** và **Search → Jump** trong Chat (HC/ABP/.NET + PostgreSQL)

> Mục tiêu: khi user bấm vào preview reply hoặc kết quả tìm kiếm thì chat tự động nhảy đúng message cũ (giống Messenger), đồng thời vẫn chạy nhanh khi dữ liệu lớn.

---

## 1) Hiện trạng của dự án (để bám đúng code đang có)

Hiện tại dự án đã có nền tảng tốt cho chat:

- Message có `ConversationId`, `ReplyToMessageId`.  
- API tìm tin nhắn đã có: `FindMessagesInConversationAsync` → gọi repository `GetMessagesInConversationAsync`.  
- Query tìm kiếm hiện dùng `x.Text.Contains(messageText)` + `OrderByDescending(CreationTime)` + `PageBy(skip, take)`.
- UI chat đã có infinite scroll kiểu `SkipCount`/`MaxResultCount`, auto scroll xuống đáy khi có tin mới.

=> Nghĩa là bạn đã có đủ “xương sống”, chỉ thiếu phần **anchor jump** (nhảy theo message id) + tối ưu query/index để không chậm khi room lớn.

---

## 2) Pattern phổ biến nhất trên Internet/GitHub cho bài toán này

Trong các hệ chat lớn, pattern thường dùng là:

1. **Anchor-based loading** (load quanh 1 message id), không load toàn bộ lịch sử.  
2. **Seek/Keyset pagination** (dựa trên cursor `CreatedAt + Id`) thay vì OFFSET sâu.  
3. **Search index chuyên dụng** cho text (PostgreSQL Full-Text / trigram), không dựa vào `%keyword%` thuần.  
4. **UI jump 2-phase**: 
   - Phase A: kiểm tra message đã có trong DOM chưa → scroll trực tiếp.
   - Phase B: nếu chưa có thì gọi API “load around anchor” → render rồi scroll + highlight ngắn.

Đây là pattern dễ scale nhất và cũng là cách các app chat production thường áp dụng.

---

## 3) Thiết kế API nên thêm (quan trọng nhất)

### 3.1 API cho Reply click và Search click

Thêm endpoint kiểu:

- `GET /api/chat/messages/{messageId}/context?conversationId=...&before=20&after=20`
- Trả về:
  - `anchorMessage` (message đích)
  - `before[]` (20 tin cũ hơn)
  - `after[]` (20 tin mới hơn)
  - `hasMoreBefore`, `hasMoreAfter`

Lợi ích:
- Bấm reply/search là có đủ dữ liệu để vẽ ngay “đoạn hội thoại quanh tin đích”.
- Không cần kéo lịch sử nhiều lần mới chạm đúng tin.

### 3.2 API tìm kiếm trả về “light result” + anchor

Tách làm 2 bước:

- B1: Search chỉ trả về danh sách kết quả nhẹ: `MessageId`, `ConversationId`, `Snippet`, `Rank`, `CreationTime`.
- B2: Khi click kết quả, gọi API context ở trên để jump.

Như vậy search panel chạy nhanh và giảm payload.

---

## 4) Tối ưu Database (PostgreSQL) — đề xuất thực tế

## 4.1 Index bắt buộc cho jump theo hội thoại + thời gian

```sql
-- Nếu chưa có index tối ưu đọc lịch sử theo conversation/time
CREATE INDEX CONCURRENTLY IF NOT EXISTS ix_chatmessages_conv_created_id
ON "ChatMessages" ("ConversationId", "CreationTime" DESC, "Id" DESC);
```

Index này phục vụ:
- load page mới nhất
- load cũ hơn/mới hơn quanh anchor
- seek pagination.

## 4.2 Text search: ưu tiên 1 trong 2 hướng

### Hướng A (phổ biến cho chat): `pg_trgm` (linh hoạt, gần LIKE)

```sql
CREATE EXTENSION IF NOT EXISTS pg_trgm;

CREATE INDEX CONCURRENTLY IF NOT EXISTS ix_chatmessages_text_trgm
ON "ChatMessages" USING GIN ("Text" gin_trgm_ops);
```

Phù hợp khi cần:
- tìm chuỗi con gần giống nhập tự nhiên người dùng.

### Hướng B: Full-Text Search (tốt cho ngôn ngữ có tách từ rõ)

Tạo cột `tsvector` (generated hoặc cập nhật trigger), sau đó GIN index:

```sql
ALTER TABLE "ChatMessages"
ADD COLUMN IF NOT EXISTS "SearchVector" tsvector
GENERATED ALWAYS AS (to_tsvector('simple', coalesce("Text", ''))) STORED;

CREATE INDEX CONCURRENTLY IF NOT EXISTS ix_chatmessages_searchvector
ON "ChatMessages" USING GIN ("SearchVector");
```

> Nếu team chủ yếu tiếng Việt, bạn nên benchmark thực tế giữa `pg_trgm` và FTS để chọn (thường trigram cho UX gõ “na ná” khá ổn).

## 4.3 Có nên thêm cột DB mới?

Nên, nếu chat lớn:

- `NormalizedText` (lower/unaccent) để giảm cost chuẩn hóa query runtime.
- `SearchVector` (tsvector) nếu dùng FTS.
- (tuỳ chọn) `MessageSeq` BIGINT theo conversation để seek ổn định hơn (đặc biệt khi có nhiều bản ghi cùng `CreationTime`).

---

## 5) Query/EF Core tối ưu cho .NET

## 5.1 Tránh OFFSET sâu cho history/search result

Thay vì `Skip(50000).Take(30)`, dùng keyset:

- Cũ hơn anchor:
  - `WHERE ConversationId = @cid AND (CreationTime, Id) < (@anchorTime, @anchorId)`
  - `ORDER BY CreationTime DESC, Id DESC LIMIT @take`
- Mới hơn anchor tương tự với `>`.

## 5.2 Dùng projection + AsNoTracking cho luồng read

- Query read-only nên `.AsNoTracking()`.
- Chỉ select cột cần (`Id`, `Text`, `CreationTime`, sender summary), tránh load object graph nặng.

## 5.3 Compiled query (EF Core)

Các query hot path như `GetMessageContext`, `SearchMessageIds`, `LoadBefore/After` nên cân nhắc compiled query để giảm overhead translate.

---

## 6) Thuật toán UI Jump (Blazor) đề xuất

Khi click reply preview hoặc kết quả search:

1. **Fast path**: kiểm tra message đã có trong danh sách render hiện tại chưa (`HashSet<Guid>` hoặc dictionary id->index).
   - Có: scroll đến element `id="msg-{MessageId}"`, highlight 2s.
2. **Miss path**: gọi API context theo anchor.
   - Merge `before + anchor + after` vào list hiện tại (dedupe theo Id).
   - Render xong → scroll đến `msg-{MessageId}`.
   - highlight ngắn để user định vị.
3. Preload nhẹ 1 page trước/sau anchor để user kéo tiếp mượt.

### JS interop gợi ý

- `scrollIntoView({block:'center', behavior:'smooth'})`
- thêm class `message-jump-highlight` rồi remove sau timeout.

---

## 7) Kế hoạch triển khai theo pha (ít rủi ro)

### Phase 1 (nhanh thấy kết quả)

- Thêm API `GetMessageContext(anchorId, before, after)`.
- UI: click reply preview → gọi API context nếu message chưa có.
- Thêm index `(ConversationId, CreationTime DESC, Id DESC)`.

### Phase 2 (tối ưu search)

- Chuyển search sang `pg_trgm` hoặc FTS.
- Search panel trả về lightweight DTO + snippet + rank.
- Click search result → jump qua API context.

### Phase 3 (scale lớn)

- Chuyển pagination nặng sang keyset hoàn toàn.
- Bổ sung metrics (P95 search latency, jump latency, DB rows scanned).

---

## 8) KPI nên đặt để kiểm chứng tối ưu

- P95 `search API` < 200ms (room vừa), < 500ms (room lớn).
- P95 `jump-to-message` < 300ms backend + < 150ms render/scroll.
- Query plan: hạn chế Seq Scan trên `ChatMessages` khi có filter conversation + text.
- Không tăng memory UI tuyến tính theo toàn bộ lịch sử (chỉ giữ window + cache hợp lý).

---

## 9) Mapping thẳng vào code hiện tại (gợi ý điểm sửa)

- `IConversationAppService`:
  - thêm `GetMessageContextAsync(...)`
  - thêm `SearchMessagesAsync(...)` trả lightweight result.
- `EfCoreMessageRepository`:
  - thay `Contains` thuần bằng query dùng trigram/FTS + ranking.
  - thêm query before/after anchor dạng seek.
- `Chat1/MessageItem.razor`:
  - cho reply preview `@onclick` gọi `JumpToMessageAsync(replyMessageId)`.
- `Chat1.razor.cs`:
  - thêm `JumpToMessageAsync(Guid messageId)` + cache id/index + JS scroll/highlight.

---

## 10) Tài liệu tham khảo (Internet/Official)

1. PostgreSQL docs – `pg_trgm` (index hỗ trợ LIKE/ILIKE và similarity):  
   https://www.postgresql.org/docs/current/pgtrgm.html
2. PostgreSQL docs – Full Text Search + tsvector/Gin index:  
   https://www.postgresql.org/docs/current/textsearch.html
3. PostgreSQL docs – LIMIT/OFFSET (lưu ý cost của OFFSET lớn):  
   https://www.postgresql.org/docs/current/queries-limit.html
4. Microsoft docs – EF Core efficient querying/performance:  
   https://learn.microsoft.com/ef/core/performance/efficient-querying
5. Microsoft docs – Pagination (offset vs keyset):  
   https://learn.microsoft.com/ef/core/querying/pagination
6. Microsoft docs – Blazor virtualization (giảm render cost list dài):  
   https://learn.microsoft.com/aspnet/core/blazor/components/virtualization

---

## 11) Kết luận ngắn

Giải pháp tối ưu nhất cho use-case của bạn là:

- **Search tốt + Jump chuẩn Messenger = Search index (trgm/FTS) + API context theo anchor + keyset pagination + UI highlight/scroll 2-phase**.

Nếu bạn muốn, bước tiếp theo tôi có thể viết luôn:
- contract DTO + app service signature,
- EF query mẫu cho PostgreSQL,
- đoạn code Blazor `JumpToMessageAsync` tương thích trực tiếp với `Chat1` hiện tại.
