> Mới nhất: [Explorer gọn, tab thấp và DB lazy load](WEBVIEW-EXPLORER-COMPACT.vi.md).

﻿# Cập nhật workbench WebView — 10/11/2026

Bản này dùng React + TypeScript / WebView2 và .NET với các lớp Domain, Application, Infrastructure, Desktop. Bố cục explorer bên trái, editor ở giữa, kết quả SQL bên dưới ưu tiên sửa controller và truy vấn dữ liệu.

## Các thay đổi

| Yêu cầu | Cách sử dụng / triển khai |
|---|---|
| Tự tải chức năng | Chọn dự án tự tải menu từ SYS ở nền. Lỗi tải hiển thị trong workbench. |
| Menu gọn | Bỏ icon cạnh chức năng; caption chỉ có dấu trừ thành đường phân cách ngang. |
| Database | Tự tải object khi target tab thay đổi, bỏ kết quả cũ và chặn response trễ ghi nhầm database. Nhận profile có WebConfigPath, không đòi lưu trùng SysConnectionString. Profile cơ bản dùng server/database và thông tin xác thực catalog. |
| Thanh cửa sổ | WPF WindowStyle=None, WebView2 non-client drag region. Nút riêng thu nhỏ, phóng to/khôi phục và đóng; kéo/nhấp đúp vùng titlebar; resize bằng viền. Đóng vẫn kiểm tra file chưa lưu. |
| Controller explorer | Mỗi chức năng mở thêm nhóm controller, gập nhóm/thư mục và đóng riêng từng nhóm. Không dựng cây toàn bộ dự án trong explorer. Quick Open và tìm kiếm dự án vẫn hoạt động. |
| Icon file | Folder vàng; ASPX tím; .f hổ phách; XML cam; XLSX xanh lá; FRX hồng; RPT xanh dương; TXT xám. Spreadsheet/report/text có icon riêng, đuôi file luôn hiện. |
| Sao chép bảng | Nhấp cell, Shift+nhấp chọn vùng, Ctrl+C sao chép. Nhấp số dòng để chọn dòng; tiêu đề cột để chọn cột. Thanh thao tác sao chép ô/vùng, dòng, cột. TSV quote tab/xuống dòng; NULL thành ô trống. Sao chép cả dữ liệu đã tải ngoài vùng virtual scrolling. |
| Profiler | Nhập thông tin đăng nhập trong hộp Profiler. Mật khẩu chỉ trong bộ nhớ phiên, không ghi settings; target SQL giữ xác thực riêng. Nhập/chọn PROFILER.EXE trực tiếp, kiểm tra thư viện rồi lưu đường dẫn. Chọn SSMS Trace hoặc Extended Events. |
| Mở chương trình | Nút mũi tên trên titlebar mở ProgramPath của dự án: file, thư mục hoặc HTTP(S). Khai báo trong chi tiết dự án. Đường dẫn tương đối tính từ SourcePath. |
| Theme | Icon SunMoon khác icon Cài đặt. Có thể thêm lệnh Đổi theme vào activity bar qua Cài đặt. |
| Chia editor | Ctrl+\ hoặc Chia editor. Mỗi pane chọn file độc lập. Nhấp pane để chọn nơi mở file/thực thi thao tác. Cùng file dùng chung model; caret/scroll riêng theo pane. |
| App/SYS | Ctrl+Alt+D chuyển target tab hiện tại giữa các kết nối dự án, giữ target các tab khác. Có thể đổi shortcut trong Cài đặt. |
| Chi tiết dự án | Chuột phải lên dự án để xem metadata đã che bí mật và sửa tên, server, database, SourcePath, ProgramPath. Lưu xong áp dụng lại dự án. Giữ trường XML tùy biến. Kết nối dự án đồng bộ lấy từ web.config; đồng bộ lại khi đổi kết nối. |
| Khởi động | Hiện hộp chọn dự án ngay cả catalog rỗng. Monaco/workbench thành chunk riêng, tải sau picker; tái sử dụng bootstrap khi vào editor. Recovery không chặn picker. |
| XML view | Đặt caret hoặc hover lên object trong item để hiện kích thước và phép cộng ở status bar/hover. |

## Quy tắc kích thước XML

Với kích thước `120, 30, 70, 100, 100, 130` và item `110100: [ma_nv].Label, [ma_nv], [ten]`:

- Mỗi bit 1 bắt đầu object mới; bit 0 sau nó kéo dài object qua cột tiếp theo.
- `1 | 10 | 100` tương ứng `120 | 30 + 70 | 100 + 100 + 130`.
- `[ma_nv].Label = 120 px`, `[ma_nv] = 100 px`, `[ten] = 330 px`.
- Mỗi view dùng dòng kích thước của chính nó. Bỏ qua comment; input thiếu/sai không tạo hint. Không nạp DTD/external entity.

## Khởi động và giới hạn kiểm chứng

Gói JavaScript picker khoảng 161 kB trước gzip, tách khỏi workbench/Monaco khoảng 3,43 MB. Đây là giảm phần tải trước picker, không phải số đo tổng thời gian khởi động desktop. WebView2 Runtime, DPAPI, ổ đĩa/mạng và dò App database vẫn ảnh hưởng thời gian vào editor. Performance measure `startup.picker` đo riêng bootstrap UI; transport mô phỏng không đại diện SQL/catalog thật.

SSMS 18 Trace cần TraceDefinitions tương thích phiên bản server. SQL Server 2022 trên máy được kiểm tra bằng Extended Events; không suy ra SSMS 18 Trace tương thích mọi server. Login profiler cần tồn tại và có quyền phù hợp; đợt này không tạo login hoặc đổi quyền database khách hàng.

Không có ảnh tham chiếu đính kèm, nên explorer dựng theo mô tả VS Code: nhiều nhóm controller, cây thư mục gập/mở và đóng riêng.

## Build và kiểm thử

`./build-webview.ps1 -Test -SqlIntegration -Smoke -Publish` kiểm tra Core, bridge/.NET, SQL/Profiler thật bằng database fixture riêng, WebView2 smoke và publish.

`node scripts/preview-webview.mjs`, sau đó `node scripts/test-webview-ui.mjs <thư-mục-có-playwright>` kiểm tra tương tác với transport fixture. Đây là kiểm thử UI, không thay thế native/SQL.

Bản chạy: `artifacts/publish-webview/LSCode.WebView.exe`. Giữ Assets, wwwroot và thư viện đi kèm khi sao chép thư mục publish.

## Kết quả xác minh

- 137 kiểm tra Core hiện có đã qua.
- 59 kiểm tra WebView/.NET/SQL đã qua, gồm tải schema thật, sửa metadata dự án, tìm file controller con, tính kích thước XML và thu/dừng Extended Events.
- Smoke WebView2: 8 mục đã qua, gồm bridge, Monaco và mở/lưu file giữ BOM.
- Probe giao diện: không phát hiện lỗi đo được tại 375, 1280, 1440, 1920 px; ảnh/báo cáo ở artifacts/webview-ui/probe-revision-final.
- Bộ kiểm thử UI dùng transport fixture; báo cáo nằm tại artifacts/webview-ui/interaction-report.json.

Monaco 0.52 có timer tô sáng ký hiệu phát sinh rejection Canceled khi đổi model nhanh. Bản này tắt occurrencesHighlight, giữ selectionHighlight, để chuyển file độc lập giữa các pane không phát sinh lỗi nền này. Không chặn hoặc nuốt lỗi JavaScript toàn cục.

Kiểm thử tương tác cuối: 37/37 mục đã qua, không có lỗi JavaScript chưa xử lý; gồm nhiều controller, đóng riêng nhóm, file độc lập giữa hai pane và sao chép dữ liệu ngoài vùng render. Bản publish đã đối chiếu index.html với dist bằng SHA-256 và khớp nhau.
