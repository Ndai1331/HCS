# Mobile Read-Only API Integration

Tài liệu này chỉ giữ các API đọc dữ liệu để mobile hiển thị thông tin cho 2 màn:

1. `Workspace Index`
2. `Calendar Events`

Không bao gồm:

- create
- update
- delete
- mark as read
- các thao tác làm thay đổi dữ liệu

## Quy ước chung

### Authentication

Tất cả API nghiệp vụ cần access token.

```http
Authorization: Bearer <access_token>
Content-Type: application/json
```

### Base URL

Ví dụ local:

- `https://localhost:44379`

### Lưu ý quan trọng

#### Calendar event `RelatedId`

Trong code hiện tại, `RelatedId` ở lịch sự kiện đang được dùng như mã nghiệp vụ:

- project code
- task code

Vì vậy mobile nên ưu tiên:

- dùng `relatedEntityId` để điều hướng/detail nếu có
- dùng `relatedId` để hiển thị mã
---

## 1. Module A - Workspace Index

Trang `Workspace Index` load song song các nhóm dữ liệu:

- projects
- task statistics
- calendar events
- recent documents
- workflow chart
- notifications
- my tasks

### 1.1. Projects list

`GET /api/app/projects`

Mục đích:

- lấy tổng số project
- lấy danh sách project theo khoảng ngày để hiển thị card `Projects`

#### Filter properties dùng trong code

- `MaxResultCount`: số bản ghi lấy ra, đang dùng `200`
- `SkipCount`: offset paging, đang dùng `0`
- `StartDateMin`: ngày bắt đầu nhỏ nhất, gán từ `FilterStartDate.Value.Date`
- `StartDateMax`: ngày bắt đầu lớn nhất, gán từ `FilterEndDate.Value.Date.AddDays(1).AddSeconds(-1)`

#### Query mẫu

```json
{
  "MaxResultCount": 200,
  "SkipCount": 0,
  "StartDateMin": "2026-04-01T00:00:00",
  "StartDateMax": "2026-04-30T23:59:59"
}
```

#### Fields hiển thị

- `items[].project.id`
- `items[].project.code`
- `items[].project.name`
- `items[].project.status`
- `items[].project.creationTime`
- `items[].ownerDepartment.name`
- `items[].projectMemberCount`
- `totalCount`

#### Ghi chú

- UI còn gọi thêm 1 lần `GET /api/app/projects` chỉ với:
- `MaxResultCount = 200`
- `SkipCount = 0`

để lấy `TotalProjectsCount`.

### 1.2. Task statistics

`GET /api/app/project-tasks`

Mục đích:

- lấy task theo khoảng ngày
- client tự group theo trạng thái để vẽ biểu đồ

#### Filter properties dùng trong code

- `MaxResultCount`: đang dùng `200`
- `SkipCount`: đang dùng `0`
- `StartDateMin`: gán từ `FilterStartDate.Value.Date`
- `StartDateMax`: gán từ `FilterEndDate.Value.Date.AddDays(1).AddSeconds(-1)`

#### Query mẫu

```json
{
  "MaxResultCount": 200,
  "SkipCount": 0,
  "StartDateMin": "2026-04-01T00:00:00",
  "StartDateMax": "2026-04-30T23:59:59"
}
```

#### Fields hiển thị

- `items[].projectTask.status`
- `totalCount`

### 1.3. Calendar events block

#### API 1 - Lấy danh sách event

`GET /api/app/calendar-events`

Mục đích:

- lấy event theo khoảng ngày để hiển thị lịch trên dashboard

#### Filter properties dùng trong code

- `MaxResultCount`: đang dùng `200`
- `SkipCount`: đang dùng `0`
- `Sorting`: đang dùng `"StartTime"`
- `StartTimeMin`:
- nếu có `FilterStartDate` thì dùng `FilterStartDate.Value.Date`
- nếu chưa có filter thì mặc định là `DateTime.Now`
- `StartTimeMax`:
- nếu có `FilterEndDate` thì dùng `FilterEndDate.Value.Date.AddDays(1).AddSeconds(-1)`
- nếu chưa có filter thì mặc định là `DateTime.Now.AddDays(7)`

#### Query mẫu khi có filter ngày

```json
{
  "MaxResultCount": 200,
  "SkipCount": 0,
  "Sorting": "StartTime",
  "StartTimeMin": "2026-04-01T00:00:00",
  "StartTimeMax": "2026-04-30T23:59:59"
}
```

#### Fields hiển thị

- `items[].id`
- `items[].title`
- `items[].description`
- `items[].startTime`
- `items[].endTime`
- `items[].relatedType`
- `items[].relatedId`
- `items[].relatedEntityId`
- `items[].relatedName`

#### API 2 - Lấy participant để lọc theo user hiện tại

`GET /api/app/calendar-event-participants`

Mục đích:

- workspace chỉ giữ lại event mà user hiện tại là participant

#### Filter properties dùng trong code

- `IdentityUserId`: `CurrentUser.Id`
- `MaxResultCount`: đang dùng `200`
- `SkipCount`: đang dùng `0`
- `Sorting`: `"CalendarEventParticipant.CreationTime DESC"`

#### Query mẫu

```json
{
  "IdentityUserId": "<current_user_id>",
  "MaxResultCount": 200,
  "SkipCount": 0,
  "Sorting": "CalendarEventParticipant.CreationTime DESC"
}
```

#### Fields hiển thị

- `items[].calendarEvent.id`
- `items[].calendarEventParticipant.responseStatus`

#### API 3 - Lấy chi tiết event

`GET /api/app/calendar-events/{id}`

#### API 4 - Lấy participant của một event

`GET /api/app/calendar-event-participants`

#### Filter properties dùng trong code

- `CalendarEventId`: id event đang xem
- `MaxResultCount`: đang dùng `200`
- `SkipCount`: đang dùng `0`

#### Query mẫu

```json
{
  "CalendarEventId": "<event_id>",
  "MaxResultCount": 200,
  "SkipCount": 0
}
```

### 1.4. Recent documents block

Workspace ghép 2 nguồn:

- document được giao cho user
- personal document

#### API 1 - Assigned documents

`GET /api/app/document-assignments`

#### Filter properties dùng trong code

- `ReceiverUserId`: `CurrentUser.Id`
- `MaxResultCount`: đang dùng `10`
- `SkipCount`: đang dùng `0`
- `Sorting`: `"DocumentAssignment.CreationTime DESC"`
- `AssignedAtMin`: gán từ `FilterStartDate.Value.Date`
- `AssignedAtMax`: gán từ `FilterEndDate.Value.Date.AddDays(1).AddSeconds(-1)`

#### Query mẫu

```json
{
  "ReceiverUserId": "<current_user_id>",
  "MaxResultCount": 10,
  "SkipCount": 0,
  "Sorting": "DocumentAssignment.CreationTime DESC",
  "AssignedAtMin": "2026-04-01T00:00:00",
  "AssignedAtMax": "2026-04-30T23:59:59"
}
```

#### Fields hiển thị

- `items[].document.id`
- `items[].document.title`
- `items[].document.sourceType`
- `items[].documentAssignment.creationTime`

#### Ghi chú

- UI hiện chỉ giữ item có `document.sourceType == Archive`

#### API 2 - Personal documents

`GET /api/app/documents`

#### Filter properties dùng trong code

- `SourceType`: đang dùng `Personal`
- `IncommingDateMin`: gán từ `FilterStartDate`
- `IncommingDateMax`: gán từ `FilterEndDate`
- `MaxResultCount`: đang dùng `10`
- `SkipCount`: đang dùng `0`
- `Sorting`: `"Document.CreationTime DESC"`

#### Query mẫu

```json
{
  "SourceType": "Personal",
  "IncommingDateMin": "2026-04-01T00:00:00",
  "IncommingDateMax": "2026-04-30T23:59:59",
  "MaxResultCount": 10,
  "SkipCount": 0,
  "Sorting": "Document.CreationTime DESC"
}
```

#### Fields hiển thị

- `items[].document.id`
- `items[].document.title`
- `items[].document.creationTime`

#### API 3 - Lấy file của document để xác định PDF

`GET /api/app/document-files`

#### Filter properties dùng trong code

- `DocumentId`: id document đang xem
- `MaxResultCount`: đang dùng `1`
- `SkipCount`: đang dùng `0`

#### Query mẫu

```json
{
  "DocumentId": "<document_id>",
  "MaxResultCount": 1,
  "SkipCount": 0
}
```

#### Fields hiển thị

- `items[].documentFile.id`
- `items[].documentFile.name`
- `items[].documentFile.path`

### 1.5. Notifications block

`GET /api/app/notification-receivers`

Mục đích:

- lấy danh sách thông báo gần nhất của user hiện tại

#### Filter properties dùng trong code

- `IdentityUserId`: `CurrentUser.Id`
- `MaxResultCount`: đang dùng `10`
- `SkipCount`: đang dùng `0`
- `Sorting`: `"NotificationReceiver.CreationTime DESC"`
- `CreationTimeMin`: gán từ `FilterStartDate.Value.Date`
- `CreationTimeMax`: gán từ `FilterEndDate.Value.Date.AddDays(1).AddSeconds(-1)`

#### Query mẫu

```json
{
  "IdentityUserId": "<current_user_id>",
  "MaxResultCount": 10,
  "SkipCount": 0,
  "Sorting": "NotificationReceiver.CreationTime DESC",
  "CreationTimeMin": "2026-04-01T00:00:00",
  "CreationTimeMax": "2026-04-30T23:59:59"
}
```

#### Fields hiển thị

- `items[].notificationReceiver.id`
- `items[].notificationReceiver.isRead`
- `items[].notificationReceiver.creationTime`
- `items[].notification.title`
- `items[].notification.content`
- `items[].notification.relatedType`
- `items[].notification.relatedId`

### 1.6. Workflow chart block

`GET /api/app/document-workflow-instance-logss/workflow-chart-statistics`

Mục đích:

- lấy số liệu cho biểu đồ workflow

#### Filter properties dùng trong code

- `fromDate`: `FilterStartDate`
- `toDate`: `FilterEndDate`

#### Query mẫu

```http
GET /api/app/document-workflow-instance-logss/workflow-chart-statistics?fromDate=2026-04-01T00:00:00&toDate=2026-04-30T00:00:00
```

#### Response fields

- `signedCount`
- `sentCount`
- `returnedOrRejectedCount`
- `totalCount`

### 1.7. My Tasks block

`GET /api/app/project-tasks`

Mục đích:

- lấy danh sách task để hiển thị block `My Tasks`

#### Filter properties dùng trong code

- `MaxResultCount`: đang dùng `200`
- `SkipCount`: đang dùng `0`
- `Sorting`: `"ProjectTask.StartDate DESC"`
- `StartDateMin`: gán từ `FilterStartDate.Value.Date`
- `StartDateMax`: gán từ `FilterEndDate.Value.Date.AddDays(1).AddSeconds(-1)`

#### Query mẫu

```json
{
  "MaxResultCount": 200,
  "SkipCount": 0,
  "Sorting": "ProjectTask.StartDate DESC",
  "StartDateMin": "2026-04-01T00:00:00",
  "StartDateMax": "2026-04-30T23:59:59"
}
```

#### Fields hiển thị

- `items[].projectTask.id`
- `items[].projectTask.code`
- `items[].projectTask.title`
- `items[].projectTask.description`
- `items[].projectTask.startDate`
- `items[].projectTask.dueDate`
- `items[].projectTask.status`
- `items[].projectTask.priority`
- `items[].projectTask.progressPercent`

#### API detail task

`GET /api/app/project-tasks/with-navigation-properties/{id}`

### 1.8. Project detail và members

#### API 1 - Project detail

`GET /api/app/projects/{id}`

Hoặc với navigation đầy đủ:

`GET /api/app/projects/with-navigation-properties/{id}`

#### API 2 - Project members

`GET /api/app/project-members`

#### Filter properties dùng trong code

- `ProjectId`: id project
- `MaxResultCount`: đang dùng `200`
- `SkipCount`: đang dùng `0`

#### Query mẫu

```json
{
  "ProjectId": "<project_id>",
  "MaxResultCount": 200,
  "SkipCount": 0
}
```

#### API 3 - Tasks của project

`GET /api/app/project-tasks`

#### Filter properties dùng trong code

- `ProjectId`: id project
- `MaxResultCount`: đang dùng `200`
- `SkipCount`: đang dùng `0`

#### Query mẫu

```json
{
  "ProjectId": "<project_id>",
  "MaxResultCount": 200,
  "SkipCount": 0
}
```

### 1.9. Project chat entry

#### API - Tìm conversation theo project

`GET /api/chat/conversation/project/{projectId}`

Mục đích:

- kiểm tra project đã có conversation hay chưa
- lấy `conversation.id` để điều hướng sang màn chat

---

## 2. Module B - Calendar Events

Trang `Calendar Events` có 2 chế độ:

1. list view
2. calendar view

### 2.1. Calendar events list

`GET /api/app/calendar-events`

#### List view - filter properties dùng trong code

- `MaxResultCount`: `PageSize`
- `SkipCount`: `(CurrentPage - 1) * PageSize`
- `Sorting`: `CurrentSorting`
- `FilterText`
- `Title`
- `Description`
- `StartTimeMin`
- `StartTimeMax`
- `EndTimeMin`
- `EndTimeMax`
- `AllDay`
- `EventType`
- `Location`
- `RelatedType`
- `RelatedId`
- `Visibility`

#### Query mẫu list view

```json
{
  "MaxResultCount": 10,
  "SkipCount": 0,
  "Sorting": "StartTime DESC",
  "FilterText": "keyword",
  "Title": "Họp giao ban",
  "Description": "Q2",
  "StartTimeMin": "2026-04-01T00:00:00",
  "StartTimeMax": "2026-04-30T23:59:59",
  "EndTimeMin": "2026-04-01T00:00:00",
  "EndTimeMax": "2026-04-30T23:59:59",
  "AllDay": false,
  "EventType": "MEETING",
  "Location": "Room A",
  "RelatedType": "PROJECT",
  "RelatedId": "P0000001",
  "Visibility": "PRIVATE"
}
```

#### Calendar view - filter properties dùng trong code

- `MaxResultCount`: đang dùng `200`
- `SkipCount`: đang dùng `0`
- `Sorting`: `CurrentSorting`
- `StartTimeMax`: mặc định bằng ngày cuối visible range của calendar
- `EndTimeMin`: mặc định bằng ngày đầu visible range của calendar
- nếu có filter từ UI thì gán thêm:
- `FilterText`
- `Title`
- `Description`
- `AllDay`
- `EventType`
- `Location`
- `RelatedType`
- `RelatedId`
- `Visibility`
- `StartTimeMin`
- `EndTimeMax`
- `StartTimeMax`: lấy giá trị nhỏ hơn giữa filter user và visible range
- `EndTimeMin`: lấy giá trị lớn hơn giữa filter user và visible range

#### Query mẫu calendar view

```json
{
  "MaxResultCount": 200,
  "SkipCount": 0,
  "Sorting": "",
  "StartTimeMax": "2026-04-30T23:59:59",
  "EndTimeMin": "2026-04-01T00:00:00",
  "FilterText": "keyword",
  "Title": "Họp giao ban",
  "Description": "Q2",
  "AllDay": false,
  "EventType": "MEETING",
  "Location": "Room A",
  "RelatedType": "PROJECT",
  "RelatedId": "P0000001",
  "Visibility": "PRIVATE",
  "StartTimeMin": "2026-04-01T00:00:00",
  "EndTimeMax": "2026-04-30T23:59:59"
}
```

#### Fields hiển thị

- `items[].id`
- `items[].title`
- `items[].description`
- `items[].startTime`
- `items[].endTime`
- `items[].allDay`
- `items[].eventType`
- `items[].location`
- `items[].relatedType`
- `items[].relatedId`
- `items[].relatedEntityId`
- `items[].relatedName`
- `items[].visibility`

### 2.2. Participant counts theo batch

`POST /api/app/calendar-event-participants/participant-counts-by-calendar-event-ids`

Đây là API read-only dù method là `POST`.

#### Body properties dùng trong code

- `CalendarEventIds`: list id của các event đang hiển thị

#### Body mẫu

```json
{
  "calendarEventIds": [
    "00000000-0000-0000-0000-000000000001",
    "00000000-0000-0000-0000-000000000002"
  ]
}
```

#### Response fields

- `calendarEventId`
- `count`

### 2.3. Event detail

`GET /api/app/calendar-events/{id}`

Mục đích:

- mở event detail modal
- click event trên calendar nếu không điều hướng sang project/task

### 2.4. Event participants

`GET /api/app/calendar-event-participants`

#### Filter properties dùng trong code

- `CalendarEventId`: id event đang xem
- `MaxResultCount`: đang dùng `200`
- `SkipCount`: đang dùng `0`

#### Query mẫu

```json
{
  "CalendarEventId": "<event_id>",
  "MaxResultCount": 200,
  "SkipCount": 0
}
```

#### Fields hiển thị

- `items[].calendarEventParticipant.id`
- `items[].calendarEventParticipant.identityUserId`
- `items[].calendarEventParticipant.responseStatus`
- `items[].identityUser.name`
- `items[].identityUser.surname`
- `items[].identityUser.userName`

### 2.5. Participant lookup

`GET /api/app/calendar-event-participants/identity-user-lookup`

#### Filter properties dùng trong code

- `Filter`: keyword tìm user
- `MaxResultCount`: đang dùng `20`
- `SkipCount`: đang dùng `0`

#### Query mẫu

```http
GET /api/app/calendar-event-participants/identity-user-lookup?Filter=<keyword>&MaxResultCount=20&SkipCount=0
```

### 2.6. Project lookup trong filter

`GET /api/app/projects`

#### Filter properties dùng trong code

- `FilterText`: keyword nhập vào select
- `MaxResultCount`: đang dùng `20`
- `SkipCount`: đang dùng `0`

#### Query mẫu

```json
{
  "FilterText": "P0000123",
  "MaxResultCount": 20,
  "SkipCount": 0
}
```

#### Dữ liệu UI map để hiển thị

- `Id = project.Code`
- `DisplayName = "{Code} - {Name}"`

### 2.7. Task lookup trong filter

`GET /api/app/project-tasks`

#### Filter properties dùng trong code

- `FilterText`: keyword nhập vào select
- `MaxResultCount`: đang dùng `20`
- `SkipCount`: đang dùng `0`

#### Query mẫu

```json
{
  "FilterText": "T0000456",
  "MaxResultCount": 20,
  "SkipCount": 0
}
```

#### Dữ liệu UI map để hiển thị

- `Id = projectTask.Code`
- `DisplayName = "{Code} - {Title}"`

### 2.8. Điều hướng sang project/task liên quan

#### Nếu event liên quan project

1. `GET /api/app/projects`
2. `GET /api/app/projects/with-navigation-properties/{id}`
3. `GET /api/app/project-members`
4. `GET /api/app/project-tasks`

##### Filter properties dùng trong code

- Bước 1:
- `FilterText = relatedId`
- `MaxResultCount = 1`
- `SkipCount = 0`
- Bước 3:
- `ProjectId = projectId`
- `MaxResultCount = 200`
- `SkipCount = 0`
- Bước 4:
- `ProjectId = projectId`
- `MaxResultCount = 200`
- `SkipCount = 0`

#### Nếu event liên quan task

1. `GET /api/app/project-tasks`
2. `GET /api/app/project-tasks/with-navigation-properties/{id}`

##### Filter properties dùng trong code

- Bước 1:
- `FilterText = relatedId`
- `MaxResultCount = 1`
- `SkipCount = 0`

---

## 3. Danh sách endpoint cuối cùng cho mobile

### 3.1. Workspace Index

- `GET /api/app/projects`
- `GET /api/app/projects/{id}`
- `GET /api/app/projects/with-navigation-properties/{id}`
- `GET /api/app/project-members`
- `GET /api/app/project-tasks`
- `GET /api/app/project-tasks/with-navigation-properties/{id}`
- `GET /api/app/calendar-events`
- `GET /api/app/calendar-events/{id}`
- `GET /api/app/calendar-event-participants`
- `GET /api/app/document-assignments`
- `GET /api/app/documents`
- `GET /api/app/document-files`
- `GET /api/app/notification-receivers`
- `GET /api/app/document-workflow-instance-logss/workflow-chart-statistics`
- `GET /api/chat/conversation/project/{projectId}`

### 3.2. Calendar Events

- `GET /api/app/calendar-events`
- `GET /api/app/calendar-events/{id}`
- `GET /api/app/calendar-event-participants`
- `GET /api/app/calendar-event-participants/identity-user-lookup`
- `POST /api/app/calendar-event-participants/participant-counts-by-calendar-event-ids`
- `GET /api/app/projects`
- `GET /api/app/projects/with-navigation-properties/{id}`
- `GET /api/app/project-members`
- `GET /api/app/project-tasks`
- `GET /api/app/project-tasks/with-navigation-properties/{id}`
## 1. Module A - API cho trang Workspace Index

Trang `Workspace Index` đang hiển thị các block:

- calendar
- notifications
- workflow chart
- documents
- projects
- my tasks
- project chat entry

## 1.1. Date filter dùng chung

Khi mở trang hoặc đổi khoảng ngày lọc, UI reload dữ liệu theo khoảng:

- `FilterStartDate`
- `FilterEndDate`

Ví dụ:

```json
{
  "from": "2026-04-01T00:00:00",
  "to": "2026-04-30T23:59:59"
}
```

## 1.2. Calendar block

### Lấy danh sách event

`GET /api/app/calendar-events`

Mục đích:

- lấy event theo khoảng ngày để hiển thị block lịch ở dashboard

Query điển hình:

```json
{
  "MaxResultCount": 200,
  "SkipCount": 0,
  "Sorting": "StartTime",
  "StartTimeMin": "2026-04-01T00:00:00",
  "StartTimeMax": "2026-04-30T23:59:59"
}
```

Field mobile nên dùng:

- `id`
- `title`
- `description`
- `startTime`
- `endTime`
- `relatedType`
- `relatedId`
- `relatedEntityId`
- `relatedName`

### Lấy participant để lọc event theo user hiện tại

`GET /api/app/calendar-event-participants`

Mục đích:

- workspace chỉ hiển thị các event mà user hiện tại là participant

Query điển hình:

```json
{
  "IdentityUserId": "<current_user_id>",
  "MaxResultCount": 200,
  "SkipCount": 0,
  "Sorting": "CalendarEventParticipant.CreationTime DESC"
}
```

Client flow hiện tại:

1. Gọi `GET /api/app/calendar-events`
2. Gọi `GET /api/app/calendar-event-participants?IdentityUserId=...`
3. Lấy `calendarEvent.id`
4. Filter danh sách event ở client

### Lấy chi tiết event khi bấm xem

`GET /api/app/calendar-events/{id}`

### Lấy participant của 1 event khi mở detail

`GET /api/app/calendar-event-participants`

Query:

```json
{
  "CalendarEventId": "<event_id>",
  "MaxResultCount": 200,
  "SkipCount": 0
}
```

## 1.3. Notifications block

### Lấy danh sách notification receiver của user hiện tại

`GET /api/app/notification-receivers`

Query điển hình:

```json
{
  "IdentityUserId": "<current_user_id>",
  "MaxResultCount": 10,
  "SkipCount": 0,
  "Sorting": "NotificationReceiver.CreationTime DESC",
  "CreationTimeMin": "2026-04-01T00:00:00",
  "CreationTimeMax": "2026-04-30T23:59:59"
}
```

Field mobile nên dùng:

- `items[].notificationReceiver.id`
- `items[].notificationReceiver.isRead`
- `items[].notificationReceiver.creationTime`
- `items[].notification.title`
- `items[].notification.content`
- `items[].notification.relatedType`
- `items[].notification.relatedId`

## 1.4. Workflow chart block

### Lấy thống kê workflow chart

`GET /api/app/document-workflow-instance-logss/workflow-chart-statistics`

Query:

```http
GET /api/app/document-workflow-instance-logss/workflow-chart-statistics?fromDate=2026-04-01T00:00:00&toDate=2026-04-30T00:00:00
```

Response:

```json
{
  "signedCount": 0,
  "sentCount": 0,
  "returnedOrRejectedCount": 0,
  "totalCount": 0
}
```

## 1.5. Documents block

Workspace đang ghép 2 nguồn dữ liệu:

- document được giao cho user
- personal document

### Lấy document assignment của user

`GET /api/app/document-assignments`

Query điển hình:

```json
{
  "ReceiverUserId": "<current_user_id>",
  "MaxResultCount": 10,
  "SkipCount": 0,
  "Sorting": "DocumentAssignment.CreationTime DESC",
  "AssignedAtMin": "2026-04-01T00:00:00",
  "AssignedAtMax": "2026-04-30T23:59:59"
}
```

Field mobile nên dùng:

- `items[].document.id`
- `items[].document.title`
- `items[].document.sourceType`
- `items[].documentAssignment.creationTime`

Ghi chú:

- UI hiện chỉ lấy các item có `Document.SourceType == Archive`

### Lấy personal document

`GET /api/app/documents`

Query điển hình:

```json
{
  "SourceType": "Personal",
  "IncommingDateMin": "2026-04-01T00:00:00",
  "IncommingDateMax": "2026-04-30T23:59:59",
  "MaxResultCount": 10,
  "SkipCount": 0,
  "Sorting": "Document.CreationTime DESC"
}
```

Field mobile nên dùng:

- `items[].document.id`
- `items[].document.title`
- `items[].document.creationTime`

### Lấy file của document để xác định PDF

`GET /api/app/document-files`

Query:

```json
{
  "DocumentId": "<document_id>",
  "MaxResultCount": 1,
  "SkipCount": 0
}
```

Field mobile nên dùng:

- `items[].documentFile.id`
- `items[].documentFile.name`
- `items[].documentFile.path`

### Xem PDF watermark

UI hiện dùng app service:

- `DocumentPdfViewerAppService.GetWatermarkedPdfAsync`

Input:

```json
{
  "blobPath": "documents/2026/04/file.pdf",
  "watermarkAction": "view"
}
```

Nhưng route HTTP cần xác nhận lại trên Swagger trước khi mobile dùng.

## 1.6. Projects block

### Lấy danh sách project

`GET /api/app/projects`

UI hiện gọi endpoint này 2 lần:

- 1 lần không filter để lấy tổng số
- 1 lần có filter ngày để lấy danh sách hiển thị

Query có filter ngày:

```json
{
  "MaxResultCount": 200,
  "SkipCount": 0,
  "StartDateMin": "2026-04-01T00:00:00",
  "StartDateMax": "2026-04-30T23:59:59"
}
```

Field mobile nên dùng:

- `items[].project.id`
- `items[].project.code`
- `items[].project.name`
- `items[].project.status`
- `items[].project.creationTime`
- `items[].ownerDepartment.name`
- `items[].projectMemberCount`
- `totalCount`

### Lấy chi tiết project

`GET /api/app/projects/{id}`

Ở page lịch có chỗ dùng bản đầy đủ:

`GET /api/app/projects/with-navigation-properties/{id}`

### Lấy danh sách thành viên project

`GET /api/app/project-members`

Query:

```json
{
  "ProjectId": "<project_id>",
  "MaxResultCount": 200,
  "SkipCount": 0
}
```

### Lấy task thuộc project

`GET /api/app/project-tasks`

Query:

```json
{
  "ProjectId": "<project_id>",
  "MaxResultCount": 200,
  "SkipCount": 0
}
```

## 1.7. My Tasks block

### Lấy danh sách task

`GET /api/app/project-tasks`

Query điển hình:

```json
{
  "MaxResultCount": 200,
  "SkipCount": 0,
  "Sorting": "ProjectTask.StartDate DESC",
  "StartDateMin": "2026-04-01T00:00:00",
  "StartDateMax": "2026-04-30T23:59:59"
}
```

Field mobile nên dùng:

- `items[].projectTask.id`
- `items[].projectTask.code`
- `items[].projectTask.title`
- `items[].projectTask.description`
- `items[].projectTask.startDate`
- `items[].projectTask.dueDate`
- `items[].projectTask.status`
- `items[].projectTask.priority`
- `items[].projectTask.progressPercent`

### Lấy chi tiết task

`GET /api/app/project-tasks/with-navigation-properties/{id}`

## 1.8. Project chat entry

Trang workspace có nút chat ở project card.

Phần đọc dữ liệu đang dùng:

### Tìm conversation theo project

`GET /api/chat/conversation/project/{projectId}`

Mục đích:

- kiểm tra project đã có conversation hay chưa
- lấy `conversation.id` để điều hướng sang màn chat

### Lấy thành viên project để build danh sách chat member

`GET /api/app/project-members`

Query:

```json
{
  "ProjectId": "<project_id>",
  "MaxResultCount": 100,
  "SkipCount": 0
}
```

## 1.9. Tóm tắt nhanh theo block

| Block UI | API đọc dữ liệu |
|---|---|
| Calendar | `GET /api/app/calendar-events`, `GET /api/app/calendar-event-participants`, `GET /api/app/calendar-events/{id}` |
| Notifications | `GET /api/app/notification-receivers` |
| Workflow chart | `GET /api/app/document-workflow-instance-logss/workflow-chart-statistics` |
| Documents | `GET /api/app/document-assignments`, `GET /api/app/documents`, `GET /api/app/document-files` |
| Projects | `GET /api/app/projects`, `GET /api/app/projects/{id}`, `GET /api/app/projects/with-navigation-properties/{id}`, `GET /api/app/project-members` |
| My Tasks | `GET /api/app/project-tasks`, `GET /api/app/project-tasks/with-navigation-properties/{id}` |
| Project Chat | `GET /api/chat/conversation/project/{projectId}` |

---

## 2. Module B - API cho trang Calendar Events

Trang `Calendar Events` có 2 chế độ:

- calendar view
- list view

Trong tài liệu này chỉ giữ các API đọc dữ liệu để hiển thị.

## 2.1. Lấy danh sách calendar events

`GET /api/app/calendar-events`

### List view

List view dùng paging + sorting + filter đầy đủ.

Query mẫu:

```json
{
  "MaxResultCount": 10,
  "SkipCount": 0,
  "Sorting": "startTime DESC",
  "FilterText": "keyword",
  "Title": "Họp giao ban",
  "Description": "Q2",
  "StartTimeMin": "2026-04-01T00:00:00",
  "StartTimeMax": "2026-04-30T23:59:59",
  "EndTimeMin": "2026-04-01T00:00:00",
  "EndTimeMax": "2026-04-30T23:59:59",
  "AllDay": false,
  "EventType": "MEETING",
  "Location": "Room A",
  "RelatedType": "PROJECT",
  "RelatedId": "P0000001",
  "Visibility": "PRIVATE"
}
```

### Calendar view

Calendar view load theo visible range hiện tại của FullCalendar.

Query mẫu:

```json
{
  "MaxResultCount": 200,
  "SkipCount": 0,
  "Sorting": "",
  "StartTimeMax": "2026-04-30T23:59:59",
  "EndTimeMin": "2026-04-01T00:00:00",
  "FilterText": "keyword",
  "Title": "Họp giao ban",
  "Description": "Q2",
  "AllDay": false,
  "EventType": "MEETING",
  "Location": "Room A",
  "RelatedType": "PROJECT",
  "RelatedId": "P0000001",
  "Visibility": "PRIVATE",
  "StartTimeMin": "2026-04-01T00:00:00",
  "EndTimeMax": "2026-04-30T23:59:59"
}
```

Field mobile nên dùng:

- `id`
- `title`
- `description`
- `startTime`
- `endTime`
- `allDay`
- `eventType`
- `location`
- `relatedType`
- `relatedId`
- `relatedEntityId`
- `relatedName`
- `visibility`

## 2.2. Lấy số participant theo batch

Đây là API đọc dữ liệu, nhưng method là `POST`.

`POST /api/app/calendar-event-participants/participant-counts-by-calendar-event-ids`

Body:

```json
{
  "calendarEventIds": [
    "00000000-0000-0000-0000-000000000001",
    "00000000-0000-0000-0000-000000000002"
  ]
}
```

Response:

```json
[
  {
    "calendarEventId": "00000000-0000-0000-0000-000000000001",
    "count": 5
  }
]
```

Mục đích:

- list page pre-load số participant của nhiều event cùng lúc

## 2.3. Lấy chi tiết event

`GET /api/app/calendar-events/{id}`

Dùng khi:

- mở event detail modal
- click event trên calendar nếu không điều hướng sang project/task

## 2.4. Lấy participant của event

`GET /api/app/calendar-event-participants`

Query:

```json
{
  "CalendarEventId": "<event_id>",
  "MaxResultCount": 200,
  "SkipCount": 0
}
```

Field mobile nên dùng:

- `items[].calendarEventParticipant.id`
- `items[].calendarEventParticipant.identityUserId`
- `items[].calendarEventParticipant.responseStatus`
- `items[].identityUser.name`
- `items[].identityUser.surname`
- `items[].identityUser.userName`

## 2.5. Lookup user để hiển thị/chọn participant

`GET /api/app/calendar-event-participants/identity-user-lookup`

Query:

```http
GET /api/app/calendar-event-participants/identity-user-lookup?Filter=<keyword>&MaxResultCount=20&SkipCount=0
```

## 2.6. Lookup project/task liên quan trong filter

### Lookup project

`GET /api/app/projects`

Query:

```json
{
  "FilterText": "P0000123",
  "MaxResultCount": 20,
  "SkipCount": 0
}
```

UI map thành:

- `Id = project.Code`
- `DisplayName = "{Code} - {Name}"`

### Lookup task

`GET /api/app/project-tasks`

Query:

```json
{
  "FilterText": "T0000456",
  "MaxResultCount": 20,
  "SkipCount": 0
}
```

UI map thành:

- `Id = projectTask.Code`
- `DisplayName = "{Code} - {Title}"`

## 2.7. Điều hướng từ event sang entity liên quan

Khi click event, page có thể mở project detail hoặc task detail thay vì chỉ mở event detail.

### Nếu event liên quan project

1. `GET /api/app/projects?FilterText=<relatedId>&MaxResultCount=1&SkipCount=0`
2. `GET /api/app/projects/with-navigation-properties/{id}`
3. `GET /api/app/project-members?ProjectId=<id>&MaxResultCount=200&SkipCount=0`
4. `GET /api/app/project-tasks?ProjectId=<id>&MaxResultCount=200&SkipCount=0`

### Nếu event liên quan task

1. `GET /api/app/project-tasks?FilterText=<relatedId>&MaxResultCount=1&SkipCount=0`
2. `GET /api/app/project-tasks/with-navigation-properties/{id}`

## 2.8. Tóm tắt endpoint read-only

- `GET /api/app/calendar-events`
- `GET /api/app/calendar-events/{id}`
- `GET /api/app/calendar-event-participants`
- `GET /api/app/calendar-event-participants/identity-user-lookup`
- `POST /api/app/calendar-event-participants/participant-counts-by-calendar-event-ids`
- `GET /api/app/projects`
- `GET /api/app/projects/with-navigation-properties/{id}`
- `GET /api/app/project-members`
- `GET /api/app/project-tasks`
- `GET /api/app/project-tasks/with-navigation-properties/{id}`

---

## 3. Danh sách endpoint cuối cùng cho mobile

## 3.1. Workspace Index

- `GET /api/app/calendar-events`
- `GET /api/app/calendar-events/{id}`
- `GET /api/app/calendar-event-participants`
- `GET /api/app/notification-receivers`
- `GET /api/app/document-workflow-instance-logss/workflow-chart-statistics`
- `GET /api/app/document-assignments`
- `GET /api/app/documents`
- `GET /api/app/document-files`
- `GET /api/app/projects`
- `GET /api/app/projects/{id}`
- `GET /api/app/projects/with-navigation-properties/{id}`
- `GET /api/app/project-members`
- `GET /api/app/project-tasks`
- `GET /api/app/project-tasks/with-navigation-properties/{id}`
- `GET /api/chat/conversation/project/{projectId}`

## 3.2. Calendar Events

- `GET /api/app/calendar-events`
- `GET /api/app/calendar-events/{id}`
- `GET /api/app/calendar-event-participants`
- `GET /api/app/calendar-event-participants/identity-user-lookup`
- `POST /api/app/calendar-event-participants/participant-counts-by-calendar-event-ids`
- `GET /api/app/projects`
- `GET /api/app/projects/with-navigation-properties/{id}`
- `GET /api/app/project-members`
- `GET /api/app/project-tasks`
- `GET /api/app/project-tasks/with-navigation-properties/{id}`

## 1. Module A - API cho trang Workspace Index

Trang `Workspace Index` đang là một dashboard ghép dữ liệu từ nhiều module nhỏ: lịch sự kiện, thông báo, workflow chart, tài liệu, dự án, công việc cá nhân.

### 1.1. Luồng tải dữ liệu ban đầu của dashboard

Khi mở trang hoặc đổi khoảng ngày lọc, UI gọi song song các API sau.

#### 1.1.1. Danh sách dự án

`GET /api/app/projects`

Mục đích:

- Lấy tổng số project
- Lấy danh sách project theo khoảng ngày để render card `Projects`

Query dùng trong code:

```json
{
  "MaxResultCount": 200,
  "SkipCount": 0,
  "StartDateMin": "2026-04-01T00:00:00",
  "StartDateMax": "2026-04-30T23:59:59"
}
```

Trường mobile cần để hiển thị:

- `items[].project.id`
- `items[].project.code`
- `items[].project.name`
- `items[].project.status`
- `items[].project.creationTime`
- `items[].ownerDepartment.name`
- `totalCount`

Ghi chú:

- UI hiện gọi 2 lần endpoint này:
- 1 lần không filter để lấy `TotalProjectsCount`
- 1 lần có filter ngày để lấy `ActiveProjectsList`

#### 1.1.2. Thống kê task theo trạng thái

`GET /api/app/project-tasks`

Mục đích:

- Lấy toàn bộ task theo khoảng ngày
- Tự group ở client theo `ProjectTask.Status` để vẽ biểu đồ

Query dùng trong code:

```json
{
  "MaxResultCount": 200,
  "SkipCount": 0,
  "StartDateMin": "2026-04-01T00:00:00",
  "StartDateMax": "2026-04-30T23:59:59"
}
```

Trường mobile cần:

- `items[].projectTask.status`
- `totalCount`

#### 1.1.3. Danh sách calendar event cho user hiện tại

`GET /api/app/calendar-events`

Mục đích:

- Lấy event theo khoảng ngày lọc
- Dùng để render block `Calendar`

Query dùng trong code:

```json
{
  "MaxResultCount": 200,
  "SkipCount": 0,
  "Sorting": "StartTime",
  "StartTimeMin": "2026-04-01T00:00:00",
  "StartTimeMax": "2026-04-30T23:59:59"
}
```

Trường mobile cần:

- `id`
- `title`
- `description`
- `startTime`
- `endTime`
- `relatedType`
- `relatedId`

#### 1.1.4. Lọc event theo participant là user hiện tại

`GET /api/app/calendar-event-participants`

Mục đích:

- Trang workspace không hiển thị toàn bộ event
- Chỉ giữ lại event mà user hiện tại là participant

Query dùng trong code:

```json
{
  "IdentityUserId": "<current_user_id>",
  "MaxResultCount": 200,
  "SkipCount": 0,
  "Sorting": "CalendarEventParticipant.CreationTime DESC"
}
```

Trường mobile cần:

- `items[].calendarEvent.id`

Luồng client:

1. Gọi `GET /api/app/calendar-events`
2. Gọi `GET /api/app/calendar-event-participants?IdentityUserId=...`
3. Lấy `calendarEvent.id` từ participant list
4. Filter lại danh sách events theo tập `eventIds`

#### 1.1.5. Danh sách tài liệu được giao cho user

`GET /api/app/document-assignments`

Mục đích:

- Lấy các document assignment dành cho user hiện tại
- Workspace chỉ lấy document có `Document.SourceType == Archive`

Query dùng trong code:

```json
{
  "ReceiverUserId": "<current_user_id>",
  "MaxResultCount": 10,
  "SkipCount": 0,
  "Sorting": "DocumentAssignment.CreationTime DESC",
  "AssignedAtMin": "2026-04-01T00:00:00",
  "AssignedAtMax": "2026-04-30T23:59:59"
}
```

Trường mobile cần:

- `items[].document.id`
- `items[].document.title`
- `items[].document.sourceType`
- `items[].documentAssignment.creationTime`

#### 1.1.6. Danh sách personal document của user

`GET /api/app/documents`

Mục đích:

- Lấy document cá nhân
- Gộp với danh sách assignment ở trên để render block `Documents`

Query dùng trong code:

```json
{
  "SourceType": "Personal",
  "IncommingDateMin": "2026-04-01T00:00:00",
  "IncommingDateMax": "2026-04-30T23:59:59",
  "MaxResultCount": 10,
  "SkipCount": 0,
  "Sorting": "Document.CreationTime DESC"
}
```

Trường mobile cần:

- `items[].document.id`
- `items[].document.title`
- `items[].document.creationTime`

#### 1.1.7. Workflow chart statistics

`GET /api/app/document-workflow-instance-logss/workflow-chart-statistics`

Mục đích:

- Vẽ biểu đồ workflow dạng doughnut

Query:

```http
GET /api/app/document-workflow-instance-logss/workflow-chart-statistics?fromDate=2026-04-01T00:00:00&toDate=2026-04-30T00:00:00
```

Response:

```json
{
  "signedCount": 0,
  "sentCount": 0,
  "returnedOrRejectedCount": 0,
  "totalCount": 0
}
```

#### 1.1.8. Notifications của user hiện tại

`GET /api/app/notification-receivers`

Mục đích:

- Render block `Notifications`

Query dùng trong code:

```json
{
  "IdentityUserId": "<current_user_id>",
  "MaxResultCount": 10,
  "SkipCount": 0,
  "Sorting": "NotificationReceiver.CreationTime DESC",
  "CreationTimeMin": "2026-04-01T00:00:00",
  "CreationTimeMax": "2026-04-30T23:59:59"
}
```

Trường mobile cần:

- `items[].notificationReceiver.id`
- `items[].notificationReceiver.isRead`
- `items[].notificationReceiver.creationTime`
- `items[].notification.title`
- `items[].notification.content`
- `items[].notification.relatedType`
- `items[].notification.relatedId`

#### 1.1.9. Danh sách task cá nhân

`GET /api/app/project-tasks`

Mục đích:

- Render block `My Tasks`

Query dùng trong code:

```json
{
  "MaxResultCount": 200,
  "SkipCount": 0,
  "Sorting": "ProjectTask.StartDate DESC",
  "StartDateMin": "2026-04-01T00:00:00",
  "StartDateMax": "2026-04-30T23:59:59"
}
```

Trường mobile cần:

- `items[].projectTask.id`
- `items[].projectTask.code`
- `items[].projectTask.title`
- `items[].projectTask.description`
- `items[].projectTask.startDate`
- `items[].projectTask.dueDate`
- `items[].projectTask.status`
- `items[].projectTask.priority`
- `items[].projectTask.progressPercent`

### 1.2. API thao tác từ Workspace Index

#### 1.2.1. Tạo project mới

##### Lấy department lookup

`GET /api/app/projects/department-lookup`

Query:

```http
GET /api/app/projects/department-lookup?Filter=<keyword>
```

##### Tạo project

`POST /api/app/projects`

Body:

```json
{
  "code": "P0000123",
  "name": "Triển khai mobile app",
  "description": "Mô tả dự án",
  "startDate": "2026-04-11T08:00:00",
  "endDate": "2026-05-30T17:00:00",
  "status": "PLANNING",
  "ownerDepartmentId": "00000000-0000-0000-0000-000000000000"
}
```

Ghi chú:

- UI hiện tự sinh `code` ở client bằng cách scan danh sách project hiện có
- Nếu mobile cũng tạo project, nên ưu tiên backend cấp mã tự động để tránh race condition

#### 1.2.2. Xem chi tiết project từ dashboard

##### Lấy chi tiết project

`GET /api/app/projects/{id}`

Hoặc ở page lịch dùng endpoint đầy đủ navigation:

`GET /api/app/projects/with-navigation-properties/{id}`

##### Lấy thành viên project

`GET /api/app/project-members`

Query:

```json
{
  "ProjectId": "<project_id>",
  "MaxResultCount": 200,
  "SkipCount": 0
}
```

##### Lấy task thuộc project

`GET /api/app/project-tasks`

Query:

```json
{
  "ProjectId": "<project_id>",
  "MaxResultCount": 200,
  "SkipCount": 0
}
```

#### 1.2.3. Xem chi tiết task

`GET /api/app/project-tasks/with-navigation-properties/{id}`

Mục đích:

- Mở modal task detail từ dashboard

#### 1.2.4. Xem PDF document từ dashboard

##### Bước 1: lấy file của document

`GET /api/app/document-files`

Query:

```json
{
  "DocumentId": "<document_id>",
  "MaxResultCount": 1,
  "SkipCount": 0
}
```

##### Bước 2: lấy PDF đã đóng watermark

Trong Blazor hiện gọi:

- `DocumentPdfViewerAppService.GetWatermarkedPdfAsync`

Input:

```json
{
  "blobPath": "documents/2026/04/file.pdf",
  "watermarkAction": "view"
}
```

Ghi chú quan trọng:

- Trong source hiện chưa thấy controller HTTP khai báo tường minh cho module `DocumentPdfViewer` như các module khác
- Nếu backend đang publish API này bằng ABP conventional controller thì cần xác nhận chính xác route trên Swagger trước khi mobile tích hợp
- Nếu backend không public route HTTP, mobile sẽ cần một endpoint riêng để xem PDF watermark

#### 1.2.5. Đánh dấu notification đã đọc

Trang workspace hiện đang dùng:

`PUT /api/app/notification-receivers/{id}`

Body cần gửi gần như full object update:

```json
{
  "isRead": true,
  "readAt": "2026-04-11T10:15:00Z",
  "notificationId": "00000000-0000-0000-0000-000000000000",
  "identityUserId": "00000000-0000-0000-0000-000000000000",
  "concurrencyStamp": "..."
}
```

Ghi chú:

- Controller cũng có API chuyên dụng:
- `POST /api/app/notification-receivers/mark-as-read?notificationId=<notification_id>`
- `POST /api/app/notification-receivers/mark-all-as-read`
- Với mobile, nên ưu tiên xác nhận lại backend xem có thể dùng API chuyên dụng thay cho `PUT` full DTO hay không

#### 1.2.6. Mở chat theo project

##### Tìm conversation theo project

`GET /api/chat/conversation/project/{projectId}`

##### Nếu chưa có thì tạo conversation cho project

`POST /api/chat/conversation/project`

Body:

```json
{
  "projectId": "00000000-0000-0000-0000-000000000000",
  "name": "Tên dự án",
  "memberUserIds": [
    "00000000-0000-0000-0000-000000000001",
    "00000000-0000-0000-0000-000000000002"
  ]
}
```

Trước khi tạo, UI hiện gọi thêm:

- `GET /api/app/project-members?ProjectId=<project_id>&MaxResultCount=100&SkipCount=0`

để lấy danh sách `memberUserIds`.

### 1.3. Mapping nhanh theo block UI

| Block UI | API chính |
|---|---|
| Calendar | `GET /api/app/calendar-events` + `GET /api/app/calendar-event-participants` |
| Notifications | `GET /api/app/notification-receivers` + `PUT /api/app/notification-receivers/{id}` |
| Workflow chart | `GET /api/app/document-workflow-instance-logss/workflow-chart-statistics` |
| Documents | `GET /api/app/document-assignments` + `GET /api/app/documents` + `GET /api/app/document-files` |
| Projects | `GET /api/app/projects` + `POST /api/app/projects` + `GET /api/app/project-members` |
| My Tasks | `GET /api/app/project-tasks` + `GET /api/app/project-tasks/with-navigation-properties/{id}` |
| Project Chat | `GET /api/chat/conversation/project/{projectId}` + `POST /api/chat/conversation/project` |

---

## 2. Module B - API cho trang Calendar Events

Trang `Calendar Events` có 2 chế độ:

- `Calendar view`
- `List view`

Ngoài list/filter cơ bản, trang này còn có:

- create event
- update event
- delete single / delete selected / delete all
- export excel
- quản lý participant
- mở chi tiết project/task liên quan

### 2.1. Lấy danh sách calendar events

`GET /api/app/calendar-events`

#### 2.1.1. List view

List view dùng paging + sorting + filter đầy đủ.

Query điển hình:

```json
{
  "MaxResultCount": 10,
  "SkipCount": 0,
  "Sorting": "startTime DESC",
  "FilterText": "keyword",
  "Title": "Họp giao ban",
  "Description": "Q2",
  "StartTimeMin": "2026-04-01T00:00:00",
  "StartTimeMax": "2026-04-30T23:59:59",
  "EndTimeMin": "2026-04-01T00:00:00",
  "EndTimeMax": "2026-04-30T23:59:59",
  "AllDay": false,
  "EventType": "MEETING",
  "Location": "Room A",
  "RelatedType": "PROJECT",
  "RelatedId": "P0000001",
  "Visibility": "PRIVATE"
}
```

#### 2.1.2. Calendar view

Calendar view không load toàn bộ dữ liệu, mà load theo range đang nhìn thấy trên FullCalendar.

Query điển hình:

```json
{
  "MaxResultCount": 200,
  "SkipCount": 0,
  "Sorting": "",
  "StartTimeMax": "2026-04-30T23:59:59",
  "EndTimeMin": "2026-04-01T00:00:00",
  "FilterText": "keyword",
  "Title": "Họp giao ban",
  "Description": "Q2",
  "AllDay": false,
  "EventType": "MEETING",
  "Location": "Room A",
  "RelatedType": "PROJECT",
  "RelatedId": "P0000001",
  "Visibility": "PRIVATE",
  "StartTimeMin": "2026-04-01T00:00:00",
  "EndTimeMax": "2026-04-30T23:59:59"
}
```

Response fields mobile nên dùng:

- `id`
- `title`
- `description`
- `startTime`
- `endTime`
- `allDay`
- `eventType`
- `location`
- `relatedType`
- `relatedId`
- `relatedEntityId`
- `relatedName`
- `visibility`

### 2.2. Đếm participant cho nhiều event cùng lúc

`POST /api/app/calendar-event-participants/participant-counts-by-calendar-event-ids`

Body:

```json
{
  "calendarEventIds": [
    "00000000-0000-0000-0000-000000000001",
    "00000000-0000-0000-0000-000000000002"
  ]
}
```

Response:

```json
[
  {
    "calendarEventId": "00000000-0000-0000-0000-000000000001",
    "count": 5
  }
]
```

Mục đích:

- Trang list pre-load số participant theo batch thay vì gọi từng event

### 2.3. Xem chi tiết 1 event

`GET /api/app/calendar-events/{id}`

Trang còn gọi thêm:

- `GET /api/app/calendar-event-participants?CalendarEventId=<event_id>&MaxResultCount=200&SkipCount=0`

để render tab participant trong modal/view detail.

### 2.4. Tạo mới calendar event

`POST /api/app/calendar-events`

Body:

```json
{
  "title": "Họp sprint planning",
  "description": "Trao đổi backlog",
  "startTime": "2026-04-11T09:00:00",
  "endTime": "2026-04-11T10:00:00",
  "allDay": false,
  "eventType": "MEETING",
  "location": "Meeting Room 01",
  "relatedType": "PROJECT",
  "relatedId": "P0000123",
  "visibility": "PRIVATE"
}
```

Validation phía UI hiện tại:

- `title` bắt buộc
- Nếu `relatedType = PROJECT` thì phải chọn project
- Nếu `relatedType = TASK` thì phải chọn task

Ghi chú:

- Ở trang lịch, `relatedId` đang được set bằng `Code` của project/task, không phải GUID
- Ví dụ:
- Project lookup trả về `Id = project.Code`
- Task lookup trả về `Id = task.Code`

### 2.5. Cập nhật calendar event

`PUT /api/app/calendar-events/{id}`

Body:

```json
{
  "title": "Họp sprint planning",
  "description": "Trao đổi backlog cập nhật",
  "startTime": "2026-04-11T09:00:00",
  "endTime": "2026-04-11T10:30:00",
  "allDay": false,
  "eventType": "MEETING",
  "location": "Meeting Room 02",
  "relatedType": "PROJECT",
  "relatedId": "P0000123",
  "visibility": "PRIVATE",
  "concurrencyStamp": "..."
}
```

### 2.6. Xóa calendar event

#### Xóa 1 event

`DELETE /api/app/calendar-events/{id}`

#### Xóa nhiều event được chọn

`DELETE /api/app/calendar-events`

Body:

```json
[
  "00000000-0000-0000-0000-000000000001",
  "00000000-0000-0000-0000-000000000002"
]
```

#### Xóa toàn bộ theo filter

`DELETE /api/app/calendar-events/all`

Query/Input cùng cấu trúc với `GetCalendarEventsInput`.

### 2.7. Export Excel

#### Lấy download token

`GET /api/app/calendar-events/download-token`

Response:

```json
{
  "token": "..."
}
```

#### Download file Excel

`GET /api/app/calendar-events/as-excel-file`

Query được build từ:

- `DownloadToken`
- toàn bộ filter hiện tại
- `culture`

Ví dụ:

```http
GET /api/app/calendar-events/as-excel-file?DownloadToken=...&FilterText=...&Title=...&StartTimeMin=2026-04-01T00:00:00.0000000Z&StartTimeMax=2026-04-30T23:59:59.0000000Z&EventType=MEETING&RelatedType=PROJECT&RelatedId=P0000123&Visibility=PRIVATE
```

### 2.8. Participant APIs cho create/edit wizard

#### Lấy participant list của 1 event

`GET /api/app/calendar-event-participants`

Query:

```json
{
  "CalendarEventId": "<event_id>",
  "MaxResultCount": 200,
  "SkipCount": 0
}
```

#### Lookup user để thêm participant

`GET /api/app/calendar-event-participants/identity-user-lookup`

Query:

```http
GET /api/app/calendar-event-participants/identity-user-lookup?Filter=<keyword>&MaxResultCount=20&SkipCount=0
```

#### Thêm participant

`POST /api/app/calendar-event-participants`

Body:

```json
{
  "calendarEventId": "00000000-0000-0000-0000-000000000000",
  "identityUserId": "00000000-0000-0000-0000-000000000001",
  "responseStatus": "INVITED",
  "notified": false
}
```

#### Xóa participant

`DELETE /api/app/calendar-event-participants/{id}`

### 2.9. Lookup project/task liên quan trong form lọc và form create/edit

#### Lookup project

Trang hiện dùng:

- `GET /api/app/projects?FilterText=<keyword>&MaxResultCount=20&SkipCount=0`

UI map dữ liệu thành:

- `Id = project.Code`
- `DisplayName = "{Code} - {Name}"`

#### Lookup task

Trang hiện dùng:

- `GET /api/app/project-tasks?FilterText=<keyword>&MaxResultCount=20&SkipCount=0`

UI map dữ liệu thành:

- `Id = projectTask.Code`
- `DisplayName = "{Code} - {Title}"`

### 2.10. Điều hướng từ event sang project/task liên quan

Khi click event trên calendar hoặc trong day modal, page thực hiện:

#### Nếu event liên quan project

1. `GET /api/app/projects?FilterText=<relatedId>&MaxResultCount=1&SkipCount=0`
2. Lấy `project.id`
3. `GET /api/app/projects/with-navigation-properties/{id}`
4. `GET /api/app/project-members?ProjectId=<id>&MaxResultCount=200&SkipCount=0`
5. `GET /api/app/project-tasks?ProjectId=<id>&MaxResultCount=200&SkipCount=0`

#### Nếu event liên quan task

1. `GET /api/app/project-tasks?FilterText=<relatedId>&MaxResultCount=1&SkipCount=0`
2. Lấy `projectTask.id`
3. `GET /api/app/project-tasks/with-navigation-properties/{id}`

### 2.11. Lưu ý tích hợp cho mobile

#### Lưu ý 1 - `relatedId` hiện đang là string nghiệp vụ

Trong trang `Calendar Events`, `relatedId` đang được dùng như mã nghiệp vụ:

- Project: thường là `project.Code`
- Task: thường là `projectTask.Code`

Không nên mặc định `relatedId` là GUID khi tích hợp mobile.

#### Lưu ý 2 - Workspace và Calendar đang dùng `relatedId` chưa hoàn toàn nhất quán

Trang `Calendar Events` điều hướng task bằng cách tìm theo `FilterText = relatedId`.

Nhưng trong `Workspace Index`, nhánh mở task từ calendar event có đoạn parse:

- `Guid.Parse(calendarEvent.RelatedId)`

Do đó mobile nên thống nhất trước với backend về contract thật của `relatedId`:

- dùng `Code`
- hay dùng `EntityId/GUID`

Khuyến nghị tốt nhất:

- Backend nên trả đồng thời:
- `relatedId` là mã nghiệp vụ để hiển thị
- `relatedEntityId` là GUID để điều hướng/detail

#### Lưu ý 3 - Có thể cân nhắc API aggregate cho mobile

Riêng `Workspace Index` hiện phải gọi rất nhiều API song song. Nếu mobile cần tối ưu:

- có thể cân nhắc thêm 1 endpoint tổng hợp kiểu `/api/mobile/workspace`

để backend trả sẵn:

- project summary
- task summary
- event summary
- notifications
- recent documents
- workflow chart

Nhưng tài liệu này đang mô tả đúng trạng thái code hiện tại, chưa giả định endpoint aggregate mới.

---

## 3. Danh sách endpoint rút gọn

### Workspace Index

- `GET /api/app/projects`
- `GET /api/app/projects/{id}`
- `GET /api/app/projects/with-navigation-properties/{id}`
- `GET /api/app/projects/department-lookup`
- `POST /api/app/projects`
- `GET /api/app/project-tasks`
- `GET /api/app/project-tasks/with-navigation-properties/{id}`
- `GET /api/app/calendar-events`
- `GET /api/app/calendar-event-participants`
- `GET /api/app/document-assignments`
- `GET /api/app/documents`
- `GET /api/app/document-files`
- `GET /api/app/document-workflow-instance-logss/workflow-chart-statistics`
- `GET /api/app/notification-receivers`
- `PUT /api/app/notification-receivers/{id}`
- `GET /api/app/project-members`
- `GET /api/chat/conversation/project/{projectId}`
- `POST /api/chat/conversation/project`

### Calendar Events

- `GET /api/app/calendar-events`
- `GET /api/app/calendar-events/{id}`
- `POST /api/app/calendar-events`
- `PUT /api/app/calendar-events/{id}`
- `DELETE /api/app/calendar-events/{id}`
- `DELETE /api/app/calendar-events`
- `DELETE /api/app/calendar-events/all`
- `GET /api/app/calendar-events/download-token`
- `GET /api/app/calendar-events/as-excel-file`
- `GET /api/app/calendar-event-participants`
- `POST /api/app/calendar-event-participants`
- `DELETE /api/app/calendar-event-participants/{id}`
- `GET /api/app/calendar-event-participants/identity-user-lookup`
- `POST /api/app/calendar-event-participants/participant-counts-by-calendar-event-ids`
- `GET /api/app/projects`
- `GET /api/app/projects/with-navigation-properties/{id}`
- `GET /api/app/project-members`
- `GET /api/app/project-tasks`
- `GET /api/app/project-tasks/with-navigation-properties/{id}`
