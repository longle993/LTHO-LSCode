> Tham chiếu cập nhật UI/menu: [PROJECT-SYNC-UI-UPDATE.vi.md](PROJECT-SYNC-UI-UPDATE.vi.md). Batch menu mới thay mapping wcommand/command cũ bên dưới.

# Đồng bộ dự án và xem dữ liệu

## Sử dụng

### Đồng bộ toàn bộ từ FSD Studio

Trong FSD Studio, nút **Synchronize from standard project list** lấy `CurrUser` đã lưu qua `Reg.GetRegValue("CurrUser")`, kết nối DB danh mục chuẩn bằng tài khoản nhúng, gọi `ns_getListDept`, rồi cập nhật các dòng `dmda` đang hoạt động và thêm dự án còn thiếu. FSD Studio tự làm các bước này trong một lần bấm và làm mới lưới dự án.

Trong **Dự án của bạn**, nhập `CurrUser` FSD Studio một lần; tên này được lưu trong settings LSCode. Các lần sau chỉ cần bấm **Đồng bộ** để đọc danh mục chuẩn, lập kế hoạch và ghi catalog trong cùng một thao tác. Nút **Dừng** hủy truy vấn đang chạy. LSCode không tự đọc Registry của FSD Studio.

LSCode chỉ giải mã kết nối nhúng trong bộ nhớ lúc gọi DB và không trả connection string về UI hoặc log. Payload tương thích nằm trong embedded resource của `LSCode.Infrastructure`; máy chạy không cần cài FSD Studio hoặc có `clsDatabase.cs`. `CurrUser` là tên người dùng danh mục chuẩn và có thể khác tên đăng nhập Windows.

Mỗi dòng chuẩn dùng `ma_da` làm mã đối chiếu; lấy đường dẫn từ `dir_pro_web`, rồi `dir_src_web`, `dir_pro_app`, `dir_src_app`; lấy server từ `ten_server`, SYS DB từ `db_sys`, thư mục update từ `dir_update`. Đồng bộ hàng loạt chỉ ghi đường dẫn `web.config`; khi mở một dự án, LSCode mới đọc kết nối App/SYS của dự án đó. Cách này tránh việc gần 3.000 lần kiểm tra đường dẫn mạng làm nút đồng bộ đứng lâu. Profile hiện có được giữ ID, đường dẫn chương trình và thư mục update đã chỉnh; dự án cũ vắng trong danh mục chuẩn không bị xóa. Dòng thiếu mã, server, SYS DB hoặc đường dẫn nguồn bị bỏ qua và hiện lý do. Nếu `web.config` không có hoặc không đọc được, lỗi được báo khi mở dự án tương ứng.

Luồng **Đồng bộ một dự án từ web.config** đã được bỏ khỏi giao diện. Profile đã có vẫn giữ `WebConfigPath`; khi mở dự án, LSCode đọc App/SYS hiện tại từ file đó. Nếu file không còn đọc được, LSCode dùng connection string đã lưu trong catalog khi có.

**Ctrl+Shift+Q** mở tab SQL mặc định App của project hiện tại. **Ctrl+Shift+T** mở phần cấu trúc và dữ liệu theo target tab đang chọn. Tab SQL cũ giữ target riêng; cây chức năng luôn dùng SYS.

Ví dụ bộ lọc cho `dbo.freecolumns`: SELECT `*`, WHERE `controller LIKE 'GLDetail'`. Danh sách cột hiển thị kiểu dữ liệu và tooltip nullable. Có thể gõ tên nhanh để tìm trong combo database/bảng.

## Mapping đã xác minh

Đã đọc schema thực tế tại BACHVIET/FBISP24 bằng truy vấn chỉ đọc:

| Nguồn | ID | Cha | Caption | Liên kết |
|---|---|---|---|---|
| dbo.wcommand | wmenu_id | wmenu_id0 | bar | menu_id → dbo.command.menu_id; link |
| dbo.command | menu_id | menu_id0 | bar | exe |

Cây chính theo `wcommand`; caption/link trống dùng giá trị tương ứng từ `command`. Các command chưa được tham chiếu nằm trong nhóm **Command khác**, theo mẹ–con của bảng command. Node mồ côi hoặc vòng lặp được đưa lên gốc để không mất nội dung. Tìm tên/sysid/mã bằng Enter mở các node cha và cuộn tới dòng khớp; Enter tiếp chuyển kết quả, không dựng lại cây khi gõ.

Nhấp đúp một dòng menu tìm ASPX ở root hoặc `Main`, đọc `Controller` trên `FastBusiness:ReportExtender`, rồi mở một tab cây thư mục trong cột file dự án (chuyển tab giống Output / SQL Results). Với dự án không có thư mục `Main`, tra `<page id="…" controller="…"/>` trong `AppData/PageMetadata.xml` (hỗ trợ cả `App_Data`), kể cả khi link ASPX không tồn tại. `page id` lấy từ tên link trước, rồi mới thử `sysid`; so khớp không phân biệt chữ hoa thường. ASPX không được mở trong editor; file chỉ mở khi nhấp đúp chính file đó. Cây lồng theo thư mục thật, tên folder/file giữ nguyên. Duyệt AppData/App_Data một lần mỗi project; gom theo tiền tố tên controller và controller con khai báo `<items style="Grid" controller="…">`. Không thực thi ASPX hay giải entity XML. Giới hạn index 100000 file, 64 controller con, chỉ đọc XML khai báo dưới 4 MB. Đây là tìm file theo quy ước tên/controller, chưa phân giải toàn bộ include động hoặc lookup; Workspace → Refresh explorer làm mới cache/tab controller.

Trên WebView, mỗi nhóm controller được nhận diện bằng cặp `link` và `sysid`. Vì vậy hai menu không có link hoặc dùng chung link vẫn mở được hai cây file riêng khi `sysid` khác nhau. Menu chỉ có `sysid` vẫn mở được nhóm file; nhóm hiện tại được tải lại theo đúng `sysid`.

Trong dự án không có `Main`, `page id` thường là tên file link bỏ `.aspx`, còn `sysid` của `wcommand` có thể là **tên controller**. Ví dụ FBISP30: menu `arkh.aspx` có `sysid=Customer`; metadata khai báo `<page id="arkh" controller="Customer"/>`. Vì vậy tra page theo link trước, rồi thử `sysid` như page id; nếu không có page phù hợp thì dùng `sysid` làm tên controller để tìm file. Không dùng `arkh` làm tên controller khi metadata đã chỉ ra `Customer`.

`PageMetadata.xml` có thể chứa DTD và các entity `Page.001`… trỏ tới `Metadata/Page.*.xml`. Bộ đọc phân giải cả include lồng để tìm page theo `sysid`; chỉ cho phép include file nằm trong thư mục dự án, chặn URL ngoài và giới hạn số ký tự mở rộng từ entity. Đã đối chiếu với `App_Data/PageMetadata.xml` của dự án FBISP30: page `arnhkh` trong `Metadata/Page.001.xml` cho controller `CustomerGroup`.

## Kết nối và lưu trữ

Màn hình Projects hiện lúc khởi động, hiển thị Sys Data/App Data/Program Path và thời điểm truy cập. Có ô tìm kiếm, nút kết nối, các dự án gần đây, đồng bộ mới và xóa khỏi danh sách (không xóa file/DB). Kiểm tra kết nối SYS thành công trước khi vào project đã đồng bộ. Đóng màn hình vẫn có thể dùng editor hoặc mở lại từ menu Project.

Toolbar trên từng tab SQL cho phép đổi SYS/App độc lập. App database đã chọn lưu trong catalog mã hóa; đồng bộ lại cùng chuỗi kết nối giữ lựa chọn này. Menu không đổi sang App khi đổi target SQL. Có thể dùng **Chọn App DB…** để thay database đã chọn. Trường `%UserID` trong Application Name được thay bằng tên client `LS Code`, không giả lập danh tính người dùng ERP.

Gợi ý SQL lấy metadata bằng truy vấn chỉ đọc từ target hiện tại, cache trong phiên theo connection, tối đa 10.000 dòng metadata và 500 gợi ý mỗi popup. Hỗ trợ từ khóa/hàm thông dụng, bảng/view/procedure, cột, schema prefix và alias FROM/JOIN đơn giản (gồm identifier trong dấu ngoặc vuông). Ctrl+Space hoặc gõ ba chữ cái/dấu chấm mở gợi ý; Enter/Tab chèn thay prefix. Chưa tương đương đầy đủ IntelliSense SSMS: chưa phân giải CTE/temp table, scope phức tạp hoặc chữ ký tham số. Nút **↻ Gợi ý SQL** xóa cache để tải lại.

- Đọc kết nối từ web.config bằng XML parser chặn DTD. Hỗ trợ `connectionStrings configSource` trỏ file trong thư mục ứng dụng.
- Kết nối sys phải có database cụ thể. Chuỗi app gốc giữ nguyên trong catalog; khi kết nối App, `%Database` được giải quyết bằng database người dùng chọn, không đoán tên từ hậu tố SYS. Có thể chọn database đích riêng trong cửa sổ bảng.
- Catalog lưu hai chuỗi kết nối bằng Windows DPAPI, theo tài khoản Windows hiện tại. Không ghi mật khẩu vào settings hoặc log; export catalog bỏ các trường connection string.
- Không sửa web.config hoặc DB khi đồng bộ. Chỉ lưu catalog sau khi đọc cây chức năng thành công. Đọc share theo quyền Windows của người chạy LS Code.
- Nếu web.config không khai báo Encrypt, giữ mặc định tương thích driver legacy (`Optional`). Nếu có khai báo, giữ đúng giá trị; không tự bật TrustServerCertificate.
- Nút Dừng hủy công việc và SQL đang chạy. Lời gọi SMB của Windows có thể tiếp tục chờ ở nền tới timeout hệ điều hành; UI không chờ đồng bộ trên luồng chính trong bước tìm dự án/đọc config.

## Kiểm chứng

Build solution sạch; 144 kiểm tra Core và 78 kiểm tra WebView nội bộ đạt trong đợt triển khai ngày 16/11/2026. Bộ kiểm tra mới bao gồm import/export catalog qua file, chặn DTD, thay file export nguyên tử, đóng gói kết nối chuẩn và build Dynamic Template Studio hoàn toàn từ source trong LSCode.

Kiểm tra tích hợp dùng `LSCODE_TEST_WEB_CONFIG` và `LSCODE_DATA_HOME` trỏ thư mục trống, chạy `LSCode.exe --project-smoke`; chỉ chạy thủ công khi có quyền đọc share/DB. Báo cáo không chứa connection string. Đây là kiểm thử từ bên trong ứng dụng, chưa thay thế kiểm thử thao tác chuột/bàn phím đầy đủ.
