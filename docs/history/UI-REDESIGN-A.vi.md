# Thiết kế giao diện A — editor làm trung tâm

Bản chạy: `artifacts/publish-ui-a/LSCode.exe`.

## Bố cục được chọn

- Một cột điều hướng rộng 264 DIP (có thể kéo từ 220 đến 420), thay hai cột 280 + 250.
- File / Chức năng / DB dùng cùng một vùng. Search và Snippets thay nội dung cột này khi mở bằng lệnh hoặc phím tắt.
- Mở controller vẫn chuyển đến cây file liên quan. Controller đang chọn, lịch sử và danh sách đầy đủ được giữ lại.
- Thu gọn điều hướng còn 26 DIP. Mở lại qua nút hoặc lệnh File/Chức năng/DB.
- SQL có target của riêng tab cạnh Chạy F5 và Dừng. Menu ⋯ chứa kết nối, chọn App DB, tải lại gợi ý và định dạng.
- Nút thu gọn kết quả nằm cùng hàng tab, bớt một thanh công cụ riêng.

## Bộ giao diện chung

Segoe UI cho giao diện; Cascadia Mono/Consolas cho mã. Giữ màu nhấn xanh ngọc, logo LS và ba theme Dark / Light / High Contrast. Nút chính/phụ, input, checkbox, tab, cây, bảng, thanh chia và hộp thoại lấy từ resource chung. Hover tách khỏi trạng thái chọn. Bảng cao 32 DIP mỗi dòng, header 40 DIP, ưu tiên đường kẻ ngang. Icon cây file và thanh lối tắt dùng Segoe MDL2 Assets có sẵn trên Windows, không thêm thư viện.

Các bề mặt áp dụng gồm shell/editor, chọn dự án, tìm kiếm/snippets/outline, dữ liệu bảng, Profiler, Compare Text/Folder, Beautify SQL, cài đặt công cụ, phím tắt và các hộp thoại dùng chung. Các công cụ phụ vẫn là cửa sổ WPF theo luồng hiện có; bản HTML trước đó là mô phỏng bố cục, không phải chuyển dự án sang web. Giữ bộ tô màu cú pháp chuyên biệt, lịch sử controller và đầy đủ cột dữ liệu thật dù mẫu chỉ có vài mục.

Điểm chỉnh brand: `src/LSCode.Shell/App.xaml` (style/font/resource mặc định) và `MainWindow.xaml.cs::ApplyTheme` (bảng màu của ba theme). Font shell nằm ở `MainWindow.xaml`, font editor ở `EditorTab.cs`.

## Kiểm chứng

- Build Release và publish vào thư mục riêng.
- 120 kiểm tra lõi đã qua.
- Bộ smoke WPF kiểm tra lưu/reload file, split, completion, shortcut, target SQL theo tab, compare/undo, Profiler fixture, bảng lớn và các luồng hiện có.
- Thêm kiểm tra chuyển File/Function/DB/Search/Snippets, thu gọn/khôi phục, dấu chọn, độ rộng editor ở 1280/980 DIP và render ba theme.
- Ảnh và báo cáo bản cuối: `artifacts/ui-a-validation/`.
- Thử nghiệm dùng dữ liệu giả trong thư mục smoke riêng; không chạy SQL lên database khách hàng. Chưa xác nhận tích hợp SQL/Profiler với máy chủ thật trong lượt thiết kế này.
- Build có cảnh báo NU1900 do không truy cập được nguồn dữ liệu kiểm tra lỗ hổng NuGet; build/restore từ package hiện có vẫn thành công.

Chạy lại: `./build.ps1 -Test -Smoke -Publish -PublishDirectory artifacts/publish-ui-a`.

Source trước khi sửa được lưu ở `artifacts/ui-redesign-backup/`. Các bản publish trước được giữ nguyên. Workspace hiện không có Git metadata, nên không có commit/diff Git cho lần thay đổi này.
