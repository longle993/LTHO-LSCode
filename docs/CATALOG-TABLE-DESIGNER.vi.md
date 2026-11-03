# Tạo danh mục — thiết kế bảng và nhập dữ liệu

Trạng thái: **đặc tả mới, chưa triển khai**. Cập nhật ngày 03/11/2026.

## 1. Mục tiêu và phạm vi

Người dùng tạo bảng danh mục trực tiếp trong LS Code: nhập tên bảng, khai báo tên trường, chọn kiểu dữ liệu và ràng buộc, rồi nhấn **Lưu** để tạo bảng thật trong database đang chọn. Sau đó có thể chọn bảng khác để nhập dữ liệu hoặc thêm trường.

“Danh mục” trong đặc tả này là bảng dữ liệu SQL Server. Chức năng không đồng nghĩa với sinh controller, XML, ASPX hay menu ERP; các generator đó vẫn thuộc lộ trình riêng.

Ảnh người dùng cung cấp là design reference: lấy cách tổ chức lưới, tiêu đề trường và thao tác thêm dòng/thêm cột như Lark Base. **Không có cột danh sách bên trái trong màn này.** Vùng bảng chiếm toàn bộ nội dung; ô chọn bảng nằm trên thanh đầu trang. Không tự thêm các chức năng nhóm, tô màu điều kiện hay quản lý chế độ xem chỉ vì chúng xuất hiện trong ảnh.

Đây là cập nhật tài liệu yêu cầu và bố cục dự kiến, chưa phải bản giao diện đã dựng hoặc đã kiểm thử. Khi triển khai UI sẽ áp dụng quy trình duyệt thiết kế của skill ui-ux.

## 2. Nền tảng và thành phần tái sử dụng

- Ứng dụng desktop C# / WPF / .NET 10; dùng control và resource chung, không chuyển thành web hoặc cài thư viện UI mới.
- `App.xaml`: style Button, TextBox, ComboBox, DataGrid và font Segoe UI; `MainWindow.xaml.cs::ApplyTheme`: màu theo theme.
- `TableBrowser.cs` đang là cửa sổ xem cấu trúc/dữ liệu **chỉ đọc**. `Services/TableDefinition.cs` đang đọc metadata để tạo mô tả DDL; không coi đây là dịch vụ tạo bảng đã có.
- Bổ sung điểm vào **SQL → Tạo danh mục…**, mở công cụ WPF theo quy ước các cửa sổ phụ. Đây là đề xuất tích hợp mới; giữ luồng xem bảng chỉ đọc hiện tại.
- Target server/database thuộc cửa sổ, khởi tạo từ ngữ cảnh SQL hiện tại; nếu không có thì lấy App DB của dự án. Hiện rõ target để biết bảng sẽ được tạo ở đâu. Không tự chuyển về SYS khi thiếu App DB.

## 3. Bố cục dự kiến

### 3.1. Thanh đầu và vùng nội dung

Thanh đầu gồm tiêu đề **Danh mục**, target hiện tại, ô **Chọn bảng** có tìm theo `schema.tên_bảng`, và nút **Tạo bảng mới**. Không dùng cây hay danh sách bên trái để đổi bảng.

Trong vùng nội dung (tương ứng phần bên phải của ảnh tham chiếu):

- Hàng thông tin bảng có **Schema**, **Tên bảng** và nút **Lưu** ở cuối hàng. Khi tạo mới, tên bảng là ô nhập bắt buộc; khi chọn bảng đã có, tên chỉ đọc để không hiểu nhầm là thao tác đổi tên bảng.
- Hai chế độ **Cấu trúc** và **Dữ liệu** dùng chung một vùng bảng lớn. Bảng mới chưa lưu chỉ có thể khai báo cấu trúc; chế độ Dữ liệu bị khóa kèm lý do “Lưu bảng trước khi nhập dữ liệu”.
- Chế độ Cấu trúc: mỗi dòng là một trường, gồm **Tên trường**, **Kiểu dữ liệu**, **Tham số kiểu**, **Ràng buộc**, **Giá trị mặc định**, thao tác bỏ trường nháp. Nút **Thêm trường** tạo dòng khai báo mới.
- Chế độ Dữ liệu: mỗi cột là một trường thật, mỗi dòng là một bản ghi. Có **Thêm bản ghi** trên toolbar và dấu **+** cuối dãy tiêu đề cột để mở khai báo trường mới. Dấu + có tooltip “Thêm trường”, không tạo cột ngay khi bấm.
- Chỉ một nút Lưu chính trong mỗi chế độ: tạo bảng, lưu trường mới hoặc lưu bản ghi mới theo ngữ cảnh. Vùng trạng thái ghi rõ thao tác đang chờ, ví dụ “3 trường mới chưa lưu”.

Ví dụ nội dung minh họa, không phải dữ liệu thật:

```text
Danh mục    Server / App DB    Chọn bảng [dbo.dm_bo_phan v]    [Tạo bảng mới]

Schema [dbo v]    Tên bảng [dm_bo_phan                  ]           [Lưu]
Cấu trúc | Dữ liệu

Tên trường    Kiểu dữ liệu    Tham số    Ràng buộc           Mặc định
ma_bo_phan    nvarchar        32        Khóa chính          —
ten_bo_phan   nvarchar        128       Bắt buộc             —
trang_thai    bit             —         Bắt buộc             1
user_id0      int             —         Hệ thống · khóa      —
user_id2      int             —         Hệ thống · khóa      —
datetime0     datetime        —         Hệ thống · khóa      —
datetime2     datetime        —         Hệ thống · khóa      —
[+ Thêm trường]
```

### 3.2. Quy tắc thị giác và thao tác

- Lưới dữ liệu là nội dung chính, nền phẳng, đường chia nhẹ; không đặt từng vùng vào card có bóng. Bám bố cục ảnh nhưng dùng màu nhấn và font LS Code cho nhất quán.
- Dùng nhịp bảng hiện có: dòng dữ liệu 32 DIP, header 40 DIP; ô nhập và nút form 40 DIP. Dòng cấu trúc có thể cao hơn dòng dữ liệu để chứa control và lỗi. Đây là nhịp bảng dày của dự án, thay cho nhịp danh sách thoáng mặc định của skill.
- Ô nhập có nhãn rõ, không icon bên trái. Trường bắt buộc có dấu `*`; lỗi ghi ngay tại ô/trường liên quan, không chỉ đổi màu.
- Tái sử dụng ba theme **Light / Dark / High Contrast**. Không hard-code nền trắng theo ảnh cho mọi theme. Font và màu nhấn sửa tại resource chung nêu ở mục 2.
- Khi cửa sổ hẹp, thanh thông tin xuống hàng; Lưu luôn truy cập được. Bảng được cuộn ngang trong vùng riêng, không kéo toàn cửa sổ; tiêu đề và tên trường phải đủ đọc. Tái sử dụng scrollbar WPF và kiểm tra thu cửa sổ/DPI khi triển khai; CSS/probe trình duyệt không áp dụng trực tiếp cho WPF.
- Tab di chuyển giữa ô; Enter kết thúc chỉnh sửa ô, không tự tạo bảng; Escape hủy chỉnh sửa ô hiện tại. Nhãn nút và tooltip phân biệt thêm bản ghi với thêm trường.

## 4. Luồng tạo bảng mới

1. Chọn **Tạo bảng mới**; hiển thị bản nháp với bốn trường hệ thống ở mục 7.
2. Nhập tên bảng; chọn schema, mặc định đề xuất `dbo` nếu có quyền sử dụng. Không tự tạo schema.
3. Nhấn **Thêm trường**, nhập tên và chọn kiểu dữ liệu. Chỉ hiện tham số phù hợp với kiểu: độ dài hoặc precision/scale.
4. Chọn ràng buộc của từng trường. Có thể sửa hoặc bỏ trường tự khai báo khi bảng còn là bản nháp; không được bỏ trường hệ thống.
5. Nhấn **Lưu**: kiểm tra toàn bộ định nghĩa rồi thực hiện tạo bảng và các ràng buộc trong một transaction. Không thực thi khi chỉ đổi tên/chọn kiểu/thêm dòng khai báo.
6. Thành công: thông báo “Đã tạo bảng dbo.dm_bo_phan”, nạp metadata thật, cập nhật ô chọn bảng, DB Objects và cache gợi ý SQL của target tương ứng; chuyển sang Dữ liệu để nhập bản ghi.
7. Thất bại: giữ nguyên bản nháp và chỉ rõ lỗi; không báo thành công hoặc để lại bảng tạo dở. Nếu mất kết nối khiến kết quả commit chưa xác định, kiểm tra metadata trước khi thử lại để tránh tạo trùng.

## 5. Kiểu dữ liệu và ràng buộc

Danh sách dưới đây là phạm vi đề xuất ban đầu cho triển khai, không phải mô tả khả năng đã có.

| Nhóm | Kiểu có thể chọn | Cấu hình |
| --- | --- | --- |
| Văn bản | `nvarchar`, `varchar`, `nchar`, `char` | Độ dài hợp lệ theo kiểu; `MAX` chỉ cho kiểu hỗ trợ |
| Số nguyên | `int`, `bigint`, `smallint`, `tinyint` | Không có độ dài |
| Số thập phân | `decimal`, `numeric` | Precision và scale hợp lệ |
| Đúng/sai | `bit` | Giá trị 0/1 |
| Ngày/giờ | `date`, `datetime`, `datetime2` | `datetime2` có độ chính xác phần giây |
| Định danh | `uniqueidentifier` | Giá trị GUID hợp lệ |

| Ràng buộc | Hành vi cấu hình |
| --- | --- |
| Bắt buộc (`NOT NULL`) | Bật/tắt cho trường tự khai báo; mặc định đề xuất cho phép NULL |
| Khóa chính (`PRIMARY KEY`) | Bắt buộc không NULL; một khóa chính cho mỗi bảng. Chọn nhiều trường là một khóa ghép, phải hiển thị nhóm và thứ tự cột |
| Không trùng (`UNIQUE`) | Phạm vi đầu tiên áp dụng từng trường; kiểm tra dữ liệu hiện có theo quy tắc SQL Server |
| Tự tăng (`IDENTITY`) | Thuộc tính bổ sung, không phải khóa chính; đề xuất hỗ trợ cho kiểu số nguyên, tối đa một trường/bảng, mặc định seed/increment 1/1; không kết hợp DEFAULT |
| Giá trị mặc định (`DEFAULT`) | Nhập giá trị đúng kiểu; không tự hiểu chuỗi nhập là SQL để thực thi |
| Khóa ngoại (`FOREIGN KEY`) | Chọn bảng/cột đích từ metadata cùng database, kiểu tương thích và khóa đích hợp lệ. Phạm vi đầu tiên một cột, mặc định không cascade |

Không cung cấp ô thực thi SQL tùy ý trong cấu hình ràng buộc. CHECK tùy biến, khóa ngoại ghép, UNIQUE ghép, computed column và kiểu đặc biệt chưa nằm trong phạm vi đầu tiên. Bảng có các thành phần này vẫn hiển thị metadata thật; thành phần không hỗ trợ chỉnh sửa phải chỉ đọc, không bị chuyển kiểu hay loại bỏ.

Tên bảng/trường bắt buộc, không chỉ chứa khoảng trắng, hợp lệ với SQL Server và không vượt giới hạn identifier. Kiểm tra bảng trùng trong schema và trường trùng theo collation của database. Bốn tên hệ thống là tên dành riêng trong bảng mới, không được khai báo lại với cách viết hoa/thường khác.

Không tự thêm trường `id` hoặc khóa chính ngoài lựa chọn của người dùng. Việc tạo bảng không bắt buộc có khóa chính; phạm vi nhập dữ liệu ở mục 6 chỉ thêm bản ghi mới, không sửa/xóa bản ghi cũ thiếu định danh.

## 6. Chọn bảng có sẵn, nhập dữ liệu và thêm trường

### Chọn bảng

Ô chọn bảng liệt kê các bảng người dùng có quyền xem trong database hiện tại, gồm schema để phân biệt tên trùng. Không đưa view vào luồng thay đổi cấu trúc bảng. Chọn một bảng sẽ nạp metadata và dữ liệu thật; không đọc toàn bảng không giới hạn, dùng tải từng phần theo hạ tầng hiện có.

Chuyển bảng hoặc đóng cửa sổ khi còn thay đổi chưa lưu: chọn **Lưu**, **Bỏ thay đổi**, **Ở lại**. Lưu lỗi thì ở lại giữ bản nháp. Khi đang lưu, khóa chuyển bảng/target để kết quả không được gắn nhầm nơi. Đổi dự án ngoài shell không tự đổi target của cửa sổ đã mở.

### Nhập dữ liệu

- **Thêm bản ghi** tạo dòng nháp trong lưới; control nhập theo kiểu dữ liệu, ví dụ checkbox cho bit, ô ngày cho datetime.
- Phân biệt NULL, chuỗi rỗng và không truyền giá trị để database áp dụng DEFAULT. Hiện rõ lựa chọn “Dùng mặc định” khi cột có DEFAULT.
- Trường identity/computed do database quản lý chỉ đọc. Bốn trường audit của bảng do công cụ tạo quản lý theo mục 7.
- Nhấn **Lưu** để ghi các dòng mới trong một transaction; lỗi một dòng thì không ghi một phần, giữ bản nháp và đánh dấu dòng/ô có lỗi. Sau thành công tải lại giá trị do server sinh.
- Phạm vi yêu cầu ban đầu là thêm dữ liệu; chưa bao gồm sửa hoặc xóa bản ghi đã lưu.

### Thêm trường vào bảng đã có

- Chọn **Thêm trường** ở Cấu trúc hoặc dấu + cuối tiêu đề lưới Dữ liệu; nhập tên, kiểu và ràng buộc trong cùng công cụ. Còn dòng dữ liệu nháp thì phải lưu/bỏ trước khi chuyển sang thay đổi cấu trúc.
- **Lưu** áp dụng các trường mới bằng thay đổi cấu trúc bảng; không tạo lại bảng hoặc ghi đè dữ liệu cũ. Không đổi tên, xóa hoặc đổi kiểu trường đã tồn tại trong phạm vi này.
- Với bảng đã có dữ liệu: trường nullable có thể để NULL ở dòng cũ; trường NOT NULL phải có cách cấp giá trị hợp lệ cho dòng cũ. Khi cần, yêu cầu DEFAULT hợp lệ và nói rõ sẽ áp dụng cho các dòng cũ. Kiểm tra UNIQUE/PK/FK trước khi lưu; không âm thầm bỏ ràng buộc để vượt lỗi.
- Đọc lại metadata trước khi ghi để phát hiện bảng đã thay đổi bởi phiên khác; báo xung đột, giữ khai báo nháp để đối chiếu.
- Chọn bảng có sẵn không tự thêm bốn trường hệ thống còn thiếu. Bảng cũ được giữ nguyên cho đến khi có yêu cầu thay đổi rõ ràng.

## 7. Bốn trường hệ thống bắt buộc khi tạo bảng

Mọi bảng tạo bằng chức năng này luôn có đúng các trường sau, độc lập với các trường người dùng khai báo:

| Tên trường | Kiểu SQL bắt buộc | Ý nghĩa đề xuất |
| --- | --- | --- |
| `user_id0` | `int` | Người tạo |
| `user_id2` | `int` | Người cập nhật gần nhất |
| `datetime0` | `datetime` | Thời điểm tạo |
| `datetime2` | `datetime` | Thời điểm cập nhật gần nhất |

**`datetime2` trong cột tên trường vẫn có kiểu `datetime`, không đổi sang kiểu SQL `datetime2`.** Tên và kiểu của cả bốn trường là yêu cầu cố định của người dùng.

Hiện cả bốn dòng ngay từ bản nháp cấu trúc, gắn nhãn **Hệ thống**, khóa sửa tên/kiểu/xóa. Trong chế độ Dữ liệu chúng chỉ đọc, đặt sau các trường nghiệp vụ.

Người dùng chưa quy định NOT NULL, DEFAULT và nguồn user ID. Mặc định đề xuất cho thiết kế ban đầu: cả bốn cho phép NULL, không gán DEFAULT hoặc khóa ngoại tự động. Khi ghi bản ghi qua công cụ, nếu có ánh xạ user ID nghiệp vụ hợp lệ thì gán cả user_id0/user_id2 bằng ID đó; không suy ra ID từ tài khoản Windows và không tự điền 0. Nếu chưa có ánh xạ thì để NULL. Gán datetime0/datetime2 cùng thời điểm lấy từ SQL Server trong lần ghi.

Các quy tắc điền giá trị trên là **đề xuất**, không phải yêu cầu đã chốt về audit ERP; phải đối chiếu cơ chế user/thời gian của dự án trước khi triển khai. Không tự thêm trigger. Ghi dữ liệu từ công cụ khác không được bảo đảm có audit tự động. Nếu sau này có sửa bản ghi, giữ nguyên user_id0/datetime0 và chỉ cập nhật user_id2/datetime2.

## 8. Trạng thái và lỗi cần thể hiện

| Tình huống | Cách hiển thị/hành vi |
| --- | --- |
| Chưa có bảng | “Chưa có bảng danh mục” và nút Tạo bảng mới |
| Đang tải | Giữ khung bảng, hiện “Đang tải bảng…”; không hiển thị dữ liệu bảng trước dưới tên mới |
| Bảng chưa có bản ghi | Giữ header đúng cấu trúc; gợi ý Thêm bản ghi |
| Tên/kiểu/ràng buộc sai | Lỗi ngay trường liên quan; chưa thực thi và chuyển tới lỗi đầu tiên |
| Thiếu quyền | Nêu thao tác không được phép, khóa thao tác tương ứng; vẫn xem nếu có quyền đọc |
| Đang lưu | Nút “Đang lưu…” giữ nguyên kích thước, không cho gửi lặp |
| Lưu lỗi | Giữ bản nháp, thông báo dễ hiểu, cho thử lại; không lộ connection string/password |
| Thành công | Thông báo theo thao tác vừa hoàn thành, xóa dấu chưa lưu sau khi xác nhận kết quả |
| Tên dài/nhiều trường | Cuộn trong vùng bảng, tooltip tên đầy đủ; không đẩy Lưu khỏi cửa sổ |

## 9. Yêu cầu triển khai và tiêu chí nghiệm thu

Các mục sau là hợp đồng cần thực hiện ở lượt phát triển, không phải logic đã viết trong lần cập nhật tài liệu này:

- Tách bản nháp cấu trúc và bản nháp dữ liệu khỏi metadata đã lưu. Giá trị dữ liệu dùng parameter; tên schema/bảng/cột được kiểm tra và quote theo helper của dự án. Kiểu/ràng buộc từ danh sách cho phép, không ghép nguyên văn đầu vào thành SQL.
- Kiểm tra quyền thực tế cho tạo bảng, thay đổi cấu trúc, đọc và thêm dữ liệu. Không nâng quyền, không chuyển database để né lỗi quyền.
- Transaction bao phủ đầy đủ một lần Lưu; rollback khi lỗi và xử lý riêng kết quả commit chưa xác định. Hủy/tải chậm không được cập nhật nhầm bảng sau khi người dùng chuyển lựa chọn.

| Mã | Tiêu chí nghiệm thu |
| --- | --- |
| DM-01 | Tạo bảng bằng tên + các trường + kiểu + ràng buộc; chỉ nhấn Lưu mới tạo bảng thật |
| DM-02 | Mọi bảng mới có đủ user_id0 int, user_id2 int, datetime0 datetime, datetime2 datetime; không sửa/xóa/khai báo trùng được |
| DM-03 | Không có cột danh sách bên trái; chọn bảng trên thanh đầu; tên bảng và Lưu nằm trong vùng nội dung |
| DM-04 | Chọn bảng khác để đọc và thêm bản ghi; dữ liệu không ghi trước khi Lưu; giá trị không hợp lệ được giữ để sửa |
| DM-05 | Thêm trường vào bảng đã có dữ liệu không làm mất dữ liệu; xử lý đúng NOT NULL/DEFAULT và xung đột ràng buộc |
| DM-06 | Tên bảng/trường trùng, sai kiểu, thiếu quyền, lỗi kết nối không tạo schema/bản ghi dở dang; xử lý retry sau mất kết nối |
| DM-07 | Chuyển bảng/đóng cửa sổ có bản nháp không mất dữ liệu âm thầm; target được giữ đúng |
| DM-08 | Tên dài, bảng rỗng, nhiều cột, NULL, chuỗi rỗng, số 0 và ngày giờ hợp lệ đều thể hiện đúng |
| DM-09 | Light/Dark/High Contrast, thu cửa sổ và DPI scaling giữ chữ đọc được, thao tác Lưu truy cập được |
| DM-10 | Bảng có sẵn không tự bị thêm trường audit; các thành phần chưa hỗ trợ vẫn đọc đúng và không bị thay đổi |

Kiểm chứng khi triển khai: kiểm tra logic sinh định nghĩa và validation, tích hợp transaction/rollback trên database thử nghiệm, smoke tương tác WPF và ảnh render ba theme. Không dùng database khách hàng để thử thao tác ghi. Lần cập nhật tài liệu này không chạy SQL, không build tính năng và không xác nhận giao diện đã đạt các tiêu chí trên.
