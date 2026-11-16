# Đường dẫn Profiler lưu một lần — 0.22

Bản chạy: `artifacts/publish-0.22/LSCode.exe`.

Mở Tools → External tools settings. Trong SQL Profiler — SSMS 18, chọn `D:\Program\SSMS 18\Common7\PROFILER.EXE`, rồi Lưu cấu hình. Không cần nhập Crystal hoặc SSMS 22. Cấu hình dùng chung mọi dự án, lưu vào `%LocalAppData%\LSCode\settings.json` (hoặc LSCODE_DATA_HOME nếu được thiết lập).

SQL Trace dùng đúng Profiler18Path đã lưu để tìm Common7/IDE/Microsoft.SqlServer.ConnectionInfo.dll, Common7/IDE/Microsoft.SqlServer.ConnectionInfoExtended.dll và Common7/PFCLNT.DLL. Kiểm tra file khi lưu và trước khi mở trace; lỗi chỉ rõ file thiếu và nơi cấu hình. Không phải khai báo riêng từng DLL. Nếu để trống, thử vị trí cài SSMS 18 mặc định.

Đã kiểm tra các file có tồn tại ở đường dẫn D:\Program\SSMS 18 trên máy này, và lưu đường dẫn vào cấu hình tài khoản hiện tại. Nếu bản cũ còn mở ghi đè settings lúc thoát, chọn/lưu đường dẫn một lần trong bản mới sau khi đóng bản cũ.

Giữ mặc định login profile và template SQL2014.tdf từ bản 0.21. Cấu hình này phục vụ thư viện trace tích hợp, không khởi chạy PROFILER.EXE ngoài.

Kiểm chứng: build/publish thành công không lỗi/cảnh báo; 54 Core checks và 40 WPF smoke checks đạt, gồm lưu/đọc đường dẫn tùy chỉnh và phát hiện thiếu PFCLNT.DLL. Chưa kiểm thử live capture trong lần sửa này.

Báo cáo: `artifacts/smoke-cc2d3e076fca4727aac2039caae292ec/smoke-result.txt`.
