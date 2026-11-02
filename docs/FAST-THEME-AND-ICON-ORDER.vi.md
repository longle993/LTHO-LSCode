# Đổi theme và thứ tự lối tắt — 0.9

Bản chạy: `artifacts/publish-0.9/LSCode.exe`.

- Bộ tô màu được dùng chung theo ngôn ngữ/theme, không tạo lại cho từng editor mỗi lần chuyển theme.
- Chỉ editor đang hiển thị cập nhật ngay. Tab ẩn nhận theme mới khi được mở lại, kể cả editor chia đôi.
- Di chuột lên icon hiện tên chức năng sau 100 ms, bên phải thanh lối tắt; tooltip có màu theo theme.
- Nhấn ⚙ ở cuối thanh lối tắt, dùng ↑/↓ để đổi thứ tự từng chức năng, đánh dấu/bỏ dấu để thêm/bỏ rồi nhấn **Lưu**. Đóng hộp thoại mà không lưu sẽ bỏ thay đổi. Thứ tự đã lưu được giữ khi mở ứng dụng và mở lại hộp cấu hình.

## Kiểm tra

Build/publish thành công. 23 mục smoke WPF đạt, gồm bộ tô màu dùng chung, cập nhật theme khi tab hiện lại, thứ tự lên/xuống, tooltip và lưu cấu hình.

Phép đo 5 lần cập nhật theme với 40 editor ẩn: tổng 31 ms trên máy kiểm thử, **chưa bao gồm thời gian render**. Đây không phải cam kết độ trễ cho mọi kích thước file hay máy tính; chưa có phép đo trước/sau trên dữ liệu thực của người dùng.

Báo cáo: `artifacts/smoke-ui-0afd29b73d9143fb80a188584ab7c9dc/smoke-result.txt`.
