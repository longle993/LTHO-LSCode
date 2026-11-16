# CR04 — Inline rename field và kế thừa khóa cha

Đặc tả cập nhật trước source theo yêu cầu người dùng.

## Rename field

- Icon bút cạnh tên field trong inspector thay Rename bằng chữ. Bấm bút/F2/context Rename Field mở TextBox ngay tại tên field, tự focus/select-all; không mở dialog.
- Enter hoặc blur/đổi selection commit qua GraphEditor; Esc hủy buffer (cập nhật theo [CR06](change-request-06.md)). Tên trống/sai/trùng báo lỗi tại ô. Một undo unit khôi phục tên và toàn bộ references/parent-key mapping.
- Lối gọi từ lưới đưa người dùng về Field inspector và mở cùng editor.

## Bảng con phải có đủ khóa cha

- Tất cả Business Key fields của parent phải có cột tương ứng trong child, kể cả parent đang bật Generated RowKey. Với composite key phải đủ từng thành phần.
- Khi gán Parent Table, tự thêm các cột còn thiếu, giữ SQL type và Max length của cha, Required=true; Property mặc định `@#Parent.KeyField` để lấy giá trị đã mapping của đúng record cha.
- Không tự tick Business Key của child: parent có nhiều child nên các giá trị FK được phép lặp. Nếu dùng RowKey, `_<Parent>RowKey` vẫn là technical column do parentKey sinh, không tạo một field thường trùng technical column.
- Nếu đã có cột cùng tên hoặc custom destination trong relation: tái sử dụng; khi đúng type, nối mapping về khóa cha. Nếu khác type, giữ dữ liệu người dùng và báo lỗi, không ghi đè type hoặc tạo cột trùng.
- Với businessKey strategy, mặc định tạo đủ cặp cho toàn bộ khóa ghép; giữ custom destination hợp lệ. Một key dùng single pair, nhiều key dùng danh sách pairs trong Studio (runtime composite shape vẫn theo giới hạn Engine chưa chốt).
- Kiểm tra relationship phát hiện thiếu cột child, thiếu cặp key, type/length/precision khác parent, mapping sai nguồn. Sửa/xóa field hoặc pair sau đó không được tự chữa ngầm mọi lỗi: báo chính xác để người dùng sửa hoặc Undo.
- Khi parent thay đổi Key/RowKey, đồng bộ quan hệ và bổ sung khóa cần thiết. Không xóa các cột cũ tự động khi đổi parent/bỏ key để tránh mất cấu hình; các mapping không còn hợp lệ phải được chỉ ra.
- Save/Open/JSON Preview/Mapping Preview dùng cùng model. Gán parent và tự sinh keys được gom trong lịch sử chỉnh sửa; rename field cập nhật cả child destination và reference đến field này.

## Kiểm chứng

Unit/integration: parent đơn/ghép; parent có RowKey nhưng vẫn cần business keys; thêm khóa không trùng cột; type/length conflict; custom destinations; missing/deleted field; parent-field rename; nested mapping từ record cha; save/reopen không mất cấu hình.

WPF smoke: bút field, Enter/Esc/blur/duplicate, undo/redo references; gán parent tự xuất hiện keys trong grid; validation/JSON cập nhật; giữ các regression CR02/CR03 và publish lại.

## Kết quả triển khai

- Build/publish Release thành công, 0 warnings/0 errors; **120 tests pass**, không skip. Fixture hợp lệ cũ được cập nhật để có đầy đủ parent-key columns theo yêu cầu mới, không nới validator để giữ fixture thiếu khóa.
- Tests bổ sung cho đơn/ghép, RowKey + đầy đủ business keys, SQL length/type conflict, custom destination, không tạo cột trùng, xóa thiếu field/cặp, rename cả reference lẫn destination, mapping không cần lặp key trong JSON con, nested inheritance và save/reopen.
- WPF smoke trên cửa sổ thật: bút field focus/select-all; buffer không đổi model trước Enter; Enter cập nhật inherited reference; undo/redo, tên trùng/Esc/blur; gán Parent Table sinh OrderNo trong grid, xóa field báo lỗi mà không tự chữa ngầm. Đã xem ảnh Light/Dark, editor/error và inherited fields; binding trace không có warning/error.
- Composite business-key pairs giữ đầy đủ trong Studio draft/Mapping Preview. Do runtime shape chưa được chốt từ Engine, JSON runtime preview/export báo rõ chưa hỗ trợ, không còn serialize im lặng chỉ cặp đầu tiên. Với RowKey strategy, vẫn export bình thường cùng đầy đủ business-key fields kế thừa.
- Chỉnh type/length/mapping của cột đã có giữ quyền chỉnh sửa của người dùng; khi không khớp cha, validator chặn runtime save/export. Không tự xóa các field cũ khi bỏ Key/đổi parent. Named mapped references đến bảng không còn là ancestor cũng báo lỗi.
- Artifacts: `artifacts/change-04-tests/`, `artifacts/change-04-published/`; bản chạy cập nhật tại `artifacts/release/DynamicTemplateStudio.App.exe`.
