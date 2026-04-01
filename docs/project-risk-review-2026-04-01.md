# HCS Project Risk Review

Ngày rà soát: 2026-04-01

Phạm vi:
- Static review toàn bộ solution `HC.sln`
- Quét cấu hình, startup/auth, EntityFramework repositories, Blazor pages/components, module `Volo.Forms`
- Không chạy full services/test end-to-end vì môi trường local phụ thuộc ABP commercial login, Redis, PostgreSQL remote và các secret runtime

Tóm tắt:
- High: 4
- Medium: 6
- Low: 4

## High

### H1. Hardcoded secret/credential đang được commit trong repo
Mức độ ảnh hưởng:
- Rò rỉ DB credential, MinIO credential, OIDC client secret, encryption passphrase, ABP secret file
- Chỉ cần lộ source code là đủ để truy cập hoặc leo thang sang các thành phần khác

Evidence:
- `src/HC.AuthServer/appsettings.Production.json:10`
- `src/HC.AuthServer/appsettings.Production.json:15`
- `src/HC.AuthServer/appsettings.Production.json:18`
- `src/HC.AuthServer/appsettings.Production.json:23-29`
- `src/HC.HttpApi.Host/appsettings.Production.json:9`
- `src/HC.HttpApi.Host/appsettings.Production.json:33`
- `src/HC.Blazor/appsettings.Production.json:38`
- `src/HC.Blazor/appsettings.Production.json:41`
- `src/HC.Blazor/appsettings.Production.json:43-49`
- Repo còn đang track các file secret:
  - `src/HC.AuthServer/appsettings.secrets.json`
  - `src/HC.Blazor/appsettings.secrets.json`
  - `src/HC.DbMigrator/appsettings.secrets.json`
  - `src/HC.HttpApi.Host/appsettings.secrets.json`
  - `modules/Volo.Forms/test/Volo.Forms.HttpApi.Client.ConsoleTestApp/appsettings.secrets.json`

Khuyến nghị:
- Rotate toàn bộ credential đã commit
- Chuyển secret sang secret manager hoặc environment variables
- Bỏ track `appsettings.secrets.json`, log files, local runtime artifacts
- Tách `appsettings.Production.json` khỏi secret thật

### H2. `UserSignature.Secret` đang được lưu và trả về dạng plain text
Mức độ ảnh hưởng:
- Đây là dữ liệu nhạy cảm phục vụ chữ ký số
- Secret đang tồn tại ở domain model, DTO và UI, nghĩa là bị lưu DB và bị trả về client
- Nếu tài khoản ứng dụng hoặc DB bị lộ, attacker có thể tái sử dụng secret ký số

Evidence:
- Field domain model: `src/HC.Domain/UserSignatures/UserSignature.cs:27-28`
- DTO expose ra application contract: `src/HC.Application.Contracts/UserSignatures/UserSignatureDto.cs:12-14`
- Mapper đang map thẳng entity sang DTO, không có masking/encryption layer: `src/HC.Application/HCApplicationMappers.cs:683-698`
- UI đang hiển thị secret cho người dùng: `src/HC.Blazor/Pages/MyProfile.razor:369-375`
- Không thấy dấu hiệu dùng `IStringEncryptionService`, `Protect/Unprotect`, `Encrypt/Decrypt` cho luồng này trong `src/HC.Application`, `src/HC.Domain`, `src/HC.Blazor`

Khuyến nghị:
- Không trả `Secret` ra DTO/UI
- Mã hóa secret trước khi persist, giải mã đúng scope tại chỗ dùng
- Nếu secret chỉ dùng một chiều thì cân nhắc mô hình token tham chiếu hoặc vault ngoài DB
- Audit toàn bộ chỗ đọc/ghi `UserSignature`

### H3. Auth server vẫn bật Password Grant và seed weak fallback secret
Mức độ ảnh hưởng:
- Resource Owner Password Flow là grant type yếu, khó bảo vệ MFA/conditional access, dễ bị lạm dụng trong môi trường healthcare
- Có fallback `ClientSecret` mặc định yếu `"1q2w3e*"` nếu config thiếu

Evidence:
- Bật password flow ở auth server: `src/HC.AuthServer/HCAuthServerModule.cs:127-128`
- Console/Angular app được seed với `Password` grant: `src/HC.Domain/OpenIddict/OpenIddictDataSeedContributor.cs:78-84`
- Blazor confidential client fallback secret yếu: `src/HC.Domain/OpenIddict/OpenIddictDataSeedContributor.cs:107`
- Blazor app production config đang chứa cùng client secret: `src/HC.Blazor/appsettings.Production.json:33-39`

Khuyến nghị:
- Loại bỏ Password Grant nếu không có lý do bắt buộc
- Chuyển toàn bộ client sang Authorization Code + PKCE hoặc machine-to-machine riêng
- Xóa fallback secret hardcoded
- Rà lại toàn bộ OpenIddict seeded applications

### H4. Production đang bật PII/security artifact logging và detailed errors
Mức độ ảnh hưởng:
- Token/claim/security artifact có thể bị ghi vào log
- Blazor Server detailed error có thể làm lộ stack trace hoặc nội dung lỗi nội bộ
- Trong môi trường bệnh viện, đây là risk compliance và incident response rất cao

Evidence:
- Production config để `DisablePII = false`:
  - `src/HC.AuthServer/appsettings.Production.json:6`
  - `src/HC.HttpApi.Host/appsettings.Production.json:5`
  - `src/HC.Blazor/appsettings.Production.json:5`
- Khi `DisablePII = false`, code bật:
  - `src/HC.AuthServer/HCAuthServerModule.cs:139-143`
  - `src/HC.HttpApi.Host/HCHttpApiHostModule.cs:68-72`
  - `src/HC.Blazor/HCBlazorModule.cs:172-176`
- Blazor luôn bật circuit detailed errors: `src/HC.Blazor/HCBlazorModule.cs:157-163`

Khuyến nghị:
- Ở non-dev phải mặc định tắt PII/security artifact logging
- Chỉ bật tạm thời theo feature flag hoặc env var có kiểm soát
- `DetailedErrors` phải phụ thuộc `IsDevelopment()`

## Medium

### M1. Middleware debug đang log `UserId`, `TenantId`, trạng thái cookie cho mọi request
Mức độ ảnh hưởng:
- Tăng rò rỉ dữ liệu vào log
- Tăng noise, khó truy vết incident thật
- Có thể ảnh hưởng hiệu năng ở tải cao

Evidence:
- `src/HC.Blazor/HCBlazorModule.cs:752-775`

Khuyến nghị:
- Loại bỏ middleware debug khỏi default pipeline
- Nếu cần, chỉ bật qua feature flag và sample log có kiểm soát

### M2. Nhiều repository EF dựng navigation bằng subquery `FirstOrDefault(...)` trong projection
Mức độ ảnh hưởng:
- Dễ tạo SQL phức tạp, khó optimize, tăng latency ở list endpoint
- Pattern này lặp lại nhiều nơi nên risk mang tính hệ thống, không phải lỗi cục bộ

Evidence:
- Ví dụ rõ nhất: `src/HC.EntityFrameworkCore/Documents/EfCoreDocumentRepository.cs:36-39`
- Số occurrence dạng này trong `src/HC.EntityFrameworkCore`: khoảng 25
- Tổng số method `GetWithNavigationPropertiesAsync` / `GetListWithNavigationPropertiesAsync`: khoảng 54

Khuyến nghị:
- Chuẩn hóa bằng `join`/`DefaultIfEmpty` hoặc query composition dùng projection chung
- Benchmark lại các màn hình nặng như Documents, Projects, Workflow, Survey

### M3. Fire-and-forget `Task.Run` trong Blazor component không có cancellation/disposal-safe
Mức độ ảnh hưởng:
- Task chạy nền sau khi component dispose có thể gây race condition, exception ngầm, memory leak
- Đặc biệt rủi ro trên Blazor Server vì circuit lifecycle nhạy cảm

Evidence:
- `src/HC.Blazor/Components/NotificationToast.razor:198-207`
- Repo có khoảng 8 occurrence `Task.Run(` trong mã nguồn ứng dụng

Khuyến nghị:
- Dùng `CancellationTokenSource` gắn vòng đời component
- Tránh `Task.Run` cho UI state update; ưu tiên timer/service hàng đợi hoặc `InvokeAsync`

### M4. Một số API/feature đang mở route nhưng chưa hoàn thiện, có thể nổ lỗi runtime
Mức độ ảnh hưởng:
- Trả 500 ở runtime
- API contract công khai nhưng hành vi chưa hoàn chỉnh, dễ gây lỗi tích hợp frontend/mobile

Evidence:
- `src/HC.HttpApi/Chat/Files/FileController.cs:46-51` ném `NotImplementedException`
- `src/HC.HttpApi/Chat/Files/FileController.cs:27-29` còn TODO về upload conversion
- `src/HC.Application/Chat/Conversations/ConversationAppService.cs:1376` TODO cho signed URL download file

Khuyến nghị:
- Hoàn thiện hoặc ẩn route khỏi public contract
- Nếu chưa dùng thì trả `501 Not Implemented` có kiểm soát và gỡ khỏi client

### M5. Có chỗ swallow exception trong luồng nghiệp vụ, che mất lỗi thật
Mức độ ảnh hưởng:
- Ẩn lỗi phân quyền, network, dữ liệu
- Khi lỗi thật xảy ra thì UI vẫn hiển thị như “không có dữ liệu”, gây khó debug và sai trạng thái nghiệp vụ

Evidence:
- `src/HC.Blazor/Pages/ViewDocumentDetail.razor.cs:135-165`
- Repo hiện có khoảng 8 occurrence `catch (Exception)` trong source chính

Khuyến nghị:
- Chỉ bắt exception mong đợi
- Log warning/error có correlation id
- Với luồng nhạy cảm như revoke/assignment cần fail fast hoặc hiện trạng thái lỗi rõ ràng

### M6. Có dùng `.Result` sau async call trong UI code
Mức độ ảnh hưởng:
- Dù đang đặt sau `Task.WhenAll`, đây vẫn là pattern khó bảo trì và dễ lan sang deadlock/blocking ở chỗ khác
- Là dấu hiệu code async chưa nhất quán

Evidence:
- `src/HC.Blazor/Pages/CalendarEvents.Extended.razor.cs:320-322`
- Repo hiện có khoảng 11 occurrence `.Result` / `.Wait(`

Khuyến nghị:
- Thay bằng `await` trực tiếp và đọc kết quả sau await
- Quét toàn repo để xóa dần sync-over-async

## Low

### L1. `DateTime.Now` xuất hiện rộng, chưa thống nhất với ABP `Clock.Now`
Mức độ ảnh hưởng:
- Sai lệch timezone giữa server, browser, background jobs
- Khó chuẩn hóa audit và nghiệp vụ thời gian

Evidence:
- Repo có khoảng 76 occurrence `DateTime.Now`
- Một số chỗ đã bắt đầu sửa sang `Clock.Now`, chứng tỏ đây là debt đang tồn tại

Khuyến nghị:
- Chuẩn hóa dùng `Clock.Now` ở application/domain
- UI chỉ format hiển thị, không nên quyết định thời gian nghiệp vụ

### L2. Dùng `Guid.Empty` làm sentinel rất nhiều
Mức độ ảnh hưởng:
- Dễ đẩy trạng thái “ID hợp lệ giả” đi xuyên qua UI/API
- Tăng xác suất bug validation và filter

Evidence:
- Repo có khoảng 263 occurrence `Guid.Empty`

Khuyến nghị:
- Ưu tiên nullable `Guid?` + validation rõ ràng
- Chỉ dùng `Guid.Empty` khi protocol thực sự cần sentinel

### L3. Runtime log artifact đang tồn tại trong repo/workspace
Mức độ ảnh hưởng:
- Dễ vô tình commit log chứa URL, request, lỗi nghiệp vụ
- Là nguồn rò rỉ dữ liệu phụ trợ

Evidence:
- File đang được track: `Logs/logs.txt`
- Runtime log hiện diện trong service folders:
  - `src/HC.AuthServer/Logs/logs.txt`
  - `src/HC.HttpApi.Host/Logs/logs.txt`
  - `src/HC.Blazor/Logs/logs.txt`
  - `src/HC.DbMigrator/Logs/logs.txt`

Khuyến nghị:
- Thêm rule `.gitignore`
- Tách log runtime ra ngoài workspace source
- Dọn log cũ trước khi review release

### L4. Tồn đọng TODO/FIXME/NotImplemented khá nhiều
Mức độ ảnh hưởng:
- Không phải tất cả đều critical, nhưng phản ánh debt kỹ thuật và feature chưa đóng hoàn toàn

Evidence:
- Quét chuỗi `TODO|FIXME|NotImplementedException` cho kết quả khoảng 331 occurrence
- Một phần là false positive do enum `TODO`, nhưng vẫn còn nhiều TODO thật trong chat/file/blob/form

Khuyến nghị:
- Lập backlog cleanup theo module
- Gắn owner và deadline cho các TODO nằm trên đường chạy production

## Ưu tiên xử lý đề xuất

1. Rotate tất cả secret đã lộ và dọn khỏi repo
2. Khóa/ẩn `UserSignature.Secret` khỏi DTO/UI, triển khai encryption hoặc vault
3. Tắt PII logging, security artifact logging, detailed errors ở non-dev
4. Loại bỏ Password Grant và fallback secret yếu trong OpenIddict seed
5. Rà các endpoint chat/file chưa hoàn chỉnh để tránh 500 runtime
6. Tối ưu lại repository pattern ở các màn hình danh sách nặng

## Ghi chú

Đây là báo cáo static review ưu tiên risk thực tế. Tôi chưa xác nhận bằng chạy full service/test/integration vì repo phụ thuộc license ABP commercial và hạ tầng ngoài máy local.
