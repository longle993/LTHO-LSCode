# Kế hoạch và nghiệm thu

## Phân trang Cấu trúc và dữ liệu, tìm nội dung tệp — 22/11/2026

- [x] Đọc theo trang SQL, giới hạn dung lượng từng trang và dựng hàng theo vùng đang xem. Chuyển trang giữ bộ lọc của lần chạy, và khóa nút chuyển trang trong lúc có bản sửa chưa lưu.
- [x] Tìm nội dung ngay khi nhập ở thanh bên trái, hủy yêu cầu cũ và trả về vị trí khớp đầu tiên của mỗi tệp.
- [x] Chạy 173 kiểm tra WebView/.NET với LocalDB, gồm truy cập dòng 1.201, phân trang bản ghi có chuỗi dài, giữ dữ liệu binary/XML và tìm trong bộ đệm chưa lưu.
- [x] Build frontend và Desktop Release. Chạy 154 kiểm tra Core và 11 kiểm tra Chrome trên bản production, gồm phân trang và tìm kiếm ở chiều rộng 375 px. Probe màn chung ghi ba điểm ngoài hai khu vực thay đổi: nút **Mở thư mục** xuống dòng và nút tìm lệnh có vùng bấm dưới 24 px ở 375 px, nút **Chức năng** xuống dòng ở 1.440 px.
- [ ] Đo thời gian và bộ nhớ trên dữ liệu ERP thực tế. Nhánh đọc SQL Server 2008 chưa được chạy trên máy chủ phiên bản đó.

## Tạo danh mục ERP từ khảo sát Create List — 19/11/2026

- [x] Đọc luồng cấu hình, các bước master/detail và điểm xuất source của Fcode.
- [x] Bổ sung chế độ sinh source vào lệnh **Tạo danh mục**. Dùng controller mẫu của dự án để mang theo cấu trúc đúng phiên bản.
- [x] Nối dò file, ánh xạ controller/bảng/cột, xem trước từng file và xuất cả gói vào thư mục mới.
- [x] Nghiệm thu bằng fixture Main/Dir/Grid/Filter, kể cả bảng detail, tên trùng và mẫu thay đổi sau khi xem trước.
- [x] Chuẩn hóa mẫu đầu vào Tạo từ mô tả và quy tắc sinh `char(16)`, `nvarchar(128)`, `int`, `numeric(19,5)`, `char(1)`; cho phép duyệt/sửa trước khi tạo.
- [x] Cố định thanh chế độ và thanh hành động, chuyển bảng sang vùng cuộn 300 px, thay select/thông báo/xác nhận hệ thống bằng component LSCode.
- [x] Kiểm tra production build và Chrome fixture ở desktop/375 px: quy tắc 7 trường, dropdown nổi, thanh cố định, xác nhận nội bộ và không tràn viewport.
- [ ] Chạy gói sinh ra trên dự án ERP thử nghiệm và đối chiếu mẫu PageMetadata/ASPX theo phiên bản.
- [ ] Nếu cần mở rộng theo Fcode, bổ sung chọn cột từ schema, cấu hình bố cục Grid/Dir/Filter, mẫu Import và bước đăng ký page/menu.

## Thiết kế lại form Menu ERP — 19/11/2026

- [x] Đổi bố cục form và panel kiểm tra, gom nút lên thanh trên.
- [x] Nối từng bảng Dir master/detail với công cụ Cấu trúc và dữ liệu trên App DB.
- [x] Tách tải bản ghi và tài nguyên, kiểm tra NULL trong transaction tạo menu.
- [ ] Đo thời gian mở F3 và xác nhận giao diện trên WebView2 với dự án ERP thật.

## Làm mới bảng, xuất INSERT và báo lỗi — 19/11/2026

- [x] Nối thao tác SQL và lưu Thiết kế danh mục với việc làm mới danh sách bảng theo kết nối. Giữ kết quả đọc đầu tiên khi cập nhật tên tab.
- [x] Xuất kiểu ngày theo metadata cột trong menu Tập lệnh và dùng thẻ thông báo nổi cho lỗi SQL và lỗi tra cứu bảng.
- [x] Build frontend và chạy 160 kiểm tra .NET, gồm tạo bảng trên SQL Server cục bộ và chạy lại INSERT trong phiên dùng `DATEFORMAT mdy`.
- [x] Chạy kiểm thử Chrome `--table-regression` với dữ liệu mẫu. Bảng mới tải cột và dữ liệu ngay lần đầu. Thẻ lỗi giữ nguyên vị trí vùng làm việc và vừa chiều rộng 375 px.
- [ ] Bộ kiểm thử Chrome đầy đủ dừng ở bước kiểm tra thứ tự dự án theo lần truy cập gần nhất, trước nhóm kiểm tra Cấu trúc và dữ liệu.

- [x] Sửa gợi ý SQL cho tham số procedure và bảng tạm. Kiểm tra bằng fixture `ALTER PROC` trong bộ Core và build frontend.

## Toast, snippet nhanh và lịch sử — 18/11/2026

- [x] Thay thông báo trạng thái/lỗi và xác nhận xóa dòng trong Cấu trúc và dữ liệu bằng component LSCode; toast nổi không đẩy lưới hoặc toolbar.
- [x] Đặt WHERE và ORDER BY cùng hàng theo tỷ lệ 70/30, tự chuyển thành một cột khi chiều rộng dưới 900 px.
- [x] Đăng ký snippet người dùng có khóa `/...` với Monaco SQL; gõ khóa sẽ mở gợi ý, Enter/Tab chèn nội dung và giữ vị trí caret.
- [x] Xóa lịch sử theo kiểu optimistic, gom các thao tác trong 140 ms và gửi `project.history.removeMany` để mỗi nhóm chỉ đọc/ghi file lịch sử một lần.
- [x] Kiểm tra frontend production build và build WebView Tests; còn nghiệm thu trực quan toast/theme và độ trễ trên lịch sử dự án lớn thực tế.

## Lưu thay đổi trên lưới dữ liệu

- [x] Cập nhật viền vùng chọn riêng để tránh dựng lại các hàng khi kéo.
- [x] Lưu chỉnh sửa và dòng mới theo lô bằng Lưu hoặc Ctrl+S. Kiểm tra trùng khóa bằng ràng buộc SQL Server và khôi phục ô khóa dòng lỗi.

## Đồng nhất lưới Cấu trúc và dữ liệu với kết quả SQL — 17/11/2026

- [x] Đồng nhất hình thức ô, hàng, header, vùng chọn và thanh cuộn với lưới kết quả SQL.
- [x] Cho chọn vùng bằng kéo chuột, chọn cả dòng/cột/toàn bảng và Ctrl+C sao chép TSV.
- [x] Bấm một lần chỉ chọn ô; phím mũi tên/Tab di chuyển ô đang chọn; chỉ nhấp đúp hoặc F2 mới mở ô nhập liệu.
- [x] Đưa menu Tập lệnh lên top-layer, tự lật lên khi thiếu chỗ và không bị toolbar cắt.
- [ ] Nghiệm thu thao tác cập nhật trên database ERP sao lưu có đủ quyền ghi.

## Lưới dữ liệu và script menu ERP — 17/11/2026

- [x] Gộp Cấu trúc và dữ liệu thành một màn dữ liệu; bỏ nút chuyển Dữ liệu/Cấu trúc.
- [x] Cho lưới dữ liệu kéo chọn vùng, Ctrl+C, nhấp đúp/F2 để sửa ô và Enter để lưu.
- [x] Sửa dropdown database thành popover nổi, hiển thị App/Sys và cho Ctrl+Alt+D đổi target của tab bảng.
- [x] Đưa Ctrl+D vào menu Tập lệnh; giữ script tạo bảng và bổ sung script xóa bảng.
- [x] Chọn menu ERP chỉ dựng cây controller; F12 và menu chuột phải sinh script INSERT `wcommand` + `command`.
- [ ] Nghiệm thu thao tác ghi trên một database ERP sao lưu có đủ quyền; kiểm thử tự động chỉ dùng fixture cục bộ.

## Thời gian chạy query — 17/11/2026

- [x] Bắt đầu đếm từ lúc gửi `sql.execute`, cập nhật chỉ báo ở thanh Kết quả mỗi 100 ms.
- [x] Khi thành công, dùng `durationMs` do SQL runner trả về; khi lỗi hoặc dừng, giữ thời gian đã trôi qua phía giao diện.
- [x] Khôi phục thời gian theo result của từng tab khi đổi tab và không hiển thị sai thời gian của query chạy ở tab khác.
- [x] Giữ chỉ báo gọn, dùng số tabular và không làm tràn thanh Kết quả ở màn hẹp.

## Viền nhận diện cửa sổ — 17/11/2026

- [x] Thêm viền 1 px quanh toàn bộ cửa sổ WebView không chrome, không thay đổi kích thước tổng thể nhờ `box-sizing: border-box`.
- [x] Dùng token riêng cho Light, Dark và High Contrast để viền đủ rõ khi nhiều cửa sổ chồng lên nhau.

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

## Kế hoạch menu Web/Mobile — 17/11/2026

Chi tiết và tiêu chí: [ERP-MENU-PERFORMANCE-MOBILE](docs/specifications/ERP-MENU-PERFORMANCE-MOBILE.vi.md).

- [x] Khảo sát tĩnh luồng menu LSCode và lazy load/cache/source Mobile trong Fcode; ghi giới hạn bằng chứng.
- [ ] Đo baseline từng lớp, số lượt SQL và độ trễ cold/warm.
- [x] Gộp request, cache tối đa 8 ngữ cảnh/TTL 5 phút, invalidate/refresh và trạng thái tải riêng.
- [x] Bỏ lượt metadata SQL riêng; giữ batch/quy tắc Web và kiểm chứng fixture không đổi.
- [x] Giải chuỗi tải/schema Mobile từ Fcode và tạo fixture sạch.
- [x] Tích hợp adapter `dvcommand` và lựa chọn Web/Mobile lazy-load.
- [x] Nối cây source bằng `sysid` và Generate Update; giữ F3/F12 Web không áp nhầm Mobile.
- [x] Build frontend/.NET, chạy Core/WebView checks và smoke bố cục preview.
- [ ] Đo baseline cold/warm, chạy SQL fixture và smoke trên SYS ERP thật; máy hiện tại bị chặn bởi yêu cầu mã hóa của SQL Server cục bộ.
- [x] Sửa batch Mobile dùng `RAISERROR + RETURN` thay `THROW` để tương thích SYS SQL Server/compatibility level cũ như TAVNHRM.
- [x] Kiểm tra cây Mobile song song với Web và ẩn thanh chuyển khi dự án không có mục Mobile.
# Điều hướng và gợi ý trong editor XML

- [x] Điều chỉnh Ctrl+G theo ảnh FCodeViewer, tô màu từng TAG, tách fields/views, bỏ items/text và liệt kê sự kiện, hàm JavaScript, mốc CSS trong DETAIL.

- [x] Mở rộng FileSections theo mẫu hai danh sách của Fcode: nhóm thẻ và mục con có tên/id/controller. Lấy vị trí từ nội dung đang sửa, hỗ trợ tìm nhanh, phím mũi tên, Enter, Esc và trả focus về editor.
- [x] Đăng ký Ctrl+Alt+C để mở ô lệnh riêng. Nhập `@@` rồi Enter mở QLYC. Từ QLYC, **View All** mở **Request Processing Statistics** dưới dạng bảng chỉ đọc. Hỗ trợ cấu hình `Qlcv` cũ của FCode và kết nối SQL đang mở trong LSCode; không ghi tài khoản hay danh sách nhân viên cá nhân vào mã nguồn.
- [x] Sửa lớp hiển thị tiêu đề ghim và thanh cuộn của bảng QLYC. Import Excel ghi đè theo mã và thông báo số nhân viên vừa nhập. Đã kiểm tra bằng Chromium với bảng mẫu 60 dòng, gồm trường hợp tệp chứa mã trùng.
- [x] Rà provider Monaco theo vùng XML, SQL và JavaScript. Xác nhận gợi ý biến FAST trong SQL nhúng và tên field trong chuỗi của f./g. đã chạy qua provider tách vùng, giới hạn phân tích XML 1 MB và bỏ kết quả completion khi Monaco hủy yêu cầu.
- [x] Build/publish frontend và Desktop; 155 WebView checks với SQL fixture, 146 Core checks và 12 smoke WebView2 đạt. Còn smoke trực tiếp Ctrl+Alt+C với cơ sở dữ liệu QLYC thật vì máy kiểm thử hiện không có cấu hình `HKCU\\Software\\FCoder\\Qlcv`.
- [x] Chuyển publish mặc định sang `win-x64 --self-contained true`; chạy chính EXE self-contained qua 12 smoke WebView2 để xác nhận không phụ thuộc .NET Desktop Runtime cài trên máy.
- [x] Thêm ORDER BY có gợi ý cột/ASC/DESC vào Cấu trúc và dữ liệu; F8 xóa theo khóa chính trong transaction và đọc lại lưới.
- [x] Bổ sung completion cho biến, biến bảng và bảng tạm được khai báo trong tài liệu SQL đang mở; thêm kiểm thử Core và WebView SQL fixture.

## Workspace Main, Snippet và điều hướng Table Tool — 18/11/2026

- [x] Dò fallback file ASPX trong `Main` theo tên khi link menu có thư mục ảo; giữ kiểm tra không thoát khỏi root và fixture controller.
- [x] Chuyển nhóm mẫu `ss...` sang danh sách Snippet; đưa biến session `@@...` vào nguồn snippet dùng chung cho SQL thường và SQL nhúng XML.
- [x] Thêm nút sao chép database cạnh bộ chọn kết nối của editor và Cấu trúc và dữ liệu.
- [x] Gán F5 cho tab bảng, Home/End cho dòng hiện hành và Ctrl+G Go To theo tên trường.
- [x] Sửa Go To để focus ô Tên trường khi mở và hiển thị danh sách nổi qua mép hộp thoại. Kiểm tra bằng Chrome với dữ liệu mẫu ở 1440×900 và 375×320.
- [x] Kiểm chứng production build, 153 Core checks, 158 WebView checks có SQL fixture và publish Windows x64 self-contained.
- [ ] Smoke thủ công với menu ERP thật có link chứa virtual directory và bảng có nhiều cột trên màn hình người dùng.

## Tối ưu khởi động — 19/11/2026

- [x] Hiển thị danh sách dự án trước khi tải snippet. Khi chọn dự án, bắt đầu mở dự án đồng thời với đọc bản nháp.
- [x] Ghi thời gian mở dự án bằng `startup.project` bên cạnh số đo danh sách dự án `startup.picker`.
- [ ] Đo thời gian mở nguội và mở lại trên máy người dùng.

## Kế hoạch Check Mail

Phạm vi và tiêu chí nghiệm thu nằm trong [SPEC.md](SPEC.md#check-mail--gửi-thư-thử-qua-smtp). Đã đối chiếu package Web, component dùng chung và kiến trúc Desktop → Application → Infrastructure. Đã triển khai tab Check Mail và adapter SMTP MailKit với thời gian chờ 30 giây. Kiểm thử dùng máy chủ loopback và địa chỉ mẫu.

1. **Dựng tab công cụ.** Đăng ký lệnh trong command palette, mở tab theo yêu cầu. Dùng Button, Select và theme hiện có. Bố trí kết nối ở trên, người gửi và người nhận ở giữa, phần soạn thư và kết quả chiếm hết chiều ngang phía dưới. Đặt thứ tự Tab theo luồng nhập. Bản đầu nhập cấu hình trực tiếp và dùng được khi chưa chọn dự án.
2. **Chốt cách nhập và kiểm tra.** Dùng một lựa chọn cho STARTTLS bắt buộc, TLS ngay khi kết nối hoặc kết nối thường. Đề xuất ban đầu là STARTTLS và cổng 587. Cho sửa cổng để dùng máy chủ nội bộ. Kiểm tra host, cổng 1–65535, From và To. Tách các địa chỉ bằng dấu chấm phẩy hoặc xuống dòng. Cho phép relay không xác thực khi bỏ trống tài khoản và mật khẩu. Khi nhập mật khẩu, yêu cầu chế độ mã hóa.
3. **Nối luồng C#.** Khai báo request/response ở Contracts, command và handler MediatR ở Application, adapter SMTP bất đồng bộ ở Infrastructure. Đề xuất dùng MailKit cho hai chế độ TLS, kiểm tra tương thích với target framework trước khi thêm gói. Giữ kiểm tra chứng chỉ máy chủ. Tạo thư UTF-8 theo Text/HTML, xử lý Bcc đúng vai trò. Gắn giới hạn thời gian và hủy khi đóng tab. Rà đường ghi log bridge trước khi truyền mật khẩu.
4. **Trả kết quả tại chỗ.** Hiện bước gặp lỗi, mã phản hồi SMTP nếu có và địa chỉ bị từ chối khi xác định được. Khi mất kết nối sau khi đã chuyển nội dung nhưng chưa có xác nhận, báo chưa xác định được kết quả và để người dùng quyết định gửi lại. Không tự động gửi lại.
5. **Kiểm thử và bàn giao.** Dùng SMTP thử nghiệm cục bộ và địa chỉ thuộc example.test. Kiểm tra Text/HTML tiếng Việt, nhiều người nhận, Bcc ẩn khỏi header, lỗi cổng/chứng chỉ/mật khẩu, bấm gửi liên tiếp, đóng tab khi đang gửi và mở công cụ khi chưa chọn dự án. Kiểm tra theme sáng/tối, thu hẹp cửa sổ, điều hướng bàn phím và log đã lọc dữ liệu. Sau khi đạt tiêu chí, cập nhật hướng dẫn sử dụng và build bản chạy trực tiếp.
