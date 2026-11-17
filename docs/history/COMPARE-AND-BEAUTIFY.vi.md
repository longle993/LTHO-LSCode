# Compare Text, Compare Folder và Beautify SQL — 0.20

Mở `artifacts/publish-0.20/LSCode.exe`. Ba công cụ nằm trong menu Tools, hoặc tìm bằng Ctrl+Shift+P. Có thể gán phím tắt qua Editor → Phím tắt.

## Compare Text

Hai ô cho phép dán nội dung hoặc mở file. Ô trái nhận vùng chọn hoặc toàn bộ tab đang mở. Nhấn So sánh để xem số dòng hai bên, dòng thêm (+ xanh) và dòng xóa (− đỏ). Có tùy chọn chỉ hiện khác biệt. Khi sửa nội dung, kết quả cũ được xóa để tránh nhầm.

So sánh theo dòng, bỏ qua khác biệt CRLF/LF; vẫn phân biệt chữ hoa/thường và khoảng trắng. Dùng bộ so sánh hiện có, giới hạn tích số dòng hai bên ở 4 triệu. File mở bằng bộ đọc encoding của ứng dụng. Không ghi ngược vào file gốc.

## Compare Folder

Chọn hoặc nhập hai đường dẫn rồi nhấn So sánh. Đọc đệ quy, gồm file ẩn và thư mục rỗng; hiển thị file giống nhau, khác nội dung, chỉ bên trái/phải, khác loại file/thư mục, lỗi đọc và liên kết bị bỏ qua. Có nút Dừng và bộ lọc chỉ khác biệt.

File được so sánh theo kích thước và SHA-256 nội dung, không chỉ dựa vào ngày sửa. Đọc theo luồng để không nạp toàn file vào RAM. Không đi theo junction/symlink. Nhấp đúp một cặp file giống/khác để mở Compare Text; file không đọc được như văn bản sẽ báo lỗi. Kết quả là ảnh chụp trong lúc quét; quét lại nếu thư mục thay đổi. Công cụ không đồng bộ, sao chép hay xóa file.

## Beautify SQL (Beauty SQL)

Mở cửa sổ với vùng chọn hoặc toàn bộ tab hiện tại, hoặc dán SQL mới vào ô trái. Nhấn Beautify SQL để xem kết quả bên phải: từ khóa viết hoa, thụt lề bằng tab 4 cột, giữ comment và `GO`, xuống dòng các mệnh đề. Dùng ScriptDom SQL150 theo profile SQLBeautyFormat legacy giống chức năng Format hiện có; SQL không hợp lệ hiển thị vị trí lỗi.

Sao chép kết quả hoặc Áp dụng vào tab gốc. Áp dụng chỉ thay vùng chọn ban đầu và có thể Undo; nếu tab gốc đã sửa/đóng thì không ghi đè. Không tự lưu file hoặc thực thi SQL. Editor → Format SQL/XML/JSON vẫn giữ nguyên.

## Kiểm chứng

Build Release: 0 lỗi, 0 cảnh báo. 54 Core checks và 38 WPF smoke checks đạt. Publish thành công sau một retry do antivirus khóa tạm TraceHost.

Báo cáo: `artifacts/smoke-becfd44f52c94e79a55cd1e60bf3bf8e/smoke-result.txt`.
