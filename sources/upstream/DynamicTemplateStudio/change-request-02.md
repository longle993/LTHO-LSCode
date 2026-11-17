# Điều chỉnh UX và SQL types theo yêu cầu mới

Đặc tả đã được cập nhật trước khi sửa source. Yêu cầu mới của người dùng được ưu tiên hơn các lựa chọn ban đầu. Kết quả triển khai/kiểm chứng được bổ sung bên dưới sau khi chạy.

| # | Hành vi cần triển khai | Kiểm chứng |
|---|---|---|
| 1 | Đóng cửa sổ sạch ngay; bỏ luồng Closing cancel rồi gọi Close lặp và tránh chặn UI thread khi stop host. Khi còn thay đổi, hiện Save/Discard/Cancel ngay; chỉ đợi ghi file khi người dùng chọn Save. File đang ghi vẫn phải kết thúc an toàn trước khi process thoát. | Đo close sạch và discard bằng WPF smoke; kiểm cancel không đóng; save pending không bị ghi dở |
| 2 | Nút Rename đặt ngay cạnh tên bảng trong workspace; vẫn giữ F2/context/inspector | Render và kiểm command nhận đúng bảng |
| 3 | Grid và inspector chỉ có một ô **Property**. Nhập `VoucherID` tự lưu path `$.VoucherID`; UI ẩn prefix `$.`. Path có sẵn dạng `$...` hoặc DSL `@...` tiếp tục giữ nguyên nghĩa và chỉnh được trong cùng ô. Bỏ Property builder riêng, raw-path editor trùng, Mapping Source/Ancestor Depth và Apply mapping source. JSON Preview là nơi xem biểu thức đã sinh. | Nhập/sửa property, blank, nested, imported DSL; preview đúng; không nhân đôi prefix |
| 4 | Type Picker chính: `char`, `varchar`, `nvarchar`, `smalldatetime`, `bit`, `numeric`, `int`. Numeric mặc định `(19,5)`, có precision/scale. SQL string length dùng Max length; khai báo runtime thể hiện trong `type`, ví dụ `varchar(50)`, `numeric(19,5)`. Type cũ vẫn đọc/round-trip/preview để không phá project đã lưu, nhưng không nằm trong lựa chọn chính tạo mới. | Type conversion, numeric precision/scale/default, bit, smalldatetime, SQL string length, import legacy |
| 5 | Splitter workspace–inspector kéo thay đổi đúng chiều rộng; có min/max hợp lý và thu/mở lại giữ kích thước. Bỏ resize lệch do GridSplitter căn mặc định Center với star/fixed columns; khai báo Columns + PreviousAndNext + Stretch. Không tự reset pane width sau mỗi drag. | Smoke kéo splitter qua DragDelta, kiểm ActualWidth và giới hạn ở cửa sổ nhỏ |
| 6 | Phần field nâng cao chỉ giữ **Default** và **Max length**. Bỏ Constant, field References, Description khỏi inspector. Business Key/Required vẫn ở grid; dữ liệu cũ các thuộc tính đã bỏ UI được giữ trong model/round-trip. | Render inspector; reload không mất metadata cũ |
| 7 | References parent column dùng dropdown lấy business key của bảng cha. Nếu parent bật Generate RowKey: tự đặt strategy=rowKey, references=_RowKey, child column mặc định `_<Parent>RowKey`, hiển thị dòng Generated parent RowKey thay cho dropdown lựa chọn. Khi parent tắt RowKey: chuyển businessKey, hiển thị key options, chưa có key thì báo rõ; không chọn nhầm field không phải key. Composite pair vẫn hỗ trợ và dùng key options của parent. | Đổi parent/bật-tắt RowKey/key rename; child có thể không bật RowKey; save/preview/undo nhất quán |

## SQL type và tương thích

- Giữ strongly typed domain và wire property `type`; không tạo thêm field JSON `precision`/`scale`/`maxLength` khi chưa có hợp đồng Engine cho các field đó. SQL length được encode trong type declaration.
- Numeric dùng precision 1–38, scale 0–precision, mặc định 19,5. Preview dùng .NET decimal và báo overflow, không âm thầm làm tròn numeric.
- `bit` nhận boolean hoặc 0/1. `smalldatetime` kiểm khoảng SQL Server và làm tròn tới phút theo SQL Server; ngưỡng và giới hạn phải đối chiếu tài liệu Microsoft trước khi code.
- `char`/`varchar`/`nvarchar` kiểm Max length khi đã khai báo; không tự cắt chuỗi. Khi không có length, giữ base type để người dùng cấu hình tiếp. Giới hạn SQL: char/varchar tối đa 8000, nvarchar tối đa 4000 (MAX chưa nằm trong yêu cầu lần này).
- Với char/varchar, số byte thực tế còn tùy collation/code page của Engine; Studio kiểm độ dài chuỗi đầu vào, chưa mô phỏng collation. Không coi preview là chứng minh toàn bộ SQL conversion.
- Các thiết lập legacy bị ẩn không tự mất khi mở/lưu draft. Riêng người dùng sửa Property thì nguồn constant cũ được bỏ, để Property vừa nhập thực sự là nguồn mapping mới.
- Các type SQL mới do người dùng yêu cầu được hỗ trợ trong Studio/serializer; Engine parity vẫn cần fixture thực tế.

## Thứ tự thực hiện

1. Cập nhật brief, UI/DSL spec và kế hoạch test theo tài liệu này.
2. Sửa domain/type/property helpers và tests.
3. Sửa ViewModel, lifecycle, XAML/splitter/relationships.
4. Build/test, WPF smoke đo đóng app và kéo pane, xem ảnh Light/Dark rồi publish lại.

Nguồn đối chiếu SQL: [Microsoft smalldatetime](https://learn.microsoft.com/en-us/sql/t-sql/data-types/smalldatetime-transact-sql), [Microsoft char/varchar](https://learn.microsoft.com/en-us/sql/t-sql/data-types/char-and-varchar-transact-sql). Ngưỡng làm tròn smalldatetime là 29.999 giây; phạm vi ngày 1900-01-01 đến 2079-06-06.

## Kết quả kiểm chứng

- Release build: 0 warnings/0 errors; **103 tests pass**, không skip. Thêm tests cho Property/DSL/legacy constant, SQL declarations/length/bit/numeric/smalldatetime và automatic parent relationships.
- WPF smoke: nhập trực tiếp `VoucherID` trong TextBox → model và JSON Preview có `$.VoucherID`, UI không lặp prefix; chọn numeric tự ra 19,5, sửa precision/scale vẫn giữ được khi đổi selection.
- Splitter: phát routed DragStarted/DragDelta/DragCompleted trên GridSplitter thật; panel từ 300 lên 370 DIP; thu/mở lại không mất chiều rộng.
- Close smoke trên bản publish: sạch **46,9 ms**, Discard **30,2 ms**, Cancel **2,6 ms** và vẫn giữ cửa sổ; đang ghi file **29,2 ms** để đóng cửa sổ, sau đó file project/template hoàn tất và không còn journal dở. Các thời gian là một lần đo tại máy phát triển, không phải cam kết hiệu năng trên mọi máy.
- Đã xem ảnh inspector numeric, Property, Rename cạnh tên bảng, quan hệ Generated RowKey và dropdown Business Key. Đã sửa thêm template ComboBox để luôn hiện giá trị đã chọn, kể cả sau đổi nguồn key. Binding trace smoke không có warning/error.
- Artifacts: `artifacts/change-02-tests/`, `artifacts/change-02-published/`; bản chạy mới tại `artifacts/release/`. Hợp đồng Engine thật và kiểm DPI đa màn hình vẫn theo giới hạn trong implementation-status.md.
