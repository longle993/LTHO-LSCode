# SQL Profiler mặc định — 0.21

Bản chạy: `artifacts/publish-0.21/LSCode.exe`.

Mỗi lần mở cửa sổ Profiler dùng SQL login `profile` với mật khẩu mặc định do người dùng cung cấp. Tài khoản dự án và tài khoản Profiler cũ đã lưu không ghi đè mặc định này. Trong cửa sổ hiện tại vẫn có thể chọn Kết nối để đổi tài khoản.

Template mặc định là `src/Template/SQL2014.tdf`, được chép nguyên vẹn vào `Template/SQL2014.tdf` cạnh executable khi build/publish. Profiler tự chọn template này, không cần chọn file lần đầu, không phụ thuộc đường dẫn workspace trên máy khác. Mỗi lần mở cửa sổ dùng lại mặc định; đổi template trong cửa sổ chỉ áp dụng cho cửa sổ đó. Bộ lọc database mặc định lấy DB App của dự án hiện tại.

Kết nối SQL thông thường và App/SYS không đổi. Bridge SQL Trace vẫn dùng SSMS 18/.NET Framework và kiểm tra ALTER TRACE trước khi bắt đầu. Mật khẩu truyền sang helper qua stdin, không đưa vào command line.

Kiểm chứng: build/publish thành công, 0 lỗi/cảnh báo; 54 Core và 39 WPF smoke checks đạt. Kiểm tra tài khoản mặc định, SQL authentication, giữ target/TLS, template đóng gói và request sang helper. SHA-256 template publish trùng file nguồn. Chưa kiểm thử live capture với server thật trong lần sửa này.

Báo cáo: `artifacts/smoke-6327816316fb4145a904e8c5244e9ac0/smoke-result.txt`.
