# Include, menu và Compare Text

Bản chạy: `artifacts/publish-include-preview/LSCode.exe`.

- F12 tại entity hoặc `&name;`: xem nhanh nội dung include trong cửa sổ nhỏ, chỉ đọc. Esc đóng cửa sổ.
- Ctrl+F12: mở file include thành tab để chỉnh sửa. Có thể dùng ngay trong cửa sổ xem nhanh.
- Menu có `bar = "-"` hiển thị đường phân cách, không tham gia tìm kiếm menu.
- Folder dùng màu vàng trầm; tiêu đề file dự án, đường dẫn và nhóm cửa sổ được thu gọn.
- Tools → Compare Text: dán/mở hai văn bản, nhấn So sánh. Unified hiển thị dòng thêm/xóa; Split ghép dòng sửa cạnh nhau và đánh dấu đoạn ký tự thay đổi. Có thu gọn dòng không đổi, chỉ khác biệt, chuyển trước/sau và hiện lại ô nhập. So sánh với file và Compare Folder dùng cùng giao diện này.
- Google Dịch chỉ gửi khi nhấn Dịch; lưu tối đa 100 kết quả trong phiên. Lỗi 429 dừng gửi theo Retry-After (mặc định 5 phút), không tự thử lại. Nút Mở Google Dịch chuyển nguyên nội dung/ngôn ngữ sang trình duyệt. Không cắt bỏ văn bản quá dài khi dịch trong ứng dụng.

Đã kiểm thử bằng HTTP giả lập cho cache/429/Retry-After và smoke WPF cho include/menu/diff. Kiểm tra dịch trực tiếp chưa thành công do lỗi kết nối ở môi trường build; ứng dụng không thể gỡ giới hạn do Google áp dụng trên mạng người dùng.
