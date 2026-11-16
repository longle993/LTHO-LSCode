# Ma trận yêu cầu

Mã R dùng trong task và bằng chứng nghiệm thu; số § trỏ prompt gốc. Phần đã triển khai và giới hạn tương ứng được ghi trong [implementation-status.md](implementation-status.md); bảng này mô tả yêu cầu đích.

| ID | Nguồn | Yêu cầu bắt buộc / điều kiện đạt | Phase |
|---|---|---|---|
| R01 | §1–2,17 | WPF .NET 10, MVVM/DI/Hosting, typed models, bốn project đúng ranh giới; build Windows thành công | P1 |
| R02 | §3 | Projects có đủ tên/mô tả/location/count/modified; create/open/recent/rename/duplicate/delete có xác nhận/export | P2,P5 |
| R03 | §3,15 | Folder project portable, không cần DB riêng; phát hiện file thiếu/hỏng, không tự ghi đè | P2,P5 |
| R04 | §4 | Sidebar resize/search/template actions, workspace tabs/grid, inspector thu gọn, custom DataGrid | P1,P3 |
| R05 | §4,15 | Light/Dark, hover, inline/context/tooltip, keyboard, DPI; dirty, undo/redo, autosave không khóa UI | P1–P7 |
| R06 | §5, CR06 | Rename table/field lưu Enter hoặc blur, Esc hủy, bút mép phải; Add/rename table, Object/Array, parent/from/RowKey; abc và #abc → #abc, ##abc lỗi; rename cập nhật toàn bộ tham chiếu | P3,P4 |
| R07 | §6, CR02 | Field grid Property/Type/Required/Key; inspector chỉ Property/Default/Max length và SQL type options; char/varchar/nvarchar/smalldatetime/bit/numeric/int, numeric mặc định 19,5; giữ legacy khi import | P3,P4 |
| R08 | §6.1,8,11 | Sáu nguồn mapping; DSL riêng phân biệt JSONPath; N cấp theo context cha; ngăn cycle | P4,P6 |
| R09 | §7,18 | Business/composite key, RowKey bigint default false, Parent Key độc lập RowKey con; không trùng technical column | P3,P4,P6 |
| R10 | §9, CR02, CR04 | Relation Designer 1-N, tự RowKey; gán parent sinh đủ Business Key fields, type/length, single/composite pairs; kiểm lại ngay khi key/type cha thay đổi, kiểm thiếu/sai mapping; rename field/bảng cập nhật references/destinations | P4 |
| R11 | §10 | Paste/load sample, auto detect object/array/field/type/parent, review sửa trước xác nhận, chọn RowKey từng bảng | P6 |
| R12 | §11 | JSON preview từ cùng model, format/highlight/copy/validate/export | P5 |
| R13 | §12 | Settings: code/display name/description/version/procedure/transaction/created/modified; procedure không nhận SQL script | P3,P5 |
| R14 | §13 | Validation project/table/field/relation/export; lỗi dẫn về đúng đối tượng; chặn runtime export lỗi | P2–P6 |
| R15 | §14,18 | Test Mapping thật: traverse/convert/key/check, tabular results; không SQL, đúng record cha | P6 |
| R16 | §15,17 | Save current/all, export current/all/project, không folder lồng trùng, safe write/recovery, round-trip giữ dữ liệu/thứ tự | P2,P5,P7 |
| R17 | §19 | 24 nhóm test trong prompt và bổ sung failure cases | Mỗi phase,P7 |
| R18 | §20–21 | Mỗi phase build và chạy chức năng; nút quan trọng có tác dụng; acceptance 23 bước, README build/run thật | Mỗi phase,P7 |

## Tùy chọn, làm sau luồng chính

Compare JSON với bản lưu, Command Palette, bulk edit, autocomplete, type icons và các bổ sung §16 chưa nằm trong R01–R18. Duplicate table/field là tiện ích bổ sung; duplicate template/project vẫn nằm trong phạm vi quản lý bắt buộc. Không dùng danh sách tùy chọn để hạ ưu tiên search/reorder/context menu/keyboard đã được yêu cầu ở §4.

## Ranh giới nghiệm thu

“JSON parse được” chưa chứng minh Engine chấp nhận. “Round-trip không đổi dữ liệu” không có nghĩa bytes/indentation giống hệt. “Preview thành bảng” phải là dữ liệu đã mapping, có RowKey/Parent Key và lỗi chuyển kiểu, không chỉ flatten JSON input. Các điều kiện này phải có bằng chứng riêng.

## CR07 — Copy/paste field rows
R07/R05/R16 mở rộng theo [CR07](change-request-07.md): chọn nhiều field, copy snapshot và paste sang bảng/template khác, tên không trùng, một bước undo/redo; giữ runtime contract.
