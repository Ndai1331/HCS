# Nhà cung cấp ký số TAG (REMOTE_CA)

## Tóm tắt

Luồng ký **REMOTE_CA** trong HCS dùng REST API `/api/v2/pdf/sign/originaldata` và chữ ký HMAC (`SignTextV2`), tương thích với integration mềm được mô tả trong `mework-src/mework`.

Luồng **HSM** (ví dụ VISNAM/Vin-HSM SDK) không đổi: vẫn dùng `SignText` + `VinHsmServiceClient`, bắt buộc `LayoutImg` và `SealImg`.

## Cấu hình SignatureSetting

| Trường | Giá trị |
|--------|---------|
| `ProviderCode` | Ví dụ `TAG` (do admin đặt; user chữ ký phải trùng mã này) |
| `ProviderType` | `REMOTE_CA` |
| `ApiEndpoint` | Base URL chỉ chứa scheme + host (và cổng nếu khác mặc định). **Không** thêm `/api/v2/pdf/sign/originaldata`. Chỉ nhập IP (vd. `178.88.11.15`) hoặc `host:port` thì hệ thống tự thêm **`http://`**; nếu dùng TLS hãy ghi đủ **`https://...`**. |
| `AllowDigitalSign` | `true` |
| `DefaultSignType` | `DIGITAL` |
| `LayoutImg` | Có thể để trống (REMOTE_CA không gửi layout ảnh trong payload) |
| `ApiTimeout` | Thời gian chờ HTTP (giây) một lần gọi; hệ thống giới hạn khoảng **30–240** giây cho REMOTE_CA. **Mặc định trong code là 30** khi không cấu hình (đồng bộ với chờ ~30 giây trên modal `/document-signing`). |

Ví dụ endpoint: `https://your-tag-sign.example.vn` hoặc `http://178.88.11.15` khi chỉ có IP và HTTP.

## Cấu hình UserSignature (người dùng)

- `SignType`: `DIGITAL`
- `ProviderCode`: khớp `SignatureSetting.ProviderCode`
- `TokenRef` / `Secret`: do nhà cung cấp TAG cấp. **Secret phải là Base64** (khớp cách máy chủ TAG ký HMAC trong mework).
- `SignatureImage`: bắt buộc
- `SealImg`: **không bắt buộc** khi provider đã mapping `REMOTE_CA` (backend và form Hồ sơ không ép upload con dấu cho luồng này)

## Pdf placeholder

Luồng workflow vẫn tìm text placeholder `<<Sign{StepOrder:D2}>>` trong PDF sau khi thay tên/note (giữ đồng bộ HSM).

## Kiểm tra mạng (go-live)

Máy chạy **HC.HttpApi.Host** phải outbound được tới máy chủ TAG (DNS hoặc IP, firewall/route nội bộ).

```bash
curl -vk -o /dev/null --connect-timeout 5 "http://178.88.11.15/api/v2/pdf/sign/originaldata"
```

HTTP 401/405/415 thường vẫn chứng tỏ có kết nối TCP đến dịch vụ; không cần body/credential đúng cho bước smoke test.

## Treo không phản hồi sau log `[DIGITAL_SIGN] REMOTE_CA`

ICMP `ping` thông không đảm bảo cổng **HTTP**/**HTTPS**. Thường gặp: sai **`http`** vs **`https`**, sai **cổng** (TAG chỉ nghe ví dụ `:8443`). Kết quả có thể treo đến hết `ApiTimeout`.

Sau bản vá, log **`[SIGN_V2]`** sẽ ghi rõ URL gọi, từng **Attempt**, thời gian và HTTP status để đối chiếu. Nếu hết giờ, API trả thông báo timeout thay vì im lặng.

## Regression HSM

Provider có `ProviderType = HSM` vẫn yêu cầu đủ `LayoutImg` và `SealImg`; không có thay đổi hành vi.
