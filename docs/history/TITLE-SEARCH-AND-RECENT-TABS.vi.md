# Thanh tiêu đề, tìm DB Object và tab gần đây — 0.11

Bản chạy: `artifacts/publish-0.11/LSCode.exe`.

## Thanh tiêu đề

Cửa sổ chính dùng thanh tiêu đề riêng theo theme, có nút thu nhỏ, phóng to/khôi phục và đóng. WindowChrome giữ khả năng kéo cửa sổ, đổi kích thước và nhấp đúp vùng tiêu đề để phóng to/khôi phục. Đóng bằng nút mới vẫn đi qua luồng hỏi lưu file đang sửa.

## Tìm DB Object

- **Tìm theo tên** là mặc định, tìm trong `schema.tên` của bảng, view, procedure và function. Tự chạy sau khi ngừng gõ 350 ms; Enter tìm ngay.
- **Nội dung procedure/function** chỉ chạy khi nhấn Enter. Không tìm trong dữ liệu bảng hay định nghĩa view.
- Tách chuỗi tìm theo khoảng trắng và yêu cầu tất cả từ xuất hiện trong định nghĩa SQL. Ví dụ `exec abc` khớp `exec dbo.abc` hoặc nội dung có thêm từ nằm giữa; các từ không bắt buộc liền nhau hoặc theo thứ tự.
- Tìm bằng tham số, không nối nội dung người dùng thành mã SQL. Tìm theo tên giữ `_` là ký tự bình thường. Hoa/thường tuân theo collation của database.
- Kết quả tải theo trang 200 mục; mở Tải thêm để xem tiếp. Gõ lại sẽ hủy yêu cầu trước. Xóa chuỗi rồi Enter trả về các nhóm tải theo nhu cầu.
- Các module mã hóa, không có định nghĩa SQL hoặc tài khoản không được xem định nghĩa có thể không xuất hiện trong tìm nội dung.

## Controller gần đây

Giữ vùng controller đang ghim và chỉ hiện tối đa hai controller/tab truy cập gần đây khác trong một hàng 32 px, thay cho vùng danh sách 110 px. Nút **▾** cạnh nút đóng hiển thị tất cả tab đang mở, kể cả Toàn dự án. Tab không còn nằm trong nhóm gần đây vẫn mở và có thể chọn lại từ danh sách này. Thứ tự gần đây áp dụng trong phiên đang chạy.

## Phân biệt file trùng tên

Khi hai file cùng tên đang mở, tiêu đề tab thêm thư mục gần nhất: `Grid/dmbp.xml`, `Filter/dmbp.xml`. Nếu vẫn trùng, thêm cấp cha cho tới khi phân biệt được. Khi đóng hoặc Save As làm hết trùng, tiêu đề tự cập nhật. Tooltip vẫn là đường dẫn đầy đủ.

## Kiểm chứng

Build Release không lỗi/cảnh báo; 28 mục smoke WPF đạt. Kiểm tra gồm thanh tiêu đề/WindowChrome, giới hạn tìm nội dung và chờ Enter, tab file trùng tên thực tế và cập nhật sau khi đóng, danh sách controller gần đây giữ đủ tab đang mở. Đã xem ảnh render Dark.

Báo cáo: `artifacts/smoke-ui-a20f5b950bce4306ac814a147dea9c8b/smoke-result.txt`.

Chưa chạy tìm nội dung trên SQL Server thật và chưa kiểm tra kéo/resize cửa sổ thủ công trên nhiều màn hình/DPI khác nhau.
