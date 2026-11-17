# Thông báo, thu gọn panel và gợi ý SQL — 0.6

Bản chạy: `artifacts/publish-0.6/LSCode.exe`.

## Thông báo riêng

Các thông báo lỗi, xác nhận lưu/đóng file, tải lại file ngoài, xóa project/snippet và nhập snippet dùng hộp thoại WPF riêng theo màu LS Code. Hỗ trợ Đồng ý, Có, Không, Hủy tùy thao tác; Escape hủy hoặc chọn Không ở câu hỏi Có/Không. Có thể kéo hộp thoại bằng tiêu đề. Hộp chọn file/thư mục không thuộc phần thông báo và giữ nguyên.

## Thu gọn và mở lại

Nhấn **◀** ở Chức năng dự án hoặc File dự án để thu gọn. Dải hẹp vẫn giữ nút **▶** để mở lại với chiều rộng trước đó. Các nút trên thanh bên cũng mở lại panel tương ứng. Panel kết quả tiếp tục dùng ▼/▲.

## Màu cú pháp

- Entity khai báo với `SYSTEM`/`PUBLIC`: dấu khai báo và dấu kết thúc màu đỏ, tên entity giữ màu entity, SYSTEM/PUBLIC dùng màu từ khóa, đường dẫn dùng màu chuỗi.
- `<![CDATA[` và `]]>` chuyển sang xanh lá trong XML và SQL/JavaScript nhúng.
- Tham chiếu như `&ReportSign.Filter.Initialize;` chuyển sang đỏ.
- Việc tô màu không đọc hoặc thực thi tài nguyên bên ngoài của entity.

## Cấu trúc và dữ liệu

- Chọn bảng từ FROM sẽ điền `schema.table` và đóng danh sách gợi ý.
- Gõ chính xác tên bảng rồi rời ô FROM cũng xác nhận bảng và đóng danh sách; gõ lại để tìm bảng khác. Esc đóng danh sách.
- WHERE gợi ý tên cột đã tải của bảng đang chọn và các từ khóa/hàm SQL. Gõ tiền tố hoặc nhấn Ctrl+Space để mở, dùng ↑/↓ chọn, Enter/Tab chèn, Esc đóng. Có thể bấm chuột vào gợi ý.
- Tên cột được chèn với dấu ngoặc vuông và escape đúng. Không gợi ý bên trong chuỗi SQL. Đổi bảng/database sẽ xóa danh sách cột cũ để tránh gợi ý sai bảng.
- Gợi ý không chạy truy vấn; Run vẫn thực hiện SELECT chỉ đọc, tối đa 1000 dòng như trước.

## Kiểm tra

Build/publish thành công. 17 mục smoke WPF đạt: màu token trên ba theme (có SYSTEM/CDATA/include), lọc/escape gợi ý WHERE, thu gọn/mở lại panel và render/nút bấm hộp thoại riêng. Đã xem ảnh hộp thoại Dark.

Báo cáo: `artifacts/smoke-ui-e5d49411d0084a2db58ab5a54be2b9db/smoke-result.txt`.

Chưa kiểm thử luồng FROM/WHERE với kết nối SQL Server thực; gợi ý tên cột cần tải metadata bảng thành công.
