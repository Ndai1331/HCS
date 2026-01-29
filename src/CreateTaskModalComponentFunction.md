1. Chức năng tạo task từ src/HC.Blazor/ProjectTask.razor đã có
2. Tôi muốn đưa chức năng tạo task ra làm component riêng để áp dụng cho các màn hình khác sử dung (src/HC.Blazor/Components/ProjectTaskCreateModal)
3. Áp dụng tại trang /ProjectDetail - bổ sung chức năng tạo task ở grid task
4. ✅ ĐÃ HOÀN THÀNH:
   - Cập nhật ProjectTaskCreateModal component với parameter ProjectId và EventCallback OnTaskCreated
   - Thêm logic xử lý khi ProjectId được cung cấp: ẩn Project selector, tự động chọn project hiện tại
   - Tích hợp component vào ProjectDetail.razor.cs với methods: OpenCreateTaskModalAsync() và OnTaskCreatedAsync()
   - Thêm nút "New Task" vào UI của trang ProjectDetail (hiển thị khi có quyền Create ProjectTask)
   - Component tự động refresh task grid sau khi tạo task mới thành công

