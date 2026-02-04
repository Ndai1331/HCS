Dưới đây là đề xuất tên cột và cách làm tối ưu để phân biệt 2 phân hệ (“Văn thư lưu trữ” và “Văn bản của tôi”) trong bảng Documents, dựa trên cấu trúc Document hiện tại.



✅ Tên cột đề xuất (tối ưu & rõ nghĩa)
Khuyến nghị chính (tối ưu hiệu năng & query)
DocumentSource hoặc DocumentModule

Kiểu dữ liệu: smallint / int (enum)

Ý nghĩa:

0 = Archive (Văn thư lưu trữ)

1 = Personal (Văn bản của tôi)

Lý do chọn enum + số

Tối ưu index & filter (nhanh hơn string).

Tránh sai chính tả, dễ mở rộng thêm phân hệ sau này.

Dễ bind vào UI (hiển thị label).

👉 Đây là lựa chọn tối ưu nhất cho hiệu năng và maintainability.

✅ Cách làm tối ưu (implementation chuẩn)
1) Thêm enum trong domain
Ví dụ: DocumentSourceType

public enum DocumentSourceType
{
    Archive = 0,
    Personal = 1
}
2) Thêm property vào entity Document
public DocumentSourceType SourceType { get; set; }
Hiện Document đang là aggregate root, bạn sẽ bổ sung field vào entity này và update constructor nếu cần.

3) Tạo migration + add column + index
Thêm cột SourceType (smallint)

Index cho filter UI nhanh hơn (vì bảng Documents thường lọc theo phân hệ).

4) Update DTO + Filter Input
DocumentDto, DocumentCreateDto, DocumentUpdateDto, GetDocumentsInput

Cho phép filter theo SourceType ở màn list.

Các DTO liên quan nằm trong Application.Contracts/Documents.

5) Mapping ở EF Core
Update cấu hình trong DbContext hoặc config (nếu có).

Nếu bạn đang dùng auto mapping, chỉ cần migration.


===Menu
Đổi lại menu text 
1/Văn thư (load DocumentSourceType=0)
2/Văn bản của tôi( load (DocumentSourceType=1 && creatorId == currentUserId)  || DocumentAssignments ReceiverUserId = currentUserId)
