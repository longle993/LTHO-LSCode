# Tinh chỉnh mật độ giao diện và khả năng đọc

Yêu cầu ghi trước khi triển khai, ngày 05/11/2026. Phạm vi là các thành phần hiện có của WPF, không thiết kế lại màn hình.

## Thay đổi cần thực hiện

- Giảm cỡ chữ giao diện và editor mặc định từ 14 xuống 13 DIP; font editor giữ Cascadia Mono/Consolas.
- Tăng khoảng cách dòng editor, áp dụng cả editor chính và phần chia đôi; không chèn dòng hay thay đổi nội dung file.
- XML: phân biệt tên thẻ, thuộc tính, giá trị, comment, entity và CDATA bằng bảng màu dịu, dễ đọc theo Light/Dark/High Contrast. Giữ bộ phân tích SQL/JavaScript nhúng hiện có.
- Cây menu và cây file có đường nối nét đứt để thấy quan hệ cha/con; giữ mở/đóng, chọn và điều hướng bàn phím.
- Thu gọn tiêu đề Không gian dự án, các nút File/Chức năng/DB và các thanh phụ để dành thêm chiều cao cho danh sách file. Bỏ thanh tên dự án lặp lại ở phía trên, giữ cách đổi dự án và trạng thái tải.
- Folder và file .f/.xml/.rpt/.xlsx có ký hiệu nét rõ, nhãn loại file đủ đọc, không chỉ dựa vào màu hoặc emoji.
- Tạo danh mục: nhấn một lần để nhập ô, mở chọn kiểu và bật/tắt checkbox. Giữ các ô chỉ đọc, trường hệ thống và cơ chế Lưu hiện có; nhấn ô không tự thực thi SQL.

## Kiểm chứng

Build Release, chạy kiểm tra Core và kiểm tra WPF tại các thành phần thay đổi. Kiểm tra cả ba theme, editor chia đôi, cây nhiều cấp và ô danh mục chỉ đọc. Đóng gói bản chạy trong artifacts, không thay SDK, framework hoặc package.

## Kết quả triển khai

- Đã áp dụng font 13 DIP; font composite giữ Cascadia Mono/Consolas với chiều cao dòng đo được 21,45 DIP ở cả hai vùng editor.
- Thanh tên dự án lặp lại được ẩn khi rảnh; trạng thái tải vẫn hiện khi có tác vụ. Đổi dự án vẫn truy cập tại menu Project. Nút File/Chức năng/DB cao 28 DIP; tiêu đề và thanh controller được thu gọn, không giới hạn chiều cao cây file.
- Cây file/menu dùng đường nối nét đứt; folder là hình vector, file có icon và nhãn F/XML/RPT/XLSX. Bảng màu XML được chia riêng màu thuộc tính, giữ phân tích SQL/JavaScript nhúng.
- Lưới danh mục vào sửa sau một lần nhấn, checkbox bật/tắt một lần và ô kiểu dữ liệu mở chọn. Giữ các ô và lưới chỉ đọc.
- Build Release không lỗi/cảnh báo; 120 kiểm tra Core đạt. Kiểm tra WPF riêng `--compact-ui-smoke` kiểm tra token ba theme, editor chia đôi, nhập chữ, checkbox bật/tắt, chọn kiểu và khóa ô/lưới. Các kiểm tra nhập ô dùng fixture tại chỗ, không kết nối hoặc thay đổi SQL Server.
- Đã render và xem ảnh WPF ở Light/Dark/High Contrast, cây file/menu và kích thước 980 × 640. Ảnh và báo cáo nằm trong `artifacts/compact-check-*`; dữ liệu trong ảnh là minh họa. Không dùng CSS/probe trình duyệt cho ứng dụng WPF.
- Bản chạy mới: `artifacts/publish-ui-compact/LSCode.exe`; sao chép cả thư mục khi chuyển máy.
