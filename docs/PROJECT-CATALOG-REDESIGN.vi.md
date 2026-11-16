# Thiết kế nâng cấp danh mục dự án

Trạng thái: đã triển khai ngày 14/11/2026.

Đợt nâng cấp tự chứa đã hoàn tất; thiết kế và kiểm thử được ghi tại [SELF-CONTAINED-PROJECT-CATALOG-AND-TEMPLATE-STUDIO.vi.md](SELF-CONTAINED-PROJECT-CATALOG-AND-TEMPLATE-STUDIO.vi.md).

## Mục tiêu và phạm vi

Màn hình desktop dành cho lập trình viên ERP: tìm và mở dự án, đồng bộ danh mục chuẩn, tạo/sửa hồ sơ đầy đủ. Ảnh FSD là tham chiếu các trường thông tin. LSCode sử dụng component và theme hiện có.

Các thao tác nằm ngay trên danh sách: **Đồng bộ**, **Tạo profile**, **Import XML**, **Export XML**. Không còn đồng bộ web.config thủ công. Form và bản xem trước import mở khi chọn thao tác, có đường quay về danh sách và giữ từ khóa/trang đang xem.

## Thông tin hồ sơ

Mã dự án và tên dự án là hai thuộc tính độc lập; ID nội bộ vẫn ổn định. Tương thích profile cũ: mã lấy từ tên hiện có, tên đầy đủ ưu tiên StandardProjectTitle. Không đổi ID hoặc làm mất lịch sử.

| Nhóm | Trường phải có trong tạo/sửa và lưu trữ |
|---|---|
| Nhận diện | Mã dự án, tên dự án, phiên bản, còn sử dụng |
| Đường dẫn | Chương trình WinForms, chương trình web, source WinForms, source web |
| Triển khai | Web Host 1, Web Host 2, thư mục update, mã dự án update ở nhà |
| Kết nối | Server, kiểu Windows/SQL Authentication, tên đăng nhập, mật khẩu, Database SYS |
| Thiết lập | User mặc định, List All Data, số thứ tự, ghi chú |

Giữ thêm các thuộc tính nguồn trả về: phân loại dự án, phụ lục, bộ phận, thông tin tạo/sửa khi có. Mapping tên cột chính xác cho phiên bản, List All Data và kiểu kết nối phải đối chiếu form/schema FSD trước khi viết mã; không đoán theo nhãn.

Nguồn đã xác minh: ma_da → mã; ten_da → tên; dir_pro_app/dir_pro_web → đường dẫn chương trình; dir_src_app/dir_src_web → source; web_host1/web_host2 → Web Host; dir_update → update; ten_server → server; db_sys → SYS; xuser/xpass → đăng nhập nguồn. Mật khẩu xpass cần giải mã đúng cơ chế FSD, sau đó mã hóa DPAPI trong catalog LSCode; không dùng trực tiếp chuỗi mã hóa làm mật khẩu SQL.

Đồng bộ lấy giá trị do nguồn chuẩn cung cấp làm giá trị chính thức, kể cả đường dẫn đã đổi ở nguồn. Trường không có trong kết quả nguồn phải giữ giá trị đã lưu; không xóa bằng giá trị mặc định tự dựng. Quy tắc này thay thế cách trước đây luôn giữ ProgramPath/UpdatePath cũ. Không xóa dự án vắng trong kết quả đồng bộ.

Tách đường dẫn chạy chương trình khỏi thư mục source. Danh sách hiển thị đường dẫn chương trình web, rỗng thì WinForms; đường dẫn mở trình duyệt lấy Web Host, không nhầm với UNC. App/SYS từ web.config được đọc khi mở dự án; hồ sơ SQL nguồn có thể dùng khi không có web.config. Không giả định hai nguồn kết nối luôn giống nhau.

## Màn danh sách

Thanh hành động nằm trên vùng tìm kiếm và kết quả. Mỗi dòng/thẻ thể hiện rõ: **mã dự án**, **tên đầy đủ**, **đường dẫn chương trình**. Server/DB là thông tin phụ trong chi tiết, không chiếm vị trí tên. Đường dẫn dài được cắt có tooltip và thao tác copy; tên không bị lẫn với mã.

Giữ thứ tự truy cập gần nhất. Tìm theo mã, tên và đường dẫn; thêm server/DB vào chỉ mục tìm kiếm. Hiển thị tổng số kết quả và trạng thái không tìm thấy.

Danh sách phân trang 50 dự án/trang, không render hàng nghìn thẻ. Sắp xếp/chỉ mục tìm kiếm chỉ tính lại khi dữ liệu, từ khóa hoặc last-access thay đổi; dùng memo và trì hoãn lọc theo nhập liệu. Kết quả đồng bộ chỉ hiện tổng số thêm/cập nhật/bỏ qua.

DTO danh sách chỉ chứa thuộc tính cần hiển thị/tìm. Chi tiết, metadata XML và thông tin nhạy cảm tải theo ID khi mở form. Rà tất cả nơi dùng danh sách (sidebar, lựa chọn dự án) để không còn render toàn bộ cùng lúc.

## Đồng bộ một lần bấm

1. Chưa có CurrUser: mở form nhỏ nhập tên, lưu thiết lập khi hợp lệ và chạy đồng bộ.
2. Đã có CurrUser: chạy ngay. Có thao tác sửa người dùng trong phần thiết lập đồng bộ.
3. Trạng thái đang chạy hiển thị rõ, khóa bấm trùng, có Dừng; thành công báo số thêm/cập nhật/bỏ qua, lỗi giữ nguyên dữ liệu đang xem.
4. Lưu kết quả một lần, cập nhật danh sách một lần; không gọi preview rồi query và lập kế hoạch lần hai.
5. Không dò web.config trên hàng nghìn UNC trong đồng bộ. Dùng chỉ mục đối chiếu mã; không tìm tuyến tính và parse XML lại cho từng cặp dự án.

Không còn form đồng bộ web.config riêng hoặc đường dẫn source FSD trong UI. Kết nối chuẩn nằm trong bản build LSCode; `web.config` chỉ được đọc theo `WebConfigPath` khi mở profile đã lưu.

## Form và XML

Tạo và sửa dùng chung schema trường, chia nhóm Nhận diện / Đường dẫn / Kết nối / Thiết lập. Nhãn trên ô, nhập bằng Tab theo thứ tự; mật khẩu che mặc định. Mã bắt buộc, kiểm tra trùng không phân biệt hoa thường; số thứ tự và các trường kiểu số được kiểm tra trước khi ghi.

Import XML chọn file native, hiển thị summary số mới/cập nhật/bỏ qua và yêu cầu làm mới bản xem trước nếu đổi lựa chọn ghi đè. Export ghi thẳng ra file bằng Save As. Cả hai xử lý streaming ở .NET, không đưa toàn bộ XML vào textarea WebView. Export bảo toàn các trường không nhạy cảm và mặc định loại mật khẩu/chuỗi kết nối chứa bí mật.

## Kiểm chứng trước giao

- Mapping đủ trường nguồn và tạo/sửa; mã và tên khác nhau; phiên bản và trạng thái không mất sau lưu/mở lại.
- Đồng bộ lần hai không thêm trùng; thay đổi đường dẫn nguồn được cập nhật; dự án vắng vẫn còn.
- Profile cũ mở được, giữ ID và lịch sử; XML round-trip giữ trường hỗ trợ và không lộ mật khẩu.
- Dữ liệu 3.000–10.000 dự án: đo thời gian đọc, lập kế hoạch, lưu và render riêng. DOM danh sách chỉ chứa tối đa một trang; nhập tìm kiếm và cuộn không dựng lại toàn bộ catalog.
- Kiểm tra tên dài, đường dẫn UNC dài, dữ liệu thiếu, danh sách rỗng, SQL lỗi, hủy và bấm đồng bộ lặp.
- Xem giao diện thật ở theme sáng/tối, cửa sổ desktop và chiều rộng hẹp; build bản chạy trực tiếp sau khi kiểm tra đạt.

## Trình tự

Đã thực hiện theo trình tự: tài liệu → model/mapping/lưu trữ → API danh sách/chi tiết → giao diện → kiểm thử → build. `ns_getListDept` hiện đọc và lưu toàn bộ 68 cột nguồn dưới metadata `Fsd_*`; các trường sử dụng thường xuyên được ánh xạ sang tên ổn định của LSCode. Kết nối SQL nhúng chỉ giải mã trong bộ nhớ.

Đo với CurrUser `LONGLD` ngày 14/11/2026: 2.998 dòng được đọc trong khoảng 0,76 giây và lập kế hoạch đầy đủ trong khoảng 0,41 giây trên máy phát triển. Đây là số đo phần đọc và mapping, chưa bao gồm độ trễ render WebView ở từng máy. Danh sách chỉ đưa 50 dòng vào DOM mỗi trang.
