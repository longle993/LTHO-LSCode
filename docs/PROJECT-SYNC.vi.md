> Tham chiếu cập nhật UI/menu: [PROJECT-SYNC-UI-UPDATE.vi.md](PROJECT-SYNC-UI-UPDATE.vi.md). Batch menu mới thay mapping wcommand/command cũ bên dưới.

# Đồng bộ dự án và xem dữ liệu

## Sử dụng

1. Mở `artifacts/publish/LSCode.exe`, chọn **Project → Đồng bộ dự án** hoặc nút **Đồng bộ…** bên trái.
2. Nhập tên, ví dụ `BACHVIET`, rồi nhấn **Enter**. Ứng dụng tìm đúng tên thư mục dưới các nhóm của `\\172.168.5.14\CustomerPro`, sau đó tìm `web.config` trong các thư mục ứng dụng con một cấp. Nếu có nhiều kết quả, chọn ứng dụng trong danh sách.
3. LS Code đọc `appConnectionString` và `sysConnectionString`. Cây chức năng lấy từ DB sys; file explorer trỏ tới thư mục ứng dụng. Đồng bộ lại cập nhật cùng profile, không nhân bản.
4. **Ctrl+Shift+Q** mở tab SQL mặc định App của project hiện tại. App đã lưu hoặc database cụ thể trong cấu hình được ưu tiên; nếu cấu hình còn placeholder, tìm DB cùng tiền tố theo `_S`/`_Sys` → `_A`/`_App` (ưu tiên `_A`), không có ứng viên thì cho chọn. Tab SQL cũ giữ target riêng; cây chức năng luôn dùng SYS. F5 chạy trực tiếp, không hiện hộp xác nhận. Tab chờ chọn/kết nối App hoàn tất trước khi chạy; không tự chạy trên SYS khi App lỗi.
5. **Ctrl+Shift+T** mở cửa sổ bảng theo target tab SQL đang chọn; nếu đang chọn file khác thì dùng kết nối hiện tại. Chọn database, gõ tên bảng/view để hiện tối đa 10 kết quả, chọn một dòng, đánh dấu cột hoặc nhập SELECT; nhập WHERE/ORDER BY rồi nhấn **Run**. Có nút **Dừng**. Giới hạn 1000 dòng; không hỗ trợ sửa dữ liệu trong grid. SQL editor vẫn cho phép thực thi SQL theo quyền tài khoản.

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

Build Release sạch; 44 kiểm tra Core và 11 kiểm tra WPF nội bộ đạt. 15 kiểm tra tích hợp thật BACHVIET gồm tìm qua CustomerPro, đọc named connections, truy vấn menu sys, lưu catalog trong thư mục kiểm thử riêng, tải file explorer, kế thừa SQL target, schema completion và chèn prefix từ popup, render Projects, kết nối App giữ menu SYS, kiểm tra SELECT, đọc schema/dữ liệu bảng và render bố cục.

Kiểm tra tích hợp dùng `LSCODE_TEST_WEB_CONFIG` và `LSCODE_DATA_HOME` trỏ thư mục trống, chạy `LSCode.exe --project-smoke`; chỉ chạy thủ công khi có quyền đọc share/DB. Báo cáo không chứa connection string. Đây là kiểm thử từ bên trong ứng dụng, chưa thay thế kiểm thử thao tác chuột/bàn phím đầy đủ.
