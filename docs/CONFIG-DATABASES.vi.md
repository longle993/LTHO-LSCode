# App/SYS từ web.config — 0.19

Bản chạy: `artifacts/publish-0.19/LSCode.exe`.

Chỉ dùng hai kết nối có tên `appConnectionString` và `sysConnectionString`. Không đưa ExportFile hoặc database khác vào bộ chọn. Hỗ trợ connectionStrings tách qua configSource.

Nếu App khai báo `%Database`, lấy tiền tố từ tên SYS có hậu tố `_S`/`_Sys`, chỉ kiểm tra đúng hai tên `<tiền tố>_A` và `<tiền tố>_App` bằng DB_ID/HAS_DBACCESS với tham số. Ưu tiên `_A` nếu cả hai truy cập được. Không liệt kê database của server. Không tìm thấy App thì báo lỗi, không âm thầm chuyển sang SYS.

Ví dụ SYS `BACHVIET_FBISP24_S`: App là `BACHVIET_FBISP24_A` hoặc `BACHVIET_FBISP24_App`. Kết nối App giữ server và tùy chọn từ appConnectionString, dùng thông tin xác thực từ sysConnectionString. Placeholder Application Name được thay bằng LS Code. Nếu App có tên DB cụ thể thì dùng trực tiếp, không dò tên hậu tố.

Khi kích hoạt dự án, bắt đầu tải App mặc định. Tab SQL mới dùng App; bộ chọn App/SYS trên toolbar cho phép đổi sang SYS. Cửa sổ dữ liệu chỉ hiện hai DB đã giải quyết. Menu nghiệp vụ vẫn đọc SYS; tab SQL đã mở giữ target riêng. Profiler dùng kết nối App đã giải quyết và tài khoản SYS.

Kiểm chứng: build Release thành công; 49 Core checks và 37 WPF smoke checks đạt. Fixture BACHVIET kiểm tra cả hậu tố A/App, giới hạn hai tên, App/SYS đúng thứ tự, server App và thông tin xác thực SYS, bỏ DB khác và giữ Encrypt. Chưa kết nối dự án BACHVIET thật trong lần sửa này.

Báo cáo: `artifacts/smoke-2a343c1c931d49a2861456b5943704f5/smoke-result.txt`. Publish thành công sau một lần retry file TraceHost bị antivirus khóa tạm thời.
