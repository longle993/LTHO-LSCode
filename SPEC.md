# Mục tiêu LSCode

Menu chuột phải trong cây nguồn ERP có mục **Xóa file**. Hộp xác nhận hiện đường dẫn và cảnh báo xóa vĩnh viễn. File đang mở cần được đóng trước khi xóa. Cây nguồn bỏ file sau khi thao tác thành công.

Khi mở source của một menu ERP, cây File và danh sách tài nguyên F3 lần theo Grid con và các lời gọi form có tên cố định trong Main hoặc controller. Mỗi form liên quan hiện các file .f/.xml tìm được, kể cả form gọi tiếp form khác.

Danh sách phiên bản trong **Source Collection** xổ ra theo dạng cây. Phân hệ/sản phẩm lấy từ folder mẹ là nút cha in đậm, có nền và có mũi tên đóng/mở; các folder con là phiên bản được thụt vào. Nhóm chứa phiên bản đang chọn tự mở, các nhóm khác mặc định thu gọn. Trong từng nhóm, phiên bản mới được xếp trước phiên bản cũ; ô tìm phiên bản tìm được cả mã nhóm. Vùng xổ xuống rộng tối thiểu 420 px khi màn hình cho phép. Folder hướng dẫn có tên bắt đầu bằng `ZZZ` không được nạp vào catalog hoặc cây phiên bản.

Mỗi lần bấm nền editor ngoài các pop-up entity F12 hoặc nhấn Esc chỉ đóng pop-up mở gần nhất. Khóa cuộn giới hạn ở nền editor cho đến khi đóng pop-up cuối. Các chức năng ngoài editor vẫn hoạt động.

Xuất CSV từ kết quả SQL dùng UTF-8 có BOM để Excel nhận đúng dấu tiếng Việt khi mở trực tiếp.

Bảng lệnh có biểu tượng bánh răng **Chỉnh sửa phím tắt** cạnh nút đóng, mở trực tiếp trang Phím tắt. Phím tắt cá nhân được lưu theo tài khoản Windows trong file mã hóa riêng ngoài thư mục cài đặt. Bản cập nhật dùng lại các thiết lập đã lưu khi giữ cùng thư mục dữ liệu người dùng.

Sau khi F5, truy vấn trả bảng tự mở **Kết quả**, kể cả bảng rỗng. SQL Server trả số dòng ảnh hưởng âm thì bỏ qua số đếm. Lệnh thay đổi dữ liệu thành công hiển thị **Thực hiện lệnh thành công, đã thay đổi N dòng.**; lệnh thành công không trả bảng, số dòng hay thông báo hiển thị **Các lệnh đã được thực hiện thành công.**

Mở **SQL Profiler** sẽ tự lấy server và database từ tab hiện tại hoặc dự án, điền bộ lọc và bắt đầu thu sự kiện. Dùng đường dẫn `PROFILER.EXE` đã chọn hoặc bản SSMS mặc định khi có. Khi thiếu SSMS, dùng SQL Trace tích hợp cho SQL Server 2008, máy chủ SQL2014 có tài khoản Profiler riêng hoặc khi người dùng nhập tài khoản thu riêng; SQL Server 2012 trở lên có thể dùng Extended Events. WebView mặc định dùng tài khoản thu riêng cho Profiler và cho phép nhập tài khoản khác để chạy thủ công. Batch tạo/dọn trace không chứa cú pháp `THROW` để tương thích SQL Server 2008. Người dùng vẫn có thể chọn `PROFILER.EXE` trong Cài đặt hoặc ngay trong tab Profiler.

Sau khi Dừng rồi Chạy lại, SQL Profiler tiếp tục nối sự kiện mới vào lưới hiện có. Dữ liệu đã thu chỉ được xóa khi người dùng bấm **Xóa**.

Trong lưới SQL Profiler, cột **ID** hiển thị tên ứng dụng đã tạo kết nối (`ApplicationName`/`client_app_name`), ví dụ `1` hoặc `.Net SqlClient Data Provider`. Nhấp một lần vào ô cột **SQL** chỉ chọn/xem; nhấp đúp mới sao chép toàn bộ nội dung gốc và hiện thông báo **Đã sao chép SQL.**, kể cả khi ô chỉ hiển thị một phần. Sự kiện lỗi SQL có severity từ 11 phải được ghép với câu lệnh đang chạy: câu SQL gây lỗi được tô nền đỏ, còn dòng thông báo ngay sau nó hiển thị chữ đỏ. Cảnh báo severity dưới 11, gồm `Warning: Null value is eliminated...`, vẫn hiển thị nhưng không tô đỏ cảnh báo hoặc câu SQL. Bộ thu phải lấy cả sự kiện bắt đầu câu lệnh, severity và thông báo lỗi để vẫn hiển thị được câu SQL không phát sinh sự kiện hoàn tất.

Ô **SQL đang chọn** của Profiler hỗ trợ Ctrl+A, Ctrl+C và nút **Sao chép SQL** với toàn bộ nội dung gốc. SQL dài qua nhiều khối dữ liệu trace không được dừng ở ký tự NUL nội bộ hoặc mất các dòng phía sau.

Khi mở hoặc chuyển sang file `.xml`, panel Kết quả/Thông báo/Vấn đề tự thu gọn để dành chiều cao cho editor. Dữ liệu trong panel được giữ nguyên và người dùng có thể mở lại ngay từ thanh panel.

Trong **Dự án của bạn**, nút **Đồng bộ** đọc danh mục QLYC bằng `ns_getListDept`. Khi chưa có tên LSCode hoặc danh mục trả rỗng cho tên đã lưu, dùng `CurrUser` của FSD Studio nếu xác định được trong phiên Windows hiện tại. Đồng bộ thêm mã dự án mới, cập nhật profile đang sử dụng, giữ profile ngừng dùng và giải thích kết quả rỗng.

**Đề xuất giao diện.** Phân loại nút thành Primary, Secondary, Tertiary/Ghost, Destructive và Icon Button. Người dùng tùy chỉnh màu theo loại và theo theme, xem trước, lưu và đặt lại. Nghiệm thu bao gồm tương phản, thao tác bàn phím và tương thích với cấu hình màu nhấn cũ. Xem [đặc tả nút và theme](docs/specifications/BUTTON-THEME.vi.md).

Trong tab SQL, gõ `@` để gợi ý tham số của `CREATE/ALTER PROC` và biến khai báo trong tài liệu. Gõ `#` để gợi ý bảng tạm từ `CREATE TABLE` và `SELECT INTO`. Gợi ý lọc theo tiền tố và thay cả ký tự `@` hoặc `#` khi chèn.

Nâng cấp đã triển khai: [Generate Update và SQL không giới hạn timeout](docs/guides/ADVANCE-NOTE.vi.md#đặc-tả-nâng-cấp--17112026). Nghiệm thu: nhấp đúp ERP lấy source vào cây dưới danh sách yêu cầu, lọc `.f` cho Dir/Filter/Grid, tìm và thêm File Path; SQL Object nhận tên table/store/function để sinh DROP + CREATE; Ctrl+D tại Cấu trúc và dữ liệu lưu `bảng: WHERE` vào SQL Data để sinh INSERT; xuất gói Generate Update và giữ khả năng hủy SQL chủ động.

Cung cấp một IDE Windows cho người phát triển và bảo trì FAST ERP. Người dùng có thể chọn dự án, tìm menu và controller, sửa source, làm việc với cơ sở dữ liệu và dùng các công cụ hỗ trợ trong cùng workbench.

| Phạm vi | Tiêu chí nghiệm thu |
| --- | --- |
| Editor và workspace | Mở, tìm kiếm, định dạng, chia pane và lưu file. Kiểm tra thay đổi ngoài ứng dụng. Tệp XML luôn được lưu bằng UTF-8 có BOM. Khai báo encoding, nếu có, khớp với định dạng khi lưu. Các định dạng khác giữ encoding khi lưu. Kéo file từ Explorer vào cửa sổ sẽ mở file; Ctrl+Shift+P mở bảng lệnh và focus ô tìm. Snippet người dùng có khóa bắt đầu bằng `/` xuất hiện ngay trong gợi ý SQL; Enter/Tab thay khóa bằng nội dung và đặt caret theo vị trí đã lưu. Cửa sổ không chrome có viền bao theo theme để phân biệt khi chồng nhiều cửa sổ. Toolbar editor không hiện lối tắt Generate Update/SQL Watch; ô tìm và target được co giãn nhưng nút Chạy luôn đọc trên một dòng khi đổi kích thước cửa sổ. |
| Dự án, menu và source ERP | Chọn hoặc đồng bộ profile, import/export qua tệp, giữ đúng ngữ cảnh dự án khi mở menu, controller và Source Collection. Panel Chức năng có Web/Mobile; Web giữ `wcommand`, Mobile lazy-load `dvcommand`, cache theo project/SYS/App/identity/loại menu và refresh chủ động. Nhấp đúp tên menu tải cây controller theo link hoặc `sysid` rồi chuyển sang tab File nhưng không mở hoặc trỏ vào file nào; bấm mã menu Web mở form sửa như F3. Khi quay lại tab Chức năng, trạng thái mở của cây và toàn dòng menu vừa chọn vẫn được giữ và tô đậm. Menu chuột phải có **Làm mới menu** để bỏ qua cache và tải các mục vừa thêm mà không mất nhánh đang mở hoặc lựa chọn còn hợp lệ. F12 và các thao tác sửa/sinh script trong menu chuột phải chỉ áp dụng Web để sinh script INSERT `wcommand` và `command`. |
| SQL và danh mục | Target App/SYS gắn với từng tab. Truy vấn có thể hủy. Cấu trúc và dữ liệu có SELECT, WHERE chiếm 70% và ORDER BY chiếm 30% trên cùng hàng; màn hẹp tự xếp dọc. Trạng thái/lỗi dùng toast nội bộ và xác nhận xóa dùng modal LSCode, không gọi hộp thoại hệ thống. Lưới dùng cùng ngôn ngữ với kết quả SQL: kéo chọn vùng, chọn dòng/cột/toàn bảng, Ctrl+C, sửa trực tiếp ô và F8 xóa các dòng được chọn theo khóa chính sau bước xác nhận; dropdown hiển thị App/Sys, Ctrl+Alt+D đổi target của tab bảng. Ctrl+D tại bảng lưu khóa dữ liệu vào Generate Update; menu Tập lệnh mở trên top-layer để không bị toolbar cắt. Nhấp chuột phải trên bề mặt dữ liệu mở cùng menu với nút Tập lệnh, gồm đóng băng đến cột đang trỏ, thêm key vào SQL Data, tạo bảng, INSERT kết quả hiện tại và xóa bảng; chuột phải trong ô đang nhập vẫn giữ menu soạn thảo của hệ thống. Đóng băng giữ cột số dòng và tất cả cột từ đầu đến cột đích khi cuộn ngang; chọn lại chức năng từ toolbar hoặc menu chuột phải sẽ bỏ đóng băng. Gợi ý SQL gồm schema, cột, biến, bảng tạm và snippet nhanh khai báo trong ứng dụng. Thanh Kết quả đếm thời gian mỗi 100 ms khi đang chạy và giữ tổng thời gian sau khi hoàn tất, lỗi hoặc dừng. |
| Công cụ tích hợp | Dùng Generate Update, Template Studio, Profiler và SQL Watch trên WebView. Ctrl+Shift+4 mở Generate Update. Cài đặt phím tắt là một tab riêng, có tìm kiếm và nhóm theo chức năng. Thao tác breakpoint/step được chuyển sang SSDT. |
| Phân phối | Build WebView và các project Template Studio từ checkout LSCode. Xác minh gói cài và smoke trên Windows x64. |

Xem [ma trận WebView](docs/specifications/WEBVIEW-MIGRATION.vi.md) và [đặc tả Table Designer](docs/specifications/CATALOG-TABLE-DESIGNER.vi.md) để tra tiêu chí chi tiết. [PLAN.md](PLAN.md) ghi phạm vi còn cần nghiệm thu theo các tài liệu hiện có.

Trong **Dynamic Template Studio**, tự lưu chờ 800 ms sau chỉnh sửa và ghi nền theo từng lượt. Các thay đổi thực hiện trong lúc ghi được lưu ở lượt tiếp theo. Nút bút chì cạnh tên bảng hoặc field mở ngay ô đổi tên tại chỗ. Ô From hiển thị ví dụ JSONPath theo chế độ Object hoặc Array khi chưa có giá trị.

Lưới Cấu trúc và dữ liệu hiện viền trên mỗi ô được quét và dùng nền xám cho ô nhập. Lưu hoặc Ctrl+S ghi cả lô dữ liệu, gồm thay đổi khóa cho phép ghi. Khi có xung đột khóa, toàn bộ lần lưu được hoàn tác và các ô khóa của dòng lỗi được đánh dấu sau khi khôi phục giá trị.

Sau khi tạo bảng trong LSCode, ô FROM tìm được bảng mới ngay khi quay lại tab Cấu trúc và dữ liệu trên cùng kết nối. Chọn bảng tải được cấu trúc và dữ liệu ngay lần đầu. Tập lệnh INSERT của bảng dùng `dd/MM/yyyy` cho `smalldatetime` và `dd/MM/yyyy HH:mm` cho `datetime`, với chuyển đổi ngày tường minh để chạy được trên phiên SQL Server dùng `mdy`. Lỗi SQL và lỗi tra cứu bảng hiện trong thông báo nổi có thể đóng mà không thay đổi vị trí vùng làm việc.

Khi F5 thực thi thành công một đoạn SQL không trả tập kết quả, LSCode tự mở tab **Thông báo**. Nội dung do SQL Server trả về được giữ nguyên; nếu không có nội dung, hiển thị câu tiếng Việt **Các lệnh đã được thực hiện thành công.**

Khi F5 gặp lỗi SQL, LSCode tự mở **Thông báo** trong panel dưới và giữ lỗi theo đúng tab SQL; vị trí lỗi vẫn được liệt kê trong **Vấn đề** để nhảy đến dòng tương ứng. Lỗi SQL không được hiện ở banner phía trên editor hoặc thông báo nổi toàn cục. Banner đỏ phía trên editor chỉ dành cho lỗi phân tích XML/DTD của file `.xml`/`.f`, đặc biệt entity chưa khai báo.

Trong **Cấu trúc và dữ liệu** của WebView, truy vấn có thể duyệt qua dòng thứ 1.000 bằng các nút chuyển trang. SQL chỉ đọc dữ liệu của từng trang có giới hạn dung lượng, còn lưới chỉ dựng các hàng gần vùng đang xem. Chuyển trang dùng SELECT, WHERE và ORDER BY của lần chạy hiện tại. Các thao tác chọn, sửa, xóa, sao chép và xuất INSERT áp dụng cho dữ liệu trên trang đang xem. Ô **Tìm kiếm** ở thanh bên trái tự tìm theo nội dung khi gõ, hiện mỗi tệp khớp một lần và mở đến vị trí khớp đầu tiên.

Khi lưu **Dynamic Template Studio**, lỗi kiểm tra dữ liệu chỉ rõ template, bảng, field và ô nhập nếu xác định được. Chọn lỗi sẽ mở đúng vị trí sửa. Thuộc tính template nằm dưới danh sách bảng ở cột trái. Chọn một dòng field sẽ mở thuộc tính field phía trên lưới.

## Tải menu nhanh và menu Mobile

Nhấp mã `wcommand` trong cây Web hoặc kết quả tìm kiếm mở form sửa menu giống F3.

Ô tìm chức năng lọc thêm theo trường Main, gồm cả tên file đầy đủ hoặc một phần, không phân biệt hoa thường.

### Tiêu chí nghiệm thu form F3 Web

- Menu mới trong `dbo.wcommand` không có NULL sau khi lưu. Mặc định hoặc trigger tạo NULL phải khiến transaction rollback.
- Khi nhấn **Thêm mới** từ một menu có mã kết thúc bằng số, form giữ nguyên `wmenu_id0` của menu đang chọn và lấy `wmenu_id` kế tiếp cùng cấp bằng cách tăng hậu tố cuối, giữ độ rộng phần số; mã đã tồn tại phải được bỏ qua.
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

F12 tại tham chiếu entity XML mở một callout neo gần vị trí con trỏ, không che toàn bộ editor bằng modal. Callout ghi tên entity và đường dẫn nguồn, mặc định chỉ hiển thị đúng giá trị hoặc nội dung file mà entity tham chiếu. F12 tiếp tục hoạt động trong callout, kể cả khi khai báo entity con chỉ có ở DTD của file XML gốc; mỗi lần điều hướng mở thêm một callout con lệch theo tầng và giữ nguyên chuỗi callout cha. Đóng callout lá chỉ đóng callout đó, còn đóng một callout cha đóng toàn bộ nhánh hậu duệ. Tùy chọn **Hiện toàn bộ file** chuyển sang nội dung nguồn đầy đủ; **Mở trong tab** và Ctrl+F12 giữ luồng điều hướng toàn file. Khi còn ít nhất một callout, nền ứng dụng không cuộn hoặc nhận thao tác chuột; nội dung trong các callout vẫn cuộn bình thường.

Ctrl+Alt+C mở ô lệnh độc lập. Nhập `@@` rồi Enter mở QLYC. Người dùng chọn kết nối, người xử lý và khoảng ngày; nút **View All** mở bảng **Request Processing Statistics** chỉ đọc theo năm, gồm số yêu cầu và số lượng theo tháng/tổng/trung bình. Ưu tiên cấu hình `Qlcv` tương thích FCode nếu có, đồng thời cho phép dùng kết nối LSCode để tính năng không phụ thuộc ứng dụng FCode. Không nhúng tài khoản, mật khẩu hay danh sách nhân viên/phòng ban cá nhân vào mã nguồn.

### Nâng cấp QLYC — 27/11/2026

- Lần đầu mở QLYC, ô **Người xử lý** dùng tên người sử dụng lưu từ luồng **Dự án của bạn** và **Đồng bộ**. Người dùng vẫn có thể sửa hoặc xóa bộ lọc này. Khi chưa lưu tên, ô để trống.
- **View All** sử dụng kết nối đang chọn trên màn danh sách. Thanh điều kiện của thống kê chỉ hiện năm và các nút xem thống kê, quay lại QLYC. Bộ lọc bộ phận, ghim cột và hai ô chọn chỉ số vẫn dùng được trong bảng.
- Mỗi bộ phận có một dòng **Tổng bộ phận** sau các dòng nhân viên. Dòng tổng cộng số của từng cột đang hiển thị trong phạm vi bộ phận, gồm tháng, Tổng và TB. Việc lọc, sắp xếp và ẩn chỉ số phải giữ số liệu đúng với nhóm.
- Khối **Category** nằm cạnh danh sách yêu cầu ở màn rộng và xếp dưới danh sách ở màn hẹp. Sau khi tải danh sách, khối mở dữ liệu của yêu cầu đầu tiên. Khi chọn yêu cầu khác, khối hiển thị năm cột chỉ đọc **Chọn**, **Đầu mục**, **Số lượng**, **Lập trình** và **Tester** của yêu cầu đó, kể cả khi truy vấn trước hoàn tất muộn. Tải lại danh sách cũng tải lại đầu mục. Hiện trạng thái đang tải, rỗng hoặc lỗi ngay trong khối Category.

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

## Chẩn đoán khai báo controller ERP — 23/11/2026

Khi người dùng bấm trực tiếp một chức năng ERP, LSCode mở cây source, kiểm tra toàn bộ file `.f` và `.xml` thuộc controller chính và controller con, rồi hiện thông báo lỗi đầu tiên với tên khai báo, vị trí và đường dẫn. File có lỗi khai báo phải được tô đỏ ngay trong cây; file hợp lệ và file chỉ có cảnh báo semantic không bị tô đỏ.

Khi mở file lỗi, editor hiển thị một thanh đỏ cố định phía trên nội dung, gồm thông báo, dòng, cột và đường dẫn file. Kết quả phải theo từng document nên hai pane editor không ghi đè cảnh báo của nhau. Việc sửa source dùng buffer chưa lưu; thanh đỏ, marker và màu đỏ của file chỉ biến mất sau khi parser xác nhận khai báo đã hợp lệ, rồi xuất hiện lại nếu lỗi được tạo lại.

Phạm vi hiện tại phát hiện lỗi XML/DTD, đặc biệt entity chưa khai báo như `&zHRMListCreate;`. Các entity này có thể chứa đoạn hàm JavaScript/SQL nhúng theo cấu trúc FAST. Phân tích tĩnh mọi lời gọi hàm JavaScript toàn cục không thuộc tiêu chí của đợt này vì cần mô hình riêng cho runtime FAST và các thư viện ngoài.
