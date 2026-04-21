# Đợt 3 — Long-term (kiến trúc)

Tài liệu mô tả triển khai và hướng mở rộng cho: background job + SignalR, worker tách, read-model SentToMe, outbox notification.

## 1. Thao tác nặng: background job + SignalR + tiến trình UI

### Đã triển khai (Approve with note)

- **Entity** `DocumentBackgroundOperation` — lưu `OperationId`, `UserId`, `TenantId`, `Status`, `Progress` (0–100), `InputJson`, lỗi.
- **API**: `POST /api/app/documents/queue-approve-with-note` → enqueue `ApproveWithNoteBackgroundJob` → trả `QueueDocumentBackgroundOperationResultDto { operationId }`.
- **Polling dự phòng**: `GET /api/app/documents/background-operation-status?operationId=...` (khi RabbitMQ/SignalR không dùng được).
- **Luồng nghiệp vụ** tách trong `ExecuteApproveWithNoteForUserAsync` (báo cáo `%` qua callback); job gọi method này và publish `DocumentBackgroundOperationProgressEto` lên **RabbitMQ**.
- **Blazor**: `DocumentBackgroundOperationProgressEventHandler` nhận ETO và gọi `IHubContext<NotificationHub>.SendAsync("ReceiveDocumentOperationProgress", …)` tới group `user-{userId}`.
- **UI**: `notificationHub.js` đăng ký sự kiện; `NotificationToast` hiển thị **góc dưới phải** thanh tiến trình; ẩn sau ~8s khi `Completed` / `Failed`.

### Chưa chuyển sang job (có thể làm tương tự)

- `ApplyDigitalSignatureAsync`, `PrepareSubmissionPlaceholdersAsync` (LibreOffice): thêm loại operation + job + ETO progress.

### HTTP 202

Client ABP hiện trả **200** với body `operationId`. Có thể thêm **filter** hoặc reverse proxy để chuẩn REST trả **202 Accepted** nếu cần.

## 2. Worker service riêng cho PDF / ký

**Chưa có project riêng trong repo.** Hướng dẫn triển khai:

1. **Tách process**: chạy thêm một instance **HC.HttpApi.Host** (hoặc console host ABP) cùng connection string, **chỉ** thực thi `BackgroundJobWorker` — tắt MVC nếu cần (cấu hình riêng).
2. **Scale**: tăng số replica worker độc lập với API đọc/ghi; đảm bảo **RabbitMQ + Redis + PostgreSQL** đủ cho concurrent job.
3. **An toàn**: job phải dùng `ICurrentTenant.Change` + payload đã lưu (không phụ thuộc `ICurrentUser` trong HTTP request).

## 3. Materialized view / read-model — inbox SentToMe

**Chưa refresh tự động trong app.** Có thể:

- Tạo **MATERIALIZED VIEW** (PostgreSQL) pre-tính tập `DocumentId` theo user/tenant (join assignment + explicit SentToMe).
- **REFRESH MATERIALIZED VIEW CONCURRENTLY** theo lịch (cron/pg_cron) hoặc sau khi ghi assignment (trigger phức tạp hơn).
- Repository đọc từ view khi `FilterText` rỗng; fallback query hiện tại khi cần filter phức tạp.

File mẫu SQL (cần chỉnh theo schema thực tế): `docs/sql/phase3_sent_to_me_materialized_view.sql` (nếu được tạo).

## 4. Outbox pattern — notification

### Đã có trong DB

- Bảng `**AppNotificationOutboxes`** (migration `Phase3_DocumentBackgroundOperation_And_Outbox`): `EventType`, `PayloadJson`, `ProcessedTime`, `RetryCount`.

### Worker gửi thật

- Worker định kỳ (stub có thể thêm trong `HC.Domain`) đọc bản ghi `ProcessedTime == null`, gửi notify (email / in-app), đánh dấu `ProcessedTime`.
- **Bước tiếp**: trong cùng transaction với nghiệp vụ, **chỉ** `Insert` outbox thay vì gọi notify trực tiếp; hiện `NotifyDocumentOwnerAsync` vẫn ghi `Notification` + publish ETO như cũ — tránh đổi hành vi production trong một PR.

## Verify

1. Phê duyệt có note trên trang Documents — thấy toast góc dưới phải tiến trình %.
2. RabbitMQ bật — progress realtime; tắt RabbitMQ — dùng GET `background-operation-status` để poll.
3. `dotnet run --project src/HC.DbMigrator` — áp migration Phase 3.