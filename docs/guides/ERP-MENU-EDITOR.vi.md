# F3 — Thông tin và chỉnh sửa menu ERP

Trạng thái: đã triển khai trong WebView LSCode. Ảnh WCOMMAND được dùng làm tham chiếu nội dung nhập liệu; giao diện thực tế theo theme Light/Dark/High Contrast của LSCode.

## Bố cục và lưu menu hiện tại — 19/11/2026

- Thanh đầu có Tải lại, Thêm mới, Lưu và Đóng. Form lần lượt xếp Mã cha / Mã cây / Mã command, Tên tiếng Việt / Tên tiếng Anh, Main, rồi Controller/SysId.
- Thuộc tính kỹ thuật xếp Tham số / Syscode / Loại / Trạng thái và Icon URL / Icon. Loại có các giá trị trống, D, V. Loại cũ ngoài danh sách vẫn đọc được. Trạng thái dùng ô tick và lưu bằng 1 hoặc 0.
- Cột phải gom trạng thái thay đổi, Controller và Table Dir vào một panel. Khung file riêng nhóm theo Dir, Filter, Grid và Main. Mỗi file mở trực tiếp trong editor.
- Table Dir liệt kê tên bảng tĩnh từ khai báo bảng trong Dir chính và Dir tìm qua controller Grid chi tiết. Bấm từng tên để mở cấu trúc và dữ liệu trên App DB của dự án đã mở F3. Tên có schema hoặc hậu tố partition được giữ khi mở.
- Khi thêm mới, native điền chuỗi rỗng hoặc 0 theo kiểu cột chưa có mặc định, dùng mặc định SQL nếu được khai báo và kiểm tra cả bản ghi trong transaction. Còn cột NULL thì rollback và báo tên cột. Bản ghi cũ giữ nguyên các giá trị NULL chưa sửa.
- Form đọc dữ liệu độc lập với panel tài nguyên, nên việc dò file không giữ form ở trạng thái đang tải. Việc dò chỉ lập danh sách file controller XML/.f và hủy yêu cầu cũ khi đổi Main hoặc SysId.

Phạm vi kiểm chứng ngày 19/11/2026 gồm build WebView, bộ test .NET với SQL Server cục bộ và kiểm tra tương tác F3 trên Chrome với bridge fixture. Chưa đo thời gian mở trên dự án ERP thật qua LAN.

Các mục bên dưới lưu thiết kế và kết quả của bản triển khai ban đầu.

## Kết quả triển khai

- Bản chạy trực tiếp để nghiệm thu: `artifacts/publish-webview-menu-editor/LSCode.WebView.exe` (giữ nguyên cả thư mục khi sao chép).
- Chọn một dòng trong cây Chức năng và nhấn `F3` để mở form; chuột phải trên dòng menu cũng mở form này.
- Form đọc schema `dbo.wcommand` rồi chỉ hiển thị/cập nhật các cột có thật. Mã cây chỉ đọc khi sửa, còn New tự đề xuất mã mới.
- Lưu dùng transaction `SERIALIZABLE`, khóa bản ghi/tập mã, kiểm tra trùng `wcommand.wmenu_id`, `wcommand.menu_id` và `command.menu_id` nếu các cột/bảng này tồn tại.
- Bản sửa dùng fingerprint của dữ liệu gốc để phát hiện thay đổi từ phiên khác, không ghi đè âm thầm.
- Panel tài nguyên resolve Main từ link, đọc controller trong ASPX, quét đúng basename ở App_Data/AppData và phân loại Dir/Filter/Grid/Main. `.f` và `.xml` đều được tính; file có thể bấm để mở.
- Table Dir được đọc từ các khai báo `table`, `tableName`, `baseTable` hoặc node `<table>` trong XML Dir; không lấy bừa tên bảng từ SQL.
- Lưu thành công tải lại cây Chức năng và giữ chọn theo mã vừa lưu.

## Mục tiêu và phạm vi

- Chọn menu trong cây Chức năng rồi nhấn F3 để mở thông tin đầy đủ, sửa hoặc thêm menu mới.
- New tự đề xuất mã dạng `AA.xx.xx`, kiểm tra không trùng trong wcommand và command, kiểm tra lại khi lưu.
- Tra controller từ menu để tìm file Dir, đọc bảng khai báo trong Dir và mở nhanh cấu trúc/dữ liệu của bảng.
- Hiển thị danh sách file theo bốn nhóm Dir, Filter, Grid, Main để nhận ra file hiện có hoặc đang thiếu.
- Không đưa thao tác Delete trong ảnh vào phạm vi đợt này: yêu cầu hiện tại là thêm/sửa và kiểm tra file.

## Căn cứ mã nguồn

LSCode dùng React 18/Tailwind v4 trong WPF WebView2, native .NET 10 và Microsoft.Data.SqlClient. Dùng lại Modal, Select, Button tại `src/LSCode.Web/src/ui.tsx`; màu/font tại `styles.css`, giữ Light/Dark/High Contrast.

- `src/LSCode.Shell/Services/ProjectDatabase.cs`, `CommandsAsync`: cây lấy từ SYS.dbo.wcommand; `wmenu_id` được alias thành `menu_id` cho kết quả cây, nhưng metadata giữ `wcommand.menu_id` riêng ở CommandId. App DB cung cấp denyunitright và Startup.
- Cây menu dựng theo FSD Studio: sắp xếp `wmenu_id`, lấy gốc có `wmenu_id0=''`, sau đó ghép cấp hai và cấp ba bằng quan hệ `wmenu_id0 → wmenu_id`. Dòng mồ côi, vòng lặp và cấp sâu hơn không hiển thị.
- `src/LSCode.Core/ProjectDiscovery.cs`: CommandEntry hiện chỉ giữ Id, ParentId, Caption, Link, CommandId, SysId; không đủ dữ liệu để cập nhật nguyên bản ghi.
- `src/LSCode.Infrastructure/DeveloperTools.cs`, `project.controller`: resolve Main từ link, đọc controller trong ASPX, tìm XML/.f theo controller và lần theo grid detail.
- Vì cây đã lọc status/quyền, không dùng danh sách cây để kiểm tra trùng mã. Phải đọc bảng gốc, kể cả menu ẩn.

## Luồng sử dụng

1. F3 chỉ hoạt động khi focus ở menu/tree item hợp lệ; không chiếm F3 trong editor hoặc ô tìm kiếm. Có thêm mục chuột phải “Thông tin menu · F3”.
2. Mở hộp thoại “Thông tin menu” với dự án, server và SYS/App đang thao tác. Chụp ngữ cảnh project tại lúc mở, không đổi kết nối ngầm khi đổi project bên ngoài.
3. Đọc bản ghi gốc theo wmenu_id và schema thực tế, sau đó tải các file/bảng liên quan bất đồng bộ. Có trạng thái đang tải, lỗi kết nối, bản ghi đã bị xóa, thiếu quyền.
4. Sửa thông tin rồi Lưu/Ctrl+S. New chuyển sang bản nháp mới; nếu có thay đổi thì cho Lưu/Bỏ thay đổi/Hủy trước khi chuyển.
5. Lưu thành công tải lại cây bằng quy tắc hiện có, giữ vị trí menu nếu còn hiện. Nếu status/quyền khiến menu biến mất, báo rõ đã lưu nhưng menu không nằm trong cây hiện tại.
6. Đóng/Esc và Tải lại cũng hỏi khi còn thay đổi. Toast nổi báo kết quả, lỗi nhập nằm cạnh trường; không đẩy bố cục khi thông báo xuất hiện.

## Trường dữ liệu

| Nhóm | Trường | Hành vi |
| --- | --- | --- |
| Định danh cây | wmenu_id, wmenu_id0 | Mã cây và mã cha. Mã hiện hữu chỉ đọc trong đợt đầu; chọn cha phải chống tự tham chiếu/vòng lặp |
| Hiển thị | bar, bar2 | Tên Việt/Anh, hiển thị đầy đủ, kiểm tra độ dài theo schema |
| Liên kết | menu_id, link, sysid | Mã command liên kết, trang Main và controller; không đồng nhất menu_id với wmenu_id một cách ngầm định |
| Thuộc tính | parameter, icon_url, status, icon, type, syscode, msys, target, xtype, edition, expi_icon | Chỉ chỉnh các cột thực sự tồn tại; đọc kiểu, nullable, độ dài/default từ schema |
| Thông tin suy ra | File Dir, Table Dir, Dir/Filter/Grid/Main | Chỉ đọc, kèm mở file/mở thư mục/mở bảng |
| Báo cáo tham chiếu | Report Form, File | Chỉ hiển thị khi xác định được nguồn cấu hình thật; chưa suy đoán cột DB từ nhãn trong ảnh |

NULL và chuỗi rỗng phải được phân biệt theo kiểu cột. Không ghi lại các cột không hiển thị, không ghi giá trị mặc định giả cho các cột thiếu.

## Quy tắc New và mã AA.xx.xx

`AA` là ký hiệu cho tiền tố nhánh hiện tại, không bị hard-code thành hai chữ A. Ví dụ đang chọn `03.10.03` hoặc `03.10.00`, New duyệt mã trống trong nhánh `03.10.xx`; mã cha mặc định là `03.10.00`. Nếu mã không có đủ ba phần, LSCode giữ phần đầu làm tiền tố để vẫn hỗ trợ dữ liệu cũ.

- Chọn cha rõ ràng, mặc định cùng cha với menu đang chọn. Tiền tố cấp cây không được suy ra bằng cắt chuỗi khi parent dùng quy ước khác.
- Đề xuất duyệt `AA.01.01` tới `AA.99.99`, lấy mã trống đầu tiên, giữ padding hai chữ số. Hết dải phải báo hết mã, không quay vòng.
- Tập mã đã dùng lấy từ `wcommand.wmenu_id`, `wcommand.menu_id` và `command.menu_id` theo schema đã xác minh của project, so sánh theo collation DB và bỏ padding CHAR.
- Hiển thị riêng mã cây mới và mã command liên kết. Chỉ dùng chung một mã cho cả hai khi cấu hình triển khai đã xác nhận hai trường có cùng không gian mã. Không sửa mã menu cũ trong luồng edit.
- New chỉ tạo đề xuất, chưa insert và chưa giữ mã. Lưu kiểm tra lại trong transaction; nếu mã bị phiên khác dùng thì rollback, sinh đề xuất mới và cho người dùng xem trước khi lưu lại.
- Phải chống tranh chấp bằng khóa trên cả hai tập dữ liệu trong cùng transaction (SERIALIZABLE/range lock với index phù hợp, hoặc khóa bảng khi không có index phù hợp). Chỉ dùng application lock là chưa đủ nếu có công cụ bên ngoài không tham gia cùng giao thức.
- Mặc định chỉ thêm/sửa bản ghi wcommand. Không tự insert command chỉ vì kiểm tra trùng ở command; nếu schema nghiệp vụ bắt buộc có bản ghi command thì cần đặc tả mapping và transaction ghi cả hai sau khi khảo sát.

## Tra controller, bảng và file

Với dự án có thư mục Main, ưu tiên controller khai báo trong ASPX đã resolve từ link. Với dự án không có Main, tìm page theo tên link trước, rồi theo `sysid`, trong `AppData/PageMetadata.xml` (hoặc `App_Data/PageMetadata.xml`) và dùng thuộc tính `controller` để tìm Dir/Filter/Grid và bảng Dir. Nếu metadata không có page tương ứng, thử `sysid` như tên controller rồi giữ fallback từ ASPX/link hiện có. Nếu có nhiều controller, hiển thị bộ chọn và nguồn xác định để người dùng biết đang kiểm tra controller nào.
Page có thể nằm trong entity include của DTD, ví dụ `&Page.001;` → `Metadata/Page.001.xml`; bộ đọc phân giải include trong phạm vi thư mục dự án trước khi tìm `id`.
Trên FBISP30, `wcommand.sysid` có thể là tên controller chứ không phải `page id` (`arkh.aspx` → `id=arkh` → `controller=Customer`, trong khi `sysid=Customer`). Vì vậy tra `page id` từ link trước, rồi mới thử `sysid` như page id hoặc tên controller.

- Tìm theo tên controller chính xác, không phân biệt hoa thường; dùng convention App_Data/AppData và Controllers/Controller được dự án hỗ trợ. Không ghép đường dẫn cứng theo một khách hàng.
- Main resolve từ link (bỏ query/fragment phục vụ tìm file). Dir/Filter/Grid kiểm tra XML và .f độc lập. Chỉ có .f vẫn phải được tìm thấy.
- Grid detail được nhận diện qua tham chiếu controller trong file; giữ phân biệt grid chính và detail. Một nhóm có nhiều file thì liệt kê tất cả đường dẫn tương đối.
- Các trạng thái: Có file; Thiếu file dự kiến; Chưa xác định controller; Nhiều kết quả; Không áp dụng; Không đọc được. Thiếu quyền/truy cập lỗi không được báo thành thiếu file.
- Nhóm/report không có controller có thể không cần Dir/Filter/Grid. Kết quả là chẩn đoán, không tự chặn lưu menu chỉ vì thiếu file.
- Table Dir đọc từ khai báo XML của Dir qua parser/include resolver hiện có. Khi triển khai phải đối chiếu file mẫu thật để xác định thuộc tính/table node; không regex lấy tên bảng đầu tiên trong SQL hoặc suy tên bảng từ sysid.
- Include, entity, encoding và buffer chưa lưu được xử lý giống editor hiện tại. Nếu giá trị động hoặc nhiều bảng, hiện nguồn và các ứng viên, không tự đoán một bảng.
- Mở bảng sử dụng App DB của project theo mặc định, xác minh schema/object; tên partition như `$000000` giữ nguyên. Không tự tạo bảng hay chạy SQL trong nội dung Dir.
- Quét trong SourcePath, tái sử dụng index và có cancellation; khi controller thay đổi thì bỏ kết quả cũ. Tải lại cho phép kiểm lại sau khi người dùng bổ sung file bên ngoài.

## Bố cục của bản thiết kế ban đầu

Form rộng, header/footer cố định, phần giữa cuộn. Trên màn rộng chia thông tin menu bên trái và file/bảng liên quan bên phải; màn hẹp xếp một cột. Trường kỹ thuật ít dùng nằm trong nhóm thu gọn. Tab nhập liệu đi theo thứ tự nhãn; dropdown chọn cha có tìm theo mã/tên. Theme và cỡ chữ lấy từ LSCode.

```text
Thông tin menu                       Dự án · SYS / App       ×
Tải lại                         Thêm mới             Lưu
Thông tin menu                  Controller và tài nguyên
Mã cây       Mã cha              Controller [ ... ]
Tên Việt / Tên Anh               Table Dir ... [Mở bảng]
menu_id / sysid / link           Dir     Có/Thiếu  [Mở]
Thuộc tính kỹ thuật              Filter  Có/Thiếu  [Mở]
                                Grid    Có/Thiếu  [Mở]
                                Main    Có/Thiếu  [Mở]
Chưa lưu                                      Hủy   Lưu
```

Sơ đồ là đặc tả bố cục, chưa phải giao diện đã render/test. Giai đoạn dựng UI dùng skill ui-ux để hoàn thiện mẫu theo ảnh tham chiếu và kiểm tra desktop/màn hẹp/theme.

## Thiết kế native và lưu dữ liệu

Đã bổ sung service MenuEditor trong Infrastructure, giữ domain validation không phụ thuộc UI; bridge gồm `project.menu.details`, `project.menu.new`, `project.menu.resources`, `project.menu.save`. Payload dùng target đã đăng ký và khóa gốc; native không nhận connection string hoặc tên SYS tùy ý từ form.

Details trả schema, bản ghi gốc và concurrency token. Save chỉ gửi thay đổi được cho phép, SQL parameter cho giá trị và quote identifier đã kiểm chứng. Nếu có rowversion thì dùng rowversion; nếu không có thì so snapshot các trường cập nhật trong transaction. Bản ghi bị sửa bởi phiên khác phải báo xung đột thay vì ghi đè.

Tra schema SYS/App để xác nhận vị trí command và quan hệ thực tế trước bật lưu. Nếu chưa xác định được hoặc thiếu bảng/cột bắt buộc, cho đọc thông tin nhưng báo rõ chưa hỗ trợ ghi. Không suy đoán schema từ screenshot. Không tự chạy Startup chỉ để đọc details; refresh cây sau save vẫn dùng luồng menu hiện có.

## Trình tự triển khai và nghiệm thu

1. Đã khảo sát luồng cây hiện có và giữ riêng `wmenu_id`/`menu_id`; schema cụ thể tiếp tục được đọc động ở runtime.
2. Đã dựng form F3, trạng thái nhập/sửa/mới, panel tài nguyên, keyboard, vùng cuộn và toast không đẩy giao diện.
3. Đã nối details, controller/file/table resolver và New/Save có transaction/concurrency.
4. Test thêm/sửa/mở lại; ký tự Việt, NULL/CHAR padding, cột optional; mã trùng trong cả hai bảng và hai phiên New đồng thời; rollback khi ghi thất bại; stale update; parent cycle.
5. Test controller khác tên Main, chỉ có .f, nhiều Dir, include lồng, thiếu quyền, link có query, bảng partition, không có bảng và đổi project giữa lúc tải.
6. Web production build và .NET Release build phải qua trước khi giao bản chạy. Việc build/test không tự thay đổi dữ liệu DB; chỉ thao tác Lưu trong form mới ghi `wcommand`.
