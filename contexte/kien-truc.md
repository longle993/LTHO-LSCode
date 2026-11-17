# Kiến trúc LSCode

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

React nhận thao tác drop và gửi các `File` qua `postMessageWithAdditionalObjects`; host đọc đường dẫn tuyệt đối từ `CoreWebView2File.Path`, chỉ nhận file đang tồn tại, giới hạn 100 file mỗi lượt rồi phát sự kiện `files.dropped`. React dùng lại `document.open`, vì vậy kiểm tra tab trùng, encoding và lịch sử mở file không có luồng thứ hai.

Toolbar editor chỉ dành chỗ cho thao tác theo tài liệu đang mở. Advance Note và SQL Watch vẫn được mở từ thanh lối tắt hoặc bảng lệnh, sau đó xuất hiện dưới dạng tab công cụ; không lặp lại thành nút chữ trên toolbar. Khi cửa sổ hẹp, ô tìm trong file và ô chọn database được phép co theo chiều ngang, còn nút Chạy và các nút icon giữ kích thước để nhãn không bị bẻ thành từng ký tự.

Thời gian query được theo dõi ở React theo từng tab: đồng hồ `performance.now()` cập nhật UI mỗi 100 ms trong lúc request chạy; kết quả thành công thay bằng `QueryResult.durationMs` từ Infrastructure, còn lỗi/hủy lưu thời gian đã trôi qua. Bản đồ thời gian tách theo document ID để đổi tab không hiển thị nhầm. Khung `.studio` dùng token `--window-border` riêng cho từng theme để tạo viền 1 px bên trong cửa sổ WPF không chrome.

Table Tool chỉ còn một bề mặt dữ liệu. `TableDataGrid` dùng chung token và ngôn ngữ tương tác với lưới kết quả SQL: kéo chọn vùng, chọn dòng/cột/toàn bảng, sao chép TSV và sửa ô trực tiếp; thao tác ghi vẫn đi qua `table.write`, metadata khóa chính quyết định khả năng cập nhật. Menu Tập lệnh dùng HTML Popover top-layer và tính vị trí theo viewport để không bị `table-toolbar-slot` cắt. `Ctrl+Alt+D` được App định tuyến theo tab đang chọn, nên tab Table đổi target riêng và giữ tên bảng. `MenuEditor.InsertScriptAsync` đọc schema thực tế của `dbo.wcommand` và `dbo.command`, bỏ cột identity/computed/rowversion rồi sinh literal SQL đúng Unicode, binary, số và ngày. Bridge `project.menu.insertScript` được gọi từ F12 hoặc menu chuột phải của cây chức năng.

## Tải menu ERP và Mobile

Triển khai ngày 17/11/2026: [tối ưu và tích hợp Mobile](../docs/specifications/ERP-MENU-PERFORMANCE-MOBILE.vi.md). WebView gọi `project.commands` với `kind/refresh/context` tới `ProjectDatabase.CommandsAsync` (source Shell được Compile Link vào Infrastructure). Web dùng một connection, đọc schema rồi chạy batch có Startup; `command_id/sysid` nằm trong result cuối. Cache tối đa 8 ngữ cảnh, TTL 5 phút, gộp request đồng thời và invalidate sau khi lưu menu. Cây Web vẫn ba cấp. Mobile đọc `dbo.dvcommand`, dựng không giới hạn độ sâu theo `wmenu_id0`, dùng `bar/bar2` và `sysid`; tab Mobile lazy-load và không bật F3/F12. Source Mobile đi qua fallback `sysid` của `project.controller`, kể cả dự án còn thư mục Main. Chưa có benchmark LAN hoặc smoke database ERP thật.
