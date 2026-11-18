# Mục tiêu LSCode

Trong tab SQL, gõ `@` để gợi ý tham số của `CREATE/ALTER PROC` và biến khai báo trong tài liệu. Gõ `#` để gợi ý bảng tạm từ `CREATE TABLE` và `SELECT INTO`. Gợi ý lọc theo tiền tố và thay cả ký tự `@` hoặc `#` khi chèn.

Nâng cấp đã triển khai: [Generate Update và SQL không giới hạn timeout](docs/guides/ADVANCE-NOTE.vi.md#đặc-tả-nâng-cấp--17112026). Nghiệm thu: nhấp đúp ERP lấy source vào cây dưới danh sách yêu cầu, lọc `.f` cho Dir/Filter/Grid, tìm và thêm File Path; SQL Object nhận tên table/store/function để sinh DROP + CREATE; Ctrl+D tại Cấu trúc và dữ liệu lưu `bảng: WHERE` vào SQL Data để sinh INSERT; xuất gói Generate Update và giữ khả năng hủy SQL chủ động.

Cung cấp một IDE Windows cho người phát triển và bảo trì FAST ERP. Người dùng có thể chọn dự án, tìm menu và controller, sửa source, làm việc với cơ sở dữ liệu và dùng các công cụ hỗ trợ trong cùng workbench.

| Phạm vi | Tiêu chí nghiệm thu |
| --- | --- |
| Editor và workspace | Mở, tìm kiếm, định dạng, chia pane và lưu file. Kiểm tra thay đổi ngoài ứng dụng và giữ encoding khi lưu. Kéo file từ Explorer vào cửa sổ sẽ mở file; Ctrl+Shift+P mở bảng lệnh và focus ô tìm. Snippet người dùng có khóa bắt đầu bằng `/` xuất hiện ngay trong gợi ý SQL; Enter/Tab thay khóa bằng nội dung và đặt caret theo vị trí đã lưu. Cửa sổ không chrome có viền bao theo theme để phân biệt khi chồng nhiều cửa sổ. Toolbar editor không hiện lối tắt Generate Update/SQL Watch; ô tìm và target được co giãn nhưng nút Chạy luôn đọc trên một dòng khi đổi kích thước cửa sổ. |
| Dự án, menu và source ERP | Chọn hoặc đồng bộ profile, import/export qua tệp, giữ đúng ngữ cảnh dự án khi mở menu, controller và Source Collection. Panel Chức năng có Web/Mobile; Web giữ `wcommand`, Mobile lazy-load `dvcommand`, cache theo project/SYS/App/identity/loại menu và refresh chủ động. Chọn menu chỉ hiện cây controller theo link hoặc `sysid`, không tự mở Main. F12 và menu chuột phải chỉ áp dụng Web để sinh script INSERT `wcommand` và `command`. |
| SQL và danh mục | Target App/SYS gắn với từng tab. Truy vấn có thể hủy. Cấu trúc và dữ liệu có SELECT, WHERE chiếm 70% và ORDER BY chiếm 30% trên cùng hàng; màn hẹp tự xếp dọc. Trạng thái/lỗi dùng toast nội bộ và xác nhận xóa dùng modal LSCode, không gọi hộp thoại hệ thống. Lưới dùng cùng ngôn ngữ với kết quả SQL: kéo chọn vùng, chọn dòng/cột/toàn bảng, Ctrl+C, sửa trực tiếp ô và F8 xóa các dòng được chọn theo khóa chính sau bước xác nhận; dropdown hiển thị App/Sys, Ctrl+Alt+D đổi target của tab bảng. Ctrl+D tại bảng lưu khóa dữ liệu vào Generate Update; menu Tập lệnh mở trên top-layer để không bị toolbar cắt và sinh script tạo hoặc xóa bảng. Gợi ý SQL gồm schema, cột, biến, bảng tạm và snippet nhanh khai báo trong ứng dụng. Thanh Kết quả đếm thời gian mỗi 100 ms khi đang chạy và giữ tổng thời gian sau khi hoàn tất, lỗi hoặc dừng. |
| Công cụ tích hợp | Dùng Generate Update, Template Studio, Profiler và SQL Watch trên WebView. Ctrl+Shift+4 mở Generate Update. Cài đặt phím tắt là một tab riêng, có tìm kiếm và nhóm theo chức năng. Thao tác breakpoint/step được chuyển sang SSDT. |
| Phân phối | Build WebView và các project Template Studio từ checkout LSCode. Xác minh gói cài và smoke trên Windows x64. |

Xem [ma trận WebView](docs/specifications/WEBVIEW-MIGRATION.vi.md) và [đặc tả Table Designer](docs/specifications/CATALOG-TABLE-DESIGNER.vi.md) để tra tiêu chí chi tiết. [PLAN.md](PLAN.md) ghi phạm vi còn cần nghiệm thu theo các tài liệu hiện có.

Lưới Cấu trúc và dữ liệu hiện viền trên mỗi ô được quét và dùng nền xám cho ô nhập. Lưu hoặc Ctrl+S ghi cả lô dữ liệu, gồm thay đổi khóa cho phép ghi. Khi có xung đột khóa, toàn bộ lần lưu được hoàn tác và các ô khóa của dòng lỗi được đánh dấu sau khi khôi phục giá trị.

## Tải menu nhanh và menu Mobile

Khi profile có đường dẫn source, mở dự án và tải lại web.config dùng file web.config trong thư mục source đã lưu.

Chỉ hiện thanh Web / Mobile khi tải được cây menu Mobile có mục. Dự án chỉ có Web mở thẳng danh sách Web.

Đặc tả: [tối ưu tải menu và tích hợp Mobile](docs/specifications/ERP-MENU-PERFORMANCE-MOBILE.vi.md). Đã triển khai cache/gộp request, giảm lượt SQL Web, tab Web/Mobile lazy-load, adapter `dbo.dvcommand`, tìm trong bộ nhớ và tra source Mobile bằng `sysid`. F3/F12 vẫn chỉ dành cho Web. Còn nghiệm thu baseline LAN, SQL Server ERP thật và Customize Mobile.

Batch kiểm tra `dbo.dvcommand` phải chạy được trên các SYS FAST cũ: dùng `RAISERROR + RETURN`, không dùng cú pháp `THROW` yêu cầu SQL Server 2012 trở lên.
# Điều hướng XML và quản lý yêu cầu

Ctrl+G trong XML ERP hiển thị TAG và DETAIL với màu nhận diện cho từng nhóm. Nhóm fields có Start tại field đầu tiên và End tại field cuối cùng. Nhóm views liệt kê các view theo `id`. Nhóm command liệt kê sự kiện theo thứ tự nguồn, script chỉ liệt kê tên hàm JavaScript, và css có End/Start. Không đưa `items` hoặc `text` vào danh sách điều hướng. Chọn một mục để nhảy tới vị trí tương ứng trong buffer đang sửa.

Danh sách Ctrl+G lấy trực tiếp từ buffer đang sửa và hỗ trợ tìm nhanh, mũi tên, Enter, Esc; không đọc lại nội dung cũ từ đĩa.

Ctrl+Alt+C mở ô lệnh độc lập. Nhập `@@` rồi Enter mở QLYC. Người dùng chọn kết nối, người xử lý và khoảng ngày; nút **View All** mở bảng **Request Processing Statistics** chỉ đọc theo năm, gồm số yêu cầu và số lượng theo tháng/tổng/trung bình. Ưu tiên cấu hình `Qlcv` tương thích FCode nếu có, đồng thời cho phép dùng kết nối LSCode để tính năng không phụ thuộc ứng dụng FCode. Không nhúng tài khoản, mật khẩu hay danh sách nhân viên/phòng ban cá nhân vào mã nguồn.

Gợi ý biến `@@` trong SQL nhúng tiếp tục là thao tác trong editor, tách biệt với ô lệnh Ctrl+Alt+C. Gợi ý field trong chuỗi hàm `f.`/`g.` lấy từ `<views>` của file XML hiện hành.

## Hoàn thiện workspace, snippet và Table Tool — 18/11/2026

- Cây File phải hiện file ASPX dưới `Main` khi thư mục tồn tại, kể cả link menu chứa thư mục ảo; chọn menu vẫn chỉ dựng cây và không tự mở file Main.
- Các mẫu SQL legacy có khóa `ss...` không trộn vào completion tên bảng/cột nữa mà xuất hiện trong tab Snippet. Biến session `@@...` dùng chung nguồn `sqlsnippet.xml`, không duy trì danh sách hard-code riêng trong WebView.
- Mọi vùng chọn database có nút sao chép tên database hiện tại; phản hồi dùng thông báo nội bộ.
- Tại Cấu trúc và dữ liệu, F5 chạy truy vấn và Home/End đi tới ô đầu/cuối của dòng đang chọn. Ctrl+G mở Go To và đặt con trỏ vào ô Tên trường. Danh sách gợi ý hiển thị nổi và có thể cuộn. Enter hoặc Nhận chuyển vùng chọn đến cột tương ứng.

Bản publish chạy trực tiếp mặc định phải là Windows x64 self-contained, không yêu cầu cài .NET Desktop Runtime. Người dùng phải giữ nguyên thư mục publish và máy vẫn cần Microsoft Edge WebView2 Runtime.
