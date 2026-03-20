# Tài liệu API mobile tích hợp Project / ProjectTask

## 1. Phạm vi tài liệu

Tài liệu này mô tả các API mobile cần dùng để tích hợp theo flow web hiện tại cho 2 nhóm chức năng:

1. **Project** (DỰ ÁN)
   - Project (CRUD)
   - ProjectMembers
2. **ProjectTask** (CÔNG VIỆC)
   - ProjectTask (CRUD)
   - ProjectTaskAssignment / ProjectTaskAssigment
   - ProjectTaskDocument

Ngoài CRUD, tài liệu cũng liệt kê:
- API lookup cần gọi để mobile lấy danh sách user / document / project / department.
- Enum cần map ở mobile.
- Required fields khi tạo/sửa.
- Flow đề xuất để mobile làm giống web hiện tại.

---

## 2. Mapping tên nghiệp vụ mobile ↔ backend hiện tại

| Nghiệp vụ mobile yêu cầu | API/backend hiện tại | Ghi chú |
|---|---|---|
| Project | `Projects` | Có đầy đủ CRUD |
| ProjectMembers /  Web đang dùng `ProjectMembers` để quản lý thành viên dự án |
| ProjectTask | `ProjectTasks` | Có đầy đủ CRUD |
| ProjectTaskAssignment / ProjectTaskAssigment | `ProjectTaskAssignments` | Có CRUD + user lookup |
| ProjectTaskDocument | `ProjectTaskDocuments` | Có CRUD + document lookup |

---

## 3. Base URL và authentication

- Tất cả API bên dưới đang nằm dưới nhóm `api/app/...`.
- Ví dụ local API host theo AGENTS: `https://dev.benhvien199.vn`.
- Mobile chỉ cần ghép: `{baseUrl}` + `{route}`.

Ví dụ:
- `GET https://dev.benhvien199.vn/api/app/projects`
- `GET https://dev.benhvien199.vn/api/app/project-tasks`

Tài liệu này tập trung vào contract API; phần auth/token có thể dùng cùng cơ chế mà mobile đang dùng cho các API khác của hệ thống.

---

## 4. Flow tích hợp Dự án giống web
### 4.1. Flow Project trên web hiện tại

1. Mở danh sách project.
2. Gọi API list project.
3. Khi tạo/sửa project:
   - lấy department lookup nếu cần chọn phòng ban phụ trách,
   - gọi create/update project.
4. Khi mở tab/thao tác “thành viên dự án”:
   - gọi list `ProjectMembers` theo `ProjectId`,
   - gọi `identity-user-lookup` để chọn user,
   - gọi create/update/delete `ProjectMembers`.
5. Khi mở danh sách task theo project:
   - gọi list `ProjectTasks` với `ProjectId`.

### 4.2. Flow ProjectTask (Công việc) trên web hiện tại

1. Tạo task phần thông tin chung trước. (save xong qua bước 2)
2. Sau khi có `ProjectTaskId`:
   - gọi `ProjectTaskAssignments` để thêm người thực hiện,
   - gọi `ProjectTaskDocuments` để gắn văn bản.
3. Khi xem chi tiết task:
   - gọi `GetWithNavigationProperties` của task,
   - gọi list assignment theo `ProjectTaskId`,
   - gọi list document theo `ProjectTaskId`.
4. Khi cần mở file PDF của document gắn task:
   - gọi `DocumentFiles` theo `DocumentId` để lấy file/path,
   - nếu là PDF có thể gọi service watermark PDF.

---

## 5. API nhóm Project

## 5.1. Project CRUD

### 5.1.1. Danh sách project

**GET** `/api/app/projects`

Query thường dùng:
- `FilterText`
- `Code`
- `Name`
- `Description`
- `StartDateMin`, `StartDateMax`
- `EndDateMin`, `EndDateMax`
- `Status`
- `OwnerDepartmentId`
- `UserId`
- `Sorting`
- `SkipCount`
- `MaxResultCount`

**Ví dụ**
```http
GET /api/app/projects?FilterText=cntt&Status=IN_PROGRESS&SkipCount=0&MaxResultCount=20
```

**Kết quả trả về**
- `items[]` là `ProjectWithNavigationPropertiesDto`
- mỗi item có:
  - `project`
  - `ownerDepartment`
  - `projectMemberCount`
  - `projectTaskCount`

### 5.1.2. Chi tiết project có navigation

**GET** `/api/app/projects/with-navigation-properties/{id}`

Dùng khi mobile cần hiển thị:
- thông tin project,
- department phụ trách,
- tổng số member,
- tổng số task.

### 5.1.3. Chi tiết project cơ bản

**GET** `/api/app/projects/{id}`

### 5.1.4. Tạo project

**POST** `/api/app/projects`

**Body**
```json
{
  "code": "DA-001",
  "name": "Triển khai HIS mobile",
  "description": "Mô tả dự án",
  "startDate": "2026-03-20T00:00:00Z",
  "endDate": "2026-06-30T00:00:00Z",
  "status": 1,
  "ownerDepartmentId": "GUID hoặc null"
}
```

**Required fields**
- `code`
- `name`
- `status`

**Nên validate thêm ở mobile**
- `code` tối đa 50 ký tự
- `name` tối đa 255 ký tự
- `startDate <= endDate`

### 5.1.5. Cập nhật project

**PUT** `/api/app/projects/{id}`

Body giống create, thêm:
- `concurrencyStamp` (**bắt buộc khi update**)

### 5.1.6. Xóa project

**DELETE** `/api/app/projects/{id}`

---

## 5.2. Lookup cho Project

### 5.2.1. Lấy department lookup

**GET** `/api/app/projects/department-lookup`

Dùng khi tạo/sửa project để chọn `OwnerDepartmentId`.

**Query**
- `Filter`
- `SkipCount`
- `MaxResultCount`

**Response**
```json
{
  "items": [
    {
      "id": "GUID",
      "displayName": "Phòng Công nghệ thông tin"
    }
  ],
  "totalCount": 1
}
```

---

## 5.3. ProjectMembers


### 5.3.1. Danh sách thành viên dự án

**GET** `/api/app/project-members`

Query thường dùng:
- `ProjectId`
- `UserId`
- `MemberRole`
- `JoinedAtMin`, `JoinedAtMax`
- `FilterText`
- `Sorting`
- `SkipCount`
- `MaxResultCount`

**Kết quả**
- `ProjectMemberWithNavigationPropertiesDto`
  - `projectMember`
  - `project`
  - `user`

### 5.3.2. Chi tiết một member

**GET** `/api/app/project-members/with-navigation-properties/{id}`

### 5.3.3. Tạo member cho project

**POST** `/api/app/project-members`

**Body**
```json
{
  "memberRole": 1,
  "joinedAt": "2026-03-20T08:00:00Z",
  "projectId": "GUID",
  "userId": "GUID"
}
```

**Required fields**
- `memberRole`
- `projectId`
- `userId`

**Lưu ý tích hợp**
- Web hiện tại có check trùng trước khi add: mobile cũng nên check user đã thuộc project chưa bằng API list với `ProjectId + UserId`.

### 5.3.4. Cập nhật member role

**PUT** `/api/app/project-members/{id}`

Body giống create, thêm `concurrencyStamp`.

### 5.3.5. Xóa member

**DELETE** `/api/app/project-members/{id}`

### 5.3.6. Lookup project cho project-member

**GET** `/api/app/project-members/project-lookup`

### 5.3.7. Lookup user cho project-member

**GET** `/api/app/project-members/identity-user-lookup`

Đây là API mobile nên dùng khi cần **get user** để gán vào project.

---

## 6. API nhóm ProjectTask

## 6.1. ProjectTask CRUD

### 6.1.1. Danh sách task

**GET** `/api/app/project-tasks`

Query thường dùng:
- `FilterText`
- `OnlyParentTasks`
- `OnlyChildTasks`
- `ParentTaskId`
- `Code`
- `Title`
- `Description`
- `StartDateMin`, `StartDateMax`
- `DueDateMin`, `DueDateMax`
- `Priority`
- `Status`
- `ProgressPercentMin`, `ProgressPercentMax`
- `ProjectId`
- `UserId`
- `Sorting`
- `SkipCount`
- `MaxResultCount`

**Ví dụ**
```http
GET /api/app/project-tasks?ProjectId={projectId}&Status=IN_PROGRESS&SkipCount=0&MaxResultCount=20
```

**Response item** là `ProjectTaskWithNavigationPropertiesDto`, gồm:
- `projectTask`
- `project`
- `projectTaskAssignments[]`
- `projectTaskDocumentsCount`
- `parentTaskTitle`
- `childTaskCount`

### 6.1.2. Chi tiết task có navigation

**GET** `/api/app/project-tasks/with-navigation-properties/{id}`

Dùng khi mobile mở màn hình detail task.

### 6.1.3. Chi tiết task cơ bản

**GET** `/api/app/project-tasks/{id}`

### 6.1.4. Tạo task

**POST** `/api/app/project-tasks`

**Body**
```json
{
  "parentTaskId": null,
  "code": "TASK-001",
  "title": "Thiết kế API mobile",
  "description": "Chi tiết công việc",
  "startDate": "2026-03-20T00:00:00Z",
  "dueDate": "2026-03-25T00:00:00Z",
  "priority": "HIGH",
  "status": "TODO",
  "progressPercent": 0,
  "projectId": "GUID"
}
```

**Required fields**
- `code`
- `title`
- `priority`
- `status`
- `projectId`

**Lưu ý quan trọng**
- `priority` là **string enum**, ví dụ: `LOW`, `MEDIUM`, `HIGH`, `URGENT`.
- `status` là **string enum**, ví dụ: `TODO`, `IN_PROGRESS`, `WAITING`, `DONE`, `CANCELLED`.
- `parentTaskId` đang là kiểu **string?**, không phải `Guid?`; mobile cần gửi đúng định dạng backend đang dùng.
- `progressPercent` nên nằm trong `0..100`.

### 6.1.5. Cập nhật task

**PUT** `/api/app/project-tasks/{id}`

Body giống create, thêm `concurrencyStamp`.

### 6.1.6. Xóa task

**DELETE** `/api/app/project-tasks/{id}`

### 6.1.7. Lookup project cho task

**GET** `/api/app/project-tasks/project-lookup`

Dùng khi mobile cần chọn project lúc tạo/sửa task.

---

## 6.2. ProjectTaskAssignment

### 6.2.1. Danh sách assignment của task

**GET** `/api/app/project-task-assignments`

Query thường dùng:
- `ProjectTaskId`
- `UserId`
- `AssignmentRole`
- `AssignedAtMin`, `AssignedAtMax`
- `Note`
- `FilterText`
- `Sorting`
- `SkipCount`
- `MaxResultCount`

**Response item** là `ProjectTaskAssignmentWithNavigationPropertiesDto`:
- `projectTaskAssignment`
- `projectTask`
- `user`

### 6.2.2. Chi tiết assignment

**GET** `/api/app/project-task-assignments/with-navigation-properties/{id}`

### 6.2.3. Tạo assignment

**POST** `/api/app/project-task-assignments`

**Body**
```json
{
  "assignmentRole": "MAIN",
  "assignedAt": "2026-03-20T08:30:00Z",
  "note": "Phụ trách chính",
  "projectTaskId": "GUID",
  "userId": "GUID"
}
```

**Required fields**
- `assignmentRole`
- `projectTaskId`
- `userId`

**Mobile nên làm giống web**
- Tạo task xong mới tạo assignment.
- Khi chọn user, gọi API lookup user.
- Có thể check trùng bằng cách gọi list với `ProjectTaskId + UserId` trước khi add.

### 6.2.4. Cập nhật assignment

**PUT** `/api/app/project-task-assignments/{id}`

Body giống create, thêm `concurrencyStamp`.

### 6.2.5. Xóa assignment

**DELETE** `/api/app/project-task-assignments/{id}`

### 6.2.6. Lookup task cho assignment

**GET** `/api/app/project-task-assignments/project-task-lookup`

### 6.2.7. Lookup user cho assignment

**GET** `/api/app/project-task-assignments/identity-user-lookup`

Đây là API mobile nên dùng khi cần **get user** để gán vào task.

---

## 6.3. ProjectTaskDocument

### 6.3.1. Danh sách document của task

**GET** `/api/app/project-task-documents`

Query thường dùng:
- `ProjectTaskId`
- `DocumentId`
- `DocumentPurpose`
- `FilterText`
- `Sorting`
- `SkipCount`
- `MaxResultCount`

**Response item** là `ProjectTaskDocumentWithNavigationPropertiesDto`:
- `projectTaskDocument`
- `projectTask`
- `document`

### 6.3.2. Chi tiết relation task-document

**GET** `/api/app/project-task-documents/with-navigation-properties/{id}`

### 6.3.3. Tạo relation task-document

**POST** `/api/app/project-task-documents`

**Body**
```json
{
  "documentPurpose": "REPORT",
  "projectTaskId": "GUID",
  "documentId": "GUID"
}
```

**Required fields**
- `documentPurpose`
- `projectTaskId`
- `documentId`

### 6.3.4. Cập nhật relation task-document

**PUT** `/api/app/project-task-documents/{id}`

Body giống create, thêm `concurrencyStamp`.

### 6.3.5. Xóa relation task-document

**DELETE** `/api/app/project-task-documents/{id}`

### 6.3.6. Lookup task cho task-document

**GET** `/api/app/project-task-documents/project-task-lookup`

### 6.3.7. Lookup document cho task-document

**GET** `/api/app/project-task-documents/document-lookup`

Đây là API mobile nên dùng khi cần **get document** để gắn vào task.

---

## 7. API bổ trợ để mobile lấy user / document / file

## 7.1. Get user

### Trường hợp gán user vào Project
- **GET** `/api/app/project-members/identity-user-lookup`

### Trường hợp gán user vào ProjectTask
- **GET** `/api/app/project-task-assignments/identity-user-lookup`

**Query mẫu**
```http
GET /api/app/project-task-assignments/identity-user-lookup?Filter=an&SkipCount=0&MaxResultCount=20
```

**Response mẫu**
```json
{
  "items": [
    {
      "id": "GUID",
      "displayName": "nguyenvana"
    }
  ],
  "totalCount": 1
}
```

---

## 7.2. Get document

### Trường hợp gắn document vào task
- **GET** `/api/app/project-task-documents/document-lookup`

### Trường hợp cần danh sách văn bản tổng quát
- **GET** `/api/app/documents`
- **GET** `/api/app/documents/{id}`
- **GET** `/api/app/documents/with-navigation-properties/{id}`

**Ví dụ list documents**
```http
GET /api/app/documents?FilterText=bao%20cao&SourceType=1&SkipCount=0&MaxResultCount=20
```

---

## 7.3. Get file của document

Khi mobile cần lấy file đính kèm của document:

### Danh sách file theo document
**GET** `/api/app/document-files?DocumentId={documentId}&SkipCount=0&MaxResultCount=100`

Response mỗi item gồm:
- `documentFile.id`
- `documentFile.name`
- `documentFile.path`
- `documentFile.hash`
- `documentFile.isSigned`
- `documentFile.uploadedAt`

### Lấy chi tiết file
**GET** `/api/app/document-files/{id}`

---

## 7.4. Xem/tải PDF có watermark

Nếu document file là PDF và mobile cần xem/tải bản watermark:

- Service contract: `GetWatermarkedPdfAsync`
- Input:
```json
{
  "blobPath": "duong-dan-trong-blob",
  "action": "view"
}
```

`action` nhận:
- `view`
- `download`

> Phần này là application service chuyên cho PDF watermark. Khi mobile cần triển khai thực tế, nên xác nhận lại route publish của service này trong swagger/runtime host.

---

## 8. Enum cần map ở mobile

## 8.1. ProjectStatus

| Name | Value |
|---|---:|
| `PLANNING` | 0 |
| `IN_PROGRESS` | 1 |
| `COMPLETED` | 2 |
| `CANCELLED` | 3 |

**Dùng cho**
- `ProjectCreateDto.status`
- `ProjectUpdateDto.status`
- filter `GetProjectsInput.status`

---

## 8.2. ProjectMemberRole

| Name | Value |
|---|---:|
| `OWNER` | 0 |
| `MEMBER` | 1 |
| `VIEWER` | 2 |

**Dùng cho**
- `ProjectMemberCreateDto.memberRole`
- `ProjectMemberUpdateDto.memberRole`

---

## 8.3. ProjectTaskPriority

> API create/update task đang dùng **string**, mobile nên gửi đúng tên enum bên dưới.

| Name | Value enum |
|---|---:|
| `LOW` | 0 |
| `MEDIUM` | 1 |
| `HIGH` | 2 |
| `URGENT` | 3 |

**Giá trị gửi khuyến nghị**
- `LOW`
- `MEDIUM`
- `HIGH`
- `URGENT`

---

## 8.4. ProjectTaskStatus

> API create/update task đang dùng **string**, mobile nên gửi đúng tên enum bên dưới.

| Name | Value enum |
|---|---:|
| `TODO` | 0 |
| `IN_PROGRESS` | 1 |
| `WAITING` | 2 |
| `DONE` | 3 |
| `CANCELLED` | 4 |

**Giá trị gửi khuyến nghị**
- `TODO`
- `IN_PROGRESS`
- `WAITING`
- `DONE`
- `CANCELLED`

---

## 8.5. ProjectTaskAssignmentRole

| Name | Value enum |
|---|---:|
| `MAIN` | 0 |
| `SUPPORT` | 1 |
| `REVIEW` | 2 |

**Dùng cho**
- `ProjectTaskAssignmentCreateDto.assignmentRole`
- `ProjectTaskAssignmentUpdateDto.assignmentRole`

---

## 8.6. ProjectTaskDocumentPurpose

| Name | Value enum |
|---|---:|
| `REPORT` | 0 |
| `REFERENCE` | 1 |

**Dùng cho**
- `ProjectTaskDocumentCreateDto.documentPurpose`
- `ProjectTaskDocumentUpdateDto.documentPurpose`

---

## 8.7. DocumentSourceType

| Name | Value |
|---|---:|
| `Archive` | 0 |
| `Personal` | 1 |
| `Workflow` | 3 |

Dùng khi mobile gọi `Documents` để lọc nguồn văn bản.

---

## 9. Required fields tổng hợp cho mobile

## 9.1. Project

### Create Project
- `code`
- `name`
- `status`

### Update Project
- `code`
- `name`
- `status`
- `concurrencyStamp`

## 9.2. ProjectMember

### Create ProjectMember
- `memberRole`
- `projectId`
- `userId`

### Update ProjectMember
- `memberRole`
- `projectId`
- `userId`
- `concurrencyStamp`

## 9.3. ProjectTask

### Create ProjectTask
- `code`
- `title`
- `priority`
- `status`
- `projectId`

### Update ProjectTask
- `code`
- `title`
- `priority`
- `status`
- `projectId`
- `concurrencyStamp`

## 9.4. ProjectTaskAssignment

### Create ProjectTaskAssignment
- `assignmentRole`
- `projectTaskId`
- `userId`

### Update ProjectTaskAssignment
- `assignmentRole`
- `projectTaskId`
- `userId`
- `concurrencyStamp`

## 9.5. ProjectTaskDocument

### Create ProjectTaskDocument
- `documentPurpose`
- `projectTaskId`
- `documentId`

### Update ProjectTaskDocument
- `documentPurpose`
- `projectTaskId`
- `documentId`
- `concurrencyStamp`

---

## 10. Đề xuất flow mobile triển khai thực tế

## 10.1. Màn Project list

1. Gọi `GET /api/app/projects`.
2. Hiển thị:
   - code, name, status,
   - start/end date,
   - `projectMemberCount`,
   - `projectTaskCount`.
3. Khi filter theo phòng ban: dùng `department-lookup`.

## 10.2. Màn Project detail

1. Gọi `GET /api/app/projects/with-navigation-properties/{id}`.
2. Gọi `GET /api/app/project-members?ProjectId={id}` để load thành viên.
3. Gọi `GET /api/app/project-tasks?ProjectId={id}` để load task.
4. Nếu cần “documents của project”:
   - hiện chỉ có thể đi qua `Documents` tổng quát hoặc qua `ProjectTaskDocuments` của từng task.

## 10.3. Màn tạo/sửa Project

1. Gọi `GET /api/app/projects/department-lookup` nếu cần chọn phòng ban.
2. Submit create/update project.
3. Nếu có tab thành viên đi kèm:
   - gọi `identity-user-lookup`,
   - create `ProjectMembers` sau khi project đã có `id`.

## 10.4. Màn ProjectTask list

1. Gọi `GET /api/app/project-tasks`.
2. Nếu lọc theo project: truyền `ProjectId`.
3. Nếu lọc task của user hiện tại: truyền `UserId`.
4. Nếu chỉ lấy parent task: truyền `OnlyParentTasks=true`.

## 10.5. Màn ProjectTask detail

1. Gọi `GET /api/app/project-tasks/with-navigation-properties/{id}`.
2. Gọi `GET /api/app/project-task-assignments?ProjectTaskId={id}`.
3. Gọi `GET /api/app/project-task-documents?ProjectTaskId={id}`.
4. Khi user bấm xem document:
   - lấy `documentId`,
   - gọi `GET /api/app/documents/{documentId}` hoặc `with-navigation-properties`,
   - gọi `GET /api/app/document-files?DocumentId={documentId}` để lấy file.

## 10.6. Màn tạo task giống web

### Bước 1: tạo task
- gọi `POST /api/app/project-tasks`

### Bước 2: thêm assignee
- gọi `GET /api/app/project-task-assignments/identity-user-lookup`
- gọi `POST /api/app/project-task-assignments`

### Bước 3: thêm document
- gọi `GET /api/app/project-task-documents/document-lookup`
- gọi `POST /api/app/project-task-documents`

> Đây là flow sát web hiện tại nhất: tạo task trước, sau đó mới thêm assignment và document theo `ProjectTaskId` vừa tạo.

---

## 11. Các lưu ý tích hợp quan trọng

1. **Task priority/status đang gửi bằng string**
   - Không gửi số `0/1/2...` cho create/update task; nên gửi `HIGH`, `TODO`... theo enum name.

2. **Update cần `concurrencyStamp`**
   - Khi mobile load detail để edit, phải giữ lại `concurrencyStamp` rồi gửi lại lúc update.

3. **Document file tách khỏi document relation**
   - `ProjectTaskDocuments` chỉ là relation task ↔ document.
   - Muốn lấy file thật, phải gọi thêm `DocumentFiles` theo `DocumentId`.

4. **PDF watermark là flow riêng**
   - Nếu mobile cần xem/tải PDF có watermark, cần dùng service PDF viewer và truyền `blobPath` thực tế.

---

## 12. Checklist API mobile nên implement

### Project
- [ ] List project
- [ ] Get project detail
- [ ] Create project
- [ ] Update project
- [ ] Delete project
- [ ] Department lookup

### Project member / assignment
- [ ] List project members
- [ ] Add member
- [ ] Update member role
- [ ] Remove member
- [ ] Identity user lookup

### Project task
- [ ] List task
- [ ] Get task detail
- [ ] Create task
- [ ] Update task
- [ ] Delete task
- [ ] Project lookup

### Project task assignment
- [ ] List task assignments
- [ ] Add assignment
- [ ] Update assignment
- [ ] Delete assignment
- [ ] Identity user lookup

### Project task document
- [ ] List task documents
- [ ] Add task document
- [ ] Update task document
- [ ] Delete task document
- [ ] Document lookup

### Document/file hỗ trợ
- [ ] Get document detail
- [ ] Get document files by documentId
- [ ] PDF watermark flow (nếu app cần)

---

## 13. Kết luận ngắn gọn cho team mobile

- Muốn **get user**: dùng `identity-user-lookup`.
- Muốn **get document** để gắn task: dùng `project-task-documents/document-lookup`.
- Muốn lấy **file thật của document**: dùng `document-files?DocumentId=...`.
