# Cây thư mục ERP, đổi tên và tìm bảng SQL

Bản chạy nằm tại `artifacts/publish-webview-controller-tree/LSCode.WebView.exe`. Khi chuyển máy, sao chép cả thư mục chứa EXE.

## Cây thư mục trong tab File

- Chọn một chức năng ERP để xem các file liên quan của controller trong tab **File**. Cây giữ các tầng thư mục vật lý như `App_Data/Controllers` và cho phép thu gọn từng nhánh.
- Chọn controller khác sẽ mở nhóm mới và thu gọn các nhóm còn lại. Bấm tên nhóm để mở lại cây của controller đã chọn. Các file đã mở vẫn nằm trên thanh tab.
- Main, Dir, Filter, Grid, Lookup, Report và Templates có màu và gạch chân riêng. Gạch chân sáng hơn khi tab đang chọn nằm trong thư mục đó. Icon và gạch chân tab file theo loại file, như XML và .f.
- Tab file trùng tên hiển thị thêm phần thư mục để phân biệt.

## Đổi tên file và thư mục

1. Chọn mục trong cây rồi nhấn **F2**, hoặc nhấp phải và chọn **Đổi tên**.
2. Nhập tên mới rồi nhấn **Enter**. Với file, hộp đổi tên chọn sẵn phần tên trước đuôi file.
3. Cây cập nhật đường dẫn mới và giữ các nhánh đang mở. Tab file được đổi tên cập nhật ngay. Khi đổi tên thư mục, mọi tab đang mở bên trong dùng đường dẫn mới.

Nội dung chưa lưu, encoding và BOM được giữ khi đổi tên. Lần lưu tiếp theo ghi vào đường dẫn mới. Lệnh đổi tên thực hiện trong cùng thư mục cha và báo lỗi nếu tên đích đã tồn tại. Các thao tác sao chép tên, sao chép đường dẫn, mở thư mục và nhân bản vẫn có trong menu chuột phải.

## Tìm bảng và nhập SQL

- **Cấu trúc và dữ liệu** tải danh mục tên bảng theo kết nối hiện tại. Sau khi tải, các ký tự gõ tiếp được lọc ngay trong danh mục. Có thể tìm theo tên, `schema.bảng` hoặc nhiều từ cách nhau bằng dấu cách. Kết quả trùng khớp được ưu tiên, rồi tới kết quả khớp tiền tố.
- Nhấp một bảng trong gợi ý hoặc chọn bằng mũi tên và **Enter** để đọc ngay theo chế độ **Dữ liệu** hoặc **Cấu trúc**. Nút **Tải lại danh sách bảng** cập nhật danh mục sau khi có thay đổi schema.
- Các ô **Cột SELECT**, **WHERE** và **ORDER BY** gợi ý tên cột và cú pháp SQL phù hợp, như `COUNT()`, `COALESCE()`, `BETWEEN`, `IS NOT NULL` và `DESC`. Dùng **Ctrl+Space** để mở gợi ý, mũi tên để chọn và **Enter** để chèn.
- Hàm được chèn cùng dấu ngoặc và đặt con trỏ vào phần tham số. Ô SELECT chấp nhận biểu thức như `SUM([tien]) AS total`. Có thể dùng `total DESC` trong ORDER BY.
- Chọn bảng khác sẽ đặt lại bộ lọc và cột đang nhập để đọc bảng mới. Trình xem thực thi câu SELECT chỉ đọc.

## Kiểm chứng

- 84 kiểm tra WebView/.NET và SQL Server cục bộ đạt, gồm đổi tên file, thư mục, đổi riêng hoa/thường, lưu tiếp bản đang sửa và đọc dữ liệu với biểu thức SQL.
- 75 kiểm tra giao diện với dữ liệu mẫu đạt, gồm chuyển controller, F2, vùng SQL Profiler, Ctrl+Space, mở 36 file XML và hiển thị ở 375px.
- 8 mục smoke WebView2 trên bản đã đóng gói đạt. Ảnh kiểm chứng nằm trong `artifacts/webview-ui` với tên `erp-folder-tree.png`, `erp-rename.png` và `table-catalog-sql.png`.

## Profiler và nhiều tab file

- Ở bố cục trên dưới của Profiler, vùng SQL cao hơn để đọc được nhiều dòng hơn.
- Khi mở nhiều file cùng lúc, ứng dụng thêm các tab theo một lượt và chỉ khởi tạo editor cho file được xem. Nhãn tab và trạng thái chưa lưu được tái sử dụng. Mỗi lượt kiểm tra thay đổi trên đĩa ưu tiên tab đang chọn và kiểm tra luân phiên tối đa ba tab nền.

## Đóng gói

```powershell
./build-webview.ps1 -Test -Publish -PublishDirectory artifacts/publish-webview-controller-tree
```
