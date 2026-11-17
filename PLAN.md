# Kế hoạch và nghiệm thu

## Phím tắt, kéo thả file và tên Generate Update — 17/11/2026

- [x] Chuẩn hóa phím số theo `KeyboardEvent.code` để Ctrl+Shift+4 không bị đọc thành Ctrl+Shift+$.
- [x] Đổi nhãn người dùng từ Advance Note thành Generate Update, giữ ID nội bộ và định dạng XML để tương thích dữ liệu cũ.
- [x] Cho host WPF nhận file kéo từ Explorer, lọc file tồn tại và gửi danh sách sang WebView để mở bằng luồng tài liệu hiện có.
- [x] Ctrl+Shift+P focus ô tìm của bảng lệnh ngay sau khi dialog hiển thị.
- [x] Tách tab Phím tắt, thêm tìm kiếm và nhóm File, Editor, SQL, Dự án, Công cụ, Hệ thống.
- [ ] Nghiệm thu kéo thả thủ công từ Windows Explorer trên máy người dùng; build tự động chỉ xác nhận hợp đồng host/WebView và biên dịch.

## Advance Note và SQL — triển khai 17/11/2026

- [x] Ghi đặc tả nâng cấp trong `docs/guides/ADVANCE-NOTE.vi.md`, đối chiếu source hiện tại và khảo sát tĩnh Fcode.
- [ ] Đối chiếu hoàn chỉnh generator/clsScript của Fcode và fixture đầu ra; ánh xạ SQL Data của LSCode đã chốt là `bảng: WHERE`, còn cần so sánh tên/thứ tự thư mục gói với Fcode thực tế.
- [x] Bỏ timeout thực thi SQL trên toàn đường chạy, giữ hủy chủ động.
- [x] Dựng bố cục mới, nhấp đúp menu, cây lọc file, tìm file và thêm File Path; giữ XML cũ.
- [x] Triển khai Generate Update trong Infrastructure và nối WebView qua bridge hiện có.
- [x] Đổi SQL Object thành danh sách tên table/store/function và sinh DROP + CREATE từ database; đổi SQL Data thành danh sách `bảng: WHERE`, nối Ctrl+D từ Cấu trúc và dữ liệu và sinh INSERT theo bộ lọc.
- [x] Kiểm thử chức năng, resize cơ bản, gói fixture rồi build bản chạy thử.

Người dùng đã ra lệnh bắt đầu sau lượt tài liệu. Còn mở: đối chiếu hoàn chỉnh generator Fcode, fixture dữ liệu thực tế, theme/resize thủ công và query SQL thật vượt 60 giây.

## Tổ chức tài liệu

- [x] Kiểm kê tài liệu, điểm vào build và các module chính.
- [x] Cập nhật README theo luồng WebView hiện tại và lưu README cũ trong lịch sử.
- [x] Đưa tài liệu nguồn và tài liệu dự án vào đúng thư mục, cập nhật bản đồ và liên kết.
- [x] Khởi tạo hai tệp JSON và bốn thư mục cấu hình theo quy ước `.codex/`.

## Nghiệm thu sản phẩm

Các mục sau được ghi nhận trong [hướng dẫn WebView](docs/guides/WEBVIEW-RUNNING.vi.md). Xác nhận lại trên môi trường triển khai và cập nhật bằng chứng ở tài liệu tính năng tương ứng.

- [x] Gọn toolbar editor: bỏ hai nút chữ Advance Note và SQL Debug/Watch, giữ điểm mở ở thanh lối tắt/bảng lệnh, khóa nút Chạy trên một dòng và cho ô tìm/target co giãn khi resize.

- [ ] Chạy lại discovery LAN và menu/controller đặc thù bằng profile ERP thực tế.
- [ ] Xác minh quyền đọc/ghi SQL, tương thích SSMS Trace, và một phiên breakpoint SSDT trên máy chủ phù hợp.
- [ ] Nghiệm thu Crystal, dịch và bố cục Windows trên cấu hình DPI/nhiều màn hình thực tế.
