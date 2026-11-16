# Kế hoạch kiểm thử và nghiệm thu

Kế hoạch test đích; hiện có 120 unit/integration/performance tests và WPF smoke, gồm Property/SQL types, field rename và kế thừa/validation khóa cha. Xem [báo cáo triển khai](implementation-status.md), CR02–CR04 để phân biệt đã chạy với manual acceptance còn thiếu. Đã dùng xUnit và pin version.

## 24 nhóm test từ prompt §19

| Test | Nội dung và expected result | Liên quan |
|---|---|---|
| T01 | Create Project tạo manifest/folder đúng, không overwrite folder có sẵn ngoài ý muốn | R02,R03 |
| T02 | Open Existing phục hồi đủ templates và báo file thiếu | R03 |
| T03 | Save Template reload ra cùng semantic model | R16 |
| T04 | Export Project có manifest và đúng số file, không lồng trùng folder | R16 |
| T05 | abc/#abc → #abc; ##abc, empty, expression bị từ chối | R06 |
| T06 | Duplicate normalized table name bị chặn theo policy case đã chốt | R06 |
| T07 | Rename table sửa parent/reference/named ancestor; Undo khôi phục toàn graph | R06,R10 |
| T08 | Map Object tạo đúng một record với field đã convert | R08,R15 |
| T09 | Map Array tạo đúng số record, giữ thứ tự input | R08,R15 |
| T10 | JSON hai cấp resolve child from trên từng parent | R08 |
| T11 | JSON bốn cấp Header–Detail–Serial–Attribute đúng ancestor | R08 |
| T12 | RowKey long, unique mỗi table/run, reset giữa run | R09 |
| T13 | Parent RowKey đúng record cha, kể cả business ID trùng; con rowKey=false vẫn hoạt động | R09 |
| T14 | Business Key map và báo null/duplicate theo scope đã chốt | R09,R14 |
| T15 | Composite key đủ cặp, thiếu cặp hoặc tuple trùng có diagnostic | R09 |
| T16 | Mixed strategies trong cùng hierarchy cho expected output đúng | R09 |
| T17 | Missing Required có table/field/input path, phân biệt null theo C03 | R14 |
| T18 | Invalid/unsupported JSONPath báo đúng vị trí, không crash | R08,R14 |
| T19 | Parent không tồn tại bị chặn trước mapping/export | R10,R14 |
| T20 | Self-cycle và cycle nhiều bảng bị phát hiện | R08,R14 |
| T21 | Decimal precision/scale/value overflow theo policy, không làm tròn âm thầm | R07,R14 |
| T22 | Reload giữ table/field order, keys, defaults và unknown property hợp lệ | R16 |
| T23 | Large JSON chạy nền/cancel, hiển thị truncated nếu giới hạn preview | R05,R15 |
| T24 | Export rồi mở ở vị trí khác, sửa và save không mất runtime/metadata | R03,R16 |

## Test bổ sung rủi ro cao

CR05: regression selection table 2/3 khi edit/validate/refresh/autosave và unit/integration cho text allowed-values, CSV, default/null/max length, serialization và reopen; xem [CR05](change-request-05.md).

CR04 thêm regression inline rename field và relationship đầy đủ parent keys: đơn/ghép/RowKey, collision type/length, thiếu cột child, custom destination, rename destination/reference, mapping và round-trip. Xem [CR04](change-request-04.md).

CR03: WPF smoke cho inline table rename (commit/cancel/invalid/blur/undo graph) và CheckBox một lần toggle trên dòng chưa chọn, gồm proposal RowKey; xem [đặc tả CR03](change-request-03.md).

Đợt cập nhật UX/SQL: kiểm Property normalization và round-trip legacy; numeric(19,5), SQL lengths/bit/smalldatetime; tự chọn parent RowKey và dropdown business keys; undo quan hệ; WPF DragDelta thật của splitter và đo close sạch/discard. Chi tiết ở [change-request-02](change-request-02.md). Chưa coi các kiểm tra này là đã pass trước khi sửa source và chạy lại.

- Persistence: hết quyền/hết dung lượng/file locked; crash tại từng bước transaction; stale autosave; external edit conflict; close trong lúc save; manifest write thất bại; file nguồn không bị phá.
- Path: template code `../`, absolute path, Windows reserved names, case collision, destination trong source, symlink/reparse; mọi thao tác ở đúng project.
- Round-trip: absent vs null/default false, JSON property lạ ở mọi cấp, version không hỗ trợ, duplicate JSON property names. Không bỏ dữ liệu âm thầm.
- Mapping: parent/ancestor ngoài phạm vi, named ancestor không phải ancestor, reference đến field đã rename, technical name collision, record thiếu field, heterogeneous array, empty array, scalar array, date/time/guid/bool/overflow.
- Auto detect: review trước apply, cancel không đổi model, không tự chọn Business Key, mixed type không đoán chắc chắn, một Undo hoàn tác toàn generation.
- UI: đổi selection khi cell lỗi, keyboard scope, drag reorder rồi undo/save/reopen, theme ở dialog và editor, diagnostics click về đúng field.

## Fixture cốt lõi và dữ liệu dự kiến

Hai parent record có cùng `OrderNo=SO001`, mỗi parent có một detail khác ProductID. Header bật RowKey, Detail tắt RowKey và nhận `_HeaderRowKey`. Expected Header RowKey là 1 và 2; hai Detail trỏ lần lượt 1 và 2. Nếu OrderNo được đánh dấu key thì duplicate diagnostic vẫn xuất hiện theo policy; không được vì lỗi key mà nối cả hai detail vào parent đầu tiên. Đây là fixture để bắt nhầm khóa kỹ thuật với khóa nghiệp vụ.

Fixture bốn cấp thêm Attribute, có `@parent`, `@ancestor(2)` và named mapped ancestor. Fixture composite key có tuple hợp lệ và tuple trùng; expected do contract xác nhận, không tự dùng implementation làm oracle.

## Hiệu năng: mục tiêu đề xuất, chưa benchmark

Tập đo tối thiểu: 100 templates/project, 20 tables/template đang chỉnh, 500 fields/table; sample 10 MB và 100.000 output rows. Đây là workload thử, không cam kết dung lượng sản phẩm. Ghi CPU/RAM/OS/SDK, kích thước dữ liệu, thời gian và peak memory trong báo cáo.

Mục tiêu ban đầu: thao tác cell thông thường phản hồi dưới 100 ms; mở project mẫu dưới 5 s; Run Preview mẫu dưới 10 s; Cancel có phản hồi dưới 1 s. Điều chỉnh bằng benchmark và kỳ vọng thực tế, không tuyên bố đạt trước khi đo. Chỉ hiển thị giới hạn row preview có nhãn; validation toàn bộ hay một phần phải được nói rõ.

## Kịch bản nghiệm thu end-to-end

Thực hiện đủ 23 bước prompt §21, ghi ảnh hoặc log từng mốc:

1. Mở app → tạo SalesManagement → thêm order.import.
2. Thêm Header, from `$.orders[*]`, OrderNo business key, bật RowKey.
3. Thêm Detail parent Header, from `$.details[*]`, ProductID/Qty, chọn Parent RowKey.
4. Xem JSON: tên #Header/#Detail, parentKey đúng; Validate không lỗi với fixture hợp lệ.
5. Paste sample, Run Preview, kiểm `_HeaderRowKey` theo từng record cha.
6. Export project → đóng app → mở lại bằng Open Project Folder.
7. So tất cả template/table/field/key/relation/order/settings; sửa field → Save → Export.
8. Đối chiếu output với DSL baseline và chạy Engine parity khi có Engine chuẩn.

Definition of Done: build Release trên Windows, test liên quan pass, chức năng đã demo chạy thật, lỗi quan trọng giải quyết, UI Light/Dark/DPI được xem thực tế, portability/round-trip có bằng chứng, README tái hiện được. Nút chưa làm phải disabled có lý do; không tính nút giả là hoàn tất yêu cầu.
# CR06 regression

Native WPF smoke checks parent Key addition/removal and key type changes immediately refresh child diagnostics without changing selected table. Rename tests require Enter and blur to save graph references, Escape to cancel, duplicates to remain invalid, and switching field to commit the previous field's draft. Earlier blur-cancel expectations are superseded by [CR06](change-request-06.md).

