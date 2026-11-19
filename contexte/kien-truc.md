# Kiến trúc LSCode

Provider Monaco kích hoạt gợi ý SQL khi gõ `@` hoặc `#` và lấy ký hiệu từ buffer hiện tại. Với tiền tố này, request bỏ qua tra metadata database. Core nhận diện tham số procedure và loại bỏ chuỗi SQL, chú thích trước khi tìm khai báo cục bộ. Fixture Core kiểm tra tham số và bảng tạm trong `ALTER PROC`.

| Điểm vào | Trách nhiệm |
| --- | --- |
| [LSCode.Web](../src/LSCode.Web/) | React/TypeScript, Monaco và component theo tính năng trong `src/features`. |
| [Desktop/StudioWindow.cs](../src/LSCode.Desktop/StudioWindow.cs) | Host WPF/WebView2, bridge, vòng đời cửa sổ và nhận file kéo thả từ Windows Explorer. |
| [Contracts](../src/LSCode.Contracts/) | Giao thức thông điệp qua bridge. |
| [Application](../src/LSCode.Application/) | Lệnh MediatR, handler và các interface ứng dụng. |
| [Domain](../src/LSCode.Domain/) | Kiểu và quy tắc domain. |
| [Infrastructure](../src/LSCode.Infrastructure/) | File, SQL, catalog, Source Collection, Advance Note và adapter Template Studio. Tái sử dụng Core và một số source qua Compile Link. |
| [Core](../src/LSCode.Core/), [Shell](../src/LSCode.Shell/), [TraceHost](../src/LSCode.TraceHost/) | Logic dùng chung, ứng dụng WPF trước WebView và tiến trình Trace. |
| [DynamicTemplateStudio.Core](../src/DynamicTemplateStudio.Core/), [DynamicTemplateStudio.Infrastructure](../src/DynamicTemplateStudio.Infrastructure/) | Model, validation, mapping và persistence của Studio, được build trong repo. |

Chi tiết luồng request ở [Clean Architecture và MediatR](../docs/architecture/CLEAN-ARCHITECTURE-MEDIATR.vi.md).

## Toolbar editor WebView

Generate Update (tên cũ: Advance Note) được mô tả trong [đặc tả và hướng dẫn](../docs/guides/ADVANCE-NOTE.vi.md). Infrastructure hiện sao chép source theo đường dẫn tương đối và xuất script App/Sys UTF-16. SQL Object lưu tên object rồi đọc metadata của target App/Sys để sinh DROP + CREATE cho table/store/function. SQL Data lưu `bảng: WHERE`, đọc các dòng phù hợp và dùng metadata bảng để sinh INSERT; Ctrl+D ở Table Tool đưa khóa này vào yêu cầu đang chọn. Nội dung SQL thô của XML cũ vẫn được xuất theo chế độ tương thích. ID bridge `advance-note` và phần tử XML `advanceNotes` được giữ để không làm mất dữ liệu cũ.

Từ 19/11/2026, tab Bảng làm mới cache danh mục theo target sau khi tab SQL chạy xong hoặc Thiết kế danh mục lưu thành công. Việc cập nhật tên bảng trên tab giữ nguyên phiên đọc theo `scope`. Chỉ lệnh `table.insertScript` trong màn Cấu trúc và dữ liệu dùng định dạng ngày rút gọn theo kiểu cột. Generate Update dùng giá trị ISO đầy đủ để giữ độ chính xác khi triển khai.

React nhận thao tác drop và gửi các `File` qua `postMessageWithAdditionalObjects`; host đọc đường dẫn tuyệt đối từ `CoreWebView2File.Path`, chỉ nhận file đang tồn tại, giới hạn 100 file mỗi lượt rồi phát sự kiện `files.dropped`. React dùng lại `document.open`, vì vậy kiểm tra tab trùng, encoding và lịch sử mở file không có luồng thứ hai.

Toolbar editor chỉ dành chỗ cho thao tác theo tài liệu đang mở. Advance Note và SQL Watch vẫn được mở từ thanh lối tắt hoặc bảng lệnh, sau đó xuất hiện dưới dạng tab công cụ; không lặp lại thành nút chữ trên toolbar. Khi cửa sổ hẹp, ô tìm trong file và ô chọn database được phép co theo chiều ngang, còn nút Chạy và các nút icon giữ kích thước để nhãn không bị bẻ thành từng ký tự.

Thời gian query được theo dõi ở React theo từng tab: đồng hồ `performance.now()` cập nhật UI mỗi 100 ms trong lúc request chạy; kết quả thành công thay bằng `QueryResult.durationMs` từ Infrastructure, còn lỗi/hủy lưu thời gian đã trôi qua. Bản đồ thời gian tách theo document ID để đổi tab không hiển thị nhầm. Khung `.studio` dùng token `--window-border` riêng cho từng theme để tạo viền 1 px bên trong cửa sổ WPF không chrome.

Table Tool chỉ còn một bề mặt dữ liệu. `TableDataGrid` dùng chung token và ngôn ngữ tương tác với lưới kết quả SQL: kéo chọn vùng, chọn dòng/cột/toàn bảng, sao chép TSV và sửa ô trực tiếp; thao tác ghi vẫn đi qua `table.write`, metadata khóa chính quyết định khả năng cập nhật. Menu Tập lệnh dùng HTML Popover top-layer và tính vị trí theo viewport để không bị `table-toolbar-slot` cắt. `Ctrl+Alt+D` được App định tuyến theo tab đang chọn, nên tab Table đổi target riêng và giữ tên bảng. `MenuEditor.InsertScriptAsync` đọc schema thực tế của `dbo.wcommand` và `dbo.command`, bỏ cột identity/computed/rowversion rồi sinh literal SQL đúng Unicode, binary, số và ngày. Bridge `project.menu.insertScript` được gọi từ F12 hoặc menu chuột phải của cây chức năng.

Cập nhật 18/11/2026: Table Tool gửi thêm `orderBy` vào câu đọc đã qua `ReadOnlyQuery.Validate`; UI gợi ý tên cột cùng `ASC`/`DESC`. F8 tại lưới quy vùng chọn về danh sách dòng, xác nhận một lần và gửi batch `delete` chứa đầy đủ khóa chính. Infrastructure kiểm tra tập khóa, tham số hóa DELETE, thực hiện cả batch trong transaction rồi làm mất hiệu lực snapshot. `SqlSuggestions` phân tích buffer hiện hành để bổ sung biến `DECLARE`, biến bảng và bảng tạm từ `CREATE TABLE`/`SELECT INTO`; dữ liệu này không cần truy vấn schema SQL Server.

Cập nhật tiếp 18/11/2026: Table Tool hiển thị trạng thái/lỗi bằng toast React đưa lên `document.body` và xác nhận F8 bằng `Modal`, vì vậy không phụ thuộc hộp thoại trình duyệt và không làm đổi chiều cao vùng dữ liệu. WHERE/ORDER BY dùng grid 7/3 và stack ở màn hẹp. `App` chuyển danh sách snippet đã lưu vào provider Monaco bằng `setEditorSnippets`; provider nhận trigger `/`, lọc khóa bắt đầu bằng `/` và chèn body dưới dạng Monaco snippet với caret offset cũ. Lịch sử dùng cập nhật optimistic và hàng đợi 140 ms theo project; bridge `project.history.removeMany` gọi một lần `ProjectFileActions.RemoveHistory` với `HashSet`, chỉ ghi JSON khi thực sự có mục bị xóa. Phạm vi kiểm chứng cùng ngày: TypeScript typecheck và production build đạt; 95 WebView checks đạt, gồm fixture xóa lịch sử theo lô; publish Windows x64 self-contained và 12 smoke WebView2 đạt. Chưa đo độ trễ trên file lịch sử LAN thực tế có số lượng rất lớn.

## Tải menu ERP và Mobile

Khi mở dự án có kết nối theo web.config, ProjectDatabase đọc file từ SourcePath hiện tại của profile. WebConfigPath cũ chỉ dùng với profile chưa có SourcePath. Nút tải lại web.config cũng thao tác trong thư mục source.

Hiện tại LSCode kiểm tra cây Mobile ở nền khi mở dự án. Thanh chuyển Web / Mobile xuất hiện khi cây Mobile có mục. Nút tải lại ở Web kiểm tra lại Mobile song song.

Triển khai ngày 17/11/2026: [tối ưu và tích hợp Mobile](../docs/specifications/ERP-MENU-PERFORMANCE-MOBILE.vi.md). WebView gọi `project.commands` với `kind/refresh/context` tới `ProjectDatabase.CommandsAsync` (source Shell được Compile Link vào Infrastructure). Web dùng một connection, đọc schema rồi chạy batch có Startup; `command_id/sysid` nằm trong result cuối. Cache tối đa 8 ngữ cảnh, TTL 5 phút, gộp request đồng thời và invalidate sau khi lưu menu. Cây Web vẫn ba cấp. Mobile đọc `dbo.dvcommand`, dựng không giới hạn độ sâu theo `wmenu_id0`, dùng `bar/bar2` và `sysid`; tab Mobile lazy-load và không bật F3/F12. Source Mobile đi qua fallback `sysid` của `project.controller`, kể cả dự án còn thư mục Main. Chưa có benchmark LAN hoặc smoke database ERP thật.

Cập nhật 18/11/2026: batch Mobile không dùng `THROW` vì một số SYS FAST cũ không parse cú pháp SQL Server 2012 này. Kiểm tra thiếu `dbo.dvcommand` dùng `RAISERROR` rồi `RETURN`, giữ tương thích mà không thay đổi dữ liệu hay quy tắc dựng cây.

Cập nhật 19/11/2026: [form F3 Web](../docs/guides/ERP-MENU-EDITOR.vi.md) tải bản ghi SYS độc lập với lần dò file, hủy lần dò cũ khi Main/SysId đổi và giữ kết nối App của dự án để mở từng bảng Dir. Native kiểm tra kết quả INSERT trước COMMIT và rollback khi bản ghi mới còn NULL. Fixture SQL cục bộ kiểm tra cả bản ghi gửi thiếu trường và mặc định SQL trả NULL. Chưa đo thời gian trên nguồn ERP qua LAN.

## Sinh source danh mục ERP

`CatalogSourceTool` và `CatalogSourceGenerator` dùng bridge `catalogSource.*` cho đọc mẫu, xem trước và xuất. Native dùng chung bộ dò Main/Dir/Grid/Filter với Menu ERP, giữ encoding qua `DocumentStore`, kiểm tra fingerprint của nội dung xem trước rồi chuyển thư mục staging thành thư mục đầu ra. Thiết kế bảng SQL đi qua `catalog.*` và giữ transaction riêng. [Đặc tả và cách dùng](../docs/specifications/CATALOG-TABLE-DESIGNER.vi.md).

`CatalogDescriptionTool` giữ parser quy ước ở WebView vì người dùng phải duyệt và có thể sửa toàn bộ đề xuất trước khi gọi bridge. Dòng đầu tạo tên danh mục/bảng; các dòng sau sinh kiểu theo nhóm mã, chuỗi, số, thập phân/tiền và trạng thái. `CatalogDefinition` vẫn là chốt kiểm tra kiểu/kích thước và dựng SQL phía .NET, nên payload sửa tay không bỏ qua xác thực. Modal dùng một vùng cuộn giữa hai thanh cố định; dropdown, toast và xác nhận đều dùng component LSCode.

## Điều hướng XML và QLYC

Modal Ctrl+G dùng hai cột TAG và DETAIL theo bố cục tham chiếu FCodeViewer. Mỗi TAG dùng một màu từ nhóm token thư mục hiện có và giữ tương phản theo theme LSCode. `fields` trỏ Start vào field đầu, End vào field cuối. `views` là nhóm độc lập và liệt kê từng `view id`. Parser bỏ `items`, bỏ `text` trong script và chỉ nhận khai báo hàm JavaScript. Điểm nhảy lấy theo offset trong buffer đang sửa. Phạm vi kiểm chứng ngày 18/11/2026 gồm fixture XML cho đủ năm nhóm, TypeScript/Vite production build và publish Windows x64 self-contained; chưa smoke trực quan modal với file ERP thật.

`FileSections.tsx` phân tích buffer editor thành nhóm thẻ và mục con để Ctrl+G nhảy theo offset, không đọc lại file trên đĩa. `Ctrl+Alt+C` là lệnh riêng: `CommandBox` nhận `@@`, sau đó `RequestTracking` gọi các bridge `requestTracking.list` và `requestTracking.statistics` trong `DeveloperTools`.

`RequestTracking.cs` ưu tiên đọc kết nối QLYC tương thích từ `HKCU\\Software\\FCoder\\Qlcv` ở cả Registry 32/64-bit và giải mã theo định dạng FCode cũ. Nếu không có, giao diện dùng một target SQL đã đăng ký trong LSCode. Truy vấn dùng `CommandTimeout = 0` qua `ProjectDatabase`, tham số hóa người dùng/ngày/năm, dùng `RAISERROR + RETURN` để tương thích SQL Server FAST cũ và không chứa dữ liệu nhân viên cụ thể. Phạm vi kiểm chứng ngày 18/11/2026: frontend build, Infrastructure/Desktop build, 155 WebView checks có SQL fixture, 146 Core checks và 12 smoke WebView2; chưa kiểm chứng cơ sở dữ liệu QLYC thật trên máy không có cấu hình Registry.

## Workspace Main, nguồn snippet và điều hướng bảng

`ProjectConventions.ResolveMenuFile` ưu tiên đường dẫn chính xác dưới root/Main. Với link có thư mục ảo không tồn tại, nó fallback theo đúng tên file trong `Main`, giới hạn 1.000 kết quả và vẫn từ chối đoạn `..`; `project.controller` đưa file ASPX đã tìm thấy vào đầu danh sách để `ControllerExplorer` dựng node Main cùng source controller.

`sqlsnippet.xml` là nguồn duy nhất cho biến session FAST `@@...`. Provider SQL nhúng XML tải nguồn này qua `snippet.completions`; completion SQL thông thường tiếp tục nhận cùng danh sách từ Infrastructure. Các mẫu legacy có khóa `ss...` bị loại khỏi `CompletionSnippets.Get` và được `DeveloperTools.ReadSnippets` ánh xạ sang nhóm **SQL dựng sẵn** trong tab Snippet, tránh làm nhiễu danh sách gợi ý bảng/cột.

Table Tool giữ điều hướng ô trong `TableDataGrid`: Home/End đổi cột trong dòng hiện hành, còn Ctrl+G do `TableTool` sở hữu để mở modal gợi ý metadata và gửi yêu cầu focus cột xuống lưới. F5 vẫn đi qua command `sql.run`; `App` định tuyến sang nút Chạy của tab bảng đang chọn thay vì chạy nhầm editor SQL.

Phạm vi kiểm chứng ngày 18/11/2026: TypeScript typecheck/production build, 153 Core checks, 158 WebView checks gồm fixture SQL cho link Main có thư mục ảo, build Desktop không warning và publish Windows x64 self-contained. Chưa smoke bằng menu ERP thật có nhiều file ASPX trùng tên trong các nhánh Main.
