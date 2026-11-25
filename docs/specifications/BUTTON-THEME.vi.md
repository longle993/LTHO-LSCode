# Phân loại nút và tùy chỉnh màu trong LSCode

Trạng thái đề xuất, ngày 23/11/2026. Tài liệu dựa trên mã nguồn hiện tại.

## Hiện trạng

- WebView dùng React và Tailwind v4. [ui.tsx](../../src/LSCode.Web/src/ui.tsx) có `Button`, `IconButton` và các kiểu `primary`, `outline`, `secondary`, `ghost`. Kiểu nguy hiểm hiện xuất hiện qua lớp riêng như `danger-action` trong [styles.css](../../src/LSCode.Web/src/styles.css).
- [SettingsDialog](../../src/LSCode.Web/src/features/SettingsDialog.tsx) cho chọn một màu nhấn. [App.tsx](../../src/LSCode.Web/src/App.tsx) áp màu đó qua biến CSS ở cấp theme. [SettingsStore](../../src/LSCode.Shell/Services/SettingsStore.cs) lưu `AccentColor` cùng thiết lập hiện có.
- WPF dùng style và brush riêng trong [App.xaml](../../src/LSCode.Shell/App.xaml), gồm style mặc định và `PrimaryButton`. Màu tùy chỉnh của WebView cần ánh xạ sang brush WPF khi triển khai giai đoạn WPF.

## Quy tắc phân loại

| Loại | Khi dùng | Ví dụ trong LSCode | Dáng mặc định |
| --- | --- | --- | --- |
| Primary | Hành động chính của một hộp thoại, form hoặc vùng làm việc | Chạy SQL, Lưu menu ERP, Gửi thử mail | Nền đặc, chữ tương phản. Một hành động chính trên mỗi nhóm thao tác. |
| Secondary | Hành động thay thế cần dễ thấy cạnh nút chính | Xem trước script, Xuất mẫu Excel, Hủy trong hộp xác nhận | Viền mảnh, nền trung tính. Dạng nền nhẹ dùng khi bố cục cần phân biệt thêm. |
| Tertiary / Ghost | Thao tác phụ, thường xuyên hoặc đặt cạnh nội dung | Quay lại, Sao chép thông tin, tải lại trong bảng công cụ | Nền trong suốt ở trạng thái thường, có nền nhẹ khi rê chuột. |
| Destructive | Hành động xóa hoặc ghi đè có hậu quả khó hoàn tác | Xóa profile, xác nhận xóa dòng SQL, xóa Structure App | Màu cảnh báo, nhãn nêu rõ đối tượng và hành động. Xác nhận cuối dùng dạng nhấn mạnh theo theme. |
| Icon Button | Thao tác ngắn, biểu tượng quen thuộc và không gian hạn chế | Đóng tab, mở cài đặt, tải lại cây, sao chép tên DB | Nút vuông theo kích thước thanh công cụ, có tooltip và tên truy cập. |

Icon Button là cách trình bày. Nút chỉ có icon vẫn có thể mang mức nguy hiểm, chẳng hạn nút mở xác nhận xóa profile. Nhãn xác nhận phải có chữ. Thao tác đóng tab, bỏ bộ lọc và xóa lịch sử hiển thị được phân loại theo hậu quả thực tế. Tab, lựa chọn trong dropdown, ô sắp xếp và nút điều khiển cửa sổ giữ vai trò giao diện riêng.

Khi rà từng màn, ghi lại tên nút, vị trí, thao tác, mức ưu tiên, trường hợp cần xác nhận và nơi đang đặt style. Phân loại theo ngữ cảnh của nút. Ví dụ, **Lưu** là Primary trong form chỉnh menu nhưng có thể là Secondary trên thanh SQL nếu **Chạy** là hành động chính.

## Cài đặt màu

Đề xuất đặt mục **Màu nút** trong **Cài đặt → Giao diện**. Hiển thị năm hàng mẫu và cho chọn màu nền, chữ hoặc icon, viền theo loại nút. Mỗi thuộc tính có lựa chọn dùng mặc định theme. Dòng mẫu hiển thị trạng thái thường, hover, nhấn giữ, focus bàn phím và disabled. Nút **Đặt lại** khôi phục một loại hoặc tất cả loại trong theme đang chỉnh. **Lưu** áp dụng cấu hình, **Hủy** bỏ bản nháp.

- Lưu màu theo theme bằng trường tùy chọn `buttonColors`, với khóa cố định cho năm loại và ba thuộc tính màu. Màu nhập theo dạng `#RRGGBB`. Màu trong suốt của Ghost và Icon mặc định đến từ theme.
- Giá trị được phân giải theo thứ tự màu nút người dùng → màu mặc định của theme → token nền hiện hành. Primary mặc định tiếp tục dùng màu nhấn `AccentColor`. Màu hover và pressed có thể suy từ nền hiệu lực. Màu focus lấy từ token theme và phải nhìn rõ ở mọi trạng thái.
- Kiểm tra chữ và nền đạt ít nhất 4,5:1 cho chữ cỡ thường. Kiểm tra icon thiết yếu và dấu focus đạt 3:1 với màu kề. Nếu cặp màu tùy chỉnh không đạt, báo ngay trong xem trước và giữ giá trị hợp lệ gần nhất cho đến khi người dùng sửa.
- Dùng token của theme sáng, tối và tương phản cao. Khi bật chế độ tương phản cao, ưu tiên cặp màu hệ thống dành cho nút và con trỏ bàn phím.
- Nhãn dài được xuống dòng hoặc thanh công cụ đổi cách xếp. Nút icon giữ vùng bấm theo thanh công cụ. Nút đang xử lý và nút disabled giữ kích thước ổn định.

## Kế hoạch kỹ thuật

1. Chốt bảng phân loại theo màn. Ưu tiên thanh editor và SQL, Cấu trúc và dữ liệu, Menu ERP, Dự án của bạn, Tạo danh mục, Check Mail và QLYC. Rà thêm các nút gốc, CSS ghi đè theo màn và thao tác chỉ xuất hiện khi chọn dòng.
2. Chuẩn hóa `Button` và `IconButton` của WebView. Thêm variant `destructive`, giữ kiểu hiển thị `outline` cho Secondary và `ghost` cho Tertiary. Dùng token nút trong `styles.css` cho nền, chữ, viền và trạng thái. Chuyển từng nhóm màn sang component chung và loại CSS trùng sau khi đối chiếu.
3. Mở rộng hợp đồng settings TypeScript và C# bằng trường tùy chọn, tái sử dụng `settings.save`. Kiểm tra khóa, màu và dữ liệu thiếu ở ranh giới lưu/đọc. Settings cũ chỉ có `AccentColor` phải tải được và giữ màu hiện có. Kiểm tra lưu, khởi động lại và đặt lại từng thuộc tính.
4. Thêm vùng chỉnh màu và xem trước trong Settings. Chốt mẫu sáng/tối với năm loại nút, rồi chuyển lần lượt các màn đã kiểm kê. Kiểm tra nhãn truy cập, Enter/Space, thứ tự Tab, tooltip và hành vi xác nhận sau khi thay component.
5. Ánh xạ cùng quy tắc sang brush và style WPF trong đợt riêng. Rà nút cửa sổ do Windows vẽ theo theme hệ điều hành. Đóng gói và nghiệm thu WebView/WPF ở phần được chuyển đổi.

## Nghiệm thu

- Có bảng kiểm kê từng nhóm màn và vai trò của mỗi nút. Nút chính dễ nhận biết, nút nguy hiểm có nhãn rõ và giữ nguyên luồng xác nhận.
- Mỗi loại nút đổi màu độc lập. Lưu và mở lại ứng dụng giữ đúng theme, Xóa tùy chỉnh trả về mặc định, cấu hình cũ vẫn tải được.
- Kiểm tra theme sáng, tối, tương phản cao và cửa sổ hẹp 375 px trên các màn WebView chính. Kiểm tra hover, pressed, focus, disabled và tương phản với nền thực tế.
- Chạy typecheck/build WebView, kiểm tra round-trip settings .NET và smoke các luồng sử dụng nút sau khi triển khai. Dùng dự án mẫu cho các ca có SQL hay thao tác xóa.
