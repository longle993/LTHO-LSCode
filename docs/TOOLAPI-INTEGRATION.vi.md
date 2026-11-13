# Đề xuất tích hợp Dynamic Template Studio vào LSCode

**Thiết kế cập nhật ngày 13/11/2026.** Studio chuyển sang một tab React trong WebView2 của LSCode. Phương án nhúng WPF bên dưới là phương án đã khảo sát trước khi đổi yêu cầu.

## Nâng cấp WebView2

- Bố cục phẳng theo theme LSCode. Thanh đầu chứa mở project, chọn template và lưu. Cột trái chọn bảng. Vùng giữa sửa field và thuộc tính bảng. JSON Preview và Test Mapping mở theo nhu cầu để dành diện tích cho bảng.
- Giữ các dịch vụ .NET của Studio cho đọc/ghi project, đổi tên có cập nhật tham chiếu, kiểm tra, JSON runtime và mapping. Bridge truyền model chỉnh sửa, còn việc chọn thư mục và ghi đĩa do native host xử lý. Giữ định dạng project, draft và các giới hạn của Engine hiện tại.
- Giai đoạn đầu kiểm tra luồng mở project có sẵn, chỉnh bảng/field, lưu, xem JSON, thử mapping, chuyển tab và đóng ứng dụng có thay đổi chưa lưu. Các thao tác WPF còn lại cần đối chiếu riêng trước khi thay thế bản Studio độc lập.

## Hiện trạng

- LSCode dùng React 18, Tailwind v4 và Monaco trong WPF WebView2. Thanh công cụ đã có lối mở công cụ và các tab làm việc. Giao diện dùng chung component và token cho Light, Dark và High Contrast.
- Dự án `D:/FAST/Code/LTHO/New/lthoToolApi` là **Dynamic Template Studio**, một ứng dụng WPF trên .NET 10. `DynamicTemplateStudio.App` sở hữu giao diện. `Core` và `Infrastructure` sở hữu model, validation, mapping preview, serialization và lưu project.
- Studio đã có luồng thiết kế bảng, field, quan hệ cha–con, JSON Preview, Test Mapping, lưu và xuất project. Project Studio có `project.json`, thư mục `templates/`, `samples/` và metadata/draft dưới `.studio/`. Thư mục project ERP đang mở trong LSCode và thư mục project Studio là hai ngữ cảnh riêng.
- Điểm khởi động hiện có của Studio hỗ trợ màn Projects và các tham số demo/kiểm thử. Qua kiểm tra mã nguồn, chưa có tham số công khai để mở thẳng một thư mục project.

## Việc chính của người dùng

Từ LSCode, mở trình thiết kế, chọn project template, sửa bảng và mapping, xem JSON hoặc thử dữ liệu mẫu, rồi lưu hoặc xuất bằng các thao tác sẵn có của Studio.

## Sử dụng bản tích hợp

### Bố cục WebView

- Bộ chọn Template, Bảng cha, Chế độ nguồn và Kiểu SQL dùng dropdown chung của LSCode. Ba tab **Thiết kế**, **JSON Preview**, **Test Mapping** có chỉ báo đang chọn, hỗ trợ mũi tên trái/phải và Home/End.
- Trong **Thiết kế**, chọn bảng ở cột trái. Thuộc tính template có thể thu gọn. Lưới field cuộn riêng, ghim tiêu đề và cuộn ngang trong lưới ở cửa sổ hẹp. Chọn tên field để mở khung chỉnh kiểu SQL, Default, Max length và giá trị chấp nhận. Property nhập tên thường sẽ lưu với tiền tố `$.`; biểu thức bắt đầu bằng `$` hoặc `@` giữ cú pháp.
- Dùng biểu tượng bút để đổi tên tại chỗ, **Enter** hoặc **Áp dụng** để lưu, **Esc** hoặc **Hủy** để bỏ. Đổi tên dùng GraphEditor của Studio để cập nhật tham chiếu.
- Kéo tay nắm ở đầu dòng để sắp xếp field. Khi dùng bàn phím, focus tay nắm rồi nhấn **Alt+↑/Alt+↓**. Thứ tự mới tham gia hoàn tác/làm lại và được giữ khi lưu/mở lại.
- Khi bảng có parent, khối **Khóa cha–con** hiển thị chiến lược RowKey hoặc Business key. Quan hệ business key cho chỉnh cột con, chọn khóa cha và thêm/xóa từng cặp khóa ghép. Khi đổi `Key` hoặc `RowKey` ở master, WebView gọi `RelationshipEditor.Synchronize`: tự bổ sung khóa còn thiếu và cập nhật cặp quan hệ, nhưng giữ các cột cũ để tránh mất cấu hình.
- **Test Mapping** hiển thị từng bảng kết quả và cho xem trước cấu trúc suy ra từ JSON mẫu trước khi áp dụng. **JSON Preview** có làm mới và sao chép JSON đã qua validation.
- **Lưu template** chỉ ghi template đang chọn vào project đang mở; thay đổi của template khác vẫn được giữ trong bộ nhớ và tiếp tục hiện dấu chưa lưu. **Xuất template** kiểm tra runtime rồi mở hộp chọn tên file JSON. **Lưu project/Xuất project** vẫn xử lý toàn bộ project.
- Thông báo thành công và lỗi hiển thị thành toast cố định ở góc dưới, không chiếm dòng trong document nên không đẩy lưới hoặc tab xuống khi xuất hiện.
- Đối chiếu với `requirements.md`, `ui-spec.md` và CR02–CR07 của Studio. WebView hiện đã có kéo sắp xếp field, chỉnh cặp khóa cha–con, đồng bộ khóa master–detail và lưu/xuất riêng template. Phần quản lý project gần đây và chọn vùng field kiểu WPF vẫn là khác biệt còn lại. Ràng buộc runtime Engine vẫn theo `engine-contract.md`.

Mở **Thiết kế template API** trên thanh công cụ hoặc tìm lệnh bằng Ctrl+Shift+P. Trong tab **Dynamic Template Studio**, chọn thư mục có `project.json`. Chọn template và bảng, sửa field, rồi dùng **JSON Preview**, **Test Mapping** hoặc **Lưu project**.

Build LSCode cần checkout Studio ở `../New/lthoToolApi` tính từ thư mục gốc LSCode, hoặc truyền `-p:TemplateStudioRoot=<đường dẫn tuyệt đối>` nhất quán khi restore, build và publish. Bản publish mang theo hai DLL Studio và giao diện đã build, nên chạy độc lập với checkout Studio.

Tab WebView hỗ trợ tạo project trong thư mục trống, thêm template, chọn bảng cha, thêm/đổi tên/kéo sắp xếp field, chỉnh cặp khóa cha–con, sao chép nhiều field bằng cột **Chọn**, dán field qua clipboard, hoàn tác/làm lại, tự lưu sau 800 ms và xuất sang thư mục trống. Tắt **Tự lưu** nếu muốn thử thay đổi rồi chọn **Lưu template** hoặc **Lưu project** thủ công. **Khôi phục lượt lưu** hoàn nguyên giao dịch bị gián đoạn. Khi mở project có draft, Studio cho chọn mở bản nháp.

Kiểm thử bridge với project mẫu bằng cách đặt `LSCODE_STUDIO_SAMPLE` trỏ đến `samples/SalesManagement` của Studio rồi chạy `dotnet run --project tests/LSCode.WebView.Tests -c Release`. Kiểm thử sao chép fixture vào thư mục tạm, xem JSON, thử mapping, đổi khóa master và kiểm tra khóa con/cặp composite, lưu riêng một template, xuất riêng template, mở lại và tạo project mới.

## Phương án đã khảo sát · Plugin WPF trong tab LSCode

```text
Một cửa sổ LSCode
┌───────────────────────────────────────────────────────────┐
│ LSCode        [Quay lại editor] [Dynamic Template Studio ×] │
├───────────────────────────────────────────────────────────┤
│ Projects / SalesManagement                    Save | Export │
│ Templates    │ Header | Detail          │ JSON Preview    │
│ SalesOrder   │ Bảng và field            │ Test Mapping    │
└───────────────────────────────────────────────────────────┘
```

- Plugin đi kèm LSCode và dùng lại giao diện WPF của Studio. Tách nội dung của `MainWindow.xaml` thành `UserControl` trong thư viện WPF. Cửa sổ Studio độc lập và LSCode cùng host control này, dùng lại ViewModel, AvalonEdit, validation, mapping preview và persistence.
- Bấm **Thiết kế template API** từ LSCode để mở trang plugin. WPF host chuyển vùng nội dung giữa WebView2 và Studio, giữ cả hai instance khi đổi qua lại. WebView2 hiện là control có HWND riêng, nên khi vào Studio cần ẩn bề mặt WebView2 và hiển thị thanh điều hướng native để quay lại editor hoặc đóng tab. Cách này giữ trạng thái editor và tránh vấn đề lớp hiển thị khi chồng WPF lên WebView2.
- Tách các phụ thuộc vào `Application.Current.MainWindow`, tài nguyên theme toàn ứng dụng và lời gọi shutdown ra khỏi control. Dialog thuộc cửa sổ LSCode khi nhúng. Theme Studio áp trong phạm vi plugin, bao gồm tài nguyên dùng cho dialog. Khi đóng tab hoặc đóng LSCode, hỏi lưu/bỏ/hủy và chờ lượt ghi đang thực hiện theo hành vi hiện có của Studio. Đổi tab giữ draft và focus trở lại vùng tương ứng.
- Đóng gói plugin theo phiên bản cùng LSCode, với một điểm tạo phiên và đóng phiên do WPF host gọi. Cấu hình build chỉ rõ revision Studio. Studio mở màn Projects lần đầu, cho người dùng chọn thư mục template và lưu ở định dạng hiện tại. Liên kết thư mục template với hồ sơ ERP có thể thiết kế riêng khi cần.

**Thứ tự triển khai và nghiệm thu**

1. Tách control và dịch vụ chủ cửa sổ trong repo Studio. Kiểm tra Studio độc lập vẫn mở project, sửa, lưu, preview và xuất được như trước.
2. Tạo nguyên mẫu trong WPF host LSCode để xác minh đổi qua lại giữa WebView2 và control, focus phím tắt, dialog, resize và DPI. Phác thảo dùng tên project mẫu, giữ giao diện hiện có của Studio.
3. Nối luồng mở/đóng tab và đóng LSCode. Kiểm thử Save/Discard/Cancel, autosave đang chạy, giữ trạng thái qua đổi tab và giữ theme của từng vùng.
4. Publish và kiểm thử bộ cài Windows x64. Trên bản đóng gói, mở Studio từ LSCode, sửa một bản sao project mẫu, chạy Test Mapping, lưu, quay lại editor rồi mở lại. Xác nhận Studio độc lập vẫn chạy.

## A. Công cụ đi kèm, mở cửa sổ Studio từ LSCode

```text
LSCode
┌──────────────────────────────────────────────────────────┐
│ Không gian làm việc    [SQL Profiler] [Thiết kế template API] │
│ File dự án             Editor và các tab đang làm việc    │
└──────────────────────────────────────────────────────────┘
                            │ mở
                            ▼
Dynamic Template Studio
┌──────────────────────────────────────────────────────────┐
│ Projects        [Open Project Folder] [Open sample project] │
│ Template → bảng / field → JSON Preview hoặc Test Mapping │
└──────────────────────────────────────────────────────────┘
```

- Thêm lệnh **Thiết kế template API** vào nhóm công cụ của LSCode và cho phép tìm từ bảng lệnh. Khi bấm, mở cửa sổ Studio ở màn Projects, dù LSCode đã mở project ERP hay chưa. Nhãn tiếng Việt và trạng thái lỗi dùng các thành phần giao diện hiện có của LSCode. Cửa sổ Studio dùng theme và nhãn hiện tại của Studio.
- Đóng gói bản publish Studio x64 trong thư mục riêng dưới thư mục cài LSCode, dự kiến `Tools/DynamicTemplateStudio/`. Build lấy từ một revision được ghi rõ của Studio, qua đầu vào build có thể cấu hình. Máy đã cài dùng đường dẫn tương đối với thư mục ứng dụng, độc lập với đường dẫn checkout trên máy phát triển.
- Thêm thao tác WebView bridge chuyên biệt. Native host xác định EXE trong thư mục đi kèm và khởi động tiến trình WPF. Giao diện hiển thị lỗi nếu thiếu công cụ hoặc khởi động thất bại. Thử kích hoạt lại cửa sổ Studio còn sống trong cùng phiên LSCode để hạn chế mở trùng. Studio quản lý xác nhận đóng, autosave và dữ liệu của chính nó.
- Trong đợt đầu, người dùng chọn thư mục template ở Studio. Mở thẳng từ hồ sơ ERP là bước có thể bổ sung sau khi chốt cách gắn thư mục Studio với hồ sơ và thêm hợp đồng dòng lệnh cho Studio.

## B. Trình thiết kế nằm trong tab LSCode

```text
LSCode · [Thiết kế template API]
┌────────────────────────────────────────────────────────────┐
│ [Mở project]  SalesManagement                 [Lưu]       │
├──────────────┬───────────────────────┬─────────────────────┤
│ Templates    │ Header | Detail       │ JSON Preview        │
│ SalesOrder   │ Property / Key / Type │ Test Mapping        │
└──────────────┴───────────────────────┴─────────────────────┘
```

Phương án này đưa quy trình vào cùng cửa sổ, dùng theme và phím tắt LSCode. LSCode cần xây giao diện React cho designer và nối native bridge với các dịch vụ .NET của Studio. Trước khi chọn cần kiểm kê tương đương cho chỉnh field, clipboard, quan hệ, undo/redo, trạng thái chưa lưu, autosave và recovery. Project Studio WPF vẫn là ứng dụng độc lập. Tên trong phác thảo lấy theo project mẫu.

## Kế hoạch triển khai nếu chọn A

1. Chốt tên và vị trí lệnh, revision Studio và cách cung cấp bản publish cho build LSCode. Xác minh đóng gói runtime x64 cùng các dependency WPF của Studio và chạy được từ thư mục cài có dấu cách.
2. Nối lệnh React với native host, xử lý thiếu EXE, lỗi khởi động và bấm liên tiếp. Kiểm tra mở được từ bảng lệnh và từ nhóm công cụ, gồm cả khi LSCode chưa chọn dự án ERP.
3. Chạy trên bản LSCode publish và bản cài. Mở một bản sao project mẫu, sửa một field, chạy Test Mapping, lưu, đóng rồi mở lại. Kiểm tra cả luồng hủy đóng Studio khi có thay đổi và việc đóng cửa sổ LSCode trong khi Studio còn làm việc.
4. Ghi phiên bản hai thành phần và hướng dẫn thao tác vào tài liệu phát hành. Giữ định dạng project và quy tắc export của Studio. Mapping Preview là mô phỏng cục bộ, còn các điểm chưa xác nhận trong hợp đồng Engine vẫn theo đặc tả Studio.

Thiết kế WebView2 ở đầu tài liệu là hướng hiện tại. Các phương án đã khảo sát được giữ để tham chiếu.
