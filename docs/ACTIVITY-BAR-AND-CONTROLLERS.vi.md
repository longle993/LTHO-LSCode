# Thanh icon, controller và cấu trúc dữ liệu — 0.8

Bản chạy: `artifacts/publish-0.8/LSCode.exe`.

## Thanh lối tắt

Thanh ngoài cùng hiển thị icon tìm kiếm, SQL mới, snippets, profile dự án, SQL Profiler, đổi dự án, đổi theme, file dự án và DB Object. Di chuột để xem tên chức năng. Nhấn **⚙** ở cuối thanh, đánh dấu/bỏ dấu chức năng rồi **Lưu**. Có thể bỏ toàn bộ icon; nút cấu hình vẫn còn. Lựa chọn được lưu trong `ActivityCommands` của settings và áp dụng khi mở lại.

Ở bản 0.8, SQL Profiler sử dụng external launcher. Bản 0.14 chuyển sang cửa sổ riêng/SQL Trace; xem [trạng thái và giới hạn hiện tại](DBOBJECTS-PROFILER.vi.md).

## Panel và controller

- Dải thu gọn giảm từ 36 xuống 26 px, nền cùng theme. Thanh cuộn cũng dùng màu theme.
- Controller đang chọn có viền/màu nhấn và biểu tượng ghim riêng.
- Các tab còn lại hiển thị thành danh sách dọc có viền, trong vùng cuộn cao cố định. Không còn TabPanel tự xếp lại hàng khi chọn tab và thiếu chiều ngang.
- Tên dài được rút gọn bằng dấu ba chấm, di chuột xem đầy đủ; nút đóng ở mép phải.

## Cấu trúc và dữ liệu

- Phím Tab: SELECT → FROM → WHERE → ORDER BY; Shift+Tab đi ngược. Nếu danh sách gợi ý đang mở, Tab chèn gợi ý trước.
- ORDER BY có gợi ý tên cột và SQL như WHERE, bổ sung ASC/DESC; hỗ trợ Ctrl+Space, lên/xuống, Enter/Tab và Escape.
- Tiêu đề cột kết quả giữ nguyên tên SQL trả về, kể cả tên trùng hoặc tên rỗng. Tên nội bộ của DataTable vẫn phải duy nhất để liên kết dữ liệu đúng, nhưng không hiển thị tên nội bộ thay cho tiêu đề gốc.
- DB Object bỏ giới hạn 10.000 dòng của hàm đọc metadata. Danh sách vẫn phụ thuộc quyền xem metadata của tài khoản; truy vấn còn timeout và khả năng hủy. Giới hạn dòng dữ liệu khi Run không thay đổi.

## Icon và loading

Bổ sung biểu tượng ASPX, config/web.config, Excel, RPT, XSD. Thanh loading mang nhãn LS và màu nhấn của theme xuất hiện lúc khởi động/xử lý thao tác và tải DB Object; cửa sổ cấu trúc dữ liệu có thanh tiến trình khi tải/chạy.

## Kiểm chứng

Build Release không lỗi/cảnh báo. 21 mục smoke WPF đạt, gồm thứ tự focus, gợi ý/chèn ORDER BY, tiêu đề cột trùng/rỗng, lưu và thêm/bỏ lối tắt, vòng đời loading, ghim controller và hồi quy các tính năng trước. Đã xem ảnh Dark với nhiều controller tên dài và các icon mới.

Báo cáo: `artifacts/smoke-ui-2377e5f622a7447cb7fef202f85533bf/smoke-result.txt`.

Chưa kiểm thử DB Object trên database thực có hơn 10.000 đối tượng. Kiểm tra ORDER BY và tên cột lần này dùng dữ liệu giả trên control WPF thật, không kết nối SQL Server.
