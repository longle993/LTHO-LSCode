# Loading và vùng phóng to — 0.13

- Khởi động và kết nối dự án: lớp phủ riêng với logo LS chuyển động và thanh tiến trình; không thay đổi kích thước nội dung bên dưới. Lớp phủ chặn thao tác lặp trong lúc kết nối và được gỡ khi thành công, lỗi hoặc hủy.
- Loading tác vụ trong ứng dụng: góc phải thanh tiêu đề, vùng cố định; không thêm hàng và không đẩy editor xuống.
- Mở ứng dụng mặc định phóng to. Kích thước khi khôi phục vẫn vừa màn hình.
- Phóng to dùng vùng làm việc của màn hình hiện tại, chừa taskbar; tọa độ xử lý theo pixel của màn hình.

## Kiểm tra

Build Release không lỗi/cảnh báo; 48 kiểm tra core và 31 kiểm tra WPF đạt. Kiểm tra WPF xác nhận editor không dịch chuyển khi loading, vòng đời lớp phủ, trạng thái khởi động phóng to và thanh trạng thái nằm trong vùng làm việc. Đã xem ảnh render giao diện.

Chưa kiểm tra kết nối SQL thực tế và chuyển giữa nhiều màn hình có DPI khác nhau trên thiết bị thật.

Chạy `artifacts/publish-0.13/LSCode.exe` (giữ toàn bộ thư mục publish đi kèm).
