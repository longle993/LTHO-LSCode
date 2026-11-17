# Màu giao diện, lưới SQL và điều hướng

Bản chạy nằm tại `artifacts/publish-webview-workflow/LSCode.WebView.exe`. Khi chuyển máy, sao chép cả thư mục chứa EXE.

- **Cài đặt → Giao diện & công cụ → Màu nhấn** cho phép chọn màu, lưu và áp dụng vào các nút, trạng thái chọn và viền nhấn. Chọn **Màu mặc định** để dùng màu của theme Light, Dark hoặc High Contrast.
- **Cấu trúc và dữ liệu** ghim tiêu đề cột khi cuộn dọc. Thanh cuộn ngang cao 14 px. Kéo để chọn vùng ô hoặc chọn đầu dòng, đầu cột, rồi nhấn **Ctrl+C**. Dữ liệu được chép theo định dạng cột tab, dòng xuống hàng để dán vào Excel.
- **Gợi ý bảng** trả danh sách ngắn trong lúc tải danh mục. Sau khi có danh mục, ứng dụng lọc tại chỗ và dùng lại danh mục tối đa năm phút khi mở lại công cụ trên cùng kết nối. Dùng nút **Tải lại danh sách bảng** sau khi đổi schema.
- **Cây File ERP** tìm controller theo tên khi chỉ có file `.f` và lần theo các controller Grid được tham chiếu trong cả `.f` và `.xml`.
- **Tìm kiếm** có dropdown **Phạm vi tìm kiếm**. Sau khi chọn controller, có thể giới hạn tìm nội dung trong một thư mục liên quan và các thư mục con. Có thể chọn **Toàn dự án** để tìm từ thư mục gốc.
- **SQL Profiler** và **Cấu trúc và dữ liệu** xuất hiện cạnh đường dẫn file, ngay dưới hàng tab file. Bấm tab công cụ để chuyển qua lại. Nhấn `Ctrl+Shift+T` để mở thêm tab **Cấu trúc và dữ liệu**, mỗi tab giữ bảng, bộ lọc và kết quả riêng.
- **File dự án** gom cây file theo controller. Khi mở một controller, các nhóm còn lại tự thu gọn. Đường màu phân loại nằm ở mép trên tiêu đề thư mục.
- **Ctrl+G** mở hộp **Đi đến phần trong file**. Gõ tên hoặc chọn một mục như `fields`, `script`, `command`, `css`, `toolbar` để chuyển con trỏ tới vị trí đó trong nội dung đang chỉnh sửa. Có thể chọn tiêu đề Markdown, khai báo SQL và vùng `#region` trong các loại file tương ứng.

Đã qua 83 kiểm tra giao diện bằng dữ liệu mẫu và 85 kiểm tra .NET cùng SQL Server cục bộ. Các ca giao diện kiểm tra cả gợi ý bảng khi danh mục đầy đủ chưa trả về và dùng lại danh mục khi mở lại công cụ. Tốc độ trên máy chủ ERP thực tế chưa được đo.

```powershell
./build-webview.ps1 -Test -Publish -PublishDirectory artifacts/publish-webview-workflow
```
