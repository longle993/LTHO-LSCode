# Tạo danh mục — thiết kế bảng và nhập dữ liệu

Yêu cầu thao tác bổ sung 05/11/2026: nhấn một lần vào ô được phép sửa để nhập liệu/chọn kiểu hoặc tick checkbox, không cần nhấn đúp. Các trường chỉ đọc và trường hệ thống vẫn khóa; thao tác sửa ô không tự lưu hay thực thi SQL. Xem [kế hoạch tinh chỉnh UI](../history/UI-DENSITY-AND-EDITOR.vi.md).

Trạng thái: **đã bổ sung duplicate template và copy/paste nhiều dòng trong source**, dựa trên màn danh mục từ commit `13967fb`. Đã kiểm tra WPF ba theme và tích hợp SQL Server cục bộ bằng database thử nghiệm riêng; phạm vi kiểm chứng và giới hạn ở mục 12. Cập nhật ngày 05/11/2026.

## 1. Mục tiêu và phạm vi

Người dùng tạo bảng danh mục trực tiếp trong LS Code: nhập tên bảng, khai báo tên trường, chọn kiểu dữ liệu và ràng buộc, rồi nhấn **Lưu** để tạo bảng thật trong database đang chọn. Sau đó có thể chọn bảng khác để nhập dữ liệu hoặc thêm trường.

Bổ sung ngày 05/11/2026: **Duplicate template** để tạo bản nháp bảng mới từ mẫu cấu trúc, và **copy/paste nhiều dòng** trong lưới cấu trúc/dữ liệu. Chi tiết ở mục 10–11. Người dùng đã xác nhận: “template” là cấu trúc bảng danh mục được chọn làm mẫu; duplicate chỉ nhân bản cấu trúc, không kèm dữ liệu.

“Danh mục” trong đặc tả này là bảng dữ liệu SQL Server. Chức năng không đồng nghĩa với sinh controller, XML, ASPX hay menu ERP; các generator đó vẫn thuộc lộ trình riêng.

Ảnh người dùng cung cấp là design reference: lấy cách tổ chức lưới, tiêu đề trường và thao tác thêm dòng/thêm cột như Lark Base. **Không có cột danh sách bên trái trong màn này.** Vùng bảng chiếm toàn bộ nội dung; ô chọn bảng nằm trên thanh đầu trang. Không tự thêm các chức năng nhóm, tô màu điều kiện hay quản lý chế độ xem chỉ vì chúng xuất hiện trong ảnh.

Tài liệu yêu cầu được cập nhật trước khi triển khai theo đề nghị của người dùng. Hai thao tác mới được tích hợp vào màn danh mục hiện có, giữ WPF, bố cục không sidebar và style chung theo skill ui-ux.

## 2. Nền tảng và thành phần tái sử dụng

- Ứng dụng desktop C# / WPF / .NET 10; dùng control và resource chung, không chuyển thành web hoặc cài thư viện UI mới.
- `App.xaml`: style Button, TextBox, ComboBox, DataGrid và font Segoe UI; `MainWindow.xaml.cs::ApplyTheme`: màu theo theme.
- `TableBrowser.cs` đang là cửa sổ xem cấu trúc/dữ liệu **chỉ đọc**. `Services/TableDefinition.cs` đang đọc metadata để tạo mô tả DDL; không coi đây là dịch vụ tạo bảng đã có.
- `CatalogDesigner.cs` giữ công cụ tạo danh mục/nhập bản ghi; các partial `CatalogDesigner.Transfer.cs` và `CatalogDesigner.Clipboard.cs` bổ sung nhân bản và clipboard. `CatalogPreview.cs` hỗ trợ chọn dòng bằng cột số thứ tự, Ctrl/Shift và sao chép nhiều dòng; bấm ô vẫn có thể sao chép riêng giá trị ô.
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
- Bổ sung **Duplicate template…** cạnh Tạo bảng mới; chỉ bật khi đã chọn bảng nguồn hợp lệ. Trong lưới có menu chuột phải **Sao chép dòng / Dán dòng**, cùng phím tắt Ctrl+C / Ctrl+V theo ngữ cảnh ở mục 11; không thêm sidebar.

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
| Nhân bản mẫu | Hiện rõ bảng nguồn, tên bảng mới và “Bản nháp chưa lưu”; không đổi bảng nguồn |
| Clipboard rỗng/sai định dạng | Giữ nguyên lưới, nêu lý do không thể dán |
| Dán nhiều dòng có lỗi | Đánh dấu dòng/cột lỗi ở bước xem trước; chưa đưa bất kỳ dòng nào vào bản nháp cho đến khi toàn bộ đợt dán hợp lệ |
| Clipboard đang bị ứng dụng khác giữ | Thông báo sao chép/dán chưa hoàn tất, cho thử lại; không làm mất lựa chọn hay bản nháp |

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
| DM-11 | Duplicate template tạo bản nháp cấu trúc độc lập, bắt buộc tên bảng mới; nguồn không bị sửa và không có SQL ghi trước khi Lưu |
| DM-12 | Bản duplicate giữ đúng cấu hình được hỗ trợ; bốn trường hệ thống xuất hiện đúng một lần, datetime2 vẫn có kiểu datetime; thành phần không hỗ trợ phải được báo rõ và chặn nhân bản |
| DM-13 | Sao chép/dán được một hoặc nhiều dòng, liền hoặc không liền nhau khi chọn nguyên dòng; giữ thứ tự hiển thị và không tự lấy các dòng chưa tải |
| DM-14 | Dán dữ liệu trong ứng dụng và TSV từ Excel/bảng tính có kiểm tra ánh xạ cột, kiểu, NULL/rỗng/DEFAULT; không ghi đè bản ghi đã lưu |
| DM-15 | Khóa chính trùng, lỗi khóa ngoại, giá trị quá dài, cột thiếu/thừa và lỗi clipboard được báo rõ; không bỏ dòng/ô âm thầm hoặc lưu một phần |
| DM-16 | Dán trường tạo bản sao độc lập, không nhân đôi trường hệ thống; tên trùng/identity trùng/ràng buộc sai được sửa trước khi áp dụng |
| DM-17 | Ctrl+C/Ctrl+V trong ô đang sửa vẫn thao tác văn bản; thao tác dòng chỉ chạy khi lưới đang chọn nguyên dòng; hủy bước dán giữ nguyên bản nháp |

Kiểm chứng gồm logic sinh định nghĩa và validation, tích hợp transaction/rollback trên database SQL Server cục bộ tách biệt, smoke WPF và ảnh render ba theme; không chạy ghi vào database khách hàng. Chi tiết kết quả hiện tại ở mục 12; không coi các ca chưa thử là đã nghiệm thu.

## 10. Duplicate template — nhân bản mẫu cấu trúc bảng

### Phạm vi đã chốt

Nguồn là cấu trúc bảng đã lưu trong database của cửa sổ hiện tại. Nhân bản tạo một **bản nháp bảng mới** có danh sách trường và cấu hình độc lập; không tạo đối tượng template lưu trữ riêng trong phạm vi đầu tiên. Không sao chép bản ghi, quyền truy cập, trigger, lịch sử audit hay đối tượng phụ thuộc. Việc kèm dữ liệu là phạm vi riêng nếu người dùng yêu cầu.

### Luồng thao tác

1. Chọn bảng nguồn, nhấn **Duplicate template…**. Nếu còn thay đổi nháp, xử lý Lưu/Bỏ thay đổi/Ở lại theo luồng hiện có trước khi đọc cấu trúc nguồn đã lưu.
2. Đọc lại metadata, hiển thị bảng nguồn chỉ đọc; đề xuất tên `<tên_nguồn>_copy`, chọn schema đích trong cùng database. Tên gợi ý phải hợp lệ, còn chỗ cho hậu tố và chưa tồn tại; nếu trùng thì thử `_copy_2`, `_copy_3`… Người dùng có thể sửa tên trước khi tiếp tục.
3. Xem danh sách trường, kiểu, tham số, NULL/NOT NULL, khóa chính/UNIQUE, identity, DEFAULT và khóa ngoại. Bảo toàn thứ tự trường nghiệp vụ và thứ tự cột trong khóa ghép được hỗ trợ. Tên constraint mới phải được sinh cho bảng đích để tránh trùng constraint nguồn.
4. Tạo bản nháp độc lập, chuyển sang Cấu trúc; người dùng chỉnh trường hoặc ràng buộc rồi nhấn **Lưu**. Hủy nhân bản không tạo bảng hoặc sửa nguồn.
5. Lưu theo transaction tạo bảng hiện có; sau thành công chọn bảng mới, dữ liệu ban đầu rỗng. Thay đổi bản nháp hoặc bảng đích không được làm đổi bảng nguồn.

### Quy tắc bảo toàn cấu trúc

- Bốn trường hệ thống ở mục 7 luôn có đúng một lần. Nếu nguồn đã có tên và kiểu đúng thì nhận diện lại, không thêm bản sao; nếu nguồn chưa có thì thêm đủ. Nếu tên hệ thống tồn tại nhưng kiểu không đúng hợp đồng, báo xung đột và chặn nhân bản, không tự chuyển kiểu hoặc âm thầm bỏ trường.
- Khóa ngoại tự tham chiếu được ánh xạ sang bảng đích; khóa ngoại tham chiếu bảng khác giữ đúng bảng/cột đích trong cùng database. Kiểm tra lại tính hợp lệ trước khi Lưu.
- Với identity, giữ định nghĩa seed/increment; không sao chép giá trị bộ đếm hiện tại của bảng nguồn. Audit của bản ghi nguồn không được đưa sang bảng mới.
- Kiểu/ràng buộc/biểu thức DEFAULT không biểu diễn được an toàn bằng bộ thiết kế hiện tại, computed column, CHECK tùy biến hoặc cấu trúc đặc biệt phải được liệt kê cụ thể và **chặn nhân bản**. Không tạo bản gần giống nhưng thiếu ràng buộc mà người dùng không biết.
- Không nhân bản index phụ, phân vùng, compression, quyền hoặc extended property tùy biến trong phạm vi đầu tiên. Bước xem trước ghi rõ phạm vi này; marker nhận diện bảng danh mục của LS Code được tạo theo quy tắc tạo bảng mới, không sao chép tùy tiện từ nguồn.

## 11. Copy/paste nhiều dòng

### Chọn dòng và phím tắt

- Chọn nguyên dòng bằng đầu dòng; Shift chọn một dải, Ctrl chọn nhiều dòng không liền nhau. Sao chép theo thứ tự đang hiển thị, kể cả khi đang sắp xếp/lọc; chỉ lấy dòng được chọn đã tải, không tự truy vấn thêm dữ liệu.
- **Ctrl+C / Sao chép dòng** sao chép các dòng đang chọn. **Ctrl+V / Dán dòng** đọc clipboard để xem trước đợt dán. Khi con trỏ đang sửa văn bản trong một ô, hai phím tắt giữ hành vi sao chép/dán văn bản của ô; không kích hoạt thao tác cả dòng.
- Dán dòng luôn thêm bản nháp mới ở cuối danh sách; không ghi đè dòng đã lưu hoặc dòng đang chọn. Hỗ trợ giữa hai bảng trong cùng công cụ nếu ánh xạ và kiểu tương thích; không tự đổi target database.

### Copy/paste dòng cấu trúc

- Sao chép tên, kiểu, độ dài/precision/scale và các ràng buộc được hỗ trợ của trường nghiệp vụ. Dán thành các trường nháp độc lập; sửa bản sao không tác động trường nguồn.
- Khi dán một tên đã tồn tại, đề xuất hậu tố `_copy`, `_copy_2`… hợp lệ và duy nhất theo collation. Hiển thị tên mới trong bước xem trước trước khi người dùng áp dụng.
- Bốn dòng hệ thống bị khóa không được đưa vào đợt sao chép trường; menu sao chép dòng khóa nếu chỉ chọn các dòng này. Nếu vùng chọn lẫn trường hệ thống thì thông báo số trường bị loại rõ ràng trước khi sao chép. Clipboard bên ngoài có tên hệ thống dành riêng bị báo lỗi khi dán, không tự tạo thêm.
- Dán vào bảng đã lưu chỉ thêm trường mới, tuân thủ quy tắc dữ liệu cũ ở mục 6. Nếu sao chép identity/khóa chính vào bảng đã có thuộc tính tương ứng, bước xem trước yêu cầu sửa xung đột; không tự tắt ràng buộc.
- Khóa chính ghép chỉ sao chép đầy đủ khi chọn đủ nhóm trường; chọn thiếu nhóm phải báo lỗi, không biến khóa ghép thành khóa đơn. Ràng buộc tham chiếu được kiểm tra lại theo bảng đích; thành phần không hỗ trợ không được bị mất âm thầm.

### Copy/paste dòng dữ liệu

- Có thể sao chép các dòng bản ghi đã lưu hoặc dòng nháp. Dán tạo các dòng **mới chưa lưu**, chỉ ghi database khi nhấn Lưu; đây không phải thao tác cập nhật bản ghi cũ.
- Trong ứng dụng, clipboard có định dạng nội bộ mang tên cột, kiểu và trạng thái NULL/giá trị rỗng/Dùng mặc định; đồng thời có bản văn bản TSV để dán sang bảng tính. Nội dung clipboard luôn là dữ liệu, không thực thi như SQL, công thức hoặc lệnh.
- Với bản ghi mới, identity/computed và bốn trường audit được bỏ khỏi ánh xạ ghi; hiển thị danh sách cột bỏ qua. Database/công cụ cấp lại giá trị theo quy tắc tương ứng. Các cột này vẫn có thể xuất ra bản TSV để đọc, nhưng không được dán thành giá trị ghi cho bản ghi mới.
- Giá trị khóa chính/UNIQUE nghiệp vụ vẫn được sao chép. Khi dán lại vào cùng bảng, người dùng phải sửa giá trị trùng trước khi Lưu; không tự đổi mã nghiệp vụ, bỏ bản ghi trùng hoặc cập nhật bản ghi đã có.
- Với TSV từ Excel/bảng tính: tab phân cột, CRLF/LF phân dòng; hỗ trợ ô đặt trong dấu ngoặc kép chứa tab/xuống dòng và dấu ngoặc kép được escape. Dòng kết thúc rỗng do newline cuối clipboard không tạo thêm bản ghi; dòng trống thực sự bên trong dữ liệu vẫn được kiểm tra, không tự bỏ.
- Bước xem trước có lựa chọn **Dòng đầu là tên cột**, mặc định tắt, để không coi bản ghi đầu là header. Có header thì gợi ý ánh xạ theo tên; không header thì gợi ý theo thứ tự cột nghiệp vụ đang hiển thị và cho xem/sửa ánh xạ trước khi áp dụng. Header trùng, cột lạ, số cột lệch hoặc thiếu cột bắt buộc không có DEFAULT phải được báo cụ thể.
- TSV không mang kiểu: ô trống ở cột văn bản mặc định là chuỗi rỗng; với cột không phải văn bản thì yêu cầu chọn NULL, Dùng mặc định hoặc nhập giá trị. Không tự coi chữ `NULL` là NULL. Cột không ánh xạ dùng DEFAULT nếu có, nếu không thì NULL khi được phép; thiếu giá trị bắt buộc phải báo lỗi.
- Ngày giờ dùng định dạng hiển thị rõ trong xem trước; ưu tiên ISO (`yyyy-MM-dd`, `yyyy-MM-dd HH:mm:ss`). Số dùng quy tắc định dạng được hiển thị rõ; giá trị mơ hồ do dấu phân cách hoặc ngày/tháng phải yêu cầu sửa thay vì đoán. Không cắt chuỗi quá dài hay làm tròn số âm thầm.

### Xem trước, áp dụng và xử lý lỗi

1. Đọc clipboard, hiển thị số dòng/cột, chế độ đích (Cấu trúc hoặc Dữ liệu), ánh xạ cột và các cột không ghi. Không đưa dữ liệu vào lưới chính trong bước này.
2. Kiểm tra toàn bộ đợt dán; hiển thị lỗi theo dòng/cột, cho sửa ánh xạ hoặc giá trị. Kiểm tra trùng trong đợt dán và với bản nháp hiện tại; kiểm tra với database khi có thể và kiểm tra lại lúc Lưu vì dữ liệu có thể đổi đồng thời.
3. **Áp dụng** chỉ bật khi đợt dán hợp lệ; thêm toàn bộ dòng vào bản nháp cùng lúc và cập nhật số dòng chưa lưu. **Hủy** giữ nguyên bản nháp trước đó. Chưa có giao dịch ghi ở bước Áp dụng.
4. Người dùng có thể bỏ các dòng nháp vừa dán trước khi Lưu. Lưu dùng transaction ở mục 6; lỗi một dòng không ghi một phần đợt lưu và vẫn giữ dữ liệu để sửa.
5. Đề xuất giới hạn ban đầu 1.000 dòng hoặc 5 MB clipboard mỗi đợt; vượt giới hạn thì từ chối toàn bộ kèm số lượng/giới hạn, không cắt bớt. Kiểm tra giới hạn trước khi dựng lưới xem trước để tránh treo UI.

Các ca cần tiếp tục mở rộng khi nghiệm thu: clipboard bị ứng dụng khác khóa, dán giữa hai database có collation khác nhau, quyền hạn chế, mạng ngắt trong commit và dữ liệu lớn sát giới hạn. Kết quả đã thử ở mục 12.

## 12. Sử dụng và kiểm chứng bản triển khai

### Cách dùng

1. Mở **SQL → Tạo danh mục…**, chọn bảng nguồn, nhấn **Duplicate template…**, kiểm tra schema/tên mới và cấu trúc, chọn **Tạo bản nháp**. Chỉnh trường nếu cần, nhấn **Lưu** để tạo bảng rỗng có đủ bốn trường hệ thống.
2. Chọn nguyên dòng bằng cột số thứ tự ở lưới; Shift chọn dải, Ctrl chọn nhiều dòng. Nhấn **Ctrl+C** hoặc menu **Sao chép dòng**. Với vùng dữ liệu chỉ đọc, bấm một ô và Ctrl+C vẫn sao chép riêng ô đó.
3. Chọn chế độ đích Cấu trúc hoặc Dữ liệu, đặt focus tại lưới và nhấn **Ctrl+V**, hoặc dùng menu **Dán dòng…**. Trong ô đang sửa, Ctrl+C/V thao tác văn bản; nhấn Escape/kết thúc chỉnh sửa rồi chọn đầu dòng để thao tác cả dòng.
4. Trong xem trước, kiểm tra ánh xạ cột; khi dán TSV có header, bật **Dòng đầu là tên cột**. Sửa lỗi và nhấn **Kiểm tra**, rồi **Áp dụng** để thêm vào bản nháp. **Lưu** mới ghi database. **Hủy** không thay đổi các bản nháp đang có.
5. Chọn dòng nháp rồi dùng **Bỏ dòng nháp** ở Dữ liệu, **Bỏ trường nháp** ở Cấu trúc hoặc menu tương ứng để bỏ trước khi lưu. Các thao tác này chỉ bỏ nháp.

Clipboard từ ứng dụng giữ cấu trúc/kiểu/trạng thái NULL/DEFAULT bằng định dạng nội bộ, kèm TSV cho bảng tính. TSV cấu trúc bên ngoài dùng tám cột theo thứ tự: tên, kiểu, tham số, bắt buộc, khóa chính, không trùng, tự tăng, mặc định; các cột cờ nhận 0/1. Khóa ngoại, seed/increment, thứ tự khóa ghép và collation được giữ trong định dạng nội bộ; TSV tám cột không mang những cấu hình bổ sung này. Giới hạn mỗi đợt: 1.000 dòng, 5 MB.

### Kết quả kiểm tra

- Build Release thành công, không warning/error; 137 kiểm tra lõi đạt, gồm TSV Unicode/tab/xuống dòng/ngoặc kép, ô rỗng, token văn bản và giới hạn clipboard.
- Smoke riêng cho danh mục đạt trên Light/Dark/High Contrast: nguồn không bị sửa, bản sao độc lập, identity seed/increment, DEFAULT literal, collation, trường hệ thống đúng một lần, tự tham chiếu theo tên khóa đã sửa, tên trùng, chặn schema không hỗ trợ, dán nhiều dòng, header TSV, hủy dán và bỏ nháp.
- SQL Server trên localhost: tạo database thử nghiệm mới cho mỗi lượt; tạo nguồn và bảng nhân bản rỗng, kiểm tra FK tự tham chiếu, identity(7,3), DEFAULT, Unicode, chuỗi rỗng và chuỗi `:null`, audit, trùng khóa, rollback cả đợt và chặn chuyển varchar mất ký tự. Database thử nghiệm được xóa trong finally; không ghi database dự án/khách hàng.
- Smoke giao diện gọn hiện có đạt, gồm sửa ô bằng một lần bấm. Đã xem ảnh desktop/màn hẹp và ba theme; cửa sổ công cụ dùng resource nền/chữ theo theme để tránh chữ trắng trên nền trắng ở High Contrast.
- **Smoke tổng thể hiện dừng ở `Loading shifted editor layout`** tại `MainWindow.Tools.cs`, liên quan phần loading đang có thay đổi sẵn trong workspace (`MainWindow.Loading.cs` hiển thị/ẩn ProjectContextBar). Lượt chức năng danh mục này không chỉnh phần loading; không báo bộ smoke tổng thể là đã qua.

### Chạy lại

```powershell
# Build và kiểm tra lõi theo hạ tầng hiện có
.\build.ps1 -Test

# Smoke danh mục; dùng thư mục dữ liệu mới, rỗng cho mỗi lượt
$env:LSCODE_DATA_HOME = Join-Path $PWD ('artifacts\catalog-check-' + [Guid]::NewGuid().ToString('N'))
# Chỉ đặt 1 nếu muốn thử ghi trong database mới, tách biệt trên SQL Server localhost.
# Cần quyền tạo/xóa database thử nghiệm; mặc định không kết nối SQL khi thiếu biến này.
$env:LSCODE_CATALOG_LOCAL_SQL_TEST = '1'
Start-Process -FilePath .\src\LSCode.Shell\bin\Release\net10.0-windows\LSCode.exe -ArgumentList --catalog-transfer-smoke -Wait -WindowStyle Hidden
```

Báo cáo `catalog-transfer-result.txt` và ảnh PNG được ghi trong `LSCODE_DATA_HOME`. Chạy smoke cần phiên Windows có WPF; không thay bằng probe trình duyệt. Sau khi thử xong có thể bỏ biến `LSCODE_CATALOG_LOCAL_SQL_TEST` khỏi session.

Giới hạn có chủ ý: chỉ nhân bản trong cùng database; không kèm bản ghi, index phụ, trigger, quyền hoặc thuộc tính mở rộng tùy biến. DEFAULT biểu thức như GETDATE()/NEWID(), CHECK tùy biến, computed, UDT, khóa ngoại ghép/cascade và các thuộc tính nguồn chưa biểu diễn được đầy đủ sẽ chặn nhân bản với thông báo, không âm thầm bỏ. Các kiểu dữ liệu ngoài danh sách hỗ trợ được xem chỉ đọc. Kiểm tra khóa trùng cục bộ không thay thế ràng buộc/collation của SQL Server tại thời điểm Lưu. Cần nghiệm thu thêm trên schema ERP thật và quyền tài khoản thực tế.
