# Menu, DB Object và gợi ý SQL — 0.10

Bản chạy: `artifacts/publish-0.10/LSCode.exe`.

## Nhấp đúp menu

- Bắt sự kiện nhấp đúp ở bước preview và xác định dòng được bấm để tránh bị xử lý bởi cây trước khi mở file.
- Mở file trang tìm được ngay, sau đó tìm controller, tạo/chọn tab file dự án và mở các file XML/F trùng tên controller.
- Ưu tiên ReportExtender; nếu không có thì đọc QueryExtender. Trang không khai báo controller vẫn được mở.
- Nếu đường dẫn không tìm thấy, panel chức năng hiển thị thông báo. Phần lập chỉ mục App_Data lần đầu vẫn có thể phụ thuộc tốc độ ổ mạng.

## DB Object tải theo nhu cầu

- Vào tab DB Object chỉ tạo bốn nhóm, chưa kết nối hay truy vấn danh sách đối tượng.
- Mở một nhóm mới tải 200 đối tượng của nhóm đó. Mở **Tải thêm 200 đối tượng…** để xem tiếp; không giới hạn tổng số đối tượng.
- Phân trang theo object_id, không dùng OFFSET tăng dần và không tải cột của toàn database. Đối tượng hiển thị theo thứ tự object_id.
- Thu gọn/mở lại nhóm đã tải không truy vấn lại. Chuyển qua tab chức năng rồi quay lại giữ danh sách. Nút ↻ làm mới các nhóm và cache gợi ý SQL.
- Đổi dự án hoặc App DB hủy yêu cầu cũ. Timeout/lỗi có trạng thái báo và có thể thử lại.

## SQL

- Catalog gợi ý chỉ đọc tên đối tượng, bỏ TOP 10000 trên phép JOIN tất cả cột vốn có thể làm mất bảng ở cuối danh sách.
- Chỉ tải cột của các bảng xuất hiện trong FROM/JOIN/UPDATE/INTO, bằng truy vấn tham số object_id. Metadata được cache theo kết nối/đối tượng; các yêu cầu trùng dùng chung tác vụ đang tải.
- Khi gõ tiếp, danh sách gợi ý được tính lại từ tiền tố hiện tại. Giới hạn 200 mục của popup không còn làm mất `dmbp` nếu bảng này nằm ngoài 200 kết quả ban đầu cho `dm`.
- SELECT trong cửa sổ cấu trúc dữ liệu dùng chung cơ chế gợi ý với WHERE/ORDER BY. Ctrl+Space mở gợi ý; tên cột được bổ sung sau khi chọn bảng FROM. SELECT vẫn gợi ý cú pháp khi chưa chọn bảng.
- Tên cột trên checkbox dùng TextBlock để dấu `_` không bị coi là phím tắt: `ma_bp`, `ten_bp` giữ nguyên.
- Đọc kết nối/result-set ở hàm truy vấn dùng chung không tiếp tục xử lý từng dòng trên luồng giao diện.
- Cửa sổ tìm bảng bỏ TOP 5000 của catalog; danh sách đề xuất vẫn hiển thị số mục giới hạn để dễ chọn.

Các thay đổi tối ưu truy vấn metadata của ứng dụng, không tự viết lại câu SQL nghiệp vụ người dùng. Nhận diện bảng cho autocomplete là quy tắc đơn giản; SQL động, CTE phức tạp và tên liên database chưa được giải quyết đầy đủ.

## Kiểm chứng

- Build Release không lỗi/cảnh báo; 48 kiểm tra Core và 26 mục smoke WPF đạt.
- Đã kiểm tra điều hướng menu với fixture QueryExtender và xác nhận mở ASPX/XML/F; khởi tạo DB Object không query; nhận diện bảng cần tải cột; `dmbp` trong 501 tên đối tượng; SELECT chèn cột; checkbox giữ nguyên `ma_bp`.
- Báo cáo: `artifacts/smoke-ui-41349fbf338d435d9022774ba28691af/smoke-result.txt`.
- Chưa kiểm thử trực tiếp database và menu trên máy chủ của người dùng, nên chưa có số đo tốc độ thực tế hoặc xác nhận schema/quyền truy cập tại đó.
