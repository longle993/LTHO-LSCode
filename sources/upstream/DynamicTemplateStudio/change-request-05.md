# CR05 — Giữ bảng đang chọn và danh sách giá trị chấp nhận

Đặc tả cập nhật trước source theo yêu cầu mới.

## Giữ selection

- Thêm/sửa/reorder field, tick Key/Required, đổi type, validation, refresh JSON hoặc autosave không được đổi table/template selection.
- Các bộ chọn table ở Designer/Relationships phải dùng selection do ViewModel quản lý; không để currency của CollectionView hoặc việc dựng lại cây quan hệ tự chọn về root/table đầu tiên.
- Chỉ đổi table khi người dùng chọn table khác, điều hướng lỗi tới table đó, thêm table mới có chủ ý, xóa table đang chọn hoặc mở/undo một document khác.
- Regression phải thao tác trên table thứ 2 và cả table thứ 3, kể cả đã từng chọn node trong cây Relationships.

## Giá trị chấp nhận cho chuỗi

- Thêm **Allowed values / Giá trị chấp nhận** trong Field inspector cho char/varchar/nvarchar và legacy string, ví dụ `1,2,0`.
- Danh sách phân cách dấu phẩy, trim khoảng trắng quanh token, bỏ giá trị trùng; để trống là không hạn chế. Có thể dùng CSV quotes khi giá trị chứa dấu phẩy/khoảng trắng cần giữ nguyên.
- So sánh chính xác, phân biệt hoa thường, không tự trim/đổi giá trị input. Ví dụ Status char, danh sách `1,2,0`: nhận "1", "2", "0"; từ chối "3", "01" và " 1". JSON scalar số 1 được converter của text type chuyển thành "1" rồi kiểm.
- Giá trị null/missing tiếp tục theo Required/Default; enum không tự biến field thành Required. Default/constant và giá trị đã mapping (kể cả giá trị kế thừa từ cha) phải thỏa danh sách nếu không null.
- Danh sách sai cú pháp hoặc chứa giá trị vượt Max length báo lỗi tại field; non-text type không được có rule này. Khi đổi type sang non-text, xóa Max length và allowed-values constraint cùng nhau, có thể Undo.
- Lưu runtime column member `allowedValues` là mảng chuỗi, ví dụ `"allowedValues": ["1","2","0"]`, không lưu raw CSV trong runtime. Draft giữ nguyên text đang nhập để sửa lỗi; import/round-trip giữ đủ tập giá trị.
- Studio chịu trách nhiệm kiểm trong validation/Test Mapping. Đây là constraint bổ sung của Studio; chưa có source Engine bên ngoài để xác nhận Engine cũng thực thi member mới. Không tuyên bố API production đã enforce rule chỉ vì Studio export được JSON.

## Kiểm chứng

- WPF regression: table 2/3 thêm field, sửa Property/type/default/enum, tick, reorder, validate, chuyển các view, autosave vẫn giữ selection; refresh cây không tự đổi bảng.
- Unit/integration: parse/format CSV, các giá trị 1/2/0 hợp lệ và ngoài tập, case/whitespace/null/default/max length, legacy string, serialize/deserialize và save/reopen.
- Render field enum ở Light/Dark, chạy build/tests/smoke và publish bản mới.

## Kết quả triển khai

- Tái hiện được lỗi trước sửa: chọn bảng 2 trong Relationships rồi Add Field làm selection nhảy về Header (`artifacts/change-05-repro/smoke-result.txt`).
- Giữ collection cây ổn định khi field thay đổi, chỉ dựng lại khi cấu trúc table/parent thay đổi; bỏ phản hồi selection do cây refresh, đồng bộ node theo ViewModel và tắt collection currency cho bộ chọn bảng.
- 154 unit/integration/performance tests pass. Bao gồm allowed-values CSV, type/length, null/default/constant/inherited keys, runtime round-trip và save/reopen.
- Release publish thành công. WPF smoke pass tại `artifacts/change-05-published/`: table 2/3 qua chọn Relationships, add/edit field, type/default/allowed-values, Key/Required, reorder, validation và chờ debounce vẫn giữ selection. Binding log không có lỗi. Đã xem ảnh Allowed values ở Light/Dark.
- Bản chạy: `artifacts/release/DynamicTemplateStudio.App.exe`. Chưa xác nhận external Engine thực thi `allowedValues`; Studio đã kiểm tra khi validation/Test Mapping.
