# Explorer gọn, tab thấp và DB lazy load

Cập nhật tiếp theo của workbench WebView, thay thế mô tả explorer và cách tải DB trong bản 10/11/2026 trước đó.

## Explorer

- Controller dùng icon Workflow riêng; tên nhóm bỏ đuôi .aspx, vẫn giữ đường dẫn đầy đủ trong tooltip. Gập/mở và đóng riêng từng controller.
- Ẩn các tầng vật lý App_Data/AppData và Controllers. Hiện trực tiếp Main, Dir, Filter, Grid, Report, Templates dưới controller. Giữ các thư mục con thực sự như Upload, View bên trong Templates.
- File ASPX nằm trong nhóm Main, kể cả đường dẫn gốc không có thư mục Main. Chỉ đổi cách hiển thị; mở/lưu vẫn dùng đường dẫn vật lý ban đầu.
- Mỗi nhóm có màu riêng: Main tím, Dir xanh dương, Filter xanh lá, Grid xanh cyan, Report hồng, Templates vàng. Thư mục con kế thừa màu nhóm.
- File .f xanh mint, XML đỏ san hô, khác màu các nhóm folder. Icon folder thường màu vàng và controller màu tím. Có palette riêng cho Light/Dark/High Contrast.
- Gạch chân nhóm theo màu tương ứng, sáng hơn khi file trong nhóm đang được chọn. Tab file đang chọn cũng có gạch chân theo màu loại file.

## Chiều cao và phần đáy

- Tab file giảm từ 42px xuống 32px; bộ chọn file của pane chia đôi giảm xuống 30px.
- Workbench dùng hàng grid minmax(0,1fr), thanh điều hướng cuộn riêng; editor và bảng kết quả co trong phần không gian còn lại.
- Status bar và thanh điều khiển không bị co mất; điều chỉnh chiều cao bảng kết quả khi cửa sổ thấp.
- Cửa sổ custom dùng work area của màn hình hiện hành khi maximize, tính cả vị trí taskbar. Min size quy đổi theo DPI.

## DB theo kết nối hiện hành

- Không tải schema khi chỉ mở dự án, sửa code hoặc chưa mở mục DB.
- Mở mục DB mới tải tối đa 200 object của database tab hiện hành. Khi không có tab, dùng kết nối mặc định của dự án.
- Đóng/mở lại mục DB trên cùng kết nối giữ danh sách đã tải. Nút Tải lại hoặc tìm kiếm tải lại theo bộ lọc.
- Chuyển App/SYS hoặc chuyển tab khác database: bỏ dữ liệu cũ; nếu DB đang hiện thì tải database mới, nếu DB đang ẩn thì chờ lần mở tiếp theo.
- Response cũ và lỗi của request cũ không được ghi lên database mới. Phân trang object giữ nguyên.

## Xác minh

Kiểm thử UI có fixture Main và đủ sáu nhóm, kiểm tra màu folder/file không trùng, ẩn tầng hạ tầng, giữ nhiều controller, tải DB khi mở, cache khi mở lại, đổi database và kiểm tra đáy tại 1440x900, 1280x600, 960x480, 375x600.

Smoke native bổ sung kiểm tra maximize nằm trong work area và footer/workbench không tràn viewport ở cả trạng thái maximize/restore. Bản chạy: artifacts/publish-webview/LSCode.WebView.exe.

Kết quả cuối: 51 kiểm tra UI đã qua, không có lỗi JavaScript chưa xử lý. Probe không phát hiện lỗi đo được tại 375/1280/1440/1920px. Smoke native đã qua, bao gồm hai bước kiểm tra thêm về work area và footer khi maximize/restore. Publish đã đối chiếu SHA-256 index.html với frontend dist và khớp nhau.
