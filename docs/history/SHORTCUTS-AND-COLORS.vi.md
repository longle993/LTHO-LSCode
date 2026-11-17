# Phím tắt và màu giao diện — bản 0.5

Bản chạy: `artifacts/publish-0.5/LSCode.exe`.

## Bảng phím tắt

Mở **Phím tắt → Xem và chỉnh sửa phím tắt…**. Bảng hiển thị chức năng và hai phím tắt đang dùng.

1. Chọn ô Phím tắt 1 hoặc Phím tắt 2 và nhấn tổ hợp mới.
2. Backspace/Delete xóa ô; Tab chuyển ô.
3. Nhấn **Lưu** để áp dụng ngay và giữ cấu hình cho lần mở sau. **Hủy** không lưu thay đổi.
4. **Khôi phục mặc định** điền lại các giá trị ban đầu; cần nhấn Lưu để áp dụng.

Mỗi chức năng nhận tối đa hai tổ hợp. Bảng ngăn lưu khi hai ô dùng cùng một tổ hợp và chỉ rõ chức năng bị trùng. Các phím đơn để gõ văn bản không được dùng làm phím tắt; dùng Ctrl/Alt hoặc phím chức năng. Các lệnh biên tập chuẩn như sao chép/dán và các phím điều hướng theo ngữ cảnh vẫn do editor xử lý; tránh gán đè các tổ hợp này.

Tab SQL mới mặc định dùng **Ctrl+N** hoặc **Ctrl+Shift+Q**. **Ctrl+Shift+T** vẫn mở cấu trúc/dữ liệu bảng. Nếu muốn dùng Ctrl+Shift+T cho tab SQL, xóa phím đó ở chức năng xem bảng rồi gán vào hàng SQL mới trước khi lưu.

Cấu hình nằm trong trường `Shortcuts` của `%LocalAppData%/LSCode/settings.json` (hoặc thư mục `LSCODE_DATA_HOME`). Bảng lệnh đọc phím tắt hiện tại từ cùng cấu hình.

## Dòng chọn trong cây

Cây chức năng, cây file và XML Outline dùng nền chọn theo theme. Dark dùng nền xám xanh `#283241` và viền màu nhấn; khi rời chuột hoặc chuyển focus sang editor, nền chọn vẫn giữ nguyên, không chuyển trắng. Nút mở/đóng cây vẫn hoạt động.

## Tô màu

- `<!ENTITY Delta14 "0">`: `<!ENTITY` và `>` màu đỏ, tên `Delta14` giữ màu entity.
- Tên thẻ XML và dấu mở/đóng cùng màu xanh.
- `<![CDATA[` và `]]>` màu xanh, nội dung tiếp tục được phân loại theo ngôn ngữ nhúng.
- SQL độc lập và SQL nhúng dùng chung danh sách từ khóa, bổ sung các kiểu dữ liệu như `int`, `nvarchar`, `decimal`, `datetime2`, cùng `declare`, `select`.
- JavaScript nhận diện trọn tên chứa `$`, truy cập thành viên và callback. `f.add_onResponseComplete` và `on$Filter$ResponseComplete` cùng màu chữ sáng trên Dark; chuỗi, comment và từ khóa vẫn phân biệt màu.

Bộ tô màu dùng quy tắc theo ngữ cảnh, không phải parser JavaScript/XML đầy đủ. Nội suy template literal vẫn cùng màu chuỗi như trước.

## Kiểm chứng

- Build Release: không lỗi, không cảnh báo.
- 46 kiểm tra Core đạt.
- 14 mục smoke WPF đạt, gồm đọc/ghi hai phím tắt, phân tích phím mặc định, màu token trên ba theme và nền chọn Dark khi mất focus.
- Đã xem ảnh render Dark. Kiểm tra token bao gồm tên entity, tên thẻ XML, CDATA trong command/script, kiểu dữ liệu SQL và lời gọi JavaScript có `$`.
- Báo cáo: `artifacts/smoke-ui-6ef61ce9068c4142abeb8cbc63e0b242/smoke-result.txt`.

Chưa kiểm thử thao tác bàn phím thủ công toàn bộ bảng cấu hình và chưa chạy truy vấn tới máy chủ SQL thực trong đợt thay đổi này.
