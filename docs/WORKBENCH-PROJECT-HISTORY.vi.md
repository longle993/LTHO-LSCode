# Lịch sử file và thao tác dự án

Bản chạy nằm tại `artifacts/publish-webview-project-history/LSCode.WebView.exe`.

## Lịch sử file

- Chọn **Lịch sử** ở thanh bên. Cây hiển thị tên dự án, thư mục trực tiếp chứa file và tên file. Ví dụ, `Controller/Dir/abc.xml` xuất hiện dưới **Dự án → Dir → abc.xml**. File ở thư mục gốc nằm ngay dưới tên dự án.
- Gõ vào ô **Tìm tên file hoặc thư mục** để lọc ngay trên tất cả dự án. Kết quả giữ cây **dự án → thư mục → file**. Ô tìm khớp một phần tên file hoặc thư mục trực tiếp, bỏ qua chữ hoa và chữ thường.
- Một file được ghi nhận sau khi editor hiển thị nội dung. Khi mở hàng loạt, các tab nền được ghi nhận khi bạn chuyển sang xem từng tab.
- Lịch sử lưu đường dẫn tương đối theo từng dự án và gộp các lần mở trùng file. Khi chọn file thuộc dự án khác, ứng dụng xử lý thay đổi chưa lưu, đóng các tab của dự án hiện tại rồi chuyển sang dự án chứa file.
- Bấm **×** cạnh file để bỏ mục khỏi lịch sử. File trên đĩa và tab đang mở được giữ lại. Dự án tự ẩn khi mục cuối cùng bị xóa.

## Hai nút trước tên dự án

- **Làm mới web.config** ghi lại nội dung hiện có của `web.config` trong dự án. Ứng dụng dùng đường dẫn cấu hình đã lưu hoặc `web.config` tại thư mục gốc. ASP.NET theo dõi thay đổi của file cấu hình để nạp lại ứng dụng. [Tài liệu Microsoft](https://learn.microsoft.com/en-us/previous-versions/aspnet/ms178683(v=vs.100))
- **Xóa Structure/App** hiện đường dẫn và số file trước khi xác nhận. Sau khi xác nhận, ứng dụng xóa file trong thư mục đích và các thư mục con, giữ lại các thư mục. Ứng dụng ưu tiên `Controller/Structure/App`, sau đó tìm một đường dẫn tương ứng duy nhất trong `Controller` hoặc `Controllers` dưới thư mục dự án, `App_Data` hoặc `AppData`. Các liên kết file và thư mục được bỏ qua khi quét.

Kiểm tra thao tác file trên thư mục mẫu riêng. Việc IIS nạp lại ứng dụng cần được xác minh trên môi trường triển khai.

```powershell
./build-webview.ps1 -Test -Publish -PublishDirectory artifacts/publish-webview-project-history
```
