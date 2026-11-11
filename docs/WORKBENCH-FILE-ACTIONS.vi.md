# Tab file, thao tác ERP và sao chép Profiler

Bản chạy của đợt cập nhật này nằm tại `artifacts/publish-webview-file-tools/LSCode.WebView.exe`. Giữ cả thư mục khi sao chép sang máy khác.

## Nhận diện file đang mở

- Nếu nhiều tab có cùng tên file, tab hiện thêm phần thư mục ngắn nhất đủ phân biệt. Ví dụ `nhanvien.xml · Dir` và `nhanvien.xml · Grid`.
- Nếu cả thư mục cha cũng trùng tên, nhãn mở rộng thêm các thư mục phía trên. Tooltip vẫn hiện đường dẫn đầy đủ.
- Bộ chọn file ở từng pane khi chia editor dùng cùng cách nhận diện.

## Cây file chức năng ERP

- Chọn file hoặc đặt focus lên thư mục, nhấn **Ctrl+C** để sao chép tên, gồm đuôi với file.
- Chuột phải mở menu **Sao chép tên**, **Sao chép đường dẫn**, **Mở thư mục chứa file** hoặc **Mở thư mục**.
- Với file, menu có thêm **Nhân bản file**. Bản sao nằm cùng thư mục, mang hậu tố `_copy`, `_copy2`… trước phần mở rộng, xuất hiện ngay trong cây controller và được mở trong editor.
- Nhân bản đọc nội dung đã lưu trên đĩa, giữ nguyên byte và BOM. Khi file gốc có thay đổi chưa lưu, bản sao lấy phiên bản trên đĩa.
- Tên đích được tạo độc quyền để các yêu cầu đồng thời nhận tên khác nhau. Bản sao chưa hoàn tất được dọn nếu sao chép thất bại.
- Thư mục hiển thị trong cây vẫn ánh xạ tới đường dẫn vật lý, kể cả các tầng App_Data/Controllers đã ẩn.
- Escape đóng menu và trả focus về mục vừa chọn. Phím mũi tên di chuyển giữa các lệnh trong menu.

## Nội dung SQL Profiler

- Phần SQL dưới lưới là vùng văn bản chỉ đọc, hỗ trợ bôi đen và Ctrl+A.
- **Sao chép SQL** sao chép toàn bộ SQL đang hiển thị. **Ctrl+C** sao chép đoạn đã chọn, hoặc toàn bộ nội dung nếu chưa chọn đoạn.
- Khi focus vùng SQL, ứng dụng dừng theo dõi dòng cuối để sự kiện mới không thay nội dung đang chọn. Bấm **Theo dõi cuối lưới** để theo lại sự kiện mới nhất.
- Thông báo lỗi sao chép hiển thị cùng khu vực lỗi Profiler.

## Các thay đổi DB và kết quả SQL đi kèm

- Script mở từ DB Object kết thúc bằng GO và dòng trống. Stored procedure có đoạn tạo procedure rỗng nếu chưa tồn tại, rồi một batch ALTER chứa nội dung procedure.
- Con trỏ đặt sau GO cuối để nhập lệnh tiếp theo.
- Nhiều bảng kết quả SQL hiện thành hàng nút chọn trực tiếp, có cuộn ngang khi thiếu chỗ.
- Nhấp trái đường dẫn dự án trên thanh công cụ để mở thư mục. Nhấp phải để sao chép đường dẫn.
- Khi web.config không truy cập được và hồ sơ đã đồng bộ có đủ App/SYS, luồng mở dự án dùng cặp cấu hình đã lưu. Khi đọc được tệp, cấu hình hiện tại được ưu tiên.

## Kiểm chứng và xuất bản

Frontend được kiểm tra TypeScript và build Vite. Kiểm thử .NET kiểm tra nhân bản đồng thời, vị trí bản sao, phần mở rộng và nội dung/BOM. Kiểm thử giao diện dùng transport fixture để kiểm tra tab trùng tên, Ctrl+C, menu file/thư mục, nhân bản và sao chép SQL Profiler, cùng các theme và bề rộng màn hình hiện có.

Các kiểm tra giao diện dùng dữ liệu mẫu. Thao tác nhân bản byte được kiểm tra trên tệp fixture thật. Quyền ghi và khả năng mở thư mục mạng phụ thuộc quyền Windows của người chạy.

Kết quả đợt này gồm 78 kiểm tra giao diện, 38 kiểm tra WebView/.NET và 8 mục smoke native đều đạt. Ảnh kiểm chứng nằm tại `artifacts/webview-ui/erp-file-actions.png` và `artifacts/webview-ui/profiler-tab-dark.png`. Bộ kiểm tra giao diện bao gồm ba theme, không có lỗi JavaScript chưa xử lý và không tràn trang ở 375px.

Đóng gói bằng:

```powershell
./build-webview.ps1 -Test -Publish -PublishDirectory artifacts/publish-webview-file-tools
```
