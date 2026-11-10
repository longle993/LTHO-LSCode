> Cập nhật mới: [Workbench 10/11/2026](WEBVIEW-UPDATE-2026-11-10.vi.md), thay thế mô tả explorer, split editor, startup và Profiler cũ.

# LSCode — đặc tả chuyển đổi toàn bộ tính năng sang WebView

Ngày: 10/11/2026. Người dùng đã chọn “dựng luôn”. Kiến trúc và các luồng chuyển đổi đã có source, bản đóng gói và kiểm tra; xem [trạng thái kiểm chứng thực tế](WEBVIEW-RUNNING.vi.md). Ma trận trong tài liệu này là tiêu chí nghiệm thu, không tự động có nghĩa mọi ca đã được kiểm chứng.

## 1. Mục tiêu và phạm vi

Chuyển IDE WPF .NET 10 hiện tại thành ứng dụng Windows dùng React + TypeScript trong WebView2. Giữ nghiệp vụ C#, truy cập SQL Server, file cục bộ/UNC, mã hóa Windows và TraceHost. Thiết kế lại UI dễ đọc, ưu tiên editor và ngữ cảnh dự án/database rõ ràng. Không đồng nhất WebView với website chạy độc lập trên trình duyệt.

Phạm vi parity là những tính năng có trong source hiện tại. Decrypt, ERP generators, e-invoice, terminal, Git, extension host và AI adapter chưa triển khai không được tính là tính năng đã chuyển. Agent contracts được bảo toàn nhưng không tạo nút chat giả.

Nguồn khảo sát: README.md; Directory.Build.props; LSCode.Shell.csproj; toàn bộ danh sách source Core/Shell/TraceHost; MainWindow.xaml; EditorTab.cs; MainWindow.Shortcuts.cs; DocumentStore.cs; SqlRunner.cs; SettingsStore.cs; các tài liệu docs. Một số docs cũ mâu thuẫn với source; mã nguồn và phép thử chạy thực tế là căn cứ cuối cùng. Bảng dưới đây là baseline từ khảo sát, cần đối chiếu command registry và test hiện có trong đợt M0 trước khi tuyên bố đầy đủ.

## 2. Brief UX

- Sản phẩm: IDE phát triển ERP FAST/LSCode, không phải dashboard quản trị (đọc source).
- Người dùng: lập trình viên làm SQL/XML/controller, kết nối nhiều project/database (suy ra từ chức năng).
- Ba việc chính: tìm đúng file/chức năng; sửa code và kiểm tra SQL; xem schema/dữ liệu/trace để chẩn đoán (đọc source).
- Nền tảng: Windows desktop, bàn phím/chuột, tối ưu 1280–1920px; bố cục vẫn không tràn trang ở 375px (desktop từ source, kích thước là đề xuất).
- Khác biệt cần hiện trên màn: chuyển File/Chức năng/DB, liên kết controller và target SYS/App theo tab (đọc source).

## 3. Việc chính từng màn

| Màn | Việc người dùng cần làm | Thông tin quyết định | Hành động cuối | Quy ước đề xuất |
|---|---|---|---|---|
| Projects | Chọn đúng môi trường | Tên, đường dẫn, server, DB, lần dùng gần nhất | Mở dự án | Danh sách tìm kiếm; ngữ cảnh rõ |
| Workspace | Tìm và sửa file/chức năng | Cây, đường dẫn, dirty state, SYS/App | Lưu hoặc chạy SQL | Explorer trái, editor giữa, panel hỗ trợ |
| Tìm kiếm | Tới đúng kết quả | File, dòng, nội dung, buffer chưa lưu | Mở đúng dòng | Kết quả theo file; có hủy |
| SQL | Chạy đúng query vào đúng DB | Server/DB của tab, selection, lỗi/dòng | Run, Cancel, export | Target cạnh nút chạy, kết quả phía dưới |
| DB Objects | Tìm bảng/view/procedure | Loại, schema, tên, nội dung | Script hoặc xem bảng | Cây lazy, tìm tên/nội dung |
| Table Browser | Xem cấu trúc và dữ liệu | Database, bảng, cột, điều kiện | SELECT có giới hạn | Tách schema/data; chỉ đọc |
| Catalog Designer | Tạo/sửa cấu trúc và nhập liệu | Trường hệ thống, kiểu, default, dữ liệu nháp | Lưu có chủ ý | Cấu trúc/dữ liệu/SQL preview |
| XML | Hiểu cấu trúc và include | Outline, lỗi, XPath, file đích | Điều hướng/sửa | Panel theo ngữ cảnh |
| Compare | Nhìn khác biệt | Thêm/xóa/sửa, whitespace, đường dẫn | Mở file cần sửa | Hai cột đồng bộ |
| Profiler | Tìm query gây lỗi/chậm | Thời gian, DB, event, SQL | Lọc, xem SQL, dừng | Bảng event và SQL chi tiết |
| Snippets | Tìm và chèn mẫu | Tên, category, nội dung, placeholders | Chèn hoặc lưu mẫu | Danh sách và preview |
| Dịch | Dịch nội dung đang chọn | Ngôn ngữ, văn bản nguồn/đích | Sao chép/chèn theo hành vi hiện tại | Hai vùng văn bản |
| Settings | Chỉnh môi trường làm việc | Theme, shortcut, đường dẫn executable | Lưu hoặc reset | Nhóm rõ, báo lỗi tại trường |

Quy ước editor/explorer/panel tham khảo tài liệu chính thức VS Code: https://code.visualstudio.com/docs/editing/getting-started/userinterface và https://code.visualstudio.com/docs/configure/custom-layout . Đây là cơ sở đề xuất, không phải bằng chứng đã nghiên cứu người dùng LSCode.

## 4. Kiến trúc đích

```text
React UI -> TypeScript DesktopGateway -> WebView2 message adapter
                                             |
                                      Application use cases
                                             |
                                           Domain

Windows Host -> composition root -> Infrastructure implementations
                                  -> Application port interfaces
```

Dependency rule: Domain không tham chiếu WPF, WebView2, SQLClient, filesystem hoặc React. Application chỉ tham chiếu Domain và khai báo port. Infrastructure tham chiếu Application/Domain để triển khai port. Host tham chiếu Application/Infrastructure để DI và quản lý vòng đời. Web chỉ biết transport contracts, không biết connection string hay class WPF.

Đề xuất cấu trúc:

```text
src/LSCode.Domain             DocumentId, ProjectId, SqlTargetId, value objects
src/LSCode.Application        Use cases, ports, validation, lifecycle operations
src/LSCode.Infrastructure     Files, SqlServer, DPAPI, settings, recovery, external tools
src/LSCode.Contracts          Request/response/event DTO, protocol version
src/LSCode.Desktop            WPF host tối thiểu + WebView2 + DI + message dispatcher
src/LSCode.Web                React/TypeScript + editor + design tokens
src/LSCode.TraceHost          Giữ helper Windows/x86 độc lập
tests/LSCode.*                Domain/application/transport/integration/UI
```

LSCode.Core hiện chứa cả logic thuần và IO: không chuyển nguyên assembly vào Domain. SQL batch/diff/parser/conventions được đánh giá riêng; DocumentStore/WorkspaceSearch/ProjectDiscovery đi vào Infrastructure qua port. Không tạo repository tổng quát cho mọi thao tác: dùng IDocumentStorage, IWorkspaceReader, IProjectCatalog, ISqlExecutor, ISchemaReader, IProfilerSession, ISecretStore, IExternalToolLauncher, IRecoveryStore, ISettingsStore và ITranslationProvider theo nhu cầu thật.

React chia features/workspace, editor, projects, sql, database, catalog, profiler, compare, snippets, settings; shared/ui giữ component thống nhất. Không đặt nghiệp vụ SQL hoặc IO vào component. Monaco model là nguồn văn bản đang chỉnh; React state giữ tab/view/operation state. Host giữ snapshot hash/encoding và target secret. Cần version rõ giữa model và host để không lưu nhầm bản cũ.

## 5. Ma trận chuyển đổi và nghiệm thu

Chỉ đánh dấu Đã kiểm chứng cho ca có test hoặc biên bản acceptance tương ứng. Trạng thái triển khai/kiểm chứng từng nhóm được ghi trong WEBVIEW-RUNNING.vi.md để phân biệt source đã có với parity đã xác nhận.

| ID | Tính năng / nguồn hiện tại | Phần đích | Điều kiện nghiệm thu |
|---|---|---|---|
| F01 | New/Open/Open Folder/Save/Save As — FileActions, DocumentStore | Document use cases + native picker + editor | Mở/lưu đúng đường dẫn; Save As giữ hành vi encoding; hủy picker không mất buffer |
| F02 | Encoding/BOM/newline, 16MB, reject binary | DocumentStorage | Round-trip UTF8/16/32 có/không BOM theo hỗ trợ hiện tại; không decode lossy |
| F03 | Hash conflict, readonly, thay đổi file bên ngoài | Document lifecycle | Không ghi đè khi hash đổi; OK mới reload; Hủy giữ buffer; xử lý readonly đúng |
| F04 | Recovery, dirty tabs, đóng app/tab | Recovery use cases + close handshake | Khôi phục văn bản vào tab không gắn path; không lưu password; đóng không mất nội dung chưa xác nhận |
| F05 | Explorer lazy, refresh, recent files/tabs | Workspace service + tree | UNC chậm không khóa UI; refresh xóa cache; tên dài giữ đuôi/đường dẫn truy cập được |
| F06 | Split dùng chung buffer/undo — EditorTab | Monaco model registry | Sửa một pane cập nhật pane kia; undo thống nhất; dispose đúng khi đóng |
| F07 | Highlight SQL/XML/JS/JSON/C#/text; folding | Editor adapters | XML có script vẫn gợi ý đúng vùng; syntax theo theme; file lớn giảm analysis có thông báo |
| F08 | Find/replace, format SQL/XML/JSON | Editor + language use cases | Giữ selection/caret hợp lý; lỗi không phá text; format SQL dùng logic hiện có |
| F09 | XML outline/problems/XPath | XML application service | Dòng/cột lỗi đúng; namespace d; DTD/entity vẫn bị chặn |
| F10 | Include preview/open, F12/Ctrl+F12 — Includes | Include resolver + peek panel | Resolve tương đối đúng; missing/outside workspace theo policy rõ; mở file đúng |
| F11 | Workspace search regex/case/cancel | Search use case + result view | Tính buffer chưa lưu có path; ignore folders; tối đa 2000; kết quả cũ không ghi đè lần mới |
| F12 | Palette/quick open/shortcuts/activity customization | Command registry + shared UI | Mỗi lệnh có ID; tối đa hai shortcut; phát hiện trùng; F5/Ctrl+S không bị browser giữ |
| F13 | Profiles/import preview/merge/metadata/export — Projects, ProjectStore | Project application + DPAPI adapter | Giữ XML fields lạ; merge giữ/cập nhật; export bỏ secret nhận diện; không dùng legacy password |
| F14 | Chọn project, recents, sync CustomerPro/web.config | Project discovery use cases | Chọn đúng app khi nhiều match; hủy được; thông báo lỗi mạng; không treo startup |
| F15 | SYS/App resolution và target theo tab | Sql session registry | Đổi project không đổi target tab cũ; placeholder phải resolve; không đoán nhầm DB |
| F16 | Menu chức năng, Enter tìm kế, controller/Grid children | Navigation use cases + tree | Đọc SYS; tìm tên/sysid/mã; mở đúng controller; giữ Toàn dự án |
| F17 | Windows/SQL auth, connection settings | Connection service | Secret nằm .NET; UI chỉ giữ handle; config cũ đọc được; session disposal rõ |
| F18 | SQL selection/document, GO, messages/errors/cancel | Sql execution service | Một connection/lần chạy; temp table qua GO; timeout 60s; SQL error đúng dòng; cancel thật |
| F19 | Multi results/CSV — SqlRunner, ResultGrids | Result store + virtual grid | 30 sets/5000 rows hiện tại, báo truncated; CSV đúng phần có; số/null/binary hiển thị không sai |
| F20 | SQL completion alias/schema/snippet/cache refresh | Completion use case + Monaco provider | Ctrl+Space, trigger, đúng target tab; refresh cache; response cũ không áp sang DB mới |
| F21 | DB Objects lazy paging/search content/script — DbObjects/DbSearch/ObjectScript | Schema services + explorer | Tải theo nhóm/trang; tìm tên/nội dung; script đúng schema và loại object |
| F22 | Table Browser SELECT/WHERE/ORDER BY/chọn cột | Read-only table use cases | Tối đa 1000 hàng theo màn hiện tại; hủy được; quote identifiers; không biến thành màn ghi |
| F23 | Catalog create/alter/schema/data/SQL preview | Catalog use cases + editable grid | Giữ bốn trường hệ thống theo spec hiện tại; validate tên/kiểu/default; lưu đúng transaction hiện có |
| F24 | Duplicate template, copy/paste nhiều dòng — CatalogDesigner.Transfer | Catalog draft operations | Duplicate chỉ cấu trúc; paste chỉ bản nháp; không ghi DB trước Lưu; rollback khi lỗi |
| F25 | Snippets CRUD/import/export/placeholders | Snippet service + panel | Giữ fields/CRLF; collision policy; Tab/Shift+Tab; insert không tự chạy SQL |
| F26 | Text/folder compare, SQL beautify | Diff/format use cases + diff editor | Không trim whitespace; chỉ normalize newline như hiện tại; giữ giới hạn LCS; file dài có fallback rõ |
| F27 | Profiler live capture/filters/template/layout/storage | Profiler service + trace grid | SSMS18/helper x86 hoạt động; đúng prefix SYS/App; Start/Stop/Clear; lưu/mở trace; hàng sự kiện không làm nghẽn UI |
| F28 | Dịch tự động/đổi chiều/provider — MainWindow.Tools, TranslationClient | Translation service + two panes | Khảo sát lại browser fallback so với endpoint; debounce/cancel; kết quả cũ không ghi đè; lỗi mạng rõ |
| F29 | Crystal/external tools/Profiler paths | External tool adapter + settings | Validate executable; quote arguments; không cho JS chạy process tùy ý |
| F30 | Light/Dark/High Contrast, resize/maximize/loading/status | Tokens + host window lifecycle | Giữ ba theme; nội dung không nhảy khi loading; window bounds đúng nhiều màn/DPI |
| F31 | Settings/AppData/LSCODE_DATA_HOME/DPAPI backup | Persistence adapters | Đọc được settings/catalog cũ bằng cùng Windows user; không ghi migration phá bản cũ; backup trước đổi schema |
| F32 | Agent contracts chưa có provider | Giữ contracts, chưa có UI agent | Không tuyên bố AI đã triển khai; không cấp IO/process/DB ngầm cho provider |

## 6. Hợp đồng React–.NET

Chọn WebView2 message transport trong process; chưa cần HTTP server hoặc ASP.NET Core cho bản desktop. Nếu có yêu cầu browser sau này, thêm transport adapter trên cùng Application ports. Không mở localhost API chỉ để gọi hàm trong cùng app.

Request: { protocolVersion, requestId, method, payload }. Response: { protocolVersion, requestId, result } hoặc { protocolVersion, requestId, error: { code, message, details } }. Event: { protocolVersion, operationId, sequence, type, payload }. Dùng DTO có schema, ID chuỗi cho số 64-bit nếu cần; không serialize Encoding/Exception/DataTable nguyên đối tượng.

Các nhóm method: workspace.*, document.*, project.*, sql.*, schema.*, catalog.*, snippet.*, diff.*, profiler.*, translation.*, settings.*, window.*. Mỗi method phải có handler/validation cụ thể. Không cung cấp eval, runShell hoặc gọi reflection tùy ý.

Document edit/save gửi documentId + version + text; host lưu bằng expected hash từ snapshot đã mở. Sql execute gửi targetId + documentId + version + selection/text; host chụp target bất biến cho operation. Cancel nhận operationId. Hoàn thành/hủy giải phóng SqlCommand/reader/connection. Event có generation để loại response cũ khi đổi project/search/DB.

Kết quả SQL trả columns và row pages qua resultId; event trace gom batch có giới hạn queue, số event bị bỏ phải báo. Định dạng null/date/decimal/binary phải giữ semantics hiện tại, không chuyển decimal sang JS Number khi mất chính xác. Timeout transport chỉ báo mất phản hồi; không được coi truy vấn đã dừng nếu host chưa xác nhận cancel. Retry không tự lặp SQL ghi hoặc Save.

## 7. Quy tắc UI/UX

Chưa chốt bố cục: chờ duyệt brief rồi tạo 2–3 wireframe theo skill ui-ux. Đề xuất ưu tiên editor; một vùng điều hướng chuyển File/Chức năng/DB; context project và target SQL dễ thấy; panel kết quả/problems/output mở theo tác vụ. Không tự thêm dashboard, biểu đồ, chat hay KPI.

Token tập trung: màu nhấn, surface, border, text, trạng thái, font UI và monospace. Dùng nhận diện đang có làm đầu vào; chọn kiểu flat, tránh nhiều khung viền lồng nhau. Giữ Light/Dark/High Contrast vì là tính năng hiện hữu, không bỏ dark theo mặc định của skill dành cho app mới. Input không icon trang trí mặc định. Control nhất quán giữa các màn; scrollbar theo token của skill. Dữ liệu bảng dày được giới hạn riêng trong grid/editor; phần điều hướng vẫn dễ đọc.

Mọi màn có loading/empty/error/data, selected/hover/disabled; query có running/cancelling/cancelled/failed/succeeded/truncated. Lỗi nói rõ việc nào thất bại và cách xử lý; giữ context khi retry. Dialog hoàn trả focus; shortcuts có scope editor/grid/dialog; IME tiếng Việt phải chạy thử thực tế. Ở 375px, navigation/panel chuyển thành vùng đóng mở, bảng/editor cuộn bên trong thay vì kéo ngang toàn trang.

## 8. An toàn và vòng đời host

Chỉ tải asset local đã đóng gói qua virtual host origin cố định; chặn navigation/frame/window lạ truy cập bridge. Validate source của message, method, payload size và quyền theo resource ID. Picker cấp phạm vi file/workspace; .NET chuẩn hóa đường dẫn và kiểm tra junction/reparse theo use case. Nội dung SQL/XML được render như text, không inject HTML. CSP phù hợp Monaco workers, không tải runtime/CDN trong bản publish. Bí mật không nằm localStorage hoặc log.

Composition root quản lý WebView, document registry, cancellation registry và trace processes. UI đóng/reload cần handshake dirty/recovery; host crash không được làm mất recovery. Start/Stop Profiler và close app phải cleanup helper. Kiểm tra cách hiển thị picker/dialog cùng WebView2 để tránh vấn đề lớp hiển thị WPF/WebView; overlays của UI đặt trong React.

Nguồn Microsoft: https://learn.microsoft.com/en-us/microsoft-edge/webview2/get-started/wpf và https://learn.microsoft.com/en-us/microsoft-edge/webview2/concepts/security .

## 9. Dữ liệu và tương thích

Không thay tên thư mục %LocalAppData%/LSCode hay bỏ LSCODE_DATA_HOME. Phiên bản đầu đọc dữ liệu cũ; khi cần schema mới, ghi phiên bản riêng và giữ bản sao để quay lại WPF. DPAPI CurrentUser yêu cầu đúng tài khoản Windows; không hứa chuyển catalog mã hóa giữa máy/tài khoản. Không đưa password legacy vào session tự động. Giữ XML export redaction và cảnh báo fields tùy biến theo hành vi có thật.

Migration chạy song song bằng executable riêng; WPF hiện tại là phương án quay lại tới khi F01–F31 đạt acceptance. Tránh mở hai bản cùng ghi settings/catalog; dùng lock hoặc storage profile riêng trong kiểm thử.

## 10. Kế hoạch thực hiện

| Mốc | Công việc | Điều kiện hoàn thành |
|---|---|---|
| M0 Baseline | Chốt command/tính năng từ source; chạy test hiện hữu; ghi khả năng kiểm chứng môi trường | Có ma trận parity và danh sách lỗi có sẵn; không quy lỗi cũ cho migration |
| M1 UX | Duyệt brief; wireframe 2–3 phương án; chọn; chốt tokens/component contract | Có lựa chọn người dùng và ảnh/probe đúng skill |
| M2 Foundation | Domain/Application/Infrastructure/Contracts/Desktop/Web; DI; bridge; publish assets | Build hai stack, handshake/version/error/cancel, chạy offline, không mở API ngoài |
| M3 Documents | F01–F12, F25, F26 và recovery | Chạy xuyên suốt mở/sửa/split/lưu/conflict/recovery; không mất BOM |
| M4 Projects & SQL | F13–F22, F31 | Target isolation; query batches/cancel/result export; discovery UNC |
| M5 Tools | F23–F24, F27–F30 | Catalog draft/save; Profiler live; external tools và dịch có kiểm chứng |
| M6 Cutover | Regression, package, hướng dẫn, đánh dấu trạng thái từng dòng | F01–F31 đạt hoặc giới hạn được nêu và người dùng chấp thuận; rollback thử thành công |

Không dùng một shell React có menu nhưng handler trống để tuyên bố đã chuyển đổi. Chuyển từng vertical slice chạy được từ UI qua Application xuống Infrastructure. Giữ executable cũ tới khi đủ parity.

## 11. Kiểm thử và bằng chứng

- Domain/application: SQL GO lexical cases, error offsets, alias, XML namespaces/XXE, metadata merge, diff whitespace, target isolation, stale responses và cancel ownership.
- File integration trong temp: BOM round-trip, conflict, readonly, Save As, giới hạn file, không để backup/temp cạnh file nguồn, recovery sau reload/crash.
- Transport: origin/method/schema validation, request correlation, unknown version, oversize, cancellation và không tự retry thao tác ghi.
- SQL integration trên database fixture riêng: batches/temp tables, result limits, cancel/timeouts, Catalog create/alter/duplicate/paste/rollback; không dùng DB production để kiểm thử ghi.
- React: thao tác người dùng thật cho open/save/dirty/split/target/context; IME/clipboard/undo; bảng lớn không render toàn bộ DOM.
- Visual: probe.mjs theo skill, mở ảnh 375/1280/1440/1920 và ba theme; thực tế WebView2/DPI/focus phải có kiểm riêng vì trình duyệt thường không chứng minh host hoạt động.
- Publish: .NET Release, frontend typecheck/build, copy asset/Monaco workers, WebView2 Runtime bootstrap/thiếu runtime có thông báo; TraceHost x86 và thư viện SSMS18 được xác nhận.

Lệnh build/test cụ thể sẽ ghi sau khi scaffold chốt; không ghi kết quả PASS khi chưa chạy. Test hiện hữu được giữ và chuyển dần theo owner, không xóa để làm xanh build. Không có SQL/Profiler môi trường thì đánh dấu Chưa kiểm chứng tích hợp, không dùng dữ liệu giả làm bằng chứng.

## 12. Quyết định đã chốt

Stack React/TypeScript + .NET và mục tiêu Clean Architecture do người dùng chọn. Người dùng chọn “dựng luôn”, bỏ wireframe theo nhánh U của skill ui-ux. Bố cục ưu tiên editor, vùng điều hướng File/Chức năng/DB, target theo tab và panel kết quả phía dưới. Giữ ba theme. Không cần duyệt lại các quyết định này để tiếp tục kiểm chứng.
