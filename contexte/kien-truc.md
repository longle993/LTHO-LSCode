# Kiến trúc LSCode

Cây nguồn ERP gọi `workspace.deleteFile` sau xác nhận đường dẫn. Bridge chỉ xóa một file và giao diện bỏ file, kết quả tìm kiếm và chẩn đoán tương ứng sau khi xóa thành công. File đang mở cần được đóng trước để bảo vệ nội dung chỉnh sửa.

Xuất kết quả SQL thêm ký tự BOM ở đầu nội dung CSV trước khi gửi qua `text.export`. Workspace ghi UTF-8; dấu BOM giúp Excel nhận diện mã hóa khi mở file.

Phím tắt dùng mã lệnh làm khóa trong `shortcuts.dat` dưới thư mục dữ liệu LSCode. Windows DPAPI bảo vệ file theo tài khoản hiện tại. Khi chưa có file riêng, ứng dụng đọc phím tắt cũ từ `settings.json` rồi chuyển sang file mới khi lưu. Cập nhật ứng dụng giữ được cấu hình nếu giữ thư mục này và tài khoản Windows.

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

Từ 27/11/2026, `sourceCollection.versions` giữ thêm mã phân hệ/sản phẩm của phiên bản. Catalog dùng trực tiếp `Entry.Group`; đường dẫn dự án nằm dưới `SourceCollection` lấy folder mẹ kế tiếp làm phân hệ, còn nguồn ngoài cấu trúc này vào nhóm **Khác**. Dropdown Source Collection sắp phân hệ theo tên, để **Khác** ở cuối, rồi sắp phiên bản giảm dần bằng so sánh số tự nhiên. `Select` dùng ARIA tree khi bật `treeGroups`: phân hệ là nút cha in đậm/có nền và mũi tên đóng/mở, phiên bản là treeitem con thụt vào; nhánh chứa lựa chọn hiện tại tự mở. Ô tìm khớp cả tên phân hệ và tự mở nhánh khớp. Popover phiên bản yêu cầu rộng 420 px nhưng vẫn co theo viewport. `SourceVersionCatalog` và giao diện đều loại phòng vệ folder con bắt đầu bằng `ZZZ`, nên dữ liệu hướng dẫn cũ trong JSON cũng không xuất hiện. Các phân hệ đồng bộ gồm FAP, FastFE, FAW, FBFE, FBO-FBI, FE_TT78, HDDV, QLTB và HRM. Phạm vi kiểm chứng ngày 27/11/2026 gồm 120 WebView checks, TypeScript/Vite production build và 9 kiểm tra Chrome chuyên biệt bằng fixture phân hệ/phiên bản giả; chưa đồng bộ lại toàn bộ thư mục mạng thực tế.

Chi tiết luồng request ở [Clean Architecture và MediatR](../docs/architecture/CLEAN-ARCHITECTURE-MEDIATR.vi.md).

## Toolbar editor WebView

Từ 22/11/2026, `table.view` phân trang bằng OFFSET/FETCH và giữ snapshot của trang đang xem theo scope. Trên SQL Server 2008, reader đọc tuần tự và bỏ qua các dòng của trang trước. Khi SELECT chỉ gồm các cột và ORDER BY để trống, LSCode ưu tiên khóa chính hoặc chỉ mục duy nhất không lọc. Mỗi trang có tối đa 200 dòng và giới hạn dung lượng ước tính trước khi gửi qua bridge. Lưới React chỉ dựng hàng gần vùng cuộn. Thanh Tìm kiếm gửi `workspace.search` với `firstPerFile` sau 250 ms, hủy request cũ và nhận vị trí khớp đầu tiên của từng tệp.

Generate Update (tên cũ: Advance Note) được mô tả trong [đặc tả và hướng dẫn](../docs/guides/ADVANCE-NOTE.vi.md). Infrastructure hiện sao chép source theo đường dẫn tương đối và xuất script App/Sys UTF-16. SQL Object lưu tên object rồi đọc metadata của target App/Sys để sinh DROP + CREATE cho table/store/function. SQL Data lưu `bảng: WHERE`, đọc các dòng phù hợp và dùng metadata bảng để sinh INSERT; Ctrl+D ở Table Tool đưa khóa này vào yêu cầu đang chọn. Nội dung SQL thô của XML cũ vẫn được xuất theo chế độ tương thích. ID bridge `advance-note` và phần tử XML `advanceNotes` được giữ để không làm mất dữ liệu cũ.

Từ 19/11/2026, tab Bảng làm mới cache danh mục theo target sau khi tab SQL chạy xong hoặc Thiết kế danh mục lưu thành công. Việc cập nhật tên bảng trên tab giữ nguyên phiên đọc theo `scope`. Chỉ lệnh `table.insertScript` trong màn Cấu trúc và dữ liệu dùng định dạng ngày rút gọn theo kiểu cột. Generate Update dùng giá trị ISO đầy đủ để giữ độ chính xác khi triển khai.

React nhận thao tác drop và gửi các `File` qua `postMessageWithAdditionalObjects`; host đọc đường dẫn tuyệt đối từ `CoreWebView2File.Path`, chỉ nhận file đang tồn tại, giới hạn 100 file mỗi lượt rồi phát sự kiện `files.dropped`. React dùng lại `document.open`, vì vậy kiểm tra tab trùng, encoding và lịch sử mở file không có luồng thứ hai.

Toolbar editor chỉ dành chỗ cho thao tác theo tài liệu đang mở. Advance Note và SQL Watch vẫn được mở từ thanh lối tắt hoặc bảng lệnh, sau đó xuất hiện dưới dạng tab công cụ; không lặp lại thành nút chữ trên toolbar. Khi cửa sổ hẹp, ô tìm trong file và ô chọn database được phép co theo chiều ngang, còn nút Chạy và các nút icon giữ kích thước để nhãn không bị bẻ thành từng ký tự.

Từ 26/11/2026, `App.run` chuẩn hóa kết quả `sql.execute` ở lớp trình bày: nếu không có bảng và không có thông báo server, bổ sung **Các lệnh đã được thực hiện thành công.**; mọi thông báo thật vẫn được giữ. Lần chạy không có bảng tự chọn tab Thông báo, còn SELECT có bảng tiếp tục ở tab Kết quả. Fixture dùng `ALTER PROCEDURE dbo.StoreFixture` và không chứa dữ liệu khách hàng. Phạm vi kiểm chứng: TypeScript/Vite production build đạt và Chrome fixture riêng đạt 5 kiểm tra.

Cùng ngày, diagnostic Monaco giữ thêm `owner` của marker. Banner trong `editor-group` chỉ đọc lỗi có owner `xml`; marker `sql` vẫn tạo gạch lỗi và danh sách **Vấn đề** nhưng không đi lên banner. Khi `sql.execute` thất bại, `App.run` tạo một `QueryResult` lỗi theo document, lưu vào `resultsByTab`, tự chọn **Thông báo** và không ghi vào lỗi toàn cục `WorkbenchNotice`. Vì vậy đổi tab rồi quay lại vẫn thấy đúng lỗi SQL, còn banner trên chỉ phục vụ lỗi XML/DTD như entity chưa khai báo. Phạm vi kiểm chứng: TypeScript/Vite production build đạt; Chrome `--sql-error-routing-regression` đạt 6 kiểm tra, `--sql-success-message-regression` đạt 5, `--controller-diagnostics-regression` đạt 7 và `--menu-regression` đạt 13; fixture không chứa dữ liệu khách hàng.

Thời gian query được theo dõi ở React theo từng tab: đồng hồ `performance.now()` cập nhật UI mỗi 100 ms trong lúc request chạy; kết quả thành công thay bằng `QueryResult.durationMs` từ Infrastructure, còn lỗi/hủy lưu thời gian đã trôi qua. Bản đồ thời gian tách theo document ID để đổi tab không hiển thị nhầm. Khung `.studio` dùng token `--window-border` riêng cho từng theme để tạo viền 1 px bên trong cửa sổ WPF không chrome.

Table Tool chỉ còn một bề mặt dữ liệu. `TableDataGrid` dùng chung token và ngôn ngữ tương tác với lưới kết quả SQL: kéo chọn vùng, chọn dòng/cột/toàn bảng, sao chép TSV và sửa ô trực tiếp; thao tác ghi vẫn đi qua `table.write`, metadata khóa chính quyết định khả năng cập nhật. Menu Tập lệnh dùng HTML Popover top-layer và tính vị trí theo viewport để không bị `table-toolbar-slot` cắt. Từ 23/11/2026, state của menu lưu nguồn neo là nút toolbar hoặc tọa độ chuột; `contextmenu` trên bề mặt dữ liệu mở lại đúng popover và tự chặn ở input/textarea/button/contenteditable để không chiếm menu soạn thảo hay menu cột. Cùng menu có thao tác đóng băng theo cột active hoặc cột mang `data-table-column` tại vị trí chuột phải. `TableDataGrid` dùng `ResizeObserver` đo header, tính offset tích lũy và sinh quy tắc sticky cho cột số dòng cùng các cột từ đầu đến cột đích; chọn lại thao tác xóa state và toàn bộ quy tắc sticky. `Ctrl+Alt+D` được App định tuyến theo tab đang chọn, nên tab Table đổi target riêng và giữ tên bảng. `MenuEditor.InsertScriptAsync` đọc schema thực tế của `dbo.wcommand` và `dbo.command`, bỏ cột identity/computed/rowversion rồi sinh literal SQL đúng Unicode, binary, số và ngày. Bridge `project.menu.insertScript` được gọi từ F12 hoặc menu chuột phải của cây chức năng.

Cập nhật 18/11/2026: Table Tool gửi thêm `orderBy` vào câu đọc đã qua `ReadOnlyQuery.Validate`; UI gợi ý tên cột cùng `ASC`/`DESC`. F8 tại lưới quy vùng chọn về danh sách dòng, xác nhận một lần và gửi batch `delete` chứa đầy đủ khóa chính. Infrastructure kiểm tra tập khóa, tham số hóa DELETE, thực hiện cả batch trong transaction rồi làm mất hiệu lực snapshot. `SqlSuggestions` phân tích buffer hiện hành để bổ sung biến `DECLARE`, biến bảng và bảng tạm từ `CREATE TABLE`/`SELECT INTO`; dữ liệu này không cần truy vấn schema SQL Server.

Cập nhật tiếp 18/11/2026: Table Tool hiển thị trạng thái/lỗi bằng toast React đưa lên `document.body` và xác nhận F8 bằng `Modal`, vì vậy không phụ thuộc hộp thoại trình duyệt và không làm đổi chiều cao vùng dữ liệu. WHERE/ORDER BY dùng grid 7/3 và stack ở màn hẹp. `App` chuyển danh sách snippet đã lưu vào provider Monaco bằng `setEditorSnippets`; provider nhận trigger `/`, lọc khóa bắt đầu bằng `/` và chèn body dưới dạng Monaco snippet với caret offset cũ. Lịch sử dùng cập nhật optimistic và hàng đợi 140 ms theo project; bridge `project.history.removeMany` gọi một lần `ProjectFileActions.RemoveHistory` với `HashSet`, chỉ ghi JSON khi thực sự có mục bị xóa. Phạm vi kiểm chứng cùng ngày: TypeScript typecheck và production build đạt; 95 WebView checks đạt, gồm fixture xóa lịch sử theo lô; publish Windows x64 self-contained và 12 smoke WebView2 đạt. Chưa đo độ trễ trên file lịch sử LAN thực tế có số lượng rất lớn.

Từ 27/11/2026, SqlRunner chỉ thêm số dòng bị ảnh hưởng khi RecordsAffected không âm và diễn đạt thành **Thực hiện lệnh thành công, đã thay đổi N dòng.** App chọn Kết quả khi có tập kết quả và Thông báo khi lệnh thành công không trả bảng. Phạm vi kiểm chứng: formatter tiếng Việt với 4 dòng, 119 WebView checks, TypeScript/Vite production build và publish Windows x64 self-contained.

Cũng từ 27/11/2026, `SqlTraceStream` xóa các ký tự UTF-16 NUL mà rowset trace để lại bên trong `TextData` dài. Đây là ký tự phân khối, nhưng clipboard Windows xem NUL đầu tiên là kết thúc `CF_UNICODETEXT`. Ngoài ra, `textarea` chuẩn hóa CRLF thành LF nên chỉ số vùng chọn không thể dùng để cắt trực tiếp chuỗi SQL gốc; sau 105 lần xuống dòng, cách cũ lệch khoảng 105 ký tự và dừng giữa dòng 106. `ProfilerTool` dùng nguyên chuỗi gốc khi chọn toàn bộ, chỉ dùng giá trị DOM khi sao chép một phần, đồng thời chuẩn hóa NUL trước khi ghi clipboard. Fixture giao diện dùng SQL 112 dòng có NUL giữa dòng 106 để ngăn cả hai lỗi quay lại. Phạm vi kiểm chứng: 120 WebView checks, 21 kiểm tra giao diện Profiler, TypeScript/Vite production build và publish Windows x64 self-contained.

## Tải menu ERP và Mobile

Bộ dò source WebView lần theo tên cố định ở đối số đầu của `showForm`, `openForm` và `loadForm` trong Main ASPX hoặc controller .f/.xml. Cây File và tài nguyên F3 tra tên file không phân biệt hoa thường, duyệt tiếp form lồng và bỏ qua tên đã duyệt. Tên ghép hoặc truyền qua biến cần được xác định lúc chạy nên không suy ra từ source tĩnh. Luồng tạo danh mục từ mẫu giữ phạm vi file cũ.

Nút mã Web trong cây và kết quả tìm kiếm gọi `openMenuEditor` như F3, dùng chung kiểm tra bản nháp chưa lưu. Có thể mở Main từ cây File.

Ô tìm chức năng dùng `entry.link` (trường Main trong form ERP) cùng tên, SysId và mã để lọc cục bộ.

Khi mở dự án có kết nối theo web.config, ProjectDatabase đọc file từ SourcePath hiện tại của profile. WebConfigPath cũ chỉ dùng với profile chưa có SourcePath. Nút tải lại web.config cũng thao tác trong thư mục source.

Hiện tại LSCode kiểm tra cây Mobile ở nền khi mở dự án. Thanh chuyển Web / Mobile xuất hiện khi cây Mobile có mục. Nút tải lại ở Web kiểm tra lại Mobile song song.

Triển khai ngày 17/11/2026: [tối ưu và tích hợp Mobile](../docs/specifications/ERP-MENU-PERFORMANCE-MOBILE.vi.md). WebView gọi `project.commands` với `kind/refresh/context` tới `ProjectDatabase.CommandsAsync` (source Shell được Compile Link vào Infrastructure). Web dùng một connection, đọc schema rồi chạy batch có Startup; `command_id/sysid` nằm trong result cuối. Cache tối đa 8 ngữ cảnh, TTL 5 phút, gộp request đồng thời và invalidate sau khi lưu menu. Cây Web vẫn ba cấp. Mobile đọc `dbo.dvcommand`, dựng không giới hạn độ sâu theo `wmenu_id0`, dùng `bar/bar2` và `sysid`; tab Mobile lazy-load và không bật F3/F12. Source Mobile đi qua fallback `sysid` của `project.controller`, kể cả dự án còn thư mục Main. Chưa có benchmark LAN hoặc smoke database ERP thật.

Cập nhật 18/11/2026: batch Mobile không dùng `THROW` vì một số SYS FAST cũ không parse cú pháp SQL Server 2012 này. Kiểm tra thiếu `dbo.dvcommand` dùng `RAISERROR` rồi `RETURN`, giữ tương thích mà không thay đổi dữ liệu hay quy tắc dựng cây.

Cập nhật 19/11/2026: [form F3 Web](../docs/guides/ERP-MENU-EDITOR.vi.md) tải bản ghi SYS độc lập với lần dò file, hủy lần dò cũ khi Main/SysId đổi và giữ kết nối App của dự án để mở từng bảng Dir. Native kiểm tra kết quả INSERT trước COMMIT và rollback khi bản ghi mới còn NULL. Fixture SQL cục bộ kiểm tra cả bản ghi gửi thiếu trường và mặc định SQL trả NULL. Chưa đo thời gian trên nguồn ERP qua LAN.

Cập nhật 25/11/2026: chọn node trong `FunctionTree` gọi `openController` với `preserveNavigation`, vì vậy controller và diagnostics vẫn tải nhưng sidebar không chuyển sang File và component cây không bị unmount; state `expanded` theo ID được giữ. `selectedCommands` tiếp tục tách Web/Mobile và `data-selected` tô cả tên lẫn mã của dòng hiện hành. Lệnh **Làm mới menu** trong context menu gọi `loadCommands(kind, true)` để truyền `refresh: true` xuống `project.commands`; dữ liệu mới thay thế cây nhưng component và các ID cũ được giữ nên nhánh mở/lựa chọn còn tồn tại không bị reset. Phạm vi kiểm chứng: TypeScript, Vite production build và Chrome fixture `--menu-regression` 9 kiểm tra; chưa đối chiếu menu vừa thêm trên SYS ERP thật.

Cập nhật tiếp 25/11/2026: `MenuEditor.NewAsync` tra menu đang chọn trong `dbo.wcommand` trước khi tạo bản nháp. Nếu `wmenu_id` có hậu tố số, mã mới giữ nguyên `wmenu_id0`, tăng hậu tố theo đúng độ rộng và tiếp tục tăng khi mã kế tiếp đã tồn tại; mã cũ không có hậu tố số vẫn dùng quy tắc tạo nhánh trước đây. Fixture dùng mã mẫu `03.69.43`, không chứa dữ liệu khách hàng. Phạm vi kiểm chứng: TypeScript/Vite production build, 114 WebView checks và Chrome `--menu-regression` 11 kiểm tra đạt; kiểm tra tích hợp SQL đã được thêm nhưng không chạy được do máy hiện tại không có SQL instance cục bộ. Chưa nghiệm thu trên SYS ERP thật.

Hiệu chỉnh cùng ngày theo nghiệm thu giao diện: click menu thông thường gọi `openController` với điều hướng mặc định để chuyển sang tab File. Khối `FunctionTree` được giữ mounted bằng `display: none/contents` khi đổi tab, nên state mở nhánh bên trong không mất; `selectedCommands` tiếp tục giữ dòng được chọn để tô đậm khi người dùng quay lại tab Chức năng. Riêng luồng chọn menu cho Advance Note vẫn dùng `preserveNavigation` vì đó là hộp chọn phụ trợ. TypeScript/Vite production build và .NET build sạch; Chrome fixture xác nhận đủ chuỗi Chức năng → File → Chức năng.

Hiệu chỉnh 26/11/2026: trạng thái `suppressControllerFileSelection` tách lựa chọn trong cây controller khỏi `active.path` của editor. Nhấp tên menu bật trạng thái này trước khi tải controller, nên chỉ chuyển sang File và mở rộng cây mà không tô Main hoặc file đang active từ trước; mở file trực tiếp hoặc bấm mã menu đi qua `setActiveId` để khôi phục lựa chọn bình thường. Tab editor hiện có không bị đóng và dữ liệu chưa lưu không bị thay đổi. Phạm vi kiểm chứng: TypeScript/Vite production build đạt, Chrome `--menu-regression` đạt 13 kiểm tra và `--controller-diagnostics-regression` đạt 7 kiểm tra bằng fixture không chứa dữ liệu khách hàng. Bộ UI tổng quát dừng trước kịch bản menu tại locator cũ **Chạy F5**; chưa smoke trên project ERP thật.

## Sinh source danh mục ERP

`CatalogSourceTool` và `CatalogSourceGenerator` dùng bridge `catalogSource.*` cho đọc mẫu, xem trước và xuất. Native dùng chung bộ dò Main/Dir/Grid/Filter với Menu ERP, giữ encoding qua `DocumentStore`, kiểm tra fingerprint của nội dung xem trước rồi chuyển thư mục staging thành thư mục đầu ra. Thiết kế bảng SQL đi qua `catalog.*` và giữ transaction riêng. [Đặc tả và cách dùng](../docs/specifications/CATALOG-TABLE-DESIGNER.vi.md).

`CatalogDescriptionTool` giữ parser quy ước ở WebView vì người dùng phải duyệt và có thể sửa toàn bộ đề xuất trước khi gọi bridge. Dòng đầu tạo tên danh mục/bảng; các dòng sau sinh kiểu theo nhóm mã, chuỗi, số, thập phân/tiền và trạng thái. `CatalogDefinition` vẫn là chốt kiểm tra kiểu/kích thước và dựng SQL phía .NET, nên payload sửa tay không bỏ qua xác thực. Modal dùng một vùng cuộn giữa hai thanh cố định; dropdown, toast và xác nhận đều dùng component LSCode.

## Điều hướng XML và QLYC

Modal Ctrl+G dùng hai cột TAG và DETAIL theo bố cục tham chiếu FCodeViewer. Mỗi TAG dùng một màu từ nhóm token thư mục hiện có và giữ tương phản theo theme LSCode. `fields` trỏ Start vào field đầu, End vào field cuối. `views` là nhóm độc lập và liệt kê từng `view id`. Parser bỏ `items`, bỏ `text` trong script và chỉ nhận khai báo hàm JavaScript. Điểm nhảy lấy theo offset trong buffer đang sửa. Phạm vi kiểm chứng ngày 18/11/2026 gồm fixture XML cho đủ năm nhóm, TypeScript/Vite production build và publish Windows x64 self-contained; chưa smoke trực quan modal với file ERP thật.

`FileSections.tsx` phân tích buffer editor thành nhóm thẻ và mục con để Ctrl+G nhảy theo offset, không đọc lại file trên đĩa. `Ctrl+Alt+C` là lệnh riêng: `CommandBox` nhận `@@`, sau đó `RequestTracking` gọi các bridge `requestTracking.list` và `requestTracking.statistics` trong `DeveloperTools`.

`RequestTracking.cs` ưu tiên đọc kết nối QLYC tương thích từ `HKCU\\Software\\FCoder\\Qlcv` ở cả Registry 32/64-bit và giải mã theo định dạng FCode cũ. Nếu không có, giao diện dùng một target SQL đã đăng ký trong LSCode. Truy vấn dùng `CommandTimeout = 0` qua `ProjectDatabase`, tham số hóa người dùng/ngày/năm, dùng `RAISERROR + RETURN` để tương thích SQL Server FAST cũ và không chứa dữ liệu nhân viên cụ thể. Phạm vi kiểm chứng ngày 18/11/2026: frontend build, Infrastructure/Desktop build, 155 WebView checks có SQL fixture, 146 Core checks và 12 smoke WebView2; chưa kiểm chứng cơ sở dữ liệu QLYC thật trên máy không có cấu hình Registry.

Ngày 27/11/2026, đối chiếu luồng Category trong `fcontrol/frmQLCV.cs`, lớp được `FcodeViewer` gọi khi mở QLYC. Fcode tải danh mục từ `dbo.nbdsdaumuclt` và ghép chi tiết `dbo.nbctdaumuc` theo `stt_rec` của yêu cầu và `ma_daumuc` của đầu mục. Cột Chọn phản ánh `chon`, Số lượng phản ánh `so_luong`, Lập trình phản ánh `ma_lt` và Tester phản ánh `ma_tester`. Thao tác ghi của Fcode có điều kiện theo người dùng và máy. LSCode dùng Category để xem dữ liệu của yêu cầu đang chọn.

QLYC lấy tên mặc định từ `StandardCatalogUser` của lần đồng bộ danh mục chuẩn đã lưu. Luồng lưu thiết lập thông thường giữ lại trường này vì giao diện có thể đang giữ bản thiết lập cũ. **View All** dùng kết nối danh sách đang chọn. Dòng tổng bộ phận cộng theo từng cột, gồm cả các giá trị TB đã tính cho từng nhân viên. Các luồng được kiểm chứng bằng bài kiểm thử WebView với SQL Server cục bộ và trình duyệt Chromium dùng dữ liệu fixture. Chưa kiểm chứng trên cơ sở dữ liệu QLYC đang sử dụng.

### Callout entity F12

Lớp chắn giới hạn trong editor. Bấm lớp chắn hoặc nhấn Esc đóng pop-up cuối và giữ các pop-up cha. Bộ chặn cuộn chỉ áp dụng cho nền editor, nên các vùng còn lại vẫn nhận thao tác.

Từ 20/11/2026, `XmlIncludes.Find` trả thêm tên và phạm vi `Start/Length`. Với entity nội tuyến, phạm vi là riêng nội dung trong dấu nháy; với entity `SYSTEM` hoặc DOCTYPE, phạm vi là file đích. Chỉ số được tính trên bản đã che comment nhưng giữ nguyên độ dài và xuống dòng, nên vẫn ánh xạ đúng về buffer gốc, kể cả buffer chưa lưu.

WebView mở file nguồn để giữ encoding/nội dung đầy đủ nhưng dựng một model Monaco chỉ chứa phạm vi cần xem. Offset trong model rút gọn được cộng lại với `Start` trước khi gọi F12 lần nữa. Request lồng nhau gửi thêm `contextPath/contextText` của XML gốc; Core thử đồ thị khai báo của file hiện hành trước rồi mới dò từ gốc. Cách này xử lý trường hợp file `.txt` con chứa `&Job.Nested.Key.a;` trong khi khai báo của entity đó nằm ở DTD cha.

Callout trong `.editors` được quản lý thành một chuỗi có `id`, `parentId` và `level`; mỗi node giữ model/editor Monaco riêng. F12 từ một node giữ chuỗi tổ tiên, giải phóng nhánh hậu duệ cũ của node đó nếu có, rồi nối thêm callout con lệch theo tầng. Đóng một node giải phóng document/model của node đó và mọi hậu duệ; đóng lá không ảnh hưởng cha. Từng callout hiện đường dẫn, có checkbox toàn file và giữ Ctrl+F12/mở tab. Khi chuỗi chưa rỗng, một lớp chắn fixed chặn con trỏ trên toàn nền và listener `wheel`/`touchmove` ở capture phase hủy cuộn ngoài `.entity-peek`; cuộn bên trong Monaco của các callout vẫn được giữ. Phạm vi kiểm chứng: fixture Core cho entity nội tuyến, file ngoài và đồ thị cha; 105 WebView checks; production build; regression Playwright 14 bước cho callout, khóa cuộn, F12 lồng thành nhánh cha–con, vòng đời đóng nhánh và toàn file. Chưa kiểm chứng trực quan trên source FAST ERP thực tế có nhiều mức lồng.

## Workspace Main, nguồn snippet và điều hướng bảng

`ProjectConventions.ResolveMenuFile` ưu tiên đường dẫn chính xác dưới root/Main. Với link có thư mục ảo không tồn tại, nó fallback theo đúng tên file trong `Main`, giới hạn 1.000 kết quả và vẫn từ chối đoạn `..`; `project.controller` đưa file ASPX đã tìm thấy vào đầu danh sách để `ControllerExplorer` dựng node Main cùng source controller.

`sqlsnippet.xml` là nguồn duy nhất cho biến session FAST `@@...`. Provider SQL nhúng XML tải nguồn này qua `snippet.completions`; completion SQL thông thường tiếp tục nhận cùng danh sách từ Infrastructure. Các mẫu legacy có khóa `ss...` bị loại khỏi `CompletionSnippets.Get` và được `DeveloperTools.ReadSnippets` ánh xạ sang nhóm **SQL dựng sẵn** trong tab Snippet, tránh làm nhiễu danh sách gợi ý bảng/cột.

Table Tool giữ điều hướng ô trong `TableDataGrid`: Home/End đổi cột trong dòng hiện hành, còn Ctrl+G do `TableTool` sở hữu để mở modal gợi ý metadata và gửi yêu cầu focus cột xuống lưới. F5 vẫn đi qua command `sql.run`; `App` định tuyến sang nút Chạy của tab bảng đang chọn thay vì chạy nhầm editor SQL.

Phạm vi kiểm chứng ngày 18/11/2026: TypeScript typecheck/production build, 153 Core checks, 158 WebView checks gồm fixture SQL cho link Main có thư mục ảo, build Desktop không warning và publish Windows x64 self-contained. Chưa smoke bằng menu ERP thật có nhiều file ASPX trùng tên trong các nhánh Main.

## Đồng bộ danh mục chuẩn QLYC

Đối chiếu ngày 20/11/2026 với `GetConnectionQLYC`, `GetProjectToSync` và `Reg.GetRegValue` của FSD Studio cài trên máy. Bản này dùng `FSG_A` và đọc `CurrUser` từ nhóm `RE` của bảng `options` trong SQLite `config.db`. LSCode tìm cấu hình cạnh FSD Studio đang chạy trong cùng phiên, mở SQLite ở chế độ chỉ đọc và tiếp tục cho phép nhập tên thủ công.

LSCode gọi `dbo.ns_getListDept` qua tham số `@Name`. Nút Đồng bộ thử lại bằng tên phát hiện được nếu danh mục trả rỗng cho tên đã lưu. Profile khớp theo mã dự án được cập nhật tại chỗ nếu còn hoạt động. Mã mới được thêm vào catalog LSCode, profile ngừng dùng được giữ nguyên. Kết nối SQL dùng điểm chính trước và thử điểm dự phòng nếu mở kết nối đầu thất bại.

Phạm vi kiểm chứng ngày 20/11/2026 gồm truy vấn thật qua điểm chính, lập kế hoạch trong bộ nhớ, đọc SQLite fixture, giao diện với bridge giả lập và smoke WebView2. Điểm dự phòng hết thời gian chờ khi kiểm tra kết nối từ máy này.

## Khởi động SQL Profiler

`profiler.defaults` trả về tiền tố database, kiểu xác thực và đường dẫn executable từ cấu hình hoặc SSMS mặc định. `profiler.start` với `engine: "auto"` ưu tiên SQL Trace qua SSMS. Khi thiếu SSMS, tài khoản Profiler riêng hoặc SQL Server 2008 dùng bộ thu [SQL Trace tích hợp](../src/LSCode.Infrastructure/SqlTraceStream.cs); kết nối dùng SQL Server 2012 trở lên mới chọn Extended Events. Version được đọc từ `SqlConnection.ServerVersion`, không suy đoán theo tên instance. Tab giữ kết nối tại lúc bắt đầu thu và chỉ dừng khi người dùng bấm Dừng hoặc đóng tab. Hai instance chuẩn `SQL2008` và `SQL2014` dùng tài khoản Profiler riêng ở lớp native để có quyền `ALTER TRACE`; mật khẩu không truyền qua bridge hoặc hiển thị trên UI.

Đối chiếu ngày 20/11/2026 với `ucQuery.btnProfile_Click` của FSD Studio, `ProfilerTool`, `DeveloperTools` và `SqlTraceSession`. Đã kiểm tra tự chạy và chọn executable bằng fixture giao diện, thu và dọn phiên thật bằng Extended Events trên LocalDB, chạy smoke và publish. Chưa chạy SQL Trace bằng SSMS trên máy này.

Bổ sung ngày 20/11/2026 sau khi tái hiện lỗi quyền Extended Events. Bộ thu tích hợp dùng SQL Trace qua kết nối `master`, lọc tiền tố database trên server và xác nhận lại trước khi gửi sự kiện vào lưới. Đã thu câu SQL mẫu và dọn phiên trên LocalDB, đồng thời xác nhận mở, đọc và đóng phiên có lọc trên máy chủ SQL2014. Tài khoản thu riêng có quyền trace cấp máy chủ và có thể thiếu quyền truy vấn trực tiếp vào database dự án.

Bổ sung ngày 23/11/2026 sau lỗi `Incorrect syntax near 'THROW'` và lỗi quyền `SP_TRACE_CREATE` trên SQL Server 2008. Batch cấu hình và dọn SQL Trace dùng `RAISERROR(..., 16, 1)` kèm `RETURN`; batch khởi động Extended Events không còn wrapper `TRY/CATCH/THROW`. Regression dùng `TSql100Parser` để kiểm tra ngữ pháp SQL Server 2008, kiểm tra version 10.x được định tuyến sang SQL Trace và kiểm tra hai instance chuẩn dùng tài khoản Profiler riêng. Phạm vi tự động đạt 109 WebView checks; chưa smoke phiên thu trên instance SQL Server 2008 thật của người dùng.

Bổ sung ngày 26/11/2026 cho chẩn đoán câu SQL lỗi. Cả TraceHost dùng template SSMS, SQL Trace tích hợp và Extended Events đều thu sự kiện bắt đầu câu lệnh, severity cùng thông báo lỗi. Giao diện ghép theo session (hoặc database/login khi trace không có session), tô nền câu SQL gây lỗi severity từ 11 rồi đặt dòng thông báo chữ đỏ ngay sau; `User Error Message` severity dưới 11 hoặc có tiền tố `Warning:` chỉ là cảnh báo và không tô đỏ. Nhấp một lần ô SQL chỉ chọn nội dung; nhấp đúp mới gọi clipboard bằng giá trị đầy đủ trong model, không đọc chuỗi đã bị CSS rút gọn, rồi hiện toast xác nhận. Fixture hồi quy dùng câu SQL/danh mục giả; chưa kiểm chứng với dữ liệu ERP thật.

Bổ sung ngày 27/11/2026 cho cột ID. Giao diện lấy `ProfilerEvent.Application`: SQL Trace tích hợp đọc cột `ApplicationName`, Extended Events đọc action `client_app_name`, còn TraceHost chuyển tiếp `ApplicationName` của sự kiện SSMS. Cột này giúp phân biệt nguồn gọi như `1` hoặc `.Net SqlClient Data Provider` mà không thay đổi số thứ tự dòng `#`. Phạm vi kiểm chứng gồm 120 WebView checks, TypeScript/Vite production build và 23 kiểm tra Chrome chuyên biệt cho Profiler; dữ liệu kiểm thử là fixture, chưa thu lại trên SQL Server ERP thật.

## Chẩn đoán khai báo theo controller

Từ 23/11/2026, `project.controllerDiagnostics` nhận project root, danh sách file do `project.controller` đã tìm và các buffer Monaco chưa lưu. Infrastructure chuẩn hóa đường dẫn, chỉ đọc `.f`/`.xml` nằm trong root, ưu tiên buffer rồi gọi `XmlTools.InspectEditor` cho từng file. Kết quả lỗi cứng có `path/message/line/column/severity`; cảnh báo semantic về field/view vẫn ở bảng Vấn đề nhưng không làm đỏ cây file. API tách khỏi cache controller để kết quả cũ không quay lại sau khi source đã sửa.

WebView giữ diagnostics theo document thay vì một mảng dùng chung. `ControllerGroup.problems` lưu trạng thái theo đường dẫn cho cây file; marker realtime của Monaco cập nhật file đang sửa ngay và một lượt quét controller có debounce đồng bộ các file phụ thuộc/include. Mỗi pane lấy lỗi theo `doc.id`, vì vậy pane sạch không xóa banner của pane lỗi. Màu đỏ và banner chỉ dựa trên severity `error`.

Phạm vi bộ phân tích là XML/DTD của FAST. Entity chưa khai báo được phát hiện kể cả khi entity đại diện cho đoạn hàm nhúng; chưa có bộ phân tích call graph JavaScript toàn cục. Fixture không chứa đường dẫn, thông tin xác thực hoặc dữ liệu khách hàng.

Phạm vi kiểm chứng ngày 23/11/2026: regression Chrome chuyên biệt xác nhận bấm trực tiếp tên chức năng, file lỗi đỏ, banner đúng vị trí/đường dẫn và tự xóa sau khi thêm khai báo; TypeScript/Vite production build đạt; 111 WebView checks, 156 Core checks và 12 smoke WebView2 trên bản Windows x64 self-contained đạt. Bản chạy được cập nhật tại `artifacts/publish-webview/LSCode.WebView.exe`; chưa nghiệm thu với source FAST ERP thật của người dùng.
