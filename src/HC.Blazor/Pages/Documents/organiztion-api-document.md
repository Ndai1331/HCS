1. Get phòng ban

UI gửi văn bản trong `Documents.razor` dùng endpoint nội bộ:

`GET api/app/documents/organization-unit-tree`

Endpoint này đọc từ module ABP Identity (`AbpOrganizationUnits`) và trả về danh sách node đã sort theo `Code`:

```json
[
  {
    "id": "3a1f3d67-4519-0c44-513b-64d9198ca6af",
    "parentId": null,
    "code": "00001",
    "displayName": "Phòng ban CNTT"
  }
]
```

Endpoint gốc của ABP Identity để tham chiếu/debug:

`GET api/identity/organization-units?Sorting=code`

responsive:
{
  "totalCount": 7,
  "items": [
    {
      "parentId": null,
      "code": "00001",
      "displayName": "Phòng ban CNTT",
      "roles": [],
      "userCount": 5,
      "concurrencyStamp": "7af07baafd6f4ef2ba1602e8a916a552",
      "isDeleted": false,
      "deleterId": null,
      "deletionTime": null,
      "lastModificationTime": "2026-05-08T09:52:28.239521",
      "lastModifierId": "3a1e8563-9a32-71bd-4aba-ce41bbf90831",
      "creationTime": "2026-02-05T00:36:15.447299",
      "creatorId": "3a1e8563-9a32-71bd-4aba-ce41bbf90831",
      "id": "3a1f3d67-4519-0c44-513b-64d9198ca6af",
      "extraProperties": {}
    },
    {
      "parentId": "3a1f3d67-4519-0c44-513b-64d9198ca6af",
      "code": "00001.00001",
      "displayName": "Phần mềm",
      "roles": [
        {
          "name": "người dùng",
          "isDefault": false,
          "isStatic": false,
          "isPublic": true,
          "userCount": 0,
          "concurrencyStamp": "b9a841e1578b4ffd9323552ff1f15f27",
          "creationTime": "2026-01-16T03:33:59.044897",
          "id": "3a1ed70a-cc36-83d3-03a9-0ab74eb70a0e",
          "extraProperties": {}
        }
      ],
      "userCount": 2,
      "concurrencyStamp": "adac937235ff4fcd9ae979f1915abe97",
      "isDeleted": false,
      "deleterId": null,
      "deletionTime": null,
      "lastModificationTime": "2026-05-08T09:53:08.038494",
      "lastModifierId": "3a1e8563-9a32-71bd-4aba-ce41bbf90831",
      "creationTime": "2026-02-05T00:37:07.129404",
      "creatorId": "3a1e8563-9a32-71bd-4aba-ce41bbf90831",
      "id": "3a1f3d68-0f5d-f66b-5ada-f28febbff4c4",
      "extraProperties": {}
    },
    {
      "parentId": "3a1f3d67-4519-0c44-513b-64d9198ca6af",
      "code": "00001.00002",
      "displayName": "Kỹ thuật",
      "roles": [],
      "userCount": 1,
      "concurrencyStamp": "6b4a8f1018ce480693580bccebd4971b",
      "isDeleted": false,
      "deleterId": null,
      "deletionTime": null,
      "lastModificationTime": "2026-05-08T09:53:00.50268",
      "lastModifierId": "3a1e8563-9a32-71bd-4aba-ce41bbf90831",
      "creationTime": "2026-02-05T00:37:19.590526",
      "creatorId": "3a1e8563-9a32-71bd-4aba-ce41bbf90831",
      "id": "3a1f3d68-4008-0dc5-bfff-7b771c39778a",
      "extraProperties": {}
    },
    {
      "parentId": null,
      "code": "00002",
      "displayName": "Kế toán",
      "roles": [],
      "userCount": 0,
      "concurrencyStamp": "2554a073af284e2c9daa589e81f08c03",
      "isDeleted": false,
      "deleterId": null,
      "deletionTime": null,
      "lastModificationTime": null,
      "lastModifierId": null,
      "creationTime": "2026-05-08T09:53:19.007481",
      "creatorId": "3a1e8563-9a32-71bd-4aba-ce41bbf90831",
      "id": "3a2117ad-d029-126d-08b8-caacc69b584f",
      "extraProperties": {}
    },
    {
      "parentId": null,
      "code": "00003",
      "displayName": "Quản trị",
      "roles": [],
      "userCount": 0,
      "concurrencyStamp": "184036bbccb942b497e8bd20bca2052d",
      "isDeleted": false,
      "deleterId": null,
      "deletionTime": null,
      "lastModificationTime": null,
      "lastModifierId": null,
      "creationTime": "2026-05-08T09:53:24.198308",
      "creatorId": "3a1e8563-9a32-71bd-4aba-ce41bbf90831",
      "id": "3a2117ad-e45d-ded6-d835-cf0c6a10b99d",
      "extraProperties": {}
    },
    {
      "parentId": null,
      "code": "00004",
      "displayName": "Giám đốc",
      "roles": [],
      "userCount": 0,
      "concurrencyStamp": "25d35c38bf054652b57150d42258eb89",
      "isDeleted": false,
      "deleterId": null,
      "deletionTime": null,
      "lastModificationTime": null,
      "lastModifierId": null,
      "creationTime": "2026-05-08T09:53:28.441014",
      "creatorId": "3a1e8563-9a32-71bd-4aba-ce41bbf90831",
      "id": "3a2117ad-f4f9-ef99-bf7f-6c347e77c777",
      "extraProperties": {}
    },
    {
      "parentId": null,
      "code": "00005",
      "displayName": "Văn thư",
      "roles": [],
      "userCount": 0,
      "concurrencyStamp": "c10413e10e2d40d68878cd5ee9402494",
      "isDeleted": false,
      "deleterId": null,
      "deletionTime": null,
      "lastModificationTime": null,
      "lastModifierId": null,
      "creationTime": "2026-05-08T09:53:32.193178",
      "creatorId": "3a1e8563-9a32-71bd-4aba-ce41bbf90831",
      "id": "3a2117ae-03a2-e6ec-3dc1-a90a55aea961",
      "extraProperties": {}
    }
  ]
}

2. Logic gửi theo phòng ban

Logic gửi văn bản không còn dùng bảng `UserDepartments`/`Departments` và không expand thành nhiều người nhận. Khi người dùng chọn phòng ban, client gửi một `organizationUnitId` trong `organizationUnits`; server lưu `Document.OrganizationUnitId = organizationUnitId`.

Người dùng thuộc phòng ban đó sẽ thấy văn bản trong tab “Gửi tới tôi” nhờ query membership từ `IdentityUser.OrganizationUnits`.

Chỉ cho chọn một phòng ban vì `Document.OrganizationUnitId` hiện là một field đơn.

Endpoint gốc của ABP Identity để tham chiếu/debug:

`GET api/identity/organization-units/3a1f3d67-4519-0c44-513b-64d9198ca6af/members`
responsive 
{
  "totalCount": 2,
  "items": [
    {
      "tenantId": null,
      "userName": "dungtester",
      "email": "dungdung@yopmail.com",
      "name": "Dung",
      "surname": "Phùng Anh",
      "emailConfirmed": false,
      "phoneNumber": "0999123123",
      "phoneNumberConfirmed": false,
      "supportTwoFactor": false,
      "twoFactorEnabled": false,
      "isActive": true,
      "lockoutEnabled": false,
      "isLockedOut": false,
      "lockoutEnd": null,
      "shouldChangePasswordOnNextLogin": false,
      "concurrencyStamp": "5ec16bd9c1fe4759b478578c50ba447d",
      "roleNames": [
        "admin",
        "người dùng"
      ],
      "accessFailedCount": 0,
      "lastPasswordChangeTime": "2026-03-23T21:02:33.058441+07:00",
      "isExternal": false,
      "isDeleted": false,
      "deleterId": null,
      "deletionTime": null,
      "lastModificationTime": "2026-04-21T01:23:30.175969",
      "lastModifierId": "3a1e8563-9a32-71bd-4aba-ce41bbf90831",
      "creationTime": "2026-03-23T14:02:33.236685",
      "creatorId": "3a1e8563-9a32-71bd-4aba-ce41bbf90831",
      "id": "3a202d2d-fc62-04bc-531c-96ef3db8424c",
      "extraProperties": {
        "PositionId_Text": "AD - Admin",
        "PositionId": "3a1ec895-e132-67cd-119f-6c5b9af51e30"
      }
    },
    {
      "tenantId": null,
      "userName": "longnguyen",
      "email": "longnguyen13th1d.pou@gmail.com",
      "name": "Hồ Phi Long",
      "surname": "Nguyễn",
      "emailConfirmed": false,
      "phoneNumber": "0363307951",
      "phoneNumberConfirmed": false,
      "supportTwoFactor": false,
      "twoFactorEnabled": false,
      "isActive": true,
      "lockoutEnabled": true,
      "isLockedOut": false,
      "lockoutEnd": null,
      "shouldChangePasswordOnNextLogin": false,
      "concurrencyStamp": "a09fcb4a618547ecbef3f841e63c6c03",
      "roleNames": [
        "admin",
        "người dùng"
      ],
      "accessFailedCount": 0,
      "lastPasswordChangeTime": "2026-01-02T12:53:32.52761+07:00",
      "isExternal": false,
      "isDeleted": false,
      "deleterId": null,
      "deletionTime": null,
      "lastModificationTime": "2026-04-21T01:24:12.524931",
      "lastModifierId": "3a1e8563-9a32-71bd-4aba-ce41bbf90831",
      "creationTime": "2026-01-02T12:53:32.719919",
      "creatorId": "3a1e8563-9a32-71bd-4aba-ce41bbf90831",
      "id": "3a1e8f71-8948-3043-4e18-40ebdfcc89d9",
      "extraProperties": {
        "PositionId_Text": "AD - Admin",
        "PositionId": "3a1ec895-e132-67cd-119f-6c5b9af51e30"
      }
    }
  ]
}