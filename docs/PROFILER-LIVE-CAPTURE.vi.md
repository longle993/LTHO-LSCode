# Profiler bắt được sự kiện thực tế — 0.24

Bản chạy: `artifacts/publish-0.24/LSCode.exe`.

## Nguyên nhân và bản sửa

1. TraceServer/PFCLNT tìm `Profiler/TraceDefinitions` tương đối với executable helper. Chỉ nạp DLL và đổi working directory sang SSMS chưa đủ. Ứng dụng nay tự chuẩn bị helper và dữ liệu TraceDefinitions lấy từ SSMS đã cấu hình vào cache riêng dưới thư mục dữ liệu LSCode. Cache theo hash helper/nội dung definition, cập nhật khi file thay đổi. Không cần chép DLL/definition thủ công vào bản publish hoặc thay registry.
2. Template gốc `src/Template/SQL2014.tdf` không thu `DatabaseName`, trong khi bộ lọc client so sánh database chính xác. Vì vậy reader có thể hoạt động nhưng mọi dòng đều bị bỏ. Bản `SQL2014.WithDatabase.tdf` bổ sung cột 35 (DatabaseName) cho sáu event và danh sách cột. Giữ nguyên các event/filter và mọi byte khác, không sửa template gốc. Có script tái tạo `src/Template/Build-DatabaseTemplate.ps1` chỉ chấp nhận đúng hash nguồn đã kiểm chứng.
3. Sau Stop/Close/Dispose, PFCLNT trên máy này lỗi/hang khi CLR shutdown (0xC0020001). Helper nay chờ luồng Stop hoàn tất, không Stop đồng thời với Close, gửi xác nhận `stopped` sau dispose. Chỉ khi cleanup thành công, kết thúc riêng helper bằng TerminateProcess để bỏ qua bước dỡ mixed-mode DLL đang lỗi. Ứng dụng yêu cầu xác nhận cleanup và exit code 0; không coi việc helper bị kill/crash không có xác nhận là dừng thành công.
4. Thông báo lỗi giữ chuỗi inner exception, HRESULT và giai đoạn kết nối/nạp DLL/khởi tạo/đọc; che mật khẩu. Template tùy chọn không có DatabaseName sẽ báo rõ thay vì âm thầm bỏ mọi dòng khi lọc DB.

Mặc định vẫn dùng tài khoản profile và SSMS 18 đã lưu. Template mặc định là bản SQL2014 có DatabaseName; template gốc cũng đi kèm. Đóng bản cũ, mở 0.24, chọn dự án và Tools → LS Code Profiler. Mặc định lọc DB App đang chọn; chạy thao tác ứng dụng hoặc câu SQL trong DB đó để xem sự kiện.

## Kiểm chứng

- Release build/publish không lỗi/cảnh báo; 54 Core và 43 WPF smoke checks đạt.
- Helper thật kết nối instance SQL2014, bắt được SELECT đánh dấu trên master và BACHVIET_FBISP24_A, Stop/Close/Dispose xác nhận và exit code 0.
- Kiểm chứng end-to-end bằng chính SqlTraceSession: tạo cache helper, nhận sự kiện SQL App qua JSON/queue, khớp bộ lọc DB và DropAsync thành công. Reader dùng profile; câu SQL App thử dùng tài khoản dự án BACHVIET. Không cấp thêm quyền hoặc thay dữ liệu nghiệp vụ.
- Một số lần thử có timeout mạng/pre-login; retry sau đó thành công. Chưa đánh giá tải cao hoặc độ chính xác thời gian/CPU với mọi loại event.
- Báo cáo: `artifacts/smoke-4098c890339f4839a4d1ab710f38c2d8/smoke-result.txt` và `live-profiler-result.txt`.

Tham khảo API: [Microsoft TraceServer.InitializeAsReader](https://learn.microsoft.com/en-us/previous-versions/sql/sql-server-2005/ms212708%28v%3Dsql.90%29). Nguyên nhân và kết quả sửa ở trên được xác minh bằng thử nghiệm trên máy này.
