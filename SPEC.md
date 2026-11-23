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

Sau khi tạo bảng trong LSCode, ô FROM tìm được bảng mới ngay khi quay lại tab Cấu trúc và dữ liệu trên cùng kết nối. Chọn bảng tải được cấu trúc và dữ liệu ngay lần đầu. Tập lệnh INSERT của bảng dùng `dd/MM/yyyy` cho `smalldatetime` và `dd/MM/yyyy HH:mm` cho `datetime`, với chuyển đổi ngày tường minh để chạy được trên phiên SQL Server dùng `mdy`. Lỗi SQL và lỗi tra cứu bảng hiện trong thông báo nổi có thể đóng mà không thay đổi vị trí vùng làm việc.

Trong **Cấu trúc và dữ liệu** của WebView, truy vấn có thể duyệt qua dòng thứ 1.000 bằng các nút chuyển trang. SQL chỉ đọc dữ liệu của từng trang có giới hạn dung lượng, còn lưới chỉ dựng các hàng gần vùng đang xem. Chuyển trang dùng SELECT, WHERE và ORDER BY của lần chạy hiện tại. Các thao tác chọn, sửa, xóa, sao chép và xuất INSERT áp dụng cho dữ liệu trên trang đang xem. Ô **Tìm kiếm** ở thanh bên trái tự tìm theo nội dung khi gõ, hiện mỗi tệp khớp một lần và mở đến vị trí khớp đầu tiên.

## Tải menu nhanh và menu Mobile

### Tiêu chí nghiệm thu form F3 Web

- Menu mới trong `dbo.wcommand` không có NULL sau khi lưu. Mặc định hoặc trigger tạo NULL phải khiến transaction rollback.
- Các hàng nhập liệu, Loại D/V/trống, ô tick Trạng thái và thanh nút trên cùng theo [hướng dẫn Menu ERP](docs/guides/ERP-MENU-EDITOR.vi.md).
- Form dùng được khi tài nguyên còn đang dò. Panel phải liệt kê file theo nhóm và mở từng bảng master/detail tìm được trong App DB, kèm cấu trúc và dữ liệu.

Khi profile có đường dẫn source, mở dự án và tải lại web.config dùng file web.config trong thư mục source đã lưu.

Chỉ hiện thanh Web / Mobile khi tải được cây menu Mobile có mục. Dự án chỉ có Web mở thẳng danh sách Web.

Đặc tả: [tối ưu tải menu và tích hợp Mobile](docs/specifications/ERP-MENU-PERFORMANCE-MOBILE.vi.md). Đã triển khai cache/gộp request, giảm lượt SQL Web, tab Web/Mobile lazy-load, adapter `dbo.dvcommand`, tìm trong bộ nhớ và tra source Mobile bằng `sysid`. F3/F12 vẫn chỉ dành cho Web. Còn nghiệm thu baseline LAN, SQL Server ERP thật và Customize Mobile.

## Tạo danh mục ERP

- Mở được từ nhóm SQL & dữ liệu hoặc từ menu chuột phải trên cây Chức năng Web. Dùng controller mẫu cùng dự án để thu thập Main, Grid, Dir, Filter và controller chi tiết.
- Có ánh xạ bảng/cột, đổi controller, xem trước từng file và xuất gói mới. Gói giữ đường dẫn tương đối và encoding; không ghi vào database hoặc ghi đè source mẫu. Thay đổi mẫu sau preview phải yêu cầu xem lại.
- Thiết kế bảng SQL vẫn khả dụng trong cùng lệnh. Xem [đặc tả Tạo danh mục](docs/specifications/CATALOG-TABLE-DESIGNER.vi.md).
- Tạo từ mô tả nhận dòng đầu là tên danh mục và mỗi dòng sau theo mẫu `Tên trường: kiểu, ràng buộc`. Quy ước mặc định: mã `char(16)`, chuỗi `nvarchar(128)`, số `int`, số thập phân/tiền `numeric(19,5)`, trạng thái `char(1)` mặc định `1`; kỳ/năm được tách thành hai trường `int`.
- Thanh chế độ và thanh Xem trước/Tạo hoặc Xuất source cố định trong modal; chỉ phần nội dung cuộn. Bảng duyệt có chiều cao cố định, header dính và dùng dropdown LSCode trên top-layer. Lỗi, trạng thái và xác nhận bỏ nháp không dùng hộp thoại hệ thống.

Batch kiểm tra `dbo.dvcommand` phải chạy được trên các SYS FAST cũ: dùng `RAISERROR + RETURN`, không dùng cú pháp `THROW` yêu cầu SQL Server 2012 trở lên.
# Điều hướng XML và quản lý yêu cầu

Trong **View All** của QLYC, chọn **Xuất mẫu Excel**, điền danh sách rồi dùng **Import Excel**. Bảng nhóm và lọc theo mã bộ phận. Hai ô chọn điều khiển Số yêu cầu và Số lượng dưới mỗi kỳ. Tiêu đề cột sắp xếp tăng, giảm rồi trả về thứ tự nguồn trong từng nhóm.

Sheet đầu tiên của tệp `.xlsx` có tiêu đề `ma_bp`, `ma_nv`, `ten_nv` theo thứ tự dưới đây. `ma_nv` là mã người xử lý trong dữ liệu QLYC. Mẫu định dạng cột dưới dạng văn bản để giữ số 0 đầu mã. LSCode kiểm tra trùng sau khi bỏ khoảng trắng hai đầu và bỏ phân biệt hoa/thường. Tệp lỗi giữ nguyên danh sách đang dùng. Khi import thành công, LSCode thêm mã mới và ghi đè bộ phận, tên của mã đã có, đồng thời giữ các mã vắng mặt trong tệp. Thông báo nhập hiển thị số nhân viên trong lần nhập vừa hoàn tất.

Hai hàng tiêu đề bảng luôn hiển thị khi cuộn dọc. Cột mã và tên đã ghim vẫn hiển thị khi cuộn ngang. Vùng bảng có thanh cuộn theo các chiều mà dữ liệu vượt khung.

```tsv
ma_bp	ma_nv	ten_nv
KT	NV001	Nguyễn Văn An
KT	NV002	Trần Minh Bình
```

Thống kê mở ở bộ phận **LTHO** và chỉ hiển thị người có bộ phận trong danh sách đã import. Sau khi import, bộ lọc trở về LTHO. Hai nút icon nhập/xuất mẫu Excel nằm trên thanh công cụ. Bảng cuộn đến tháng hiện tại, mặc định ghim mã và tên nhân viên, cho phép kéo chọn vùng và Ctrl+C để sao chép. Dòng đầu mỗi bộ phận có màu nhận diện. Bật cả hai ô để xem số yêu cầu bên trái và số lượng bên phải trong từng tháng, Tổng và TB.

Ctrl+G trong XML ERP hiển thị TAG và DETAIL với màu nhận diện cho từng nhóm. Nhóm fields có Start tại field đầu tiên và End tại field cuối cùng. Nhóm views liệt kê các view theo `id`. Nhóm command liệt kê sự kiện theo thứ tự nguồn, script chỉ liệt kê tên hàm JavaScript, và css có End/Start. Không đưa `items` hoặc `text` vào danh sách điều hướng. Chọn một mục để nhảy tới vị trí tương ứng trong buffer đang sửa.

Danh sách Ctrl+G lấy trực tiếp từ buffer đang sửa và hỗ trợ tìm nhanh, mũi tên, Enter, Esc; không đọc lại nội dung cũ từ đĩa.

Ctrl+Alt+C mở ô lệnh độc lập. Nhập `@@` rồi Enter mở QLYC. Người dùng chọn kết nối, người xử lý và khoảng ngày; nút **View All** mở bảng **Request Processing Statistics** chỉ đọc theo năm, gồm số yêu cầu và số lượng theo tháng/tổng/trung bình. Ưu tiên cấu hình `Qlcv` tương thích FCode nếu có, đồng thời cho phép dùng kết nối LSCode để tính năng không phụ thuộc ứng dụng FCode. Không nhúng tài khoản, mật khẩu hay danh sách nhân viên/phòng ban cá nhân vào mã nguồn.

Gợi ý biến `@@` trong SQL nhúng tiếp tục là thao tác trong editor, tách biệt với ô lệnh Ctrl+Alt+C. Gợi ý field trong chuỗi hàm `f.`/`g.` lấy từ `<views>` của file XML hiện hành.

## Hoàn thiện workspace, snippet và Table Tool — 18/11/2026

Hộp chọn dự án có thể hiện trước khi tải snippet. Khi chọn dự án, việc đọc bản nháp chưa lưu chạy đồng thời với mở dự án. Bản nháp vẫn được khôi phục sau khi áp dụng ngữ cảnh dự án.

- Cây File phải hiện file ASPX dưới `Main` khi thư mục tồn tại, kể cả link menu chứa thư mục ảo; chọn menu vẫn chỉ dựng cây và không tự mở file Main.
- Các mẫu SQL legacy có khóa `ss...` không trộn vào completion tên bảng/cột nữa mà xuất hiện trong tab Snippet. Biến session `@@...` dùng chung nguồn `sqlsnippet.xml`, không duy trì danh sách hard-code riêng trong WebView.
- Mọi vùng chọn database có nút sao chép tên database hiện tại; phản hồi dùng thông báo nội bộ.
- Tại Cấu trúc và dữ liệu, F5 chạy truy vấn và Home/End đi tới ô đầu/cuối của dòng đang chọn. Ctrl+G mở Go To và đặt con trỏ vào ô Tên trường. Danh sách gợi ý hiển thị nổi và có thể cuộn. Enter hoặc Nhận chuyển vùng chọn đến cột tương ứng.

Bản publish chạy trực tiếp mặc định phải là Windows x64 self-contained, không yêu cầu cài .NET Desktop Runtime. Người dùng phải giữ nguyên thư mục publish và máy vẫn cần Microsoft Edge WebView2 Runtime.

## Check Mail — gửi thư thử qua SMTP

Mục tiêu là giúp người cấu hình ERP kiểm tra một cấu hình SMTP từ LSCode, độc lập với kết nối SQL. Ảnh tham chiếu xác định các trường và thứ tự thao tác.

**Tiêu chí nghiệm thu**

- Từ Ctrl+Shift+P, tìm được công cụ bằng “Check Mail” hoặc “Kiểm tra gửi mail”. Mở lại lệnh đưa người dùng về tab đã mở. Chỉ tải màn hình khi mở công cụ.
- Form có máy chủ, cổng và chế độ bảo mật. Khối người gửi có email, tên hiển thị, tài khoản, mật khẩu và nút hiện/ẩn. Khối người nhận có To, Cc và Bcc, hỗ trợ nhiều địa chỉ. Bên dưới là tiêu đề, lựa chọn Text/HTML, nội dung, kết quả và nút Gửi thử. Khi thu hẹp cửa sổ, hai khối địa chỉ xếp dọc theo thứ tự nhập.
- Mỗi lần bấm Gửi thử hợp lệ chỉ gửi một thư. Hiển thị lỗi ngay tại trường sai. Khi đang gửi, khóa thao tác gửi lặp. Kết quả phân biệt lỗi kết nối, TLS, xác thực, địa chỉ bị từ chối và máy chủ đã tiếp nhận thư. Việc tiếp nhận chỉ xác nhận giao dịch SMTP, chưa xác nhận thư đến hộp thư người nhận.
- Dùng theme hiện tại. Che mật khẩu mặc định, chỉ giữ trong bộ nhớ khi tab đang mở. Nhật ký và dữ liệu khôi phục phiên phải loại thông tin xác thực và nội dung thư.
