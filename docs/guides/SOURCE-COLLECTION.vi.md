# Source Collection trong cây file ERP

Chọn **Phiên bản** rồi bấm **Đặt làm mặc định**. Lần sau mở Source Collection từ cây file ERP, phiên bản đó được chọn sẵn và LSCode tự tìm file trong nguồn của phiên bản. Nút **Bỏ mặc định** xóa lựa chọn đã lưu. Bạn vẫn có thể chọn phiên bản khác và bấm **Tìm** cho lượt hiện tại.

## Đồng bộ phiên bản nguồn

Trong Source Collection, mở **Đồng bộ phiên bản**, chọn nhóm rồi bấm **Đồng bộ**. Bảng **Phiên bản / Đường dẫn** hiển thị danh sách đã lưu trên máy.

- **FAW** lấy các thư mục con trực tiếp của `\\172.168.5.14\SourceCollection\FAW`.
- **FBO-FBI** lấy các thư mục con trực tiếp của `\\172.168.5.14\SourceCollection\FBO-FBI`.
- **HRM** đăng ký đúng hai đường dẫn `\\172.168.5.14\CustomerPro\HRM\DEMO24\SP24` và `\\172.168.5.14\CustomerPro\HRM\HRM227\SP227`. Quyền đọc hai thư mục này được kiểm tra khi tìm file.

Tên phiên bản là tên thư mục cuối cùng, ví dụ **FBOR2SP24.2.1**. Đồng bộ cập nhật danh sách đường dẫn của nhóm đã chọn. Nếu không đọc được thư mục mạng, danh sách đã lưu vẫn dùng được. Các phiên bản đã lưu xuất hiện trong **Theo phiên bản** cùng các phiên bản lấy từ profile dự án. Đồng bộ chỉ lưu danh mục đường dẫn, còn file được tìm hoặc sao chép khi bạn bấm lệnh tương ứng.

Gõ trong dropdown **Phiên bản** để lọc theo tên. Trong bảng đồng bộ, ô tìm lọc theo phiên bản hoặc đường dẫn. Sau khi tìm file, kết quả hiển thị thành cây theo thư mục nguồn, thư mục con và file. Bạn có thể lọc theo tên file hoặc đường dẫn và thu gọn từng nhánh. Nhấn **Tìm trong file** để tìm chữ trong nội dung các file văn bản của lượt tìm hiện tại, kể cả file ERP `.f` và `.ent`. File không đọc được hoặc lớn hơn 4 MiB được bỏ qua và báo số lượng. Bộ lọc giữ các dấu tích đã chọn, kể cả file tạm ẩn.

Nhấn chuột phải vào file hoặc thư mục trong cây File của controller rồi chọn **Source Collection**. Khi chọn thư mục, LSCode dùng các file đang hiển thị trong nhánh đó làm mẫu tìm.

1. Chọn phiên bản. Danh sách phiên bản lấy từ danh mục đồng bộ và profile dự án; LSCode tìm trong các đường dẫn nguồn của phiên bản đó.
2. Nhấn **Tìm**. LSCode quét thư mục con theo tên file trước dấu chấm đầu tiên, không phân biệt hoa thường. Kết quả cùng tên thư mục cha trực tiếp được tích sẵn. Đường dẫn nguồn và đường dẫn tương đối được hiển thị để phân biệt các file trùng tên.
3. Sửa lựa chọn nếu cần, chọn thư mục đích rồi nhấn **Sao chép**. Đích mặc định là thư mục dự án đang mở. LSCode giữ cấu trúc đường dẫn tương đối tính từ thư mục nguồn, bỏ qua file đích đã tồn tại và báo số file đã chép hoặc bỏ qua. Nếu hai nguồn có cùng đường dẫn đích, file đã chép trước được giữ lại.

Hộp thoại hiển thị trạng thái đang tìm, lỗi truy cập, kết quả rỗng và số file đã chọn. Kết quả giới hạn 500 file mỗi lượt; nếu chạm giới hạn, chọn thư mục nguồn hẹp hơn. Tìm kiếm không đọc nội dung XML hoặc lần theo include. Quét nguồn bỏ qua liên kết thư mục và file; sao chép từ chối đường dẫn thoát gốc hoặc đi qua liên kết.
