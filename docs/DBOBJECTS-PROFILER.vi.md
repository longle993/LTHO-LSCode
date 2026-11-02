# DB Objects và Profiler — bản 0.14

Bản chạy: `artifacts/publish-0.14/LSCode.exe` (workspace hiện tại: `D:\FAST\Code\LTHO\LSCode`).

## DB Objects

Nhấn đối tượng hoặc Enter để lấy script vào tab SQL đang active. Nếu tab active không phải SQL, tạo tab SQL mới. Tab dùng đúng SYS/App của đối tượng. Nếu nội dung tab đã thay đổi hoặc tab bị đóng trong lúc đang đọc metadata, tạo tab mới, không ghi đè thay đổi đó. Thao tác thay nội dung có thể Undo.

View/procedure/function dùng `OBJECT_DEFINITION`; bảng tạo SELECT TOP (1000) với danh sách cột, chưa tạo DDL CREATE TABLE. Object mã hóa/CLR/thiếu VIEW DEFINITION báo lỗi cụ thể. Script chỉ được đưa vào editor, không tự chạy.

## Profiler: đã có giao diện, live capture chưa đạt

Theo lựa chọn người dùng, backend hiện dùng **SQL Trace**, giữ login `profile`; không yêu cầu quyền Extended Events và không chạy/reparent PROFILER.EXE. Cửa sổ WPF riêng có chạy/dừng, bảng sự kiện, chi tiết SQL có màu, tìm trong các dòng đã nhận, lưu/mở trace `.lstrace`, chọn template `.tdf`, lưu cấu hình JSON và tài khoản theo server bằng Windows DPAPI. Mật khẩu không nằm trong source hoặc tham số dòng lệnh.

Bridge `LSCode.TraceHost.exe` là .NET Framework x86, nạp `TraceServer` và PFCLNT từ SSMS 18 đã cài. Vì vậy máy chạy cần SSMS 18 và .NET Framework 4.x ngoài .NET 10 Desktop Runtime. Chưa có adapter SSMS 22. Đóng/dừng yêu cầu helper gọi Stop/Close; nếu không xác nhận được việc kết thúc thì cửa sổ báo lỗi. Vòng đời trên phiên đang thu thật chưa được kiểm chứng vì lỗi khởi tạo bên dưới.

Lần đầu chọn `.tdf`; những lần sau tự dùng lại đường dẫn đã lưu. Các sự kiện/filter gốc thuộc file TDF; ô Database bổ sung bộ lọc chính xác tại reader (không phải SQL LIKE, không giảm lượng trace server thu). Để trống để xem mọi database template thu thập. Nếu template không thu cột DatabaseName, bộ lọc database sẽ không có dữ liệu để đối chiếu. Tối đa 5.000 dòng giữ trong UI; có thông báo nếu queue quá tải. Lưu trace giữ các dòng đã nhận, kể cả dòng bị ẩn bởi ô tìm kiếm. `.lstrace` là JSON của LS Code, không phải `.trc` của Microsoft.

## Kết quả tìm CustomerTrace

Đã kiểm tra `C:\Program Files (x86)\Microsoft SQL Server Management Studio 18\Common7`: 88 file `.tdf`, không thấy CustomerTrace theo tên hoặc nội dung chuỗi. Thư mục template roaming hiện tại cũng không có CustomerTrace. Source Fcode có `FCode.myTemplateSQLProfiler.tdf`, chưa có căn cứ xác nhận đó là CustomerTrace.

Có nút chọn Standard của SSMS 18/SQL2014 để thử, nhưng đây **không phải CustomerTrace**. Cần file CustomerTrace gốc để giữ đúng cấu hình cũ. Có thể [export template từ SQL Server Profiler](https://learn.microsoft.com/en-us/sql/tools/sql-server-profiler/export-a-trace-template-sql-server-profiler?view=sql-server-ver17); API [TraceServer.InitializeAsReader](https://learn.microsoft.com/en-us/previous-versions/sql/sql-server-2005/ms212708%28v%3Dsql.90%29) nhận đường dẫn template.

## Kiểm chứng và blocker

- Build Release: 0 lỗi, 0 cảnh báo; 48 kiểm tra Core và 34 kiểm tra WPF smoke đạt. Profiler UI/trace lưu mở dùng fixture, không phải sự kiện server thật.
- Báo cáo/ảnh: `artifacts/smoke-33cce5cf166844dda96dadc24fa5b978/`.
- Server `172.168.5.14\SQL2014`: tài khoản profile kết nối được, `ALTER TRACE=1`, `VIEW SERVER STATE=1`, `ALTER ANY EVENT SESSION=0`. Không thay đổi quyền.
- SQL Trace reader **không khởi tạo được**: `TraceServer.InitializeAsReader → CTraceControllerBase.InitSource → InvalidOperationException` (không có thông điệp native). Gặp với Standard SQL2014, TSQL SQL2019 và file template Fcode cũ; thử TCP trực tiếp cũng lỗi. Kết nối SQLNCLI11 riêng thành công. Chưa xác định nguyên nhân tương thích của reader.
- Chưa xác nhận thu sự kiện, dừng phiên live, độ đúng Duration/CPU hay parity SSMS. Không công bố Profiler đã hoàn thành. Cần xử lý lỗi reader và có CustomerTrace trước khi thay thế workflow cũ.