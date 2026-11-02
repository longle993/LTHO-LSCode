# Đóng cửa sổ, màn hình nhỏ, tìm kiếm và mở file — 0.12

Bản chạy: `artifacts/publish-0.12/LSCode.exe`.

## Đóng ứng dụng

Sự kiện Closing chỉ hủy yêu cầu đóng ban đầu và xếp lịch xử lý tiếp. Hộp hỏi lưu file và lần Close cuối chạy sau khi thoát sự kiện Closing, tránh lỗi “Cannot set Visibility … while a Window is closing”. Chặn yêu cầu đóng lặp khi đang xử lý; chọn Hủy giữ cửa sổ mở và khởi động lại timer. Lỗi khi lưu không còn ép đóng ứng dụng.

## Màn hình nhỏ

Khi tạo cửa sổ, ứng dụng lấy vùng làm việc của màn hình bằng MonitorFromWindow/GetMonitorInfo, quy đổi theo DPI và giới hạn kích thước để vừa màn hình, chừa khoảng cách 12 px theo đơn vị WPF. Kích thước tối thiểu cũng được giảm khi cần, thanh tìm lệnh trên tiêu đề có thể co lại. Không dùng chiều cao cố định 900 vượt vùng hiển thị khả dụng nữa.

## Tìm kiếm DB Object

- Tìm tên lọc trên catalog tên đối tượng được cache, thực hiện lọc ngoài luồng giao diện. Các lần gõ tiếp không truy vấn sys.objects lặp lại.
- Tìm nội dung vẫn chỉ chạy bằng Enter và chỉ xét procedure/function. Một lần tìm đọc danh sách tên khớp một lần; chuyển sang trang kết quả tiếp không quét lại mã định nghĩa SQL.
- Cache tối đa tám kết quả tìm nội dung trong hai phút. Nút ↻ xóa cache tên/nội dung để lấy thay đổi mới.
- Lần đầu tìm tên vẫn cần tải catalog. Lần đầu tìm nội dung mới vẫn phụ thuộc tốc độ SQL Server và lượng mã định nghĩa; không tự tạo index hay sửa schema database.

## Luồng menu/file dự án

Nhấp đúp menu chỉ cập nhật/chọn cây các file liên quan trong File dự án, không tự mở ASPX/XML/F vào editor. Nhấp đúp một file trong cây mới mở đúng file đó. File mở từ cây luôn có tên thư mục cha trong nhãn, ví dụ `Grid/dmbp.xml`, kể cả khi chưa mở file khác trùng tên. Khi thư mục cha cũng trùng, thêm cấp cha để phân biệt. Mở file từ các nguồn khác vẫn dùng cơ chế bổ sung thư mục khi phát hiện tên trùng.

## Kiểm chứng

Build không lỗi/cảnh báo. 30 mục smoke WPF đạt, gồm:

- Đóng một cửa sổ thật, hủy hộp hỏi lưu, kiểm tra timer hoạt động lại, rồi đóng sạch không gọi Close lồng trong Closing.
- Tính biên cửa sổ với vùng 1366×728, 910×480 và màn hình có tọa độ âm.
- Nhấp menu không mở editor; chọn file chỉ mở file được chọn, có tiền tố thư mục.
- Lọc tên trên 20.001 đối tượng mất 3 ms trong phép đo cục bộ, chưa tính tải catalog/network/render.

Báo cáo: `artifacts/smoke-ui-6e4fd3baee714f739c347e322a620bd2/smoke-result.txt`.

Chưa xác nhận trực tiếp trên màn hình laptop và SQL Server của người dùng.
