# Generate Update trong LSCode

Tên hiển thị **Advance Note** đã được đổi thành **Generate Update** từ 17/11/2026. ID bridge `advance-note`, tên tệp dữ liệu và phần tử XML cũ được giữ nguyên để các yêu cầu đã lưu tiếp tục mở được.

Trạng thái: đã triển khai nâng cấp trong LSCode WebView ngày 17/11/2026. Phạm vi đã kiểm chứng bằng build frontend, 93 kiểm tra WebView/.NET và smoke desktop; chưa chạy đối chiếu đầu ra với executable Fcode trên dữ liệu khách hàng.

Bản chạy trực tiếp đã publish tại `artifacts/publish-webview-advance-note/LSCode.WebView.exe`. Lượt kiểm chứng cuối đạt thêm 145 kiểm tra Core, build desktop 0 lỗi/0 cảnh báo và smoke WebView2 12 mục.

Phạm vi kiểm chứng cho SQL Object/SQL Data ngày 17/11/2026: `npm run build` thành công; project Infrastructure và WebView Tests build 0 lỗi/0 cảnh báo; fixture SQL cục bộ đạt 147 kiểm tra, trong đó tạo script DROP + CREATE cho table/store/function và INSERT chỉ từ dòng khớp WHERE. Chưa chạy trên database khách hàng.

## Đặc tả nâng cấp — 17/11/2026

Ảnh người dùng gửi là ảnh hiện trạng dùng làm đầu vào thiết kế. Các mục dưới đây vừa mô tả hợp đồng vừa ghi nhận phạm vi đã triển khai.

### Bố cục và tương tác

- Giữ theme, font và control nội bộ React/Tailwind của LSCode. Cột trái gồm danh sách yêu cầu ở phần trên và cây source ở phần dưới, hai vùng cuộn độc lập. Tỷ lệ hiện tại khoảng 44/56; chưa có splitter kéo tay.
- Cột phải: tiêu đề cùng Lưu/Xóa/Generate Update; bên dưới là File Path chiếm toàn bộ chiều ngang vùng chi tiết. Bỏ ô Chi tiết kỹ thuật và dropdown Trạng thái khỏi giao diện, vẫn giữ dữ liệu cũ trong XML khi đọc/ghi.
- Khối SQL bên dưới dùng toàn bộ chiều ngang vùng chi tiết: App/Sys là hai tab cố định thay dropdown để tránh popup bị che; SQL Object và SQL Data đặt cạnh nhau, SQL Top Script bên dưới chiếm hết chiều ngang và phần lớn chiều cao còn lại (mục tiêu tối thiểu 280px khi đủ không gian). SQL Object nhận mỗi dòng một tên table/store/function. SQL Data nhận mỗi dòng `tên bảng: điều kiện WHERE`. Khung hẹp chuyển Object/Data thành hai hàng, không ép editor hoặc nút thành cột chữ. Các editor có cuộn riêng và có thể kéo chiều cao.
- Bỏ nút Chọn chức năng. Khi tab Advance Note đang hiển thị và có yêu cầu đang mở, nhấp đúp menu ERP sẽ tra controller và hiển thị cây file ở nửa dưới cột yêu cầu; giữ Advance Note đang hoạt động. Khi không ở Advance Note, nhấp đúp vẫn theo luồng mở controller hiện tại.
- Đổi menu chỉ đổi kết quả tra source; không xóa File Path đã thêm. Gắn kết quả bất đồng bộ với project ID, request ID và menu ID để kết quả cũ không lọt sang yêu cầu/dự án mới.
- Trong nhánh Dir, Filter, Grid (kể cả thư mục con), chỉ hiện `.f`, không hiện `.xml` hay loại file khác; so khớp không phân biệt hoa thường theo thành phần đường dẫn. Các nhánh khác hiện mọi loại file như bình thường. Không áp bộ lọc này lên cây ERP chung.
- Có ô tìm file theo tên hoặc đường dẫn trong toàn thư mục dự án hiện tại, kết quả vẫn dạng cây và tuân theo quy tắc phần mở rộng trên. Danh sách file được quét một lần khi mở Advance Note rồi lọc trong bộ nhớ, nên không quét LAN lại mỗi ký tự. Đây là tìm tên/đường dẫn, không tìm nội dung file.
- Tích file rồi nhấn **Thêm vào File Path**: nối đường dẫn tuyệt đối vào yêu cầu đang mở, mỗi file một dòng, loại trùng theo đường dẫn Windows. Bỏ tích không tự xóa đường dẫn đã thêm; File Path vẫn sửa trực tiếp được. Không tự loại các đường dẫn XML đã lưu từ bản cũ.
- Không có yêu cầu: vùng source báo chọn/tạo yêu cầu. Đang tra: giữ kích thước khung và báo đang tải. Không có file hoặc thư mục không truy cập được: thông báo ngay trong vùng cây, giữ File Path và nội dung SQL.

Sơ đồ bố cục đề xuất theo vị trí người dùng yêu cầu:

```text
Danh sách yêu cầu       | Tiêu đề                 Generate Update · Xóa · Lưu
Tìm yêu cầu            | File Path (toàn chiều ngang vùng chi tiết)
-----------------------|---------------------------------------------------
Tìm file trong dự án    | App | Sys
Cây source có checkbox | SQL Object              | SQL Data
Thêm vào File Path     | SQL Top Script (rộng, cao, có cuộn riêng)
```

### SQL không giới hạn thời gian thực thi

- Đặt `CommandTimeout = 0` cho các command SQL trong đường chạy LSCode, bao gồm command hiện đang dùng mặc định SqlClient; bỏ hạn thời gian tự hủy truy vấn ở bridge/caller nếu có. Không chỉ sửa riêng nút Chạy trong editor.
- Kiểm kê editor, tra menu/controller, metadata/gợi ý, cấu trúc và dữ liệu, script object, đồng bộ danh mục, Profiler và Generate Update. Giữ hủy chủ động bằng nút Dừng/cancellation và hủy tác vụ lỗi thời khi đổi ngữ cảnh.
- Phân biệt command timeout với thời gian mở kết nối: yêu cầu này áp dụng thời gian thực thi SQL; không tự sửa connection string hoặc cơ chế kết nối mạng.
- Đã đối chiếu source: `LSCode.Shell/Services/SqlRunner.cs` đặt 60 giây và được Infrastructure Compile Link; `ProjectDatabase.cs`, `TableInspection.cs` đặt 30 giây; `StandardProjectSync.cs` đặt 60 giây. Profiler/Trace có command 10–20 giây. Phải rà cả command không khai báo timeout để tránh sót giới hạn mặc định.

### Generate Update từ Fcode

Mục tiêu: chọn yêu cầu rồi Generate Update để tạo gói cập nhật từ File Path và SQL của yêu cầu đó. Cách hiểu trong đặc tả: xuất gói update vào đường dẫn update của profile, không tự chạy script lên database đích hoặc chép đè chương trình đang vận hành.

Đã khảo sát tĩnh `D:/FAST/Code/LTHO/Fcode/FCode/urNoteX.cs`: có lời gọi `clsScript.GetScript` (khoảng dòng 5023, 5271), sao chép file (5212–5244), ghi script bằng `Encoding.Unicode`, xử lý tên file đã tồn tại và ánh xạ đường dẫn tương đối theo ProgramPath/MobilePath trước khi `CopyFile` (khoảng 6000–6135). Chưa chạy Fcode; nhiều tên hàm và chuỗi bị obfuscate nên chưa khẳng định chính xác tên thư mục đầu ra, cú pháp SQL Data hay thứ tự ghép script của mọi chế độ.

Hợp đồng triển khai:

1. Lấy snapshot yêu cầu được chọn, File Path, SQL App/Sys và cấu hình project; lưu XML thành công trước khi phát sinh gói. Không lấy mọi yêu cầu chỉ vì chúng đang hiện trong danh sách.
2. Lấy thư mục update từ profile, hoặc dùng quy ước `CustomerUpdate/<tên dự án>/update` hiện có của LSCode. Giữ cấu trúc đường dẫn tương đối của file dự án; từ chối file thiếu và đường dẫn nằm ngoài project.
3. Ghi SQL Top Script → Object → Data của từng database vào `SQL/App` và `SQL/Sys`, phân cách bằng `GO`, encoding UTF-16 LE có BOM. Với SQL Object, đọc định nghĩa từ target đang mở và sinh `DROP` + `CREATE` cho table, store và function theo thứ tự các dòng. Với SQL Data, đọc toàn bộ cột/dòng khớp điều kiện của từng khóa `bảng: WHERE`, sau đó sinh INSERT đúng kiểu dữ liệu, identity và bỏ cột do SQL Server tự sinh. Không chạy script kết quả lên database đích. Tên script lấy từ tiêu đề yêu cầu; mỗi lượt Generate tạo gói riêng nên không ghi đè gói cũ.
4. Xác minh chế độ một/nhiều yêu cầu và nhánh mobile của Fcode bằng fixture trước khi tuyên bố tương đương. Bản LSCode trước mắt thao tác trên yêu cầu đang chọn; không tự thêm chế độ chưa được yêu cầu.
5. Mỗi lượt tạo một thư mục con có tên dạng `Update-<ngày giờ>-<tiêu đề>-<mã>` trong đường dẫn update của profile. Gói chứa source theo đường dẫn tương đối và các script App/Sys đã nhập. Sau khi hoàn tất, Advance Note hiển thị đường dẫn chính xác cùng số file và hai nút **Sao chép đường dẫn** và **Mở thư mục**. Yêu cầu trống báo lỗi trước khi tạo thư mục.
6. WebView gửi command qua bridge hiện có; Infrastructure xử lý filesystem, SQL scripting và XML. Không phụ thuộc executable/DLL hoặc checkout Fcode khi chạy.

### Tiêu chí nghiệm thu khi triển khai

- Query thử nghiệm vượt 60 giây vẫn chạy; Dừng hủy được; các luồng SQL không âm thầm dùng timeout mặc định.
- Nhấp đúp ERP đúng yêu cầu, đúng project; đổi menu/yêu cầu liên tục không nhận kết quả cũ. File `.f` ở Dir/Filter/Grid hiện đúng, `.xml` ở các nhánh khác vẫn có.
- Tìm file trên dự án lớn không khóa UI; tick → thêm giữ File Path cũ và không trùng; lưu/mở XML giữ App/Sys, trạng thái và chi tiết kỹ thuật cũ dù không còn ô sửa.
- Đổi App/Sys không mất nội dung. File Path đủ chiều ngang, SQL Script rộng/cao; kiểm tra resize, Light/Dark/High Contrast, bàn phím và cuộn.
- Generate với fixture có file và SQL ở cả App/Sys tạo đúng gói; kiểm tra DROP + CREATE của table/store/function, INSERT chỉ chứa dòng khớp WHERE, thiếu source, SQL object không tồn tại, thiếu quyền VIEW DEFINITION, thiếu quyền ghi và hủy giữa chừng. So sánh đầu ra Fcode trước khi ghi nhận parity.
- Build/publish sau lệnh triển khai của người dùng. Bộ kiểm tra tự động xác nhận lưu XML, sao chép source tương đối, encoding/thứ tự script và không ghi đè script cũ; đối chiếu Fcode thực tế và query SQL dài hơn 60 giây vẫn là nghiệm thu thủ công.

## Mục tiêu

Đưa luồng Advance Note từ Fcode vào LSCode cho người làm dự án ERP trên desktop. Người dùng chọn yêu cầu theo tiêu đề và trạng thái, chỉnh nội dung kỹ thuật, tra source của chức năng đang xử lý rồi lưu yêu cầu để mở lại.

## Luồng sử dụng

1. Mở **Advance Note** từ danh sách lệnh. Dùng một tab công cụ trong không gian làm việc, có thể dùng lại phím **Ctrl+Shift+4** của Fcode nếu không xung đột với phím tắt hiện tại.
2. Tạo, tìm, chọn, sửa hoặc xóa yêu cầu. Mỗi yêu cầu có ID ổn định, tiêu đề, trạng thái và nội dung ghi chú. Chi tiết kỹ thuật thuộc về yêu cầu đang chọn. Xác nhận trước khi xóa.
3. Bật tra source trong Advance Note rồi chọn một chức năng ở cây menu ERP. LSCode dùng chức năng và dự án đang chọn để tra controller theo luồng ERP sẵn có, hiển thị file theo cây và cho mở file trong editor. Thay đổi menu cập nhật kết quả tra source, giữ yêu cầu đang chỉnh.
4. Nhấn **Lưu** hoặc **Ctrl+S** khi đang ở tab Advance Note. Mở lại tab để tiếp tục từ XML đã lưu. Hiển thị lỗi tải/lưu và giữ bản đang chỉnh khi thao tác lưu thất bại.

## Lưu trữ và phạm vi

- WebView gọi lệnh qua Application. Infrastructure đọc và ghi XML trong thư mục dữ liệu LSCode, không cần cài Fcode.
- XML do LSCode quản lý có phiên bản định dạng. Ghi qua file tạm cùng thư mục, sau đó thay thế bản đích. Trình đọc tắt DTD và external entity. Báo lỗi rõ nếu XML hỏng.
- Phân biệt yêu cầu theo dự án. Giữ tham chiếu menu và source theo dự án đã chọn. Bản đầu giữ luồng làm việc và lưu XML, chưa cam kết tương thích trực tiếp XML cũ của Fcode khi chưa có mẫu.

## Giao diện và nghiệm thu

- Dùng các thành phần và theme hiện có của LSCode. Việc chính là mở đúng yêu cầu, chỉnh nội dung và mở đúng file source. Ưu tiên hiển thị tiêu đề và trạng thái ở danh sách, đặt chi tiết và source theo bố cục được chọn trước khi dựng.
- Kiểm tra tạo → sửa → lưu → mở lại, xác nhận xóa, XML hỏng, lưu lỗi và đổi dự án.
- Kiểm tra chọn menu ERP có controller, menu thiếu file, chuyển menu liên tiếp và mở file kết quả trong editor.

## Kết quả triển khai ban đầu (lịch sử)

- Nút **Advance Note** nằm trên thanh công cụ. Lệnh cũng có trong danh sách lệnh với phím tắt **Ctrl+Shift+4**. Khi chưa mở dự án, lệnh đưa người dùng tới màn chọn dự án.
- Bản đầu từng đặt trạng thái, chi tiết kỹ thuật và nút **Chọn chức năng** ở cột phải. Bản nâng cấp giữ các giá trị này trong XML để tương thích nhưng đã bỏ control khỏi giao diện.
- Bản nâng cấp dùng nhấp đúp menu trong cây Chức năng ERP để lưu `wmenu_id`, tên chức năng, `sysid`, `link`, gọi `project.controller` và đưa file controller vào nửa dưới cột trái mà không rời tab Advance Note.
- Người dùng có thể mở file từ cây source vào editor, sao chép tên bằng Ctrl+C và mở thư mục từ menu chuột phải dùng chung với cây file ERP.
- **Lưu** hoặc **Ctrl+S** ghi toàn bộ yêu cầu của dự án vào `advance-notes-<mã-băm-project-id>.xml` trong thư mục dữ liệu LSCode. XML chứa menu và danh sách source đã tìm thấy để mở lại nhanh.
- Trước khi đóng tab có thay đổi chưa lưu, LSCode yêu cầu xác nhận. Xóa yêu cầu cũng yêu cầu xác nhận.
- Bộ kiểm tra WebView xác nhận vòng lưu/mở lại XML, menu ERP và source file. Tiêu đề trống bị từ chối trước khi thay thế file đang lưu.

## Cách thử

File Path chứa các đường dẫn đã tích trong cây source, mỗi file một dòng, có thể sửa trực tiếp.
Chọn chức năng ERP để tải cây thư mục rồi tích từng file cần đưa vào yêu cầu.
Các ô SQL Object, SQL Data, SQL Top Script lưu riêng theo database App/Sys trong XML. SQL Object chỉ cần nhập mỗi dòng một tên, ví dụ `dbo.d46$000000` hoặc `dbo.FastBusiness$App$Startup`. SQL Data có dạng `zhrdmloaivp: 1=1`; phần sau dấu `:` là biểu thức WHERE, không nhập từ khóa `WHERE`.
XML cũ vẫn đọc được với các ô SQL ban đầu để trống.

1. Mở một dự án ERP có kết nối SYS rồi nhấn **Ctrl+Shift+4**.
2. Nhấn **Thêm** và nhập tiêu đề.
3. Mở tab **Chức năng** bên trái rồi nhấp đúp menu ERP. Cây source xuất hiện ở nửa dưới cột yêu cầu.
4. Nhập từ khóa rồi bấm **Tìm** hoặc Enter để cập nhật cây. Ô vuông ở góc **Source ERP** chọn hoặc bỏ chọn tất cả file trong kết quả hiện tại, kể cả nhánh đang thu gọn. Nhấn **Thêm vào File Path** để đưa các đường dẫn đã chọn vào yêu cầu. Nhập tên object ở SQL Object nếu cần.
5. Để lấy dữ liệu, mở **Cấu trúc và dữ liệu**, chọn database và bảng, nhập biểu thức trong ô WHERE (để trống sẽ dùng `1=1`), rồi nhấn **Ctrl+D**. LSCode thêm hoặc cập nhật dòng `bảng: điều kiện` trong SQL Data App/Sys của yêu cầu Advance Note đang chọn; cùng một bảng được thay điều kiện thay vì tạo dòng trùng.
6. Nhấn **Lưu** để giữ XML hoặc **Generate Update** để lưu và tạo gói tại đường dẫn update của profile. Generate cần kết nối App/Sys tương ứng khi có danh sách object hoặc dữ liệu. SQL thô đã lưu từ phiên bản cũ vẫn được xuất nguyên văn để không làm hỏng yêu cầu cũ.
