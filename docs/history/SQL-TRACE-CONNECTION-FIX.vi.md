# Sửa kết nối SQL Trace — 0.15

Bản chạy: `artifacts/publish-0.15/LSCode.exe`.

Lỗi `Keyword not supported: 'trust server certificate'` phát sinh do chuỗi từ Microsoft.Data.SqlClient được chuyển nguyên sang System.Data.SqlClient của .NET Framework. Bridge nay nhận từng trường qua JSON/stdin rồi tạo cấu hình bằng API của Framework. Giữ giá trị TrustServerCertificate và Encrypt; Encrypt=Strict báo không hỗ trợ, không tự hạ mức. Không ghi mật khẩu vào source/log hoặc dòng lệnh.

Kiểm chứng: build 0 lỗi/0 cảnh báo; 35 WPF smoke checks đạt. Test mới chạy executable Framework thật với cả hai giá trị TLS và mật khẩu giả có dấu nháy, dấu chấm phẩy, Unicode. Báo cáo: `artifacts/smoke-f6099e791dd6410da2cb35c98b070521/smoke-result.txt`.

Thử tài khoản TDTU_FAP trên server trước đó `172.168.5.14\SQL2014`: SqlClient trả Login failed. Reader không còn lỗi keyword nhưng vẫn không khởi tạo được. Cần xác nhận đúng server/instance và tài khoản; chưa xác nhận live capture thành công. Trong Profiler, dùng Kết nối… để đổi login đã lưu trước đó. Không tự ghi đè thông tin đăng nhập đã lưu bằng tài khoản đang bị server từ chối.
## Cập nhật 0.16 — đúng instance SQL2008

Người dùng xác nhận server `172.168.5.14\SQL2008`, database `TDTU_FAP_App`. Đăng nhập TDTU_FAP thành công; server trả ProductVersion `10.50.1600.1`, quyền ALTER TRACE = 0. Bridge đã thêm kiểm tra quyền trước khi nạp TraceServer để phân biệt lỗi đăng nhập/thiếu quyền với lỗi thư viện. Không thay đổi quyền server và chưa tạo trace bằng tài khoản này.

Bản chạy `artifacts/publish-0.16/LSCode.exe`; build 0 lỗi/cảnh báo và 35 smoke checks đạt. Cần tài khoản có ALTER TRACE trên đúng instance để kiểm chứng live capture tiếp.