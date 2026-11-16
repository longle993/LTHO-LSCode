# Trạng thái triển khai — 04/11/2026

**CR06 mới nhất:** [kiểm tra khóa cha và rename khi rời ô](change-request-06.md). Parent Key/type thay đổi cập nhật validation ngay, không tự sinh field che mất lỗi khi tick Key; Enter/blur lưu tên và references, Escape hủy; bút nằm mép phải. 154 tests pass, build/publish và WPF smoke cuối tại `artifacts/change-06-final/` pass, binding log rỗng.

**CR05 mới nhất:** [giữ bảng đang chọn và giá trị chấp nhận](change-request-05.md). Đã sửa refresh cây Relationships tác động ngược tới table selection; thêm Allowed values cho kiểu chuỗi, kiểm tra validation/Test Mapping và lưu/import JSON. **154 tests pass**. Bản Release tại `artifacts/release/`; bằng chứng WPF tại `artifacts/change-05-published/`. Các số lượng test bên dưới là mốc của từng đợt trước.

Người dùng đã yêu cầu “thực hiện … luôn” sau khi nhận brief. Đã triển khai trực tiếp theo bố cục ba vùng; không có wireframe được duyệt riêng. Không tự đánh dấu hợp đồng Engine đã được xác nhận.

**Đã cập nhật đợt CR02:** sửa đủ 7 nội dung theo yêu cầu, sau khi cập nhật docs/brief trước source. Báo cáo mới và ảnh/đo close/resize ở [change-request-02](change-request-02.md). Tổng test hiện tại: **103 pass**. Các số đo/baseline ban đầu bên dưới được giữ làm mốc, không thay thế báo cáo CR02.

**CR03:** [đổi tên bảng tại chỗ và checkbox một click](change-request-03.md) đã triển khai sau khi cập nhật tài liệu. Bút mở editor tên bảng, Enter/Esc/blur và lỗi inline; undo/redo cập nhật toàn graph. Key/Required và proposal RowKey dùng checkbox template tương tác ở display mode. WPF smoke kiểm editor/focus/key events và toggle peer; 103 unit/integration/performance tests tiếp tục pass.

**CR04 mới nhất:** [inline rename field và parent-key inheritance](change-request-04.md). Field sửa tại chỗ bằng bút/Enter/Esc; tự thêm đủ cột khóa cha khi gán parent, type/length/source validation, composite pairs và undo graph. **120 tests pass**, Release build/publish và WPF smoke pass. Artifacts mới: `artifacts/change-04-tests/` và `artifacts/change-04-published/`.

## Đã có trong source

| Phần | Hành vi hiện tại |
|---|---|
| Foundation | Solution App/Core/Infrastructure/Tests, WPF net10.0-windows, MVVM Toolkit, Hosting/DI, Light/Dark, theme được nhớ |
| Projects | Create/open/recent/settings/duplicate/export; archive project bằng rename folder có xác nhận, có thể phục hồi thủ công |
| Templates | Add/rename/duplicate/delete/import JSON, search; code an toàn Windows, phân biệt hoa thường không gây filename collision |
| Designer | Sidebar/inspector resize, Rename cạnh tên bảng, Property/Key/Required inline, SQL type picker, numeric 19,5; inspector Table/Field chỉ Default/Max length ở phần nâng cao |
| Edit history | Snapshot undo/redo, dirty revision, reorder field bằng phím/nút hoặc Alt+drag; rename cập nhật references |
| Relationships | Cây parent 1-N; tự RowKey khi parent bật; dropdown key khi không dùng RowKey; single/composite pairs, cycle/reference/type validation |
| JSON | Read-only AvalonEdit highlighting, format tự động từ serializer, copy/validate/export; giữ unknown properties và thứ tự table/field |
| Sample | Paste/load; infer tables/fields/types/parent, review/sửa bảng và field trước apply, cancel proposal; không tự chọn business key |
| Preview | Mapping Object/Array và context cha/ancestor/named mapped ancestor; typed conversion, RowKey long, parent key theo record cha, key diagnostics; cancellation và giới hạn rõ |
| Persistence | Save current/all riêng, autosave 800 ms, draft khi lỗi, file temp/replace, project journal/recovery, external edit conflict, cleanup file quản lý sau rename/delete, portable folder |
| Distribution | Project mẫu, run-studio.cmd, publish framework-dependent win-x64 tại artifacts/release |

## Kiểm chứng đã chạy

- `dotnet build DynamicTemplateStudio.sln -c Release`: thành công, 0 warnings/0 errors.
- `dotnet test DynamicTemplateStudio.sln -c Release`: **59 passed**, không skip; unit, mapping, round-trip, persistence, fault injection và performance.
- WPF smoke mode: mở MainWindow thật; save current không lưu nhầm thay đổi template khác; save all xóa dirty đúng revision; undo/redo add table; map sample ra Header/Detail và key đúng; render Designer/Relationships/JSON/Test Mapping/Projects.
- Xem PNG Light/Dark và cửa sổ 1024×640 DIP. Binding trace sau khi màn đã mở không có warning/error. Smoke chỉ thu trace từ thời điểm runner được gắn; chưa phải bằng chứng mọi tương tác UI đều không lỗi.
- Tạo/select/layout bảng 500 field: khoảng **210–255 ms**, đã gồm 120 ms idle wait trong smoke; virtualization được bật. Smoke đã kiểm cuộn tới field cuối và số container được tạo nhỏ hơn tổng số field.
- JSON **15.088.900 bytes, 100.000 records**: **1.283 ms**, peak working set của test process khoảng **206 MB**. Máy Windows x64, Intel Core i5-12400, SDK 10.0.403, runtime 10.0.14. Kết quả là một lần chạy tại máy phát triển, không phải SLA.

Artifacts kiểm chứng được sinh trong `artifacts/test-results-release/`, `artifacts/smoke-final/` hoặc thư mục chỉ định cho `--smoke`. Thư mục artifacts được gitignore. Có thể chạy lại bằng lệnh trong README.

## Các giới hạn đang giữ rõ

1. **C01–C07 vẫn chưa được xác minh với Engine thật.** Composite wire shape/constant/field reference legacy chưa chốt vẫn chặn runtime export; SQL types và SQL length mới do người dùng yêu cầu đã hỗ trợ trong type declaration. Transaction ở metadata.
2. Policy Studio hiện tại: key uniqueness toàn bảng, so sánh giá trị theo typed tuple; null/missing dùng default nếu có; ép kiểu số bằng invariant culture; date theo yyyy-MM-dd; datetime chuẩn hóa DateTimeOffset. Đây chưa là parity với Engine.
3. Decimal kiểm schema precision tới 38 nhưng giá trị preview dùng .NET decimal, bị giới hạn độ chính xác khoảng 28–29 chữ số. Giá trị vượt khả năng biểu diễn báo lỗi, không làm tròn âm thầm.
4. Parse sample tối đa 512 cấp JSON; tổng preview mặc định 100.000 rows, chỉ đưa 5.000 rows/table vào DataGrid. Đây là giới hạn tài nguyên, không hardcode hierarchy chỉ 2/4 cấp.
5. Infer khảo sát tối đa 100 record mỗi array, cấu trúc con lấy từ container gặp đầu tiên; heterogeneous/primitive arrays cần review. Không tự chứng minh type chỉ dựa một sample.
6. File thiếu/hỏng làm Open Project dừng với đường dẫn lỗi, giữ nguyên toàn bộ file. Chưa có chế độ mở một phần project để sửa trực tiếp file hỏng.
7. Export dùng folder mới/rỗng; chưa có merge/overwrite project export tại folder đã có nội dung. Save cập nhật project đang mở theo transaction riêng.
8. Archive/Delete Project hiện đổi tên folder thành `.deleted-<timestamp>`, không xóa vĩnh viễn; Remove from recent riêng chưa có UI.
9. Chưa có Compare saved JSON, command palette, bulk edit, autocomplete hoặc installer/signing. Các bổ sung này không có nút giả đang hoạt động.
10. Chưa nghiệm thu thủ công toàn bộ 23 bước bằng chuột/bàn phím, DPI 125/150/200% và chuyển monitor; chưa chạy trên máy Windows sạch. Chưa đo workload 100 template/project và 20 bảng/template. Không đánh dấu P7 hoàn tất.

## Trạng thái phase

| Phase | Trạng thái |
|---|---|
| P0 | Nhánh dựng thẳng theo yêu cầu mới; layout đã triển khai; contract Engine còn mở |
| P1 | Có implementation và Release build/smoke |
| P2 | Luồng filesystem/CRUD có implementation và integration tests; chưa mở project hỏng theo từng phần |
| P3 | Designer chạy được, có smoke/virtualization; cần nghiệm thu keyboard/DPI thủ công |
| P4 | Studio mapping/relations có tests; các shape chưa chốt giữ ở draft/preview |
| P5 | Preview/import/export/round-trip có tests; Engine parity chưa có |
| P6 | Mapping/inference chạy được, có nested/key/performance tests |
| P7 | Đã có tests, fault injection, smoke, publish; còn manual/clean-machine acceptance và Engine parity |

Việc tiếp theo nên ưu tiên: cung cấp source/schema/fixture Engine để khóa C01–C07, rồi chạy acceptance thủ công/clean-machine. Không đổi các trạng thái còn mở thành “hoàn tất” chỉ vì build xanh.

## CR07 — Chọn nhiều dòng và clipboard

Đã bổ sung copy/paste field giữa template; bằng chứng build, 164 tests, WPF smoke, ảnh và giới hạn nghiệm thu tại [CR07](change-request-07.md). Bản chạy riêng `artifacts/release-cr07/DynamicTemplateStudio.App.exe` vì phiên release cũ đang mở.
