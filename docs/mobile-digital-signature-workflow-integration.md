# Tài liệu mobile tích hợp ký số / ký điện tử theo flow website

## 1. Mục tiêu tài liệu

Tài liệu này mô tả đầy đủ flow mobile cần bám theo code web hiện tại của HCS cho nghiệp vụ:

- chọn workflow để trình ký
- lấy cấu hình bước ký
- chọn loại ký
- chọn chữ ký người dùng
- submit trình ký
- duyệt / trả về / từ chối
- re-submit khi bị trả về
- lấy file, log, lịch sử, trạng thái từng bước

Tài liệu được viết theo code hiện tại trong repo, không suy diễn theo mong muốn tương lai.

---

## 2. Kết luận quan trọng trước khi mobile triển khai

### 2.1 Backend hiện tại là bên thực hiện ký, mobile không tự ký

Flow hiện tại của website:

- Mobile/web chỉ chọn:
  - workflow
  - hành động `APPROVE` / `RETURN` / `REJECT`
  - loại ký `ELECTRONIC` hoặc `DIGITAL`
  - chữ ký người dùng cụ thể nếu có nhiều chữ ký hợp lệ
- Backend mới thực hiện:
  - chèn placeholder
  - chèn ảnh chữ ký điện tử vào PDF
  - gọi provider ký số qua `SignatureSetting.ApiEndpoint`
  - tạo file signed mới
  - cập nhật `DocumentAssignment.DocumentFileResultId`

Nếu mobile muốn tự ký native trên thiết bị, code hiện tại chưa hỗ trợ flow đó.

### 2.2 Có một khoảng trống API upload file

Website Blazor Server hiện upload file trực tiếp vào `IBlobContainer`, sau đó mới tạo `DocumentFile` record.

Hiện tại tôi chỉ thấy API public để đọc file:

- `GET /api/app/blob-files/file?path=...`

Tôi chưa thấy REST API public tổng quát để mobile upload raw file/signature image/seal image lên blob storage. Vì vậy:

- nếu mobile chỉ dùng các `DocumentFileId` có sẵn trong hệ thống thì tích hợp được ngay
- nếu mobile cần upload file đính kèm mới, ảnh chữ ký, ảnh con dấu, file ký lại mới từ thiết bị thì nên bổ sung API upload riêng

Phần này là blocker kỹ thuật nếu app mobile cần upload file trực tiếp.

Ghi chú thêm theo code hiện tại:

- có API upload file của module Chat (`/api/chat/conversations/files/upload`, `/api/chat/files/upload`) nhưng đây là nghiệp vụ chat, không tạo `DocumentFile` cho module trình ký
- vì vậy mobile tích hợp trình ký vẫn cần API upload riêng cho document/signature nếu muốn upload binary trực tiếp

---

## 3. Base URL và authentication

- AuthServer local theo repo: `https://localhost:44301`
- HttpApi local theo repo: `https://localhost:44379`
- Tất cả API nghiệp vụ bên dưới đi qua HttpApi:
  - `https://{api-host}/api/app/...`

Ví dụ:

- `GET https://dev.benhvien199.vn/api/app/document-workflow-instances/document-signing-list`
- `POST https://dev.benhvien199.vn/api/app/document-workflow-instances/process-workflow-action`

Tài liệu này tập trung vào contract API và workflow ký. Phần login/token dùng cùng cơ chế auth hiện có của hệ thống OpenIddict.

---

## 4. Mapping nghiệp vụ mobile với backend hiện tại

| Nghiệp vụ | Backend/API hiện tại | Ghi chú |
|---|---|---|
| Chọn quy trình ký | `DocumentWorkflowInstances.GetWorkflowLookupAsync` hoặc `Workflows.GetListAsync` | Website modal submit đang dùng lookup |
| Xem cấu hình workflow trước khi submit | `GetWorkflowSubmitInfoAsync` | Trả về template, steps, assignee, sign mode |
| Trình ký | `SubmitToWorkflowAsync` | Có thể dùng file workflow template hoặc document cá nhân |
| Duyệt / trả / từ chối | `ProcessWorkflowActionAsync` | Khi duyệt có thể ký điện tử hoặc ký số |
| Danh sách trình ký | `GetDocumentSigningListAsync` | Màn `/document-signing` |
| Re-submit sau khi bị trả | `GetReturnedWorkflowInfoAsync`, `ResubmitReturnedWorkflowAsync` | Chỉ initiator được re-submit |
| Lấy loại ký | `MasterDatas.GetListAsync(Type=LOAI_KY)` | `Code` phải khớp `ELECTRONIC` hoặc `DIGITAL` |
| Lấy chữ ký người dùng | `UserSignatures.GetListAsync` | Lọc theo `IdentityUserId`, `SignType`, `IsActive` |
| Lấy provider/cấu hình ký theo loại ký | `SignatureSettings.GetSignatureSettingLookupBySignTypeAsync` | Dùng khi cấu hình chữ ký người dùng |
| Xem log/file/lịch sử | `GetWorkflowInstanceLogsAsync`, `GetWorkflowInstanceFilesAsync`, `GetDocumentHistoriesByDocumentIdAsync` | Hiển thị giống modal website |
| Xem trạng thái từng bước | `GetAllStepsWithStatusAsync` | Phục vụ timeline bước ký |

---

## 5. Enum và giá trị mobile cần map đúng

### 5.1 `DocumentSourceType`

- `0 = Archive`
- `1 = Personal`
- `2 = SentToMe`
- `3 = Workflow`

Rule rất quan trọng:

- mọi `DocumentWorkflowInstance` chỉ chạy trên document `SourceType = Workflow`
- nếu submit từ document `0/1/2`, backend sẽ duplicate sang `3`

### 5.2 `SignMode`

- `SEQUENTIAL`
- `PARALLEL`

### 5.3 `SignType`

- `ELECTRONIC`
- `DIGITAL`

### 5.4 `DocumentWorkflowInstanceStatus`

- `DRAFT`
- `IN_PROGRESS`
- `COMPLETED`
- `REJECTED`
- `RETURNED`
- `CANCELLED`

### 5.5 `DocumentAssignmentStatus`

- `PENDING`
- `DONE`
- `REJECTED`
- `REVOKE`

### 5.6 `Workflow action`

Khi gọi API xử lý action:

- duyệt: `APPROVE`
- trả về: `RETURN`
- từ chối: `REJECT`

Lưu ý:

- trong DTO comment có chỗ ghi `RETURNED/REJECTED`
- nhưng code UI web và backend xử lý theo `APPROVE`, `RETURN`, `REJECT`

Mobile nên gửi đúng 3 giá trị trên.

---

## 6. Flow mobile giống website

## 6.1 Flow A: tạo trình ký mới

### Bước 1: lấy danh sách workflow để người dùng chọn

Gọi:

- `GET /api/app/document-workflow-instances/workflow-lookup`

Hoặc nếu muốn list chi tiết hơn:

- `GET /api/app/workflows`

### Bước 2: sau khi user chọn workflow, lấy cấu hình submit

Gọi:

- `GET /api/app/document-workflow-instances/workflow-submit-info/{workflowId}`

Mobile phải dùng kết quả này để:

- hiển thị tên workflow
- hiển thị template file của workflow
- hiển thị danh sách step và user được assign
- biết `SignMode = SEQUENTIAL/PARALLEL`
- biết template có phải Word không qua `IsTemplateFileWordFormat`

### Bước 3: user chọn nguồn văn bản để trình

Có 2 nhánh:

#### Nhánh A: dùng file mẫu workflow

Gửi `UseWorkflowTemplateFile = true`

Kết quả backend:

- tạo document mới `SourceType = Workflow`
- tạo `DocumentFile` từ template
- nếu template là Word thì backend replace placeholder rồi convert PDF

#### Nhánh B: dùng document của tôi

Trước hết mobile lấy document cá nhân:

- `GET /api/app/documents?SourceType=1`

Khi user chọn document, nên kiểm tra định dạng file nguồn đầu tiên:

- `GET /api/app/document-workflow-instances/document-source-file-word-format/{documentId}`

Nếu là Word:

- `SigningContent` là bắt buộc

Nếu submit từ `Archive/Personal/SentToMe`:

- backend sẽ duplicate document sang `SourceType = Workflow`

### Bước 4: user nhập nội dung trình ký

Field:

- `SigningContent`

Bắt buộc khi:

- dùng template file Word
- hoặc document nguồn có file đầu tiên là `.doc/.docx`

### Bước 5: user đính kèm file phụ

Website hiện làm:

1. upload file lên blob storage
2. tạo `DocumentFile`
3. gửi list `AttachedFileIds`

Mobile hiện chưa có public REST upload raw file tương ứng trong repo, nên cần:

- hoặc tái sử dụng `DocumentFileId` có sẵn
- hoặc backend bổ sung API upload file trước khi mobile tích hợp phần này

### Bước 6: submit workflow

Gọi:

- `POST /api/app/document-workflow-instances/submit-to-workflow`

Body mẫu:

```json
{
  "documentId": "GUID hoặc null",
  "workflowId": "GUID",
  "useWorkflowTemplateFile": false,
  "useTemplateFile": true,
  "documentFileId": null,
  "attachedFileIds": [
    "GUID1",
    "GUID2"
  ],
  "signingContent": "<p>Nội dung cần phê duyệt...</p>"
}
```

Rule xử lý backend:

- **`useTemplateFile` hiện chưa được backend sử dụng trong `SubmitToWorkflowAsync`** (field tồn tại trong DTO nhưng logic backend quyết định theo `useWorkflowTemplateFile`, `documentId`, `documentFileId`)
- validate workflow có step runnable
- validate step đầu tiên có assignee
- nếu `PARALLEL` thì mọi step active đều phải có assignee
- tạo `DocumentWorkflowInstance`
- tạo `DocumentAssignment`
- tạo `DocumentHistory`
- tạo `DocumentWorkflowInstanceFile`
- cập nhật trạng thái document sang `DANG_XU_LY`

---

## 6.2 Flow B: màn danh sách trình ký

Gọi:

- `GET /api/app/document-workflow-instances/document-signing-list`

Query:

- `FilterText`
- `FilterMode`
- `FromDate`
- `ToDate`
- `SkipCount`
- `MaxResultCount`
- `Sorting`

`FilterMode`:

- `0 = All`
- `1 = SentToMe`
- `2 = SentByMe`
- `3 = Following`

Response item quan trọng:

- `documentId`
- `documentTitle`
- `workflowInstanceId`
- `workflowStatus`
- `currentStepName`
- `currentStepOrder`
- `myAssignmentStatus`
- `myAssignmentId`
- `canAct`
- `canResubmit`

Rule UI:

- nếu `canAct = true` và `myAssignmentStatus = PENDING` thì mở modal action cho user xử lý
- nếu `canResubmit = true` thì hiện nút trình lại

---

## 6.3 Flow C: mở chi tiết action một hồ sơ trình ký

Website hiện load song song các API sau:

- `GET /api/app/document-workflow-instances/{workflowInstanceId}`
- `GET /api/app/document-workflow-instances/workflow-instance-logs/{workflowInstanceId}`
- `GET /api/app/document-workflow-instances/workflow-instance-files/{workflowInstanceId}`
- `GET /api/app/document-workflow-instances/all-steps-with-status/{workflowInstanceId}`
- `GET /api/app/document-workflow-instances/document-histories/{documentId}`
- `GET /api/app/master-datas?Type=LOAI_KY&IsActive=true`
- `GET /api/app/document-assignments?DocumentId={documentId}`
- `POST /api/app/document-workflow-instances/check-and-handle-overdue/{workflowInstanceId}`

Mục đích:

- hiển thị step hiện tại
- hiển thị timeline các bước đã ký/chưa ký
- hiển thị file kết quả ký từng bước
- lấy danh sách loại ký cho nút duyệt
- biết workflow có quá hạn không
- biết step hiện tại có cho phép `RETURN` không

---

## 6.4 Flow D: duyệt và ký

### Bước 1: lấy loại ký

Gọi:

- `GET /api/app/master-datas?Type=LOAI_KY&IsActive=true`

Response cần dùng:

- `id`
- `code`
- `name`

Hiện tại backend strategy chỉ xử lý đúng 2 code:

- `ELECTRONIC`
- `DIGITAL`

Nếu chọn loại khác:

- workflow vẫn approve được
- nhưng không có bước ký tương ứng được apply

### Bước 2: sau khi user chọn loại ký, lấy chữ ký người dùng phù hợp

Gọi:

- `GET /api/app/user-signatures?IdentityUserId={currentUserId}&SignType=ELECTRONIC&IsActive=true`

Hoặc:

- `GET /api/app/user-signatures?IdentityUserId={currentUserId}&SignType=DIGITAL&IsActive=true`

Website còn lọc thêm ở client:

- `ValidFrom <= now`
- `ValidTo >= now`

Nếu chỉ còn 1 chữ ký hợp lệ:

- auto-select

Nếu có nhiều chữ ký:

- bắt buộc user chọn 1 `UserSignatureId`

### Bước 3: gọi approve

Gọi:

- `POST /api/app/document-workflow-instances/process-workflow-action`

Body mẫu approve:

```json
{
  "documentWorkflowInstanceId": "GUID",
  "documentAssignmentId": "GUID",
  "action": "APPROVE",
  "note": "<p>Tôi đồng ý</p>",
  "signingMethodId": "GUID master data LOAI_KY",
  "userSignatureId": "GUID hoặc null"
}
```

Rule validate của backend:

- workflow instance phải `IN_PROGRESS`
- assignment phải `PENDING`
- assignment phải thuộc user hiện tại
- assignment phải thuộc đúng document của workflow instance
- nếu workflow quá hạn thì không cho action

### Bước 4: backend xử lý theo loại ký

#### Nếu `ELECTRONIC`

Backend:

- lấy `UserSignature` của user với `SignType = ELECTRONIC`
- kiểm tra:
  - `IsActive = true`
  - có `SignatureImage`
  - còn hạn
- đọc PDF từ `assignment.DocumentFileResultId`
- chèn vào placeholder:
  - `<<SignXX>>`
  - `<<FullNameXX>>`
  - `<<NoteContentXX>>`
  - `<<PreparedBySign>>`
  - `<<PreparedFullName>>`
- lưu file signed mới vào blob path `electronic-signed/`
- tạo `DocumentFile` mới và update `assignment.DocumentFileResultId`

#### Nếu `DIGITAL`

Backend:

- lấy `UserSignature` của user với `SignType = DIGITAL`
- kiểm tra:
  - `SignatureImage`
  - `TokenRef`
  - `Secret`
  - `SealImg`
  - còn hạn
- lấy `SignatureSetting` theo `ProviderCode`
- kiểm tra:
  - provider active
  - `ApiEndpoint`
  - `LayoutImg`
- gọi provider ký số qua `SignTextLocationCustomizeV2(...)`
- lưu file signed mới vào blob path `digital-signed/`
- tạo `DocumentFile` mới và update `assignment.DocumentFileResultId`

Kết luận cho mobile:

- mobile không gửi file PDF đã ký lên API
- mobile chỉ gửi `signingMethodId` và `userSignatureId`
- backend tự xử lý ký thật

---

## 6.5 Flow E: trả về hoặc từ chối

### Trả về

Gọi:

```json
{
  "documentWorkflowInstanceId": "GUID",
  "documentAssignmentId": "GUID",
  "action": "RETURN",
  "note": "<p>Đề nghị chỉnh sửa nội dung...</p>",
  "signingMethodId": null,
  "userSignatureId": null
}
```

Kết quả:

- instance chuyển sang `RETURNED`
- current step reset về step đầu
- document status thành `TRA_VE`
- initiator có quyền re-submit

### Từ chối

Gọi:

```json
{
  "documentWorkflowInstanceId": "GUID",
  "documentAssignmentId": "GUID",
  "action": "REJECT",
  "note": "<p>Không đồng ý</p>",
  "signingMethodId": null,
  "userSignatureId": null
}
```

Kết quả:

- instance chuyển sang `REJECTED`
- các pending assignment khác bị revoke
- document status thành `TU_CHOI`

---

## 6.6 Flow F: re-submit sau khi bị trả về

### Bước 1: lấy dữ liệu cũ để fill form

Gọi:

- `GET /api/app/document-workflow-instances/returned-workflow-info/{workflowInstanceId}`

Response chính:

- `workflowInstanceId`
- `documentId`
- `workflowId`
- `documentTitle`
- `documentNo`
- `storageNumber`
- `lastSigningContent`
- `workflowInfo`
- `attachedFiles`
- `documentFiles`

### Bước 2: user chỉnh sửa

Mobile cho phép:

- đổi `SigningContent`
- đổi file nguồn
- đổi document khác
- dùng lại template workflow
- thêm file đính kèm
- xóa file đính kèm cũ

### Bước 3: gọi re-submit

Gọi:

- `POST /api/app/document-workflow-instances/resubmit-returned-workflow`

Body mẫu:

```json
{
  "returnedWorkflowInstanceId": "GUID",
  "useWorkflowTemplateFile": false,
  "documentFileId": null,
  "newDocumentId": "GUID hoặc null",
  "signingContent": "<p>Nội dung đã chỉnh sửa</p>",
  "attachedFileIds": [
    "GUID1"
  ],
  "deleteFileIds": [
    "GUID2"
  ]
}
```

Rule rất quan trọng:

- chỉ initiator mới được re-submit
- backend reuse chính `workflow instance` cũ để giữ nguyên log/history
- nếu đổi sang document không phải `Workflow`, backend sẽ duplicate sang `SourceType = Workflow`
- trạng thái document sau re-submit quay về `DANG_XU_LY`

---

## 7. Nhóm API mobile cần dùng

## 7.1 Workflow và submit

### 7.1.1 Lấy workflow lookup

**GET** `/api/app/document-workflow-instances/workflow-lookup`

Mục đích:

- đổ dropdown chọn workflow ở màn trình ký

### 7.1.2 Lấy thông tin submit workflow

**GET** `/api/app/document-workflow-instances/workflow-submit-info/{workflowId}`

Mục đích:

- lấy steps
- assignee từng step
- template file
- sign mode

Response field quan trọng:

```json
{
  "workflowId": "GUID",
  "workflowName": "Quy trình ký văn bản",
  "workflowTemplateId": "GUID",
  "workflowTemplateName": "Template v3",
  "wordTemplatePath": "workflow-templates/abc.docx",
  "pdfTemplatePath": null,
  "hasTemplateFile": true,
  "signMode": "SEQUENTIAL",
  "isTemplateFileWordFormat": true,
  "steps": [
    {
      "stepId": "GUID",
      "order": 1,
      "name": "Trưởng khoa duyệt",
      "type": "SIGN",
      "slaDays": 2,
      "allowReturn": true,
      "assignedUsers": [
        {
          "userId": "GUID",
          "userName": "admin",
          "fullName": "Nguyen Van A",
          "isPrimary": true
        }
      ]
    }
  ]
}
```

### 7.1.3 Kiểm tra document nguồn có phải Word không

**GET** `/api/app/document-workflow-instances/document-source-file-word-format/{documentId}`

Trả về:

- `true`: bắt buộc có `SigningContent`
- `false`: `SigningContent` không bắt buộc theo rule này

### 7.1.4 Submit workflow

**POST** `/api/app/document-workflow-instances/submit-to-workflow`

Body: dùng `SubmitToWorkflowInput`

Field:

- `documentId`
- `workflowId`
- `useWorkflowTemplateFile`
- `useTemplateFile`
- `documentFileId`
- `attachedFileIds`
- `signingContent`

### 7.1.5 Re-submit returned workflow

**POST** `/api/app/document-workflow-instances/resubmit-returned-workflow`

Body: dùng `ResubmitReturnedWorkflowInput`

---

## 7.2 Danh sách / trạng thái / chi tiết workflow

### 7.2.1 Danh sách trình ký

**GET** `/api/app/document-workflow-instances/document-signing-list`

### 7.2.2 Workflow active của một document

**GET** `/api/app/document-workflow-instances/active-workflow-status/{documentId}`

### 7.2.3 Log instance

**GET** `/api/app/document-workflow-instances/workflow-instance-logs/{workflowInstanceId}`

### 7.2.4 File của instance

**GET** `/api/app/document-workflow-instances/workflow-instance-files/{workflowInstanceId}`

### 7.2.5 History của document

**GET** `/api/app/document-workflow-instances/document-histories/{documentId}`

### 7.2.6 Tất cả step và trạng thái ký

**GET** `/api/app/document-workflow-instances/all-steps-with-status/{workflowInstanceId}`

Response item mỗi step:

- `stepId`
- `order`
- `name`
- `type`
- `isCurrentStep`
- `isCompleted`
- `users[]`

Mỗi user:

- `userId`
- `fullName`
- `userName`
- `isPrimary`
- `status`
- `processedAt`
- `signingIndex`

### 7.2.7 Check overdue trước khi action

**POST** `/api/app/document-workflow-instances/check-and-handle-overdue/{workflowInstanceId}`

Response:

```json
{
  "isOverdue": false,
  "allowReturn": true
}
```

Lưu ý đúng theo code mới: API này **chỉ kiểm tra (read-only)**, không tự cập nhật trạng thái huỷ. Việc huỷ quá hạn do `WorkflowOverdueBackgroundWorker` xử lý nền.

---

## 7.3 Action approve / return / reject

### 7.3.1 Process action

**POST** `/api/app/document-workflow-instances/process-workflow-action`

Body:

```json
{
  "documentWorkflowInstanceId": "GUID",
  "documentAssignmentId": "GUID",
  "action": "APPROVE",
  "note": "<p>Nội dung ghi chú</p>",
  "signingMethodId": "GUID hoặc null",
  "userSignatureId": "GUID hoặc null"
}
```

Validation mobile nên áp dụng giống web:

- nếu `action = APPROVE` thì bắt buộc chọn `signingMethodId`
- nếu cùng loại ký có nhiều chữ ký hợp lệ thì bắt buộc chọn `userSignatureId`

---

## 7.4 API lấy loại ký và chữ ký người dùng

### 7.4.1 Lấy danh sách loại ký

**GET** `/api/app/master-datas?Type=LOAI_KY&IsActive=true&MaxResultCount=100`

Field cần dùng:

- `id`
- `code`
- `name`

### 7.4.2 Lấy danh sách chữ ký của user theo loại ký

**GET** `/api/app/user-signatures`

Query dùng ở mobile:

- `IdentityUserId={currentUserId}`
- `SignType=ELECTRONIC` hoặc `DIGITAL`
- `IsActive=true`
- `SkipCount=0`
- `MaxResultCount=100`
- `Sorting=UserSignature.ValidTo desc`

Response cần dùng:

- `userSignature.id`
- `userSignature.signType`
- `userSignature.providerCode`
- `userSignature.tokenRef`
- `userSignature.secret`
- `userSignature.sealImg`
- `userSignature.signatureImage`
- `userSignature.validFrom`
- `userSignature.validTo`
- `userSignature.isActive`

Lưu ý:

- mobile không nên hiển thị `secret` ra UI
- chỉ dùng `id`, `providerCode`, `validTo`, `signType` để chọn

### 7.4.3 Lấy chi tiết 1 chữ ký

**GET** `/api/app/user-signatures/{id}`

### 7.4.4 Tạo chữ ký người dùng

**POST** `/api/app/user-signatures`

Body chính:

```json
{
  "signType": "DIGITAL",
  "providerCode": "BNS",
  "tokenRef": "token-ref",
  "secret": "secret",
  "sealImg": "user-seal-images/abc.png",
  "signatureImage": "user-signature-images/xyz.png",
  "validFrom": "2026-03-25T00:00:00Z",
  "validTo": "2027-03-25T00:00:00Z",
  "isActive": true,
  "identityUserId": "GUID"
}
```

Rule validate theo UI web:

#### Chung cho cả 2 loại ký

- bắt buộc `signType`
- bắt buộc `providerCode`
- bắt buộc `signatureImage`
- bắt buộc `identityUserId`

#### Riêng `DIGITAL`

- bắt buộc `tokenRef`
- bắt buộc `secret`
- bắt buộc `sealImg`

### 7.4.5 Cập nhật chữ ký người dùng

**PUT** `/api/app/user-signatures/{id}`

Lưu ý:

- cần gửi thêm `concurrencyStamp`

### 7.4.6 Xóa chữ ký người dùng

**DELETE** `/api/app/user-signatures/{id}`

---

## 7.5 API cấu hình provider ký

Nhóm này chủ yếu phục vụ màn cấu hình chữ ký, không dùng trực tiếp khi approve.

### 7.5.1 Lấy lookup provider theo loại ký

**GET** `/api/app/signature-settings/lookup-by-sign-type?DefaultSignType=DIGITAL`

Hoặc:

**GET** `/api/app/signature-settings/lookup-by-sign-type?DefaultSignType=ELECTRONIC`

Response:

```json
{
  "totalCount": 1,
  "items": [
    {
      "id": "GUID",
      "displayName": "BNS"
    }
  ]
}
```

Mobile dùng API này để:

- user chọn provider khi tạo `UserSignature`
- map `signatureSettingId -> providerCode`

### 7.5.2 Lấy danh sách cấu hình provider

**GET** `/api/app/signature-settings`

Field quan trọng:

- `providerCode`
- `providerType`
- `apiEndpoint`
- `layoutImg`
- `apiTimeout`
- `defaultSignType`
- `allowElectronicSign`
- `allowDigitalSign`
- `requireOtp`
- `signWidth`
- `signHeight`
- `signedFileSuffix`
- `keepOriginalFile`
- `overwriteSignedFile`
- `enableSignLog`
- `isActive`

Ý nghĩa thực tế:

- backend ký số đang dùng:
  - `providerCode`
  - `apiEndpoint`
  - `layoutImg`
  - `signWidth`
  - `signHeight`
  - `isActive`
- các field còn lại hiện là cấu hình lưu trữ/khả năng, mobile có thể hiển thị nếu có màn admin

---

## 7.6 API document và file liên quan

### 7.6.1 Lấy document cá nhân để chọn submit

**GET** `/api/app/documents?SourceType=1&CreatorId={currentUserId}&SkipCount=0&MaxResultCount=1000`

Lưu ý:

- DTO `GetDocumentsInput` base không có `CreatorId`
- nhưng UI web đang dùng field này ở phần extended contract/service
- mobile nên gọi đúng như web đang dùng

### 7.6.2 Lấy document detail

**GET** `/api/app/documents/with-navigation-properties/{id}`

### 7.6.3 Lấy file của document

**GET** `/api/app/document-files?DocumentId={documentId}`

Field cần dùng:

- `documentFile.id`
- `documentFile.name`
- `documentFile.path`
- `documentFile.isSigned`
- `documentFile.uploadedAt`

### 7.6.4 Tạo `DocumentFile`

**POST** `/api/app/document-files`

Body:

```json
{
  "name": "abc.pdf",
  "path": "workflow-files/123.pdf",
  "hash": "base64-sha256",
  "isSigned": false,
  "uploadedAt": "2026-03-25T10:00:00Z",
  "documentId": null
}
```

Lưu ý:

- API này chỉ tạo metadata record
- không upload binary file
- binary hiện tại website upload trực tiếp vào blob storage trước

### 7.6.5 Download/preview file blob

**GET** `/api/app/blob-files/file?path={url-encoded-path}`

Ví dụ:

- `/api/app/blob-files/file?path=electronic-signed%2Fabc.pdf`
- `/api/app/blob-files/file?path=user-signature-images%2Fxyz.png`

Dùng để:

- preview PDF đã ký
- tải ảnh chữ ký
- tải ảnh con dấu
- tải layout ảnh provider ký số

---

## 7.7 API document assignments

Mobile có thể dùng để hiển thị file ký từng bước giống website.

### 7.7.1 Lấy assignment theo document

**GET** `/api/app/document-assignments?DocumentId={documentId}&SkipCount=0&MaxResultCount=100`

Field quan trọng:

- `documentAssignment.id`
- `documentAssignment.stepOrder`
- `documentAssignment.actionType`
- `documentAssignment.status`
- `documentAssignment.isCurrent`
- `documentAssignment.receiverUserId`
- `documentAssignment.documentFileResultId`
- navigation `documentFileResult`

Website dùng list này để lấy các file signed hiện có.

---

## 8. Chi tiết cấu hình chữ ký điện tử / chữ ký số cho mobile

## 8.1 Mobile cần những màn nào

Tối thiểu nên có:

1. Màn danh sách chữ ký của tôi
2. Màn tạo/sửa chữ ký điện tử
3. Màn tạo/sửa chữ ký số
4. Màn chọn loại ký khi duyệt
5. Màn chọn chữ ký cụ thể nếu user có nhiều chữ ký cùng loại

## 8.2 Cấu hình chữ ký điện tử

Field bắt buộc:

- `signType = ELECTRONIC`
- `providerCode`
- `signatureImage`
- `identityUserId`

Field nên có:

- `validFrom`
- `validTo`
- `isActive`

Field không bắt buộc cho điện tử:

- `tokenRef`
- `secret`
- `sealImg`

## 8.3 Cấu hình chữ ký số

Field bắt buộc:

- `signType = DIGITAL`
- `providerCode`
- `signatureImage`
- `tokenRef`
- `secret`
- `sealImg`
- `identityUserId`

Field nên có:

- `validFrom`
- `validTo`
- `isActive`

## 8.4 Chọn provider ký

Flow giống web:

1. User chọn `signType`
2. Mobile gọi `GET /api/app/signature-settings/lookup-by-sign-type`
3. User chọn provider
4. Mobile map `lookup.id -> displayName`
5. Khi create/update `UserSignature`, backend lưu `providerCode = displayName`

Hiện tại website đang lưu `ProviderCode`, không lưu `SignatureSettingId` trong `UserSignature`.

---

## 8.5 Master data bắt buộc để mobile tích hợp ổn định

Ngoài `LOAI_KY`, flow trình ký hiện tại còn phụ thuộc các master data sau ở backend:

1. **`TRANG_THAI_VB` (status văn bản)**
   - bắt buộc có các code backend đang set trong flow: `DA_GUI`, `DANG_XU_LY`, `HT`, `DA_HUY`, `TRA_VE`, `TU_CHOI`
   - nếu thiếu 1 trong các code này, backend có thể không cập nhật trạng thái document đúng sau submit/approve/return/reject/overdue

2. **`LOAI_VB` (DocumentType)**
3. **`MUC_DO_KHAN` (UrgencyLevel)**
4. **`MUC_DO_MAT` (SecrecyLevel)**
   - 3 loại trên được dùng khi backend tự tạo document workflow từ template (`UseWorkflowTemplateFile = true`)
   - backend lấy record đầu tiên theo `SortOrder` làm default; nếu không có dữ liệu sẽ lỗi `NoDefaultMasterDataFound`

5. **`LOAI_KY` (signing methods)**
   - khuyến nghị tối thiểu có 2 code để giống webapp: `ELECTRONIC`, `DIGITAL`
   - khi `APPROVE`, nếu chọn mã khác thì workflow vẫn duyệt, nhưng backend không áp dụng strategy ký tương ứng

Khuyến nghị cho mobile team khi go-live tenant mới:

- kiểm tra đủ bộ master data ở trên trước khi bật chức năng submit/approve trên mobile
- nếu thiếu dữ liệu, hiển thị cảnh báo cấu hình thay vì cho user thao tác rồi fail runtime

## 9. Rule nghiệp vụ mobile phải tuân thủ

### 9.1 Khi submit

- luôn gọi `GetWorkflowSubmitInfoAsync` trước
- không tự assume workflow runnable
- nếu backend báo:
  - không có template
  - step đầu không có assignee
  - parallel step thiếu assignee
  thì phải chặn submit

### 9.2 Khi approve

- bắt buộc chọn `SigningMethodId`
- nếu nhiều chữ ký cùng loại thì bắt buộc chọn `UserSignatureId`
- trước khi cho bấm duyệt nên gọi `CheckAndHandleOverdueAsync`

### 9.3 Khi show danh sách chữ ký

- lọc:
  - `IsActive = true`
  - `ValidFrom <= now`
  - `ValidTo >= now`

### 9.4 Khi re-submit

- chỉ cho initiator thao tác
- nên load `GetReturnedWorkflowInfoAsync` trước để prefill form

### 9.5 Với file Word

- nếu template/document nguồn là `.doc/.docx`
- `SigningContent` là bắt buộc
- backend sẽ replace placeholder rồi convert sang PDF

---

## 10. Các lỗi backend mobile có thể gặp

Các lỗi nghiệp vụ quan trọng đã thấy trong code:

- `NoActiveWorkflowTemplateFound`
- `NoWorkflowStepsFound`
- `FirstStepMustHaveAssignedUsers`
- `AllStepsMustHaveAssignedUsers`
- `WorkflowTemplateHasNoFile`
- `DocumentAlreadyHasActiveWorkflow`
- `WorkflowNotInProgress`
- `WorkflowOverdue`
- `AssignmentNotPending`
- `NotAuthorizedForThisAction`
- `InvalidWorkflowAction`
- `WorkflowNotReturned`
- `OnlyInitiatorCanResubmit`
- `UserHasNoElectronicSignature`
- `UserHasNoDigitalSignature`
- `SelectedUserSignatureNotFound`
- `SignatureImageNotConfigured`
- `DigitalSignatureTokenRefRequired`
- `DigitalSignatureSecretRequired`
- `DigitalSignatureSealImageRequired`
- `DigitalSignatureProviderNotFound`
- `DigitalSignatureLayoutImageRequired`
- `SignatureExpired`
- `SignatureNotYetValid`

Mobile nên map các lỗi này sang thông báo thân thiện cho người dùng.

---

## 11. Đề xuất thứ tự tích hợp cho team mobile

### Giai đoạn 1: đọc và xử lý workflow

Làm trước:

1. danh sách trình ký
2. chi tiết workflow
3. approve / return / reject
4. chọn loại ký
5. chọn chữ ký user

### Giai đoạn 2: submit và re-submit

Làm tiếp:

1. chọn workflow
2. lấy submit info
3. submit từ document có sẵn
4. re-submit returned workflow

### Giai đoạn 3: cấu hình chữ ký

Làm sau:

1. quản lý `UserSignature`
2. lookup `SignatureSetting` theo `SignType`

### Giai đoạn 4: upload file native

Cần backend bổ sung nếu mobile muốn:

1. upload ảnh chữ ký
2. upload ảnh con dấu
3. upload file đính kèm submit
4. upload file đính kèm re-submit

---

## 12. Checklist test tích hợp

- User có 1 chữ ký điện tử hợp lệ, approve thành công
- User có nhiều chữ ký điện tử, phải chọn đúng chữ ký
- User có 1 chữ ký số hợp lệ, approve thành công
- Chữ ký số thiếu `TokenRef/Secret/SealImg`, backend trả lỗi đúng
- Workflow `SEQUENTIAL`, approve step 1 sinh assignment step 2
- Workflow `PARALLEL`, các step cùng ký và hoàn thành merge
- Return workflow, initiator thấy `canResubmit = true`
- Re-submit giữ nguyên log cũ
- Submit từ document personal tạo workflow child document
- File signed tải được qua `blob-files/file`
- Workflow quá hạn thì không cho action

---

## 13. Kết luận cho team mobile

Để làm giống website hiện tại, mobile chỉ cần bám đúng 3 khối chính:

1. `DocumentWorkflowInstances`
2. `UserSignatures`
3. `MasterDatas(Type=LOAI_KY)`

Về bản chất:

- mobile chọn workflow và ký kiểu nào
- backend mới là nơi thực hiện ký và tạo file signed

Điểm cần chốt sớm với backend:

- có bổ sung REST upload file/ảnh cho mobile hay không
- có cần hỗ trợ mobile-native signing hay vẫn giữ mô hình backend ký hộ như web hiện tại

