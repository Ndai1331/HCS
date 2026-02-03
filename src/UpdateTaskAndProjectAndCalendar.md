1. Khi tạo Project
- Thêm 1 record vào Calendar (Enum:EventType.TASK_ASSIGNED, Enum:EventVisibility.PUBLIC, Enum:RelatedType.PROJECT ), RelatedId là ProjectId vừa tạo 
- Khi tạo user cho ProjectMembers -> Thêm record user đó vào CalendarAssignments
- Khi xoá user khỏi ProjectMembers -> Xoá record user đó ra khỏi CalendarAssignments

2. Khi tạo ProjectTask
- Thêm 1 record vào Calendar (Enum:EventType.TASK_ASSIGNED, Enum:EventVisibility.PUBLIC, Enum:RelatedType.TASK ), RelatedId là ProjectTaskId vừa tạo
- Khi tạo user cho ProjectTaskAssignments -> Thêm record user đó vào CalendarAssignments
- Khi xoá user khỏi ProjectTaskAssignments -> Xoá record user đó ra khỏi CalendarAssignments

3. Ở trang blazor Calendar 

- Chế độ xem lịch nếu 1 ngày có nhiều sự kiện: Show modal theo tab(Sự kiện, Dự án, Công việc)

-Nếu 1 ngày có nhiều sự kiện thì tab sự kiện cho scroll cách nhau bằng Card mỗi card là 1 sự kiện

-Nếu 1 ngày có nhiều dự án thì tab dự án cho scroll cách nhau bằng Card mỗi card là 1 dự án

-Nếu 1 ngày có nhiều công việc thì tab công việc cho scroll cách nhau bằng Card mỗi card là 1 công việc



