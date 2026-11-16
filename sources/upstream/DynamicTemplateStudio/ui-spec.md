# Đặc tả UI WPF đề xuất

Đây là đặc tả UI mục tiêu. Đã có XAML và ảnh smoke WPF theo yêu cầu dựng thẳng; xem [trạng thái thực tế](implementation-status.md) để biết phần đã kiểm và phần manual QA chưa chạy. Tham chiếu skill ui-ux và brief UX. Nền tảng WPF được ưu tiên hơn các ví dụ HTML/Tailwind của skill.

## Áp dụng skill vào desktop

- Dùng ResourceDictionary, Style/ControlTemplate và DynamicResource; không thêm React/Tailwind/CSS vào runtime WPF.
- Thực hiện ý đồ token/spacing/state bằng WPF. Scrollbar được style bằng ControlTemplate; không chép `::-webkit-scrollbar` vào XAML.
- Probe web 375px không chứng minh UI WPF. QA thực bằng cửa sổ Windows ở kích thước DIP, DPI, bàn phím và screenshot. Mobile không phải nền tảng mục tiêu.
- Designer của skill mô tả UI; sản phẩm cuối vẫn phải có logic thật theo prompt, không để handler rỗng cho các nút cốt lõi.
- Prompt yêu cầu Dark Mode nên ghi đè mặc định không-dark của skill. Light là mặc định; có tùy chọn Dark, lưu lựa chọn. System có thể bổ sung nhưng không đổi default thành System ngầm định.

## Khung làm việc

Đề xuất kích thước chuẩn 1440×900 DIP; mức tối thiểu cần kiểm 1024×640 DIP. Sidebar 240 DIP, giới hạn kéo 200–320; inspector 320 DIP có thể thu gọn. Khi vùng trung tâm còn dưới 560 DIP, thu inspector trước, vẫn có nút mở; không ép tất cả cột grid vào một hàng không đọc được. Các số này là mục tiêu thiết kế, cần hiệu chỉnh bằng wireframe.

Top bar: tên ứng dụng/project, Save, Export. Sidebar: project, search template, template list, `+ New Template`. Workspace: tên template; các view Designer/Relationships/JSON Preview/Test Mapping/Settings; bên trong Designer có table tabs và `+ Add Table`, grid và `+ Add Field`. Status bar: save state, counts thật, validation summary. Không thêm KPI/dashboard marketing.

Properties Panel đổi theo selection và giữ label table/field rõ. Table properties gồm name/parent/from/mode/RowKey; field properties gồm type/options, một ô Property, Default và Max length. Business Key/Required ở grid. Bỏ mapping builder/Apply, raw path trùng, Constant/References/Description theo [yêu cầu cập nhật](change-request-02.md). Rename nằm ngay cạnh tên bảng trong workspace. Parent bật RowKey tự chọn quan hệ; nếu không, References dùng dropdown các key của parent.

Projects dùng table theo mặc định đề xuất: Name, Description, Location, Templates, Last Modified, Open và menu. Không tự tạo avatar/ảnh minh họa vì đây không phải dữ liệu có ảnh.

## Design tokens

Màu từ `ui-ux/references/tokens.css`, chuyển sang WPF theo cùng vai. Giá trị bên dưới là baseline, không phải brand đã duyệt.

| Resource dự kiến | Light | Dark |
|---|---|---|
| BackgroundBrush | #F4F4F6 | #05060F |
| SurfaceBrush | #FFFFFF | #0F111A |
| OverlayBrush | #FFFFFF | #171A26 |
| ForegroundBrush | #181818 | #E9EDF5 |
| PrimaryBrush | #181818 | #E9EDF5 |
| PrimaryForegroundBrush | #FFFFFF | #05060F |
| DangerTextBrush | #C70036 | #FF637E |
| ErrorTextBrush | #E7000B | #FF6467 |
| SuccessTextBrush | #007A55 | #00D492 |
| WarningTextBrush | #BB4D00 | #FF8904 |

Phần còn lại (border/muted/hover/selection/focus/state backgrounds) lấy đầy đủ cặp light/dark từ token nguồn khi triển khai, không rải hex trong Views. Không dùng màu đơn độc để diễn đạt key/error/save state. Surface phẳng, viền mảnh, không bóng ở grid/card thường; shadow chỉ dành lớp nổi.

Đề xuất font desktop Segoe UI, code Consolas, ưu tiên font có sẵn; đây là chuyển đổi cho Windows, khác font web mặc định của skill. Body 14 DIP, secondary 12, title 20 semibold. Nhịp 4/8/12/16/24/32 DIP, control 32–36 DIP, row grid khoảng 36 DIP. Control dưới 40 DIP bo 8, từ 40 DIP bo 12 theo skill; cell trong grid không biến mỗi ô thành card bo góc.

Chỗ đổi brand hiện có: `src/DynamicTemplateStudio.App/Styles/Tokens.Light.xaml` và `Tokens.Dark.xaml` cho primary/foreground và AppFont/CodeFont. Control templates tại `Styles/Controls.xaml`. Chưa tách Typography.xaml vì bộ font hiện nhỏ.

## Component và thao tác

- CR05: selection của table thuộc ViewModel, refresh/currency/cây quan hệ không tự chọn lại table đầu. Field inspector thêm Allowed values cho text types; CSV input và lỗi theo [CR05](change-request-05.md).

- CR04: Rename Field dùng icon bút và inline buffer/error như Rename Table; từ lưới mở Field inspector. Parent assignment tự thêm đủ FK fields của Business Keys, còn relationship validation chỉ ra thiếu cột/sai type/thiếu mapping. Xem [CR04](change-request-04.md).

- Rename cạnh tên bảng dùng icon bút và inline TextBox (Enter/blur commit, Esc cancel theo CR06, lỗi tại ô). Tất cả lối Rename Table dùng editor này. CheckBox grid dùng template tương tác trực tiếp, không dựa vào double-click/edit mode của DataGridCheckBoxColumn; xem [CR03](change-request-03.md).

- DataGrid custom header/cell/selection/editors, không giữ appearance Windows cổ điển. Bật row/column virtualization, tránh bọc ngoài bằng ScrollViewer làm mất virtualization.
- Field grid hiển thị Name, Property, Type, Key, Required. Property nhập tên thuần tự thêm `$.`, expression hiện trong JSON Preview.
- SQL type picker: char/varchar/nvarchar/smalldatetime/bit/numeric/int; numeric mặc định 19,5, chỉnh precision/scale. Inspector phần dưới chỉ Default/Max length. Type cũ được giữ để mở lại project.
- Hợp đồng dùng chung: Button, Input/Select, Tabs, DataGrid, Tree/Relation node, Dialog, Banner/Toast, Empty/Loading. Chỉ tạo một bộ style cho mỗi vai.
- Nút thường outline; một primary theo ngữ cảnh; xóa dùng nền đỏ nhạt/chữ đỏ, confirm nêu tên và tác động. Input không icon trái mặc định.
- Tooltip cho icon-only và tên bị cắt; tên trong confirm không cắt mất phần phân biệt. Toast không thay lỗi inline cần sửa.
- Scroll thumb 4 DIP theo tinh thần skill, giữ track không gây nhảy nội dung; vùng hit phù hợp chuột và có keyboard scroll.

## Keyboard đề xuất

| Phím | Hành vi |
|---|---|
| Ctrl+S / Ctrl+Shift+S | Save current / Save all theo prompt |
| Ctrl+Z / Ctrl+Y | Undo / Redo semantic command |
| F2 | Rename hoặc edit selection phù hợp |
| Enter / Esc | Commit cell / hủy edit hiện tại trước khi xử lý điều hướng |
| Tab / Shift+Tab | Đi qua editor theo thứ tự, không trap focus |
| Ctrl+F | Focus search trong vùng đang làm việc |

Ctrl+K chỉ có khi Command Palette được triển khai. Khi text editor có focus, shortcut undo/delete phải ưu tiên ngữ cảnh editor; không xóa table vì người dùng đang xóa chữ. Kéo reorder luôn có lối Move Up/Down trong menu cho keyboard. AutomationProperties.Name cho controls quan trọng; trạng thái focus có dấu hiệu nhận biết phù hợp native keyboard usage.

## QA giao diện cần làm sau

Test 1024×640, 1280×800, 1440×900, 1920×1080 DIP phù hợp work area; DPI 100/125/150/200%, chuyển monitor nếu có. Kiểm Light/Dark, empty/loading/error, tên dài, panel thu/mở, cuộn tới cột cuối, dropdown/dialog không ra ngoài vùng màn hình. Grid được cuộn trong vùng riêng; toàn app không có cuộn ngang ngoài dự kiến.

Theo 12 phép thử skill: không nhảy layout; phân biệt trạng thái; ưu tiên hành động rõ; màu có chữ; component nhất quán; lỗi có hướng sửa; không tự ghi đề xuất; không che nội dung; keyboard dùng được; phạm vi không phình; nội dung đặt đúng vai; ca chưa có mẫu được kiểm thực tế. Ảnh smoke hiện có; manual keyboard/DPI acceptance vẫn theo implementation-status.md.
# CR06 interaction update

Table/field rename commits on Enter **or focus leaving the input**; Escape cancels. Invalid names keep the original and show an inline error. Pencils align to the right edge of their respective header rows. Parent key edits immediately revalidate child relationships; see [CR06](change-request-06.md). This supersedes earlier blur-cancels instructions.


## CR07 — Field grid selection và clipboard
Quy ước chọn nhiều dòng, context menu, phím tắt và xử lý tên trùng dùng nguồn duy nhất tại [CR07](change-request-07.md).

