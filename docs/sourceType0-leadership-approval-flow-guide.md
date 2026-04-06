# Hướng dẫn triển khai flow lãnh đạo duyệt văn bản (sourceType = 0)

> Phạm vi: menu **Quản lý văn bản** với `DocumentSourceType.Archive` (`sourceType = 0`).

## 1) Mục tiêu nghiệp vụ

Flow cần đạt:

1. **Văn thư** gửi văn bản cho lãnh đạo duyệt  
   → status = `CHO_PHE_DUYET`.
2. **Lãnh đạo** mở văn bản để xem.
3. Lãnh đạo có 2 lựa chọn:
   - **Từ chối** → status = `TU_CHOI`.
   - **Phê duyệt** → chọn vị trí trên file PDF, nhập note, xác nhận  
     → status = `DA_PHE_DUYET` và note được chèn vào đúng vị trí trên PDF.

---

## 2) Kiểm tra dữ liệu nền (MasterData)

### 2.1. Đảm bảo có đủ mã trạng thái trong `masterdata/document-status`

Bạn cần có đủ các `Code` sau trong loại trạng thái văn bản:

- `CHO_PHE_DUYET`
- `TU_CHOI`
- `DA_PHE_DUYET`

> Lưu ý: hiện enum `DocumentStatusCode` trong code đang có sẵn `TU_CHOI`, nhưng chưa có `CHO_PHE_DUYET` và `DA_PHE_DUYET`, nên cần mở rộng enum hoặc xử lý theo code string khi map trạng thái.  
> Tham chiếu enum hiện tại: `src/HC.Domain.Shared/Documents/DocumentStatusCode.cs`.

### 2.2. Role/Permission tối thiểu

- Văn thư: được thao tác “Gửi lãnh đạo duyệt”.
- Lãnh đạo: được xem văn bản và thao tác “Từ chối/Phê duyệt”.

---

## 3) Thiết kế API/Backend (AppService)

Bạn nên thêm 3 API nghiệp vụ mới (đặt trong `DocumentsAppService.Extended.cs` hoặc service nghiệp vụ riêng):

## 3.1. API gửi duyệt

`POST /api/app/documents/{documentId}/submit-for-approval`

Input gợi ý:

- `LeaderUserId` (bắt buộc)
- `Message` (tuỳ chọn)

Xử lý:

1. Validate document thuộc `SourceType = Archive (0)`.
2. Cập nhật trạng thái document sang `CHO_PHE_DUYET`.
3. Tạo bản ghi giao việc duyệt (assignment/approval task) cho lãnh đạo.
4. Gửi notification cho lãnh đạo.
5. Ghi `DocumentHistory`.

## 3.2. API từ chối

`POST /api/app/documents/{documentId}/reject-approval`

Input gợi ý:

- `RejectReason` (bắt buộc)

Xử lý:

1. Kiểm tra người thực hiện là lãnh đạo được giao.
2. Cập nhật trạng thái `TU_CHOI`.
3. Ghi `DocumentHistory` kèm lý do.
4. Notify ngược về văn thư/người tạo.

## 3.3. API phê duyệt + note theo vị trí PDF

`POST /api/app/documents/{documentId}/approve-with-note`

Input gợi ý:

- `PageNumber` (1-based)
- `PdfX`, `PdfY` (tọa độ trong hệ PDF point)
- `NoteContent`
- (tuỳ chọn) `FontSize`, `Color`

Xử lý:

1. Validate quyền và trạng thái hiện tại = `CHO_PHE_DUYET`.
2. Lấy file PDF mới nhất của document.
3. Chèn note vào PDF tại `(PageNumber, PdfX, PdfY)`.
4. Lưu file PDF kết quả thành `DocumentFile` mới (không ghi đè file cũ nếu muốn audit tốt).
5. Cập nhật trạng thái document = `DA_PHE_DUYET`.
6. Ghi `DocumentHistory` và notification.

---

## 4) Cách chọn vị trí note trên file PDF (phần bạn hỏi trọng tâm)

Hiện dự án đang dùng `Blazorise.PdfViewer` để xem PDF. Component này thuận tiện để xem file, nhưng không mạnh về bắt sự kiện click chuẩn theo tọa độ PDF.

Trong source đã có sẵn JS interop `pdfInterop.js` có đúng logic:

- render PDF bằng `pdf.js`,
- bắt click chuột,
- convert điểm click sang **tọa độ PDF** (`pdfX`, `pdfY`),
- callback về .NET qua `OnPdfClick(...)`.

File: `src/HC.Blazor/wwwroot/pdfInterop.js`.

### 4.1. Mẫu luồng UI chọn vị trí

1. Lãnh đạo mở modal “Phê duyệt”.
2. Hiển thị vùng chọn vị trí (`<div id="approval-pdf-pick-container"></div>`) bằng JS interop `pdfPick.init(...)`.
3. Khi click lên PDF:
   - UI nhận `pageNumber`, `pdfX`, `pdfY`.
   - Hiển thị marker đỏ tại vị trí đã chọn.
4. Lãnh đạo nhập `NoteContent`.
5. Bấm “Đồng ý” → gọi API `approve-with-note` với tọa độ + note.

### 4.2. Mẫu code Blazor (ý tưởng)

```csharp
[JSInvokable]
public Task OnPdfClick(int pageNumber, double pdfX, double pdfY, double cssX, double cssY)
{
    ApprovalInput.PageNumber = pageNumber;
    ApprovalInput.PdfX = pdfX;
    ApprovalInput.PdfY = pdfY;
    return Task.CompletedTask;
}
```

và khi mở modal:

```csharp
await JSRuntime.InvokeVoidAsync(
    "pdfPick.init",
    DotNetObjectReference.Create(this),
    pdfUrl,
    "approval-pdf-pick-container");
```

> Gợi ý UX: disable nút “Phê duyệt” cho đến khi user đã click chọn vị trí + nhập note.

---

## 5) Chèn note vào PDF ở backend

Bạn có 2 hướng:

1. **Tái sử dụng service đang có** và mở rộng thêm hàm chèn text theo tọa độ (khuyến nghị):
   - `src/HC.Application/DocumentPdfViewer/PdfStampingService.cs`
2. Tạo service mới `PdfApprovalNoteService` nếu muốn tách watermark và approval note.

Pseudo xử lý chèn note:

1. Mở file PDF bằng `PdfSharp` chế độ `Modify`.
2. Lấy trang theo `PageNumber - 1`.
3. `XGraphics.FromPdfPage(page, Append)`.
4. `DrawString(NoteContent, font, brush, x, y)` với `x = PdfX`, `y = PdfY`.
5. Save stream ra bytes.
6. Upload blob + tạo bản ghi `DocumentFile` mới.

> Do `pdfInterop.js` đã convert sẵn theo hệ tọa độ PDF, backend chỉ cần dùng trực tiếp `PdfX/PdfY` (nhớ kiểm tra giới hạn trang, biên).

---

## 6) Gợi ý thay đổi ở UI màn hình văn bản

## 6.1. Màn hình danh sách/chi tiết văn bản (`sourceType=0`)

- Thêm nút cho văn thư: **"Gửi lãnh đạo duyệt"**.
- Khi status = `CHO_PHE_DUYET` và user là lãnh đạo được giao:
  - hiển thị 2 nút: **Từ chối**, **Phê duyệt**.

Có thể triển khai ở các trang đang quản lý văn bản:

- `src/HC.Blazor/Pages/Documents/Documents.razor`
- `src/HC.Blazor/Pages/Documents/DocumentDetail.razor`
- code-behind tương ứng `.razor.cs`.

## 6.2. Modal phê duyệt

Trong modal:

- Khung PDF chọn vị trí (`pdfPick.init`)
- Form note
- Nút xác nhận

Data gửi về API:

- `DocumentId`
- `PageNumber`
- `PdfX`, `PdfY`
- `NoteContent`

---

## 7) Checklist test nghiệp vụ end-to-end

1. Văn thư gửi duyệt một văn bản `sourceType=0`:
   - status đổi `CHO_PHE_DUYET`.
2. Lãnh đạo thấy văn bản trong danh sách chờ duyệt.
3. Lãnh đạo bấm **Từ chối**:
   - status = `TU_CHOI`, có lý do từ chối trong history.
4. Lãnh đạo bấm **Phê duyệt**:
   - bắt buộc chọn vị trí + nhập note,
   - status = `DA_PHE_DUYET`,
   - tải PDF kết quả và thấy note nằm đúng vị trí đã chọn.
5. Kiểm tra phân quyền: user thường không thao tác được nút duyệt/từ chối.

---

## 8) Lưu ý kỹ thuật khi dùng Blazorise PdfViewer

Theo hướng triển khai thực tế của dự án:

- **Blazorise PdfViewer** giữ vai trò xem tài liệu nhanh trong các màn hiện có.
- **Pick tọa độ chính xác** nên đi qua lớp JS interop (`pdfInterop.js`) dùng `pdf.js` để đảm bảo lấy đúng tọa độ PDF point.

Cách này giúp bạn đạt đúng yêu cầu “chọn vị trí trên file PDF để chèn note”, trong khi vẫn không phá kiến trúc UI hiện tại.

---

## 9) Lộ trình triển khai nhanh (khuyên dùng)

### Phase 1 (1-2 ngày)
- Thêm API `submit-for-approval`, `reject-approval`, `approve-with-note`.
- Update status theo code string `CHO_PHE_DUYET` / `TU_CHOI` / `DA_PHE_DUYET`.
- Ghi history + notification.

### Phase 2 (1-2 ngày)
- Thêm modal chọn vị trí note với `pdfPick.init`.
- Nối API approve-with-note.

### Phase 3 (1 ngày)
- Hoàn thiện stamp note vào PDF + lưu file kết quả.
- E2E test + fix UI/permission edge cases.

---

Nếu bạn muốn, mình có thể viết tiếp bản **task breakdown theo từng file cụ thể + skeleton method signature** để dev chỉ cần copy vào codebase và fill logic.
