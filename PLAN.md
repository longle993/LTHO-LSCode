# Kế hoạch và nghiệm thu

- [x] Hiển thị phiên bản Source Collection thành cây phân hệ/phiên bản theo folder mẹ/con, mở/đóng từng nhánh, tăng chiều rộng vùng chọn, sắp phiên bản mới trước và loại folder `ZZZ`.

- [x] Dò source form được gọi từ Main và controller. Kiểm tra bằng fixture có hai form con, lời gọi lồng và vòng tham chiếu.

- [x] Giới hạn vùng bấm đóng entity vào editor. Esc đóng từng pop-up và thanh công cụ vẫn hoạt động.

- [x] Bấm nền để đóng từng pop-up entity theo thứ tự mở gần nhất.

- [x] Thêm dấu nhận diện UTF-8 vào CSV xuất từ kết quả SQL.

- [x] Đặt biểu tượng bánh răng mở trang Phím tắt trên tiêu đề Bảng lệnh.

- [x] Thêm lối mở trang Phím tắt từ Bảng lệnh và lưu cấu hình cá nhân vào file DPAPI riêng.

- [x] Dùng thao tác F3 khi nhấp mã menu Web trong cây hoặc kết quả tìm kiếm.

- [x] Bổ sung tìm chức năng theo Main từ dữ liệu menu đã tải.

- [x] Bỏ số dòng ảnh hưởng âm và chọn tab dưới theo tập kết quả sau F5.
- [x] Đổi thông báo số dòng ảnh hưởng thành câu thành công tiếng Việt: **Thực hiện lệnh thành công, đã thay đổi N dòng.**
- [x] Giữ đủ SQL Profiler dài khi sao chép; loại bỏ NUL phân khối và kiểm tra Ctrl+A/C với fixture 112 dòng.
- [x] Thêm cột **ID** vào SQL Profiler từ tên ứng dụng kết nối và giữ thao tác chọn/sao chép đúng cột SQL.

## Phân luồng lỗi SQL và entity — 26/11/2026

- [x] Gắn nguồn marker `sql`/`xml` vào diagnostic; banner phía trên editor chỉ nhận lỗi XML/DTD.
- [x] Khi F5 lỗi, lưu thông báo vào kết quả của tab SQL, tự mở **Thông báo** bên dưới và vẫn giữ vị trí trong **Vấn đề**.
- [x] TypeScript/Vite production build đạt; Chrome `--sql-error-routing-regression` đạt 6 kiểm tra, đồng thời các regression SQL thành công, controller entity và menu đều đạt.

## Thông báo F5 cho lệnh SQL không có tập kết quả — 26/11/2026

- [x] Khi F5 chạy thành công `CREATE/ALTER PROCEDURE` hoặc lệnh SQL khác không trả bảng, tự chuyển sang tab **Thông báo**.
- [x] Giữ thông báo thật từ SQL Server; nếu server không trả nội dung, hiện **Các lệnh đã được thực hiện thành công.**
- [x] TypeScript/Vite production build đạt; Chrome fixture riêng đạt 5 kiểm tra, gồm F5 store, câu tiếng Việt và SELECT vẫn ở tab Kết quả.

- [x] Khi lưu hoặc lưu thành tệp XML, dùng UTF-8 có BOM. Cập nhật khai báo encoding nếu có.

- [x] Tách tải kết nối khỏi mở dự án. SQL chưa chọn kết nối riêng chờ App của dự án trước khi chạy. Hai lệnh Close trong Lịch sử chỉ xóa lịch sử, còn đóng cửa sổ dọn phiên khôi phục sau khi xử lý tab chưa lưu.

- [x] Khi lọc bảng có chuỗi vượt giới hạn hiển thị, vẫn trả dòng và các cột còn lại. Trang có dữ liệu rút gọn được đánh dấu chỉ đọc và chặn xuất INSERT.

- [x] Chèn snippet SQL giữ nguyên ký tự trong tên thủ tục và giá trị tham số. Kiểm tra bằng bộ phân tích snippet của Monaco với mẫu thủ tục phân kỳ và snippet nhập nhanh.

- [x] SQL Profiler dùng tài khoản thu riêng mặc định. Trong cây ERP và kết quả lọc menu, nhấp tên một lần để tải cây controller và chuyển sang File; nhấp Menu ID để mở tệp chính.

## Phân loại nút và tùy chỉnh màu

- [x] Đối chiếu component WebView, style WPF và luồng lưu theme. Ghi [đề xuất triển khai](docs/specifications/BUTTON-THEME.vi.md).
- [ ] Kiểm kê nút theo màn và thống nhất variant.
- [x] Thêm token cho năm loại, lưu cấu hình màu theo theme và xem trước trong Settings WebView.
- [ ] Chuyển các màn theo đợt, kiểm tra theme và bàn phím, sau đó ánh xạ sang WPF.

## Phân trang Cấu trúc và dữ liệu, tìm nội dung tệp — 22/11/2026

- [x] Đọc theo trang SQL, giới hạn dung lượng từng trang và dựng hàng theo vùng đang xem. Chuyển trang giữ bộ lọc của lần chạy, và khóa nút chuyển trang trong lúc có bản sửa chưa lưu.
- [x] Tìm nội dung ngay khi nhập ở thanh bên trái, hủy yêu cầu cũ và trả về vị trí khớp đầu tiên của mỗi tệp.
- [x] Luồng hiện tại tìm theo tên tệp, theo nội dung hoặc kết hợp sau khi nhấn Enter hay bấm Tìm. Kết quả hiện dần theo cây thư mục, giữ vị trí khớp đầu tiên của mỗi tệp.
- [x] Mặc định tìm trong Toàn dự án. Liệt kê các thư mục con trực tiếp trong AppData hoặc App_Data, bên dưới Controller hoặc Controllers. Kết quả tiếp tục hiện dần theo cây từ gốc dự án.
- [x] Chạy 173 kiểm tra WebView/.NET với LocalDB, gồm truy cập dòng 1.201, phân trang bản ghi có chuỗi dài, giữ dữ liệu binary/XML và tìm trong bộ đệm chưa lưu.
- [x] Build frontend và Desktop Release. Chạy 154 kiểm tra Core và 11 kiểm tra Chrome trên bản production, gồm phân trang và tìm kiếm ở chiều rộng 375 px. Probe màn chung ghi ba điểm ngoài hai khu vực thay đổi: nút **Mở thư mục** xuống dòng và nút tìm lệnh có vùng bấm dưới 24 px ở 375 px, nút **Chức năng** xuống dòng ở 1.440 px.
- [ ] Đo thời gian và bộ nhớ trên dữ liệu ERP thực tế. Nhánh đọc SQL Server 2008 chưa được chạy trên máy chủ phiên bản đó.

## Không chọn file khi duyệt menu ERP — 26/11/2026

- [x] Nhấp tên menu chỉ chuyển sang File và mở cây controller, không tô/chọn Main hoặc file editor đang active từ trước; tab editor và dữ liệu chưa lưu được giữ nguyên.
- [x] Bấm mã menu vẫn mở và chọn Main; khi người dùng mở một file trong cây, lựa chọn file hoạt động lại bình thường.
- [x] TypeScript/Vite production build đạt; Chrome `--menu-regression` đạt 13 kiểm tra và `--controller-diagnostics-regression` đạt 7 kiểm tra. Bộ UI tổng quát hiện dừng trước phạm vi này tại locator cũ **Chạy F5**.

## Tạo menu cùng cấp từ menu đang chọn — 25/11/2026

- [x] Đọc `wmenu_id` và `wmenu_id0` thực tế của menu đang chọn trong `dbo.wcommand`, không suy diễn mã cha từ hai đoạn đầu.
- [x] Khi nhấn **Thêm mới**, giữ mã cha và tăng hậu tố số cuối của mã cây, giữ số 0 đầu; bỏ qua mã đã tồn tại và giữ luồng cũ cho mã không có hậu tố số.
- [x] TypeScript/Vite production build, 114 WebView checks và Chrome `--menu-regression` 11 kiểm tra đạt. Kiểm tra tích hợp SQL đã được bổ sung nhưng chưa chạy vì máy hiện tại không có SQL instance cục bộ; chưa nghiệm thu trên SYS ERP thật.

## Giữ trạng thái và làm mới cây menu ERP — 25/11/2026

- [x] Chọn một menu tải controller/diagnostics và chuyển sang tab File; component cây Chức năng vẫn giữ state để khi quay lại, các nhánh cha đang mở không bị reset.
- [x] Tô nền và in đậm toàn bộ dòng menu đang chọn, gồm cả tên và mã menu.
- [x] Thêm **Làm mới menu** vào menu chuột phải, gọi lại `project.commands` với `refresh: true`, giữ nhánh mở và lựa chọn nếu menu vẫn tồn tại.
- [x] TypeScript/Vite production build và .NET build sạch; Chrome fixture `--menu-regression` đạt 11 kiểm tra, gồm chuyển sang File, quay lại giữ cây/dòng chọn và refresh menu.

## Menu Tập lệnh bằng chuột phải — 23/11/2026

- [x] Nhấp chuột phải trên bề mặt dữ liệu của tab Cấu trúc và dữ liệu mở đúng menu Tập lệnh hiện có với đủ bốn thao tác: thêm key vào SQL Data, tạo bảng, INSERT kết quả hiện tại và xóa bảng.
- [x] Dùng chung một popover cho nút toolbar và vị trí con trỏ; giữ menu riêng của danh sách cột và không chặn menu soạn thảo khi người dùng đang nhập trong ô.
- [x] Thêm thao tác đóng băng đến cột đang chọn vào menu toolbar và menu chuột phải. Lưới đo chiều rộng thực tế, giữ cột số dòng cùng mọi cột từ đầu đến cột đích khi cuộn ngang; chọn lại thao tác sẽ bỏ đóng băng.
- [x] TypeScript typecheck và production build đạt. Chrome fixture `--table-regression` đạt 9 kiểm tra, gồm cuộn ngang thật, bật/tắt từ toolbar và menu chuột phải; không publish theo yêu cầu.

## Tự chạy SQL Profiler — 20/11/2026

- [x] Đối chiếu thao tác mở Profiler từ editor của FSD Studio và xác định thông tin lấy từ kết nối hiện tại.
- [x] Điền bộ lọc database, nạp cấu hình SSMS, tự khởi động và giữ luồng chọn `PROFILER.EXE`.
- [x] Kiểm tra chọn executable và tự chạy trên giao diện bằng fixture. Thu sự kiện thật và xác nhận dọn phiên Extended Events trên SQL Server LocalDB. Chạy smoke WebView2 và publish Windows x64.
- [x] Xử lý máy chủ SQL2014 từ chối Extended Events của tài khoản dự án. Kiểm tra SQL Trace tích hợp bằng truy vấn fixture trên LocalDB và xác nhận mở, đọc, đóng phiên đã lọc trên máy chủ tương ứng.
- [x] Sửa lỗi SQL Server 2008 `Incorrect syntax near 'THROW'`: tự chọn SQL Trace cho version 10.x, đổi batch sang `RAISERROR + RETURN` và kiểm tra bằng parser T-SQL 100 trong bộ 109 WebView checks.
- [x] Sửa lỗi `You do not have permission to run 'SP_TRACE_CREATE'` trên instance SQL2008 chuẩn bằng cách tự dùng tài khoản Profiler riêng thay cho tài khoản dự án; không đưa mật khẩu qua bridge hoặc UI.

## Sao chép đầy đủ và đánh dấu lỗi SQL Profiler — 26/11/2026

- [x] Nhấp một lần chỉ chọn/xem SQL; nhấp đúp ô SQL mới sao chép chuỗi đầy đủ và hiện toast **Đã sao chép SQL.**
- [x] Thu `RPC:Starting`, `SQL:BatchStarting` và `User Error Message` ở SQL Trace; thu các sự kiện starting và `error_reported` tương ứng ở Extended Events.
- [x] Ghép lỗi severity từ 11 với câu SQL cùng phiên: tô nền câu SQL gây lỗi, đặt dòng thông báo chữ đỏ ngay sau; cảnh báo severity dưới 11 không tô đỏ.
- [ ] Smoke với một câu SQL lỗi trên instance ERP thật; kiểm chứng tự động hiện dùng fixture không chứa dữ liệu khách hàng.
- [x] Giữ dữ liệu đã thu khi Dừng rồi Chạy lại và kiểm tra sự kiện của phiên mới được nối vào lưới.
- [x] Tự thu gọn panel dưới khi mở hoặc chọn tab XML, giữ nguyên dữ liệu và cho phép mở lại.
- [ ] Smoke phiên thu thật trên instance SQL Server 2008 của người dùng sau khi cài bản publish mới.

## Đồng bộ dự án theo FSD Studio — 20/11/2026

- [x] Đối chiếu nút đồng bộ, nguồn `CurrUser` và danh mục QLYC với bản FSD Studio trên máy.
- [x] Bổ sung đọc `CurrUser` từ cấu hình FSD Studio, thử điểm kết nối dự phòng và làm mới profile đang sử dụng.
- [x] Kiểm tra đọc danh mục thật và lập kế hoạch trong bộ nhớ. Kiểm tra UI bằng fixture, chạy smoke WebView2 và publish Windows x64.

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

## Xem nhanh entity XML bằng F12 — 20/11/2026

- [x] Mở callout tại vùng editor, hiện tên và đường dẫn entity thay cho modal toàn màn hình.
- [x] Trả về phạm vi `Start/Length` của giá trị entity; entity `SYSTEM` dùng đúng nội dung file đích.
- [x] Giữ ngữ cảnh khai báo của XML gốc để F12 tiếp trong entity con vẫn dò được DTD cha.
- [x] Dùng chuỗi callout cha–con: F12 trong callout giữ tổ tiên, mở popup con mới; đóng lá giữ cha, đóng cha dọn toàn bộ hậu duệ.
- [x] Bổ sung tùy chọn **Hiện toàn bộ file**, nút mở trong tab và giữ Ctrl+F12.
- [x] Khóa cuộn và tương tác chuột của màn hình nền trong khi callout mở; vẫn cho cuộn editor bên trong callout.
- [x] Kiểm chứng bằng 156 Core checks, 105 WebView checks, TypeScript/Vite production build và 14 bước regression trình duyệt chuyên biệt, gồm mở nhánh con, đóng lá và đóng cả nhánh từ cha.
- [ ] Nghiệm thu thêm trên bộ entity FAST ERP thật có chuỗi lồng sâu và file nguồn rất dài.

## Kế hoạch Check Mail

Phạm vi và tiêu chí nghiệm thu nằm trong [SPEC.md](SPEC.md#check-mail--gửi-thư-thử-qua-smtp). Đã đối chiếu package Web, component dùng chung và kiến trúc Desktop → Application → Infrastructure. Đã triển khai tab Check Mail và adapter SMTP MailKit với thời gian chờ 30 giây. Kiểm thử dùng máy chủ loopback và địa chỉ mẫu.

1. **Dựng tab công cụ.** Đăng ký lệnh trong command palette, mở tab theo yêu cầu. Dùng Button, Select và theme hiện có. Bố trí kết nối ở trên, người gửi và người nhận ở giữa, phần soạn thư và kết quả chiếm hết chiều ngang phía dưới. Đặt thứ tự Tab theo luồng nhập. Bản đầu nhập cấu hình trực tiếp và dùng được khi chưa chọn dự án.
2. **Chốt cách nhập và kiểm tra.** Dùng một lựa chọn cho STARTTLS bắt buộc, TLS ngay khi kết nối hoặc kết nối thường. Đề xuất ban đầu là STARTTLS và cổng 587. Cho sửa cổng để dùng máy chủ nội bộ. Kiểm tra host, cổng 1–65535, From và To. Tách các địa chỉ bằng dấu chấm phẩy hoặc xuống dòng. Cho phép relay không xác thực khi bỏ trống tài khoản và mật khẩu. Khi nhập mật khẩu, yêu cầu chế độ mã hóa.
3. **Nối luồng C#.** Khai báo request/response ở Contracts, command và handler MediatR ở Application, adapter SMTP bất đồng bộ ở Infrastructure. Đề xuất dùng MailKit cho hai chế độ TLS, kiểm tra tương thích với target framework trước khi thêm gói. Giữ kiểm tra chứng chỉ máy chủ. Tạo thư UTF-8 theo Text/HTML, xử lý Bcc đúng vai trò. Gắn giới hạn thời gian và hủy khi đóng tab. Rà đường ghi log bridge trước khi truyền mật khẩu.
4. **Trả kết quả tại chỗ.** Hiện bước gặp lỗi, mã phản hồi SMTP nếu có và địa chỉ bị từ chối khi xác định được. Khi mất kết nối sau khi đã chuyển nội dung nhưng chưa có xác nhận, báo chưa xác định được kết quả và để người dùng quyết định gửi lại. Không tự động gửi lại.
5. **Kiểm thử và bàn giao.** Dùng SMTP thử nghiệm cục bộ và địa chỉ thuộc example.test. Kiểm tra Text/HTML tiếng Việt, nhiều người nhận, Bcc ẩn khỏi header, lỗi cổng/chứng chỉ/mật khẩu, bấm gửi liên tiếp, đóng tab khi đang gửi và mở công cụ khi chưa chọn dự án. Kiểm tra theme sáng/tối, thu hẹp cửa sổ, điều hướng bàn phím và log đã lọc dữ liệu. Sau khi đạt tiêu chí, cập nhật hướng dẫn sử dụng và build bản chạy trực tiếp.

## Chẩn đoán khai báo controller ERP — 23/11/2026

- [x] Dùng parser XML/DTD hiện có để quét toàn bộ `.f` và `.xml` của controller qua bridge riêng, không lưu diagnostics trong cache danh sách file.
- [x] Tô đỏ đúng file lỗi trong cây và hiển thị thanh cảnh báo trên từng pane editor với thông báo, dòng, cột và đường dẫn.
- [x] Ưu tiên buffer chưa lưu, cập nhật trạng thái theo document và quét lại controller sau debounce để khai báo sửa đúng tự xóa lỗi.
- [x] Thêm fixture native cho file hợp lệ, entity thiếu, buffer đã sửa, file ngoài dự án, file không hỗ trợ và cancellation.
- [x] Chạy regression trình duyệt cho chu trình file đỏ → mở banner → thêm khai báo → tự xóa; TypeScript/Vite build đạt, 111 WebView checks và 156 Core checks đạt, 12 smoke native đạt; đã publish `artifacts/publish-webview/LSCode.WebView.exe`.
- [ ] Nghiệm thu trên controller FAST ERP thật có DTD/include nhiều tầng; phân tích hàm JavaScript ngoài entity sẽ được đặc tả riêng nếu cần.
