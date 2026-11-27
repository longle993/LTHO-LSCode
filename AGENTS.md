# Hướng dẫn làm việc với LSCode

## Người dùng và dự án

Người dùng yêu cầu tổ chức tài liệu dự án bằng tiếng Việt, lưu kiến thức lâu dài theo chủ đề và cập nhật bản đồ khi tài liệu thay đổi vị trí. Tên, vai trò và cách xưng hô ưu tiên chưa được xác nhận.

LSCode là IDE Windows phục vụ phát triển và bảo trì FAST ERP. [SPEC.md](SPEC.md) mô tả mục tiêu và phạm vi.

## Quy tắc

1. Đầu mỗi cuộc hội thoại, đọc `AGENTS.md` và `MAP.md` trước khi trả lời.
2. Ghi kiến thức lâu dài theo chủ đề trong `contexte/`. Mỗi chủ đề có một tệp.
3. Cập nhật `MAP.md` khi tạo hoặc di chuyển tệp. Với tài liệu chi tiết, cập nhật thêm chỉ mục được liên kết từ bản đồ và sửa liên kết bị ảnh hưởng.
4. Ghi mục tiêu và tiêu chí nghiệm thu trong `SPEC.md`. Theo dõi bước thực hiện trong `PLAN.md`. Dùng `sources/` cho tư liệu nguồn và `livrables/` cho sản phẩm bàn giao. Dùng `scripts/` cho tự động hóa dự án.
5. Khi cập nhật hướng dẫn, đối chiếu luồng hiện tại với mã nguồn và ghi rõ phạm vi kiểm chứng. Khi trích kết quả cũ, chỉ rõ nguồn và thời điểm. Giữ ghi chú các phiên bản cũ làm lịch sử.
6. Dùng fixture hoặc dữ liệu mẫu trong tài liệu mới. Giữ thông tin xác thực và dữ liệu khách hàng ngoài ví dụ và sản phẩm bàn giao.
7. Sau mỗi lần sửa ứng dụng, build giao diện và publish bản Windows vào `artifacts/publish-webview`. Kiểm tra file `LSCode.WebView.exe` rồi gửi đường dẫn cho người dùng mở trực tiếp.
