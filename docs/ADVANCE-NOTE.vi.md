# Advance Note trong LSCode

Trạng thái: đã triển khai trong LSCode WebView.

## Mục tiêu

Đưa luồng Advance Note từ Fcode vào LSCode cho người làm dự án ERP trên desktop. Người dùng chọn yêu cầu theo tiêu đề và trạng thái, chỉnh nội dung kỹ thuật, tra source của chức năng đang xử lý rồi lưu yêu cầu để mở lại.

## Luồng sử dụng

1. Mở **Advance Note** từ danh sách lệnh. Dùng một tab công cụ trong không gian làm việc, có thể dùng lại phím **Ctrl+Shift+4** của Fcode nếu không xung đột với phím tắt hiện tại.
2. Tạo, tìm, chọn, sửa hoặc xóa yêu cầu. Mỗi yêu cầu có ID ổn định, tiêu đề, trạng thái và nội dung ghi chú. Chi tiết kỹ thuật thuộc về yêu cầu đang chọn. Xác nhận trước khi xóa.
3. Bật tra source trong Advance Note rồi chọn một chức năng ở cây menu ERP. LSCode dùng chức năng và dự án đang chọn để tra controller theo luồng ERP sẵn có, hiển thị file theo cây và cho mở file trong editor. Thay đổi menu cập nhật kết quả tra source, giữ yêu cầu đang chỉnh.
4. Nhấn **Lưu** hoặc **Ctrl+S** khi đang ở tab Advance Note. Mở lại tab để tiếp tục từ XML đã lưu. Hiển thị lỗi tải/lưu và giữ bản đang chỉnh khi thao tác lưu thất bại.

## Lưu trữ và phạm vi

- WebView gọi lệnh qua Application. Infrastructure đọc và ghi XML trong thư mục dữ liệu LSCode, không cần cài Fcode.
- XML do LSCode quản lý có phiên bản định dạng. Ghi qua file tạm cùng thư mục, sau đó thay thế bản đích. Trình đọc tắt DTD và external entity. Báo lỗi rõ nếu XML hỏng.
- Phân biệt yêu cầu theo dự án. Giữ tham chiếu menu và source theo dự án đã chọn. Bản đầu giữ luồng làm việc và lưu XML, chưa cam kết tương thích trực tiếp XML cũ của Fcode khi chưa có mẫu.

## Giao diện và nghiệm thu

- Dùng các thành phần và theme hiện có của LSCode. Việc chính là mở đúng yêu cầu, chỉnh nội dung và mở đúng file source. Ưu tiên hiển thị tiêu đề và trạng thái ở danh sách, đặt chi tiết và source theo bố cục được chọn trước khi dựng.
- Kiểm tra tạo → sửa → lưu → mở lại, xác nhận xóa, XML hỏng, lưu lỗi và đổi dự án.
- Kiểm tra chọn menu ERP có controller, menu thiếu file, chuyển menu liên tiếp và mở file kết quả trong editor.

## Kết quả triển khai

- Nút **Advance Note** nằm trên thanh công cụ. Lệnh cũng có trong danh sách lệnh với phím tắt **Ctrl+Shift+4**. Khi chưa mở dự án, lệnh đưa người dùng tới màn chọn dự án.
- Tab Advance Note dùng bố cục danh sách yêu cầu bên trái. Bên phải gồm tiêu đề, trạng thái, chi tiết kỹ thuật và cây source ERP.
- **Chọn chức năng** chuyển không gian dự án sang cây Chức năng ERP. Chọn một menu sẽ lưu `wmenu_id`, tên chức năng, `sysid`, `link`, gọi `project.controller` và đưa file controller vào cây source của yêu cầu.
- Người dùng có thể mở file từ cây source vào editor, sao chép tên bằng Ctrl+C và mở thư mục từ menu chuột phải dùng chung với cây file ERP.
- **Lưu** hoặc **Ctrl+S** ghi toàn bộ yêu cầu của dự án vào `advance-notes-<mã-băm-project-id>.xml` trong thư mục dữ liệu LSCode. XML chứa menu và danh sách source đã tìm thấy để mở lại nhanh.
- Trước khi đóng tab có thay đổi chưa lưu, LSCode yêu cầu xác nhận. Xóa yêu cầu cũng yêu cầu xác nhận.
- Bộ kiểm tra WebView xác nhận vòng lưu/mở lại XML, menu ERP và source file. Tiêu đề trống bị từ chối trước khi thay thế file đang lưu.

## Cách thử

File Path chứa các đường dẫn đã tích trong cây source, mỗi file một dòng, có thể sửa trực tiếp.
Chọn chức năng ERP để tải cây thư mục rồi tích từng file cần đưa vào yêu cầu.
Các ô SQL Object, SQL Data, SQL Top Script lưu riêng theo database App/Sys trong XML.
XML cũ vẫn đọc được với các ô SQL ban đầu để trống.

1. Mở một dự án ERP có kết nối SYS rồi nhấn **Ctrl+Shift+4**.
2. Nhấn **Thêm**, nhập tiêu đề và nội dung.
3. Nhấn **Chọn chức năng**, chọn một dòng trong tab **Chức năng** bên trái. LSCode tự mở cây source tương ứng trong Advance Note.
4. Nhấn một file trong cây source để mở editor.
5. Nhấn **Lưu**, đóng tab và mở lại Advance Note để kiểm tra dữ liệu XML.
