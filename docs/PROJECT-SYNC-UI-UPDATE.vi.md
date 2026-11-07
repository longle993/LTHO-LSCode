# Cập nhật khung đồng bộ và cây menu

Phạm vi triển khai: sửa các component hiện có, không đổi bố cục toàn màn.
- Hộp chọn/kết nối và hộp đồng bộ dùng thanh tiêu đề riêng, kéo được, nút đóng riêng; không có thanh cửa sổ mặc định.
- Đồng bộ: một hàng nhãn Tên dự án + textbox một dòng; tiêu đề không chứa ví dụ.
- Tab DB bỏ hướng dẫn “Mở nhóm để tải App DB • 200 đối tượng mỗi lượt”; giữ tải từng nhóm.
- Cây dùng đường dọc liền, chỉ hiện khi hover nhánh hoặc nhánh có lựa chọn.
- XML tăng độ rõ màu trên nền sáng và tối, giữ parser/highlighting hiện có.
- Menu đọc SYS của dự án, áp quyền user 2, denyunitright và Startup trong App DB của dự án theo batch được yêu cầu. Thiếu cột tùy phiên bản dùng giá trị mặc định; lỗi quyền/kết nối không bị che.
- Không hardcode tên DB mẫu; tên DB được quote, dùng App đã cấu hình/lưu hoặc giải quyết placeholder theo cấu hình dự án.

Kiểm chứng dự kiến: build Release, bộ kiểm tra hiện có và render WPF. Tích hợp DB thật chỉ có kết luận khi thực sự chạy được trên kết nối dự án.

## Validation
- Release solution build: PASS, 0 warnings, 0 errors.
- Existing Core checks: PASS.
- WPF compact UI smoke: PASS, XML/SQL/JS boundaries and Dark/Light/High Contrast rendering. Dark screenshot visually inspected.
- Live menu SQL has not been executed against a project database in this session. Startup is executed as explicitly requested; its effects depend on the project procedure.
