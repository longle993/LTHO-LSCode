# CR07 — Chọn nhiều dòng và copy/paste field

Nguồn: yêu cầu người dùng. Cập nhật đặc tả trước khi sửa source; chưa ghi nhận kiểm thử tại thời điểm lập tài liệu.

## Phạm vi và hành vi (R07, R05, R16)

- “Dòng” là field trong bảng của Template Designer. Mở rộng grid hiện hữu, không thay bố cục.
- Chọn nhiều dòng bằng Ctrl+click, Shift+click/Shift+phím; kéo trên đầu dòng để chọn vùng. Alt+kéo tiếp tục dùng để đổi thứ tự một field.
- Copy Fields / Paste Fields trong context menu và Ctrl+C / Ctrl+V khi focus ở grid. Khi đang sửa văn bản, phím tắt giữ thao tác văn bản thông thường.
- Copy tạo snapshot độc lập của các field đang chọn theo thứ tự bảng, bao gồm type, mapping, key/required, default/constant, length, allowed values, description, references và thuộc tính mở rộng.
- Clipboard dùng định dạng riêng của Studio; không diễn giải văn bản/JSON bất kỳ thành field. Hỗ trợ chuyển bảng/template trong cùng hoặc khác project đang mở lần lượt, cũng như giữa các cửa sổ Studio.
- Paste thêm vào cuối bảng đích, giữ nguồn nguyên trạng; tên trùng không phân biệt hoa thường được thêm `_copy`, `_copy2`, … Không ghi đè field có sẵn.
- Giữ nguyên mapping/reference; không đoán hay sửa tên bảng tham chiếu. Validation hiện hữu báo lỗi tại đích nếu tham chiếu không hợp lệ. Không sao chép quan hệ cấp bảng hay thêm metadata vào runtime JSON.
- Paste là một bước undo/redo, kích hoạt dirty/autosave/preview theo luồng hiện có. Khi có bộ lọc, xóa lọc sau paste để nhìn thấy các field vừa thêm.
- Không có bảng đích hoặc clipboard không hợp lệ: không đổi model, báo trạng thái. Clipboard bận: báo lỗi có thể thử lại.
- Giữ Light/Dark, màu/font/scrollbar và ngôn ngữ tiếng Anh của control hiện có.

## Kiểm chứng cần thực hiện

1. Chọn liên tiếp/rời nhau, copy theo thứ tự bảng; đổi template và paste; nguồn độc lập với đích.
2. Dán nhiều lần, tên trùng khác hoa thường, đầy đủ cấu hình và Extra.
3. Clipboard không liên quan/hỏng; không có bảng; ô text đang edit.
4. Undo/redo cả lần dán; dirty, JSON preview, save/reopen giữ các field.
5. Build/test và WPF smoke thật; ghi kết quả cùng giới hạn sau khi chạy.

## Kết quả triển khai và giới hạn

- Đã triển khai trên WPF grid hiện hữu; snapshot clipboard riêng, dán cuối bảng, tên duy nhất, một bước undo/redo và chọn các dòng mới.
- Build Release và publish `artifacts/release-cr07` thành công; 164 tests pass (10 test cases mới về snapshot độc lập, giữ cấu hình/Extra, tên trùng và clipboard hỏng).
- WPF smoke `artifacts/change-07-verified` pass: clipboard Windows, copy ngược thứ tự selection vẫn dán theo thứ tự nguồn, chuyển template, selection nhiều dòng, undo/redo, save/reopen JSON tương đương; regression Light/Dark và 1024 DIP pass, binding log rỗng. Đã xem ảnh clipboard Light/Dark. Dữ liệu trong ảnh là demo.
- Chưa nghiệm thu thủ công kéo chuột/Shift/Ctrl, tương tác clipboard giữa hai tiến trình và nhiều mức DPI. Chức năng chọn vùng dùng hành vi native Extended/FullRow của WPF DataGrid; Alt+kéo vẫn reorder như trước.
- Dùng token màu/font và scrollbar sẵn tại `Styles/Tokens.Light.xaml`, `Tokens.Dark.xaml`, `Controls.xaml`; không thêm dependency hoặc thay runtime contract.
- Thư mục `artifacts/release` đang bị phiên ứng dụng hiện hữu khóa file. Dùng bản mới `artifacts/release-cr07/DynamicTemplateStudio.App.exe`; không đóng phiên người dùng để ghi đè.

Kiểm tra UI cuối tại artifacts/change-07-final-ui: smoke và clipboard checks pass, bindings.log 0 byte; đã xem ảnh Dark sau khi sửa nút chọn tất cả theo theme.

