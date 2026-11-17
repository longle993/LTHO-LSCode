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
