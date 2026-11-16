# CR06 — Kiểm tra lại khóa cha và hoàn tất đổi tên khi rời ô

Tài liệu cập nhật trước source.

- Mỗi thay đổi Key, RowKey, tên/type/length khóa hoặc danh sách field của bảng cha phải kiểm tra lại toàn bộ quan hệ con ngay. Không giữ diagnostic hoặc dropdown khóa cũ. Thiếu khóa, mất khóa, sai type/length hoặc reference hết hợp lệ phải báo tại bảng/field liên quan. Không tự xóa dữ liệu field con khi bỏ key cha.
- Gán Parent Table tiếp tục tự sinh khóa như CR04. Thay đổi Key không được âm thầm sửa quan hệ sai trước khi validation thấy lỗi; người dùng có thể gán lại parent để sinh/sửa cấu hình.
- Đổi tên table/field: Enter hoặc rời ô đều commit tên hợp lệ và cập nhật references; Escape hủy. Tên không hợp lệ/trùng giữ nguyên tên cũ, giữ draft và báo lỗi inline. Khi chuyển table/field, commit draft vào đúng đối tượng cũ trước khi chuyển selection.
- Biểu tượng bút của table ở mép phải hàng tiêu đề bảng, field ở mép phải hàng tên field. Giữ kích thước bấm và tên accessibility.
- Kiểm tra regression WPF Enter/blur/Escape/duplicate, đổi key cha và selection bảng con; unit tests về quan hệ sau khi đổi tập khóa/type.

## Kết quả

- Build Release/publish thành công, 154 tests hiện có pass.
- WPF smoke bản publish pass tại `artifacts/change-06-final/`: thêm/bỏ Key và đổi kiểu khóa cha cập nhật lỗi con ngay; Enter/blur lưu tên cùng references; Escape/duplicate/undo/redo và đổi selected field vẫn đúng.
- Đã xem ảnh hàng tiêu đề table/field: bút sát mép phải; Light/Dark và 1024 DIP tiếp tục render. Binding log rỗng. Không thay thế nghiệm thu DPI/bàn phím thủ công.
- Bản chạy: `artifacts/release/DynamicTemplateStudio.App.exe`.
