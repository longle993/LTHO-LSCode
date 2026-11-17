# Tô màu SQL và JavaScript nhúng trong XML

Bản chạy cập nhật: `artifacts/publish-0.4/LSCode.exe`.

- SQL trong giá trị entity và `<command>` dùng quy tắc SQL: keyword không phân biệt hoa/thường, chuỗi và comment riêng màu.
- `<script>`, entity tên bắt đầu bằng `Script`/`Javascript`, và file `.js`/`.ts` dùng quy tắc JavaScript; tên hàm có `$` là một identifier thống nhất.
- CDATA không phủ màu lên toàn nội dung. CDATA nằm ngoài command/script dùng bộ quy tắc hỗn hợp; các entity khác mặc định SQL. Đây là nhận diện tô màu theo ngữ cảnh/tên, không phải parser đầy đủ cho mọi ngôn ngữ nhúng.
- Comment `//`, `/* */`, chuỗi có escape và template literal được phân biệt; nội suy `${…}` trong template literal hiện vẫn cùng màu chuỗi.
- DTD/entity chỉ được tô màu văn bản, không được thực thi hoặc nạp tài nguyên bên ngoài.

Đã kiểm tra token trên Dark/Light/High Contrast, cả chuyển ngữ cảnh entity → XML → command → script và tên `on$Voucher$GridRender`. Đã xem ảnh render Light theme. 46 kiểm tra Core và 12 mục smoke WPF đạt. Build/publish thành công; NuGet báo NU1900 vì không tải được dữ liệu audit từ nuget.org, không phải lỗi biên dịch.
