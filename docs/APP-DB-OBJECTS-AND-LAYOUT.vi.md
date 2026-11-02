# App DB, gợi ý SQL và bố cục — 0.7

Bản chạy: `artifacts/publish-0.7/LSCode.exe`.

## Gợi ý SQL ngay lần mở đầu

Editor chờ việc chọn App DB hoàn tất trước khi lấy metadata. Khi metadata tải xong, gợi ý dùng nội dung và vị trí con trỏ hiện tại, không bỏ kết quả chỉ vì người dùng tiếp tục gõ trong lúc tải. Ctrl+Space cho phép gọi gợi ý cả với tiền tố ngắn. Đổi target sẽ xóa cache của tab; nút tải lại gợi ý xóa cả cache chung và cache các tab.

## App / SYS

Với dự án đã đồng bộ và có cấu hình SYS/App:

- SQL mới, cửa sổ cấu trúc/dữ liệu khi không có tab SQL đang chọn, kết nối mặc định và DB Object đều lấy App DB.
- Cây chức năng tiếp tục đọc SYS.
- Toolbar SQL hiển thị App DB và nút chọn lại App DB; bỏ lựa chọn SYS trên toolbar này.
- Các tab SQL đã mở giữ target riêng khi đổi dự án. Người dùng vẫn có thể cấu hình kết nối thủ công.
- Nếu App DB chưa xác định được, ứng dụng tiếp tục cơ chế suy ra tên App hoặc yêu cầu chọn DB; không tự chuyển truy vấn về SYS.
- Project cũ không có cấu hình SYS/App giữ cơ chế kết nối đã cấu hình trước đây.

## Bố cục

- Bỏ thanh điều hướng ngoài cùng. File dự án, Chức năng dự án/DB Object và tìm kiếm truy cập qua menu Workspace; theme và snippets qua Tools.
- Nút thu gọn nhỏ ở góc. Khi thu gọn vẫn có mũi tên mở lại.
- Windows Explorer đổi thành icon thư mục nhỏ ở góc File dự án.
- Tab controller đang chọn có tiêu đề ghim riêng phía trên và nút đóng; tiêu đề đó không lặp lại trong dãy tab còn lại. Chọn controller khác sẽ chuyển vùng ghim sang controller đó.
- Menu, danh sách và lựa chọn combobox có nền hover/selection theo theme để tránh chữ trắng trên nền trắng ở Dark.

## DB Object

Chọn tab **DB Object** bên cạnh **Chức năng dự án**. Danh sách đọc metadata của App DB và nhóm thành **Tables**, **Views**, **Stored procedures**, **Functions**, có tên schema và số lượng. Nút ↻ tải lại. Đổi dự án hủy yêu cầu cũ và xóa dữ liệu cũ. Đây là danh sách đối tượng để duyệt, chưa có thao tác sửa cấu trúc hay thực thi đối tượng.

## Kiểm chứng

- Build Release thành công, không cảnh báo/lỗi.
- 19 mục smoke WPF đạt, gồm hồi quy gợi ý SQL đầu tiên khi App target sẵn sàng trễ, chèn đúng metadata, ghim/đóng controller, thu gọn/mở lại và màu Dark.
- Đã xem ảnh render bố cục Dark.
- Báo cáo: `artifacts/smoke-ui-5c4c397d0c664107aacb9c79c97999de/smoke-result.txt`.
- Chưa kiểm thử truy vấn DB Object và chọn App DB trên máy chủ SQL thực trong lần cập nhật này. Danh sách phụ thuộc quyền xem metadata của tài khoản kết nối.
