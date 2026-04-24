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

### Project: `src/HC.BackgroundJobWorker`

Console host ABP dùng chung **HC.Application** + **HC.EntityFrameworkCore**, cấu hình **PostgreSQL, Redis, RabbitMQ, MinIO** giống Blazor/API. Module `HCBackgroundJobWorkerModule` bật `AbpBackgroundJobOptions.IsJobExecutionEnabled = true` để worker **lấy job từ hàng đợi** (ApproveWithNote, v.v.).

- `**App:DisableDomainBackgroundWorkers` = `true`** trong `appsettings.json` của worker — không đăng ký `WorkflowOverdueBackgroundWorker` / `NotificationOutboxBackgroundWorker` trên process này (tránh trùng với Blazor).
- Chạy local: `dotnet run --project src/HC.BackgroundJobWorker` (copy `ConnectionStrings` / secrets giống DbMigrator hoặc dùng `appsettings.secrets.json`).
- **RabbitMQ** `EventBus:ClientName` mặc định `HC_BackgroundJobWorker` để phân biệt consumer trên RabbitMQ management.

### Scale & vận hành

1. **Tách process**: deploy thêm một hoặc nhiều replica **HC.BackgroundJobWorker**; Blazor/API chỉ enqueue, worker xử lý PDF/ký.
2. **Scale**: tăng replica worker độc lập; theo dõi độ sâu queue RabbitMQ và tải DB/MinIO.
3. **An toàn**: job đã dùng `ICurrentTenant.Change` + impersonation user (ví dụ `ApproveWithNoteJobExecutor`) — không phụ thuộc HTTP request.

### RabbitMQ: routing key trên giao diện vs “chuyển” sang Worker

- **Cột “To / Routing key”** trong RabbitMQ là kết quả tự động: mỗi process có `EventBus:ClientName` (ví dụ `BlazorServer`, `HC_BackgroundJobWorker`) và **cột routing key = loại ETO** mà ứng dụng đó có `IDistributedEventHandler` (đăng ký trong DI). **Không** chuyển bằng cách sửa UI RabbitMQ — phải **bỏ/giảm** handler (hoặc build module khác) ở Blazor nếu muốn bớt hàng đợi ở đó.
- **Hai cơ chế khác nhau (hay bị lẫn):**
  1. **Background job** (`IBackgroundJobManager`, ApproveWithNote, v.v.): tách bằng `**AbpBackgroundJobOptions.IsJobExecutionEnabled`**. `HCBackgroundJobWorker` bật; trên **Blazor** đặt trong `appsettings`: `"BackgroundJobs": { "IsExecutionEnabled": false }` (chỉ khi **đã** chạy worker ổn) để **web** không cạnh tranh cùng job queue.
  2. **Distributed event (ETO)** trên Rabbit: Blazor cần giữ **chat**, `NotificationCreatedEto`, `DocumentBackgroundOperationProgressEto` nếu vẫn dùng **SignalR** realtime. Muốn “bớt ETO trên Blazor” thì phải thay cơ chế (ví dụ bỏ handler tương ứng trong `HCBlazorModule.ConfigureEventHandlers`) — thường **không** chuyển hết sang worker vì worker không nối trực tiếp tới mạch SignalR user như Blazor.
- Các ETO hệ thống (GDPR, Identity, Payment, migration, tenant) xuất hiện ở **cả** `HttpApiHost` / `HC_BackgroundJobWorker` vì cùng load module ABP: đó là **hàng consumer riêng theo từng `ClientName`**, không phải “một sự kiện gửi hai lần” trừ khi cả hai host đều thực thi **cùng** side-effect (cần xác minh tài liệu từng module Volo: thường idempotent).

### Tùy chọn: Blazor không chạy `IBackgroundJob` (chủ động scale job)

- Trong `src/HC.Blazor/appsettings.json` (hoặc biến môi trường / file Production): `BackgroundJobs:IsExecutionEnabled` = **false** khi `HC.BackgroundJobWorker` đã chạy và xử lý hàng job.
- Mặc định `true` để môi trường dev một process vẫn chạy job được nếu chưa bật worker.

## 3. Materialized view / read-model — inbox SentToMe

**Chưa refresh tự động trong app.** Có thể:

- Tạo **MATERIALIZED VIEW** (PostgreSQL) pre-tính tập `DocumentId` theo user/tenant (join assignment + explicit SentToMe).
- **REFRESH MATERIALIZED VIEW CONCURRENTLY** theo lịch (cron/pg_cron) hoặc sau khi ghi assignment (trigger phức tạp hơn).
- Repository đọc từ view khi `FilterText` rỗng; fallback query hiện tại khi cần filter phức tạp.

File mẫu SQL: `docs/sql/phase3_sent_to_me_materialized_view.example.sql`.

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
4. `dotnet run --project src/HC.BackgroundJobWorker` — worker chạy, log “Background Job Worker started”; queue job từ UI được xử lý (cùng DB/RabbitMQ/MinIO đã cấu hình).

