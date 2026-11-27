# Tối ưu tải menu và tích hợp menu Mobile từ Fcode

Trạng thái: đã triển khai trên working tree ngày 17/11/2026. Đã đối chiếu tĩnh LSCode và `D:\FAST\Code\LTHO\Fcode`, giải các chuỗi cần thiết của `urMainDvCommand`, thêm fixture sạch và build/test cục bộ. Chưa đo cold/warm trên LAN hoặc database ERP thật; các chỉ tiêu tốc độ ở cuối vẫn là mục tiêu nghiệm thu, không phải kết quả đo thực tế.

## 1. Kết quả khảo sát LSCode

| Điểm vào | Hành vi xác minh từ source | Ý nghĩa |
| --- | --- | --- |
| `src/LSCode.Web/src/App.tsx`, `openProject` | Mở profile xong gửi `project.commands` nền, xóa cây cũ; kiểm tra target trước khi nhận kết quả | Không chặn toàn bộ màn hình nhưng chưa có cache menu theo dự án |
| `App.tsx`, `loadCommands` | Mỗi lần gọi đều gửi request mới; chưa gộp các request cùng ngữ cảnh | Có khả năng tải trùng khi thao tác liên tiếp; cần đo số lần gọi |
| `src/LSCode.Infrastructure/DeveloperTools.cs`, case `project.commands` | Gọi `ProjectDatabase.CommandsAsync` | Điểm nối hiện có cần giữ tương thích |
| `src/LSCode.Shell/Services/ProjectDatabase.cs`, `CommandsAsync` | Đọc schema wcommand, đọc metadata, rồi chạy batch menu: ba lượt SQL tuần tự, mỗi lượt mở connection qua pool | Có overhead round trip; không đồng nghĩa ba kết nối vật lý mới |
| Cùng phương thức | Batch lọc status/denyunitright, khôi phục cha hai lượt, chạy `FastBusiness$App$Startup`, lấy result set menu | Không được bỏ lọc hoặc Startup chỉ vì nghi chậm; phải đo và xác minh tác dụng |
| `src/LSCode.Core/ProjectDiscovery.cs`, `CommandHierarchy.Build` | Đã gom con bằng dictionary; cây Web giới hạn ba cấp theo quy ước FSD | Không thay bằng thuật toán quét bảng đệ quy của Fcode; Mobile cần chính sách độ sâu riêng |
| `App.tsx`, `matches`, `FunctionTree` | Làm phẳng cây mỗi khi từ khóa đổi; chỉ mount nhánh đang mở | Có thể lập chỉ mục một lần khi nhận cây; không kết luận đang render toàn bộ cây |
| Vùng Chức năng | LoadingLine đang phản ánh tải controller, không phải request menu | Cần tách đang tải menu, đang tải source, rỗng và lỗi |

Tệp ProjectDatabase nằm trong Shell nhưng được Infrastructure biên dịch qua Compile Link, không phải luồng WPF riêng không còn sử dụng. Timeout thực thi hiện là 0; giữ nguyên yêu cầu SQL không timeout, vẫn phải hủy được.

## 2. Menu Mobile trong Fcode hoạt động thế nào

Bằng chứng gốc: `FCode/urMainControl.cs:560`, `FCode/urMainDvCommand.cs:346` trong source Fcode.

1. `urMainControl.y87pcG1HNRc` chỉ khởi tạo/tải `urMainDvCommand` khi tab hiện tại là `TpgMobile` và cờ cần tải được bật. Khởi tạo control một lần; lỗi tải trả false thì Fcode gỡ tab Mobile.
2. `LoadDvCommand` dùng `connSys`. Nếu không force và chuỗi `DataSource + Database` giống lần trước thì trả về ngay, không đọc lại bảng. Refresh force vượt qua điều kiện này.
3. Khi tải thật: xóa cây, gọi `Dbs.GetTable("dvcommand", "1=1 ORDER BY wmenu_id", connSys)`, giữ DataTable để dựng cây và tìm kiếm.
4. `hGBaDn87uJ` lấy tên cột từ DataTable để dựng lựa chọn trường tìm kiếm. `Cmd.SearchTreeView` tìm trên cây đã tải.
5. Chế độ Customize qua `uWfaLVlELh` lọc các nhãn/mã đặc thù rồi bổ sung tổ tiên lặp đến khi số dòng ổn định. Chế độ này chưa đưa vào LSCode vì yêu cầu hiện tại là cây Mobile đầy đủ.
6. Dựng gốc bằng `wmenu_id0=''`, gọi `IvEa3uwypG` đệ quy theo `wmenu_id0 = node.wmenu_id`; dùng ArrayList kiểm tra mã lặp. Nhãn dùng `bar` hoặc `bar2`, node.Tag giữ DataRow, `sysid` là khóa tra source. Không có giới hạn ba cấp.
7. Nhấp đúp (`exaaKoJNyE`, dòng 521): nếu đang ở urUpdate thì bật `ChkMobile`, đặt chuỗi tìm và gọi `GetSource`; nếu không thì mở FileLookup, bật `ChkMobile_Path`, đặt chuỗi tìm và `ReLoadFile`. Nguồn Mobile tách với nguồn Web.

**Phạm vi bằng chứng:** các chuỗi cần cho luồng tải đã được giải từ resource của Fcode: bảng `dvcommand`, điều kiện/thứ tự, `wmenu_id`, `wmenu_id0`, `bar`, `bar2`, `sysid`, `hide_yn`. Chưa chạy trace Fcode hoặc xác minh Customize trên dữ liệu ERP thật. Fixture LSCode chỉ dùng dữ liệu giả.

Bài học áp dụng: tải theo nhu cầu, cache theo ngữ cảnh, refresh chủ động, tra source riêng Mobile. Không sao chép cache chỉ nối chuỗi server/database (thiếu App/quyền), cache đánh dấu trước khi tải thành công, SQL đồng bộ trên UI hoặc vòng DataTable.Select/ArrayList.

## 3. Phạm vi và UX dự kiến

Audit: React 18, Tailwind 4, Lucide, component nội bộ; token `--primary`, `--app-font` trong styles.css, có theme sáng/tối. Giữ bố cục phẳng và mật độ cây hiện tại. Đây là đề xuất component trong panel Chức năng, không dựng lại toàn workbench.

- Thêm hai lựa chọn **Web | Mobile** phía trên ô tìm hiện có; Web là mặc định. Dùng control tab sẵn có, không thêm thư viện UI.
- Mobile chỉ tải khi chọn lần đầu. Mỗi loại menu giữ cây, từ khóa, mục chọn và nhánh mở riêng trong dự án hiện tại.
- Lần đầu: placeholder dạng hàng cây trong vùng menu. Refresh cùng ngữ cảnh: giữ cây hiện có và thanh tiến trình mảnh; lỗi giữ cây cũ cùng thông báo thử lại. Đổi dự án không được hiện cây dự án trước.
- Thiếu bảng Mobile: thông báo “Dự án chưa hỗ trợ menu Mobile”, giữ tab để người dùng hiểu; không gỡ tab âm thầm như Fcode. Phân biệt thiếu quyền, mất kết nối và không có menu.
- Nhấp đúp tên node mở cây source tương ứng và chuyển sang File nhưng không mở hoặc tô chọn Main/file đang active từ trước; bấm mã menu Web mở form sửa như F3. Khi quay lại Chức năng, giữ nhánh đang mở và dòng đã chọn. Khi Generate Update đang mở yêu cầu, đưa source Mobile vào cây chọn của yêu cầu theo luồng hiện có.
- Tìm theo tên, mã, controller trong bộ dữ liệu đã tải; không gọi SQL trên mỗi phím. Trường tìm bổ sung chỉ đưa vào sau khi xác minh schema Fcode.
- F3/F12 đang dành cho wcommand/command: không được áp dụng mù lên Mobile. Giai đoạn đầu vô hiệu hóa thao tác sửa/xuất menu Web trên node Mobile; triển khai tương đương chỉ sau khi có đặc tả bảng Mobile.
- Phím Tab, mũi tên và Enter hoạt động, tên dài có tooltip, theme sáng/tối dùng token hiện có. Kiểm tra panel hẹp 260px, cửa sổ 1024/1440px và DPI 125/150%.

## 4. Thiết kế đã triển khai

### Tối ưu Web

1. `ProjectDatabase.CommandsAsync` giữ cache tối đa 8 ngữ cảnh, TTL 5 phút. Khóa gồm project/context, server, SYS, App, Web/Mobile và loại đăng nhập/tên SQL user; không chứa mật khẩu.
2. `SemaphoreSlim` theo khóa gộp các request đồng thời. Chỉ ghi cache sau khi tải thành công; nút Tải lại gửi `refresh=true`; lưu menu F3 xóa cache của SYS tương ứng.
3. Luồng Web mở một connection: đọc schema rồi chạy batch menu. `command_id` và `sysid` được đưa vào result cuối, bỏ lượt đọc metadata và connection riêng trước đây. Batch `denyunitright`, `FastBusiness$App$Startup`, status và quy tắc ba cấp được giữ.
4. React tách trạng thái tải/lỗi menu khỏi tải controller, kiểm tra context trước khi nhận kết quả và giữ dữ liệu Web/Mobile riêng. Tìm kiếm vẫn chạy trên cây trong bộ nhớ, không gọi SQL theo phím.

### Tích hợp Mobile

- Bridge `project.commands` giữ mặc định Web và nhận thêm `kind`, `refresh`, `context`; trình duyệt không truyền tên bảng SQL.
- Adapter Mobile đọc `dbo.dvcommand`, ánh xạ `wmenu_id/wmenu_id0/bar/bar2/sysid`, dựng cây không giới hạn độ sâu bằng dictionary và HashSet chống chu trình. Web vẫn giới hạn ba cấp.
- Batch kiểm tra bảng dùng `RAISERROR + RETURN` thay cho `THROW` để tương thích các SYS chạy SQL Server hoặc compatibility level cũ; trường hợp có bảng vẫn chỉ đọc `dvcommand` theo `wmenu_id`.
- Tab **Web | Mobile** nằm trong panel Chức năng. Web tải nền khi mở dự án; Mobile lazy-load khi chọn lần đầu. Mỗi loại giữ cây và từ khóa riêng; lỗi/thiếu bảng hiện trong panel, không làm mất cây Web.
- Node Mobile dùng `sysid` qua luồng `project.controller` hiện có. Fallback `sysid` hoạt động cả dự án có thư mục Main; chọn menu chỉ dựng cây source, không tự mở Main. Khi Generate Update đang chọn menu, luồng chọn source hiện có tiếp tục được dùng.
- F3/F12/menu chuột phải chỉ bật cho Web. LSCode không phụ thuộc DLL/exe Fcode và không thêm tài khoản nhúng.

## 5. Trình tự triển khai

| Bước | Kết quả cần giao | Điều kiện hoàn tất |
| --- | --- | --- |
| 1 | Baseline cold/warm và fixture menu Web | Chưa đo trên ERP thật; fixture Web hiện có vẫn đạt |
| 2 | Cache/gộp request và UX trạng thái Web | Đã triển khai; refresh vượt cache, lưu menu invalidate |
| 3 | Giảm round trip | Đã bỏ lượt metadata riêng; chưa có benchmark LAN trước/sau |
| 4 | Xác minh SQL/schema/source Mobile Fcode | Đã giải chuỗi tải và mapping; chưa nghiệm thu Customize/trace thật |
| 5 | Adapter Mobile + Web/Mobile | Đã triển khai lazy load và lỗi độc lập |
| 6 | Tra source, nối Generate Update, kiểm thử hồi quy | Đã nối bằng sysid; F3/F12 chỉ Web |
| 7 | Cập nhật hướng dẫn, build chạy trực tiếp | Frontend/.NET/tests đạt; publish trực tiếp ở cuối lượt triển khai |

Việc đo hiệu năng thực tế trên LAN và database ERP được giữ thành bước nghiệm thu riêng; không dùng thời gian fixture cục bộ để tuyên bố mức tăng tốc sản xuất.

## 6. Tiêu chí nghiệm thu và phép đo

- Fixture 100/1.000/10.000 menu, nhiều nhánh/nhãn dài, ID Web trùng Mobile, chu trình/orphan, Mobile sâu hơn ba cấp; Web vẫn đúng ba cấp như hiện tại.
- Cold load: so sánh cùng máy/profile/dữ liệu, 5 lượt khởi động sạch. Warm: ít nhất 20 lượt, báo median/P95. Mục tiêu warm cache tới cây tương tác P95 ≤ 200ms và không gọi SQL khi cache hợp lệ. Đây là mục tiêu cần xác nhận trên máy nghiệm thu.
- Tìm trên 10.000 mục P95 ≤ 100ms; không quét source trong thời gian tải menu. Nếu không đạt mới chọn windowing hoặc tối ưu bổ sung.
- 10 thao tác tải cùng ngữ cảnh chỉ tạo một lần truy vấn thực; lỗi không trở thành cache thành công; refresh vượt cache; cache không dùng lại sau đổi quyền.
- Chuyển A→B khi A đang tải, tải lỗi, hủy, mở nhiều cửa sổ; không sai cây/thông báo và không khóa UI. SQL giữ CommandTimeout=0, có đường hủy rõ ràng.
- Golden tests so Web trước/sau; test Mobile thiếu bảng/cột/quyền/đường dẫn, Customize nếu đưa vào phạm vi sau xác minh; test nối source và XML Generate Update bằng fixture.
- Chạy runner Core/WebView phù hợp, frontend typecheck/build; smoke bàn phím, theme, resize và đo React. SQL thật/trace Fcode là bước nghiệm thu chưa chạy, không thay bằng khẳng định từ test fixture.

## 7. Kiểm chứng và phần còn mở

- Đạt: frontend TypeScript/Vite; build Core, Infrastructure, Desktop và WebView.Tests; 146 Core checks; 95 WebView checks; kiểm tra trực quan tab Web/Mobile ở preview 1280×720; bản publish vượt native WebView smoke gồm bootstrap, bridge, Monaco, file/BOM, Generate Update và Source Collection.
- SQL integration mới đã được viết với database fixture tạm, nhưng lượt chạy trên máy này dừng trước khi tạo fixture vì SQL Server cục bộ yêu cầu mã hóa mà máy không hỗ trợ. Không dùng kết quả này để khẳng định adapter đã chạy với SQL Server thật.
- Còn mở: baseline cold/warm trên profile ERP, smoke với `dbo.dvcommand` thật, DPI/theme và LAN; Customize Mobile, sửa menu Mobile và root source Mobile riêng không thuộc đợt này.
