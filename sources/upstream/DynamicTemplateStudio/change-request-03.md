# CR03 — Đổi tên bảng tại chỗ và tick một lần

Đặc tả cập nhật trước khi triển khai source theo yêu cầu người dùng.

## Đổi tên bảng

- Nút Rename cạnh tên bảng được thay bằng **biểu tượng cây bút**, có tooltip và AutomationProperties.Name “Rename table”.
- Bấm bút hoặc gọi Rename Table/F2 sẽ thay đúng tên bảng trong workspace bằng TextBox, focus và chọn toàn bộ tên hiện tại. Không mở dialog.
- Enter xác nhận; Esc hủy. Click ra ngoài hủy bản nhập chưa xác nhận, tránh đổi tên ngầm. Khi đổi selection/template, cũng hủy bản nhập còn dang dở.
- Tên đang gõ là buffer UI, chỉ commit vào domain sau Enter. Tên sai/trùng hiển thị lỗi ngay tại ô, giữ editor để sửa; không sửa parent/reference khi validation thất bại.
- Một lần rename hợp lệ là một undo unit, cập nhật parent, generated parent-key column và named references qua GraphEditor/RelationshipEditor. JSON Preview/dirty/autosave chỉ phản ánh tên đã commit.
- Các điểm gọi Rename Table trong inspector dùng cùng editor tại workspace. Rename field/template không nằm trong phạm vi thay đổi lần này.

## Tick trên lưới

- Key/Required trong field grid là CheckBox tương tác ngay, không cần DataGrid vào edit mode trước. Một click tick; click tiếp bỏ tick; không toggle hai lần do vừa chọn dòng vừa xử lý click.
- Space vẫn toggle khi CheckBox có focus. Dùng TwoWay binding cập nhật model ngay; diagnostics, parent key options, undo/autosave và JSON Preview giữ cùng nguồn dữ liệu.
- Áp dụng cùng quy ước một click cho Generate RowKey trong lưới review proposal.

## Kiểm chứng cần thực hiện

- WPF smoke: bấm bút, nhập/Enter, Esc, tên trùng, click ra ngoài; không xuất hiện dialog; parent/reference được cập nhật; undo/redo khôi phục cả graph.
- CheckBox trên một dòng chưa được chọn: kiểm control enabled/hit-testable ở display mode, invoke Toggle peer một lần làm model đổi đúng một lần; click lặp/Space toggle ngược. Kiểm Key/Required và RowKey của proposal.
- Render ảnh editor, lỗi inline và trạng thái bình thường ở Light/Dark; build/tests và publish lại.

## Kết quả

- Release build/publish thành công, 0 warnings/0 errors; **103 unit/integration/performance tests pass**. Đợt này bổ sung kiểm chứng trong WPF smoke vì thay đổi nằm ở binding/control và tương tác cửa sổ.
- Smoke gọi bút qua Invoke peer, xác minh focus/select-all, gõ buffer không đổi model trước Enter; Enter đổi tên và toàn bộ parent/generated key; một Undo/Redo khôi phục đúng graph.
- Tên trùng giữ editor và báo lỗi tại ô; Esc hủy, mất focus lưu tên hợp lệ (quy tắc cập nhật theo [CR06](change-request-06.md)). Đã xem ảnh editor và lỗi để sửa phần chữ bị clip do chiều cao/padding TextBox.
- Key/Required kiểm ở display mode trên dòng chưa chọn: control enabled, hit-testable, focusable; một Toggle peer gây đúng một notification/model change, lần tiếp theo đổi ngược. RowKey trong proposal cũng qua cùng kiểm tra. Đây là kiểm thử WPF automation/control path, không tuyên bố đã chạy toàn bộ manual mouse/keyboard acceptance.
- Artifacts: `artifacts/change-03-tests/`, `artifacts/change-03-published/` với `inline-and-checkbox-result.txt`, `bindings.log`, PNG và smoke-result. Bản chạy được cập nhật tại `artifacts/release/`.
