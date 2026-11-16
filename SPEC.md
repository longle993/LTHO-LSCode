# Mục tiêu LSCode

Cung cấp một IDE Windows cho người phát triển và bảo trì FAST ERP. Người dùng có thể chọn dự án, tìm menu và controller, sửa source, làm việc với cơ sở dữ liệu và dùng các công cụ hỗ trợ trong cùng workbench.

| Phạm vi | Tiêu chí nghiệm thu |
| --- | --- |
| Editor và workspace | Mở, tìm kiếm, định dạng, chia pane và lưu file. Kiểm tra thay đổi ngoài ứng dụng và giữ encoding khi lưu. |
| Dự án, menu và source ERP | Chọn hoặc đồng bộ profile, import/export qua tệp, giữ đúng ngữ cảnh dự án khi mở menu, controller và Source Collection. |
| SQL và danh mục | Target App/SYS gắn với từng tab. Truy vấn có thể hủy. Thao tác ghi trên menu và danh mục tuân theo luồng xác nhận và kiểm tra xung đột của từng chức năng. |
| Công cụ tích hợp | Dùng Advance Note, Template Studio, Profiler và SQL Watch trên WebView. Thao tác breakpoint/step được chuyển sang SSDT. |
| Phân phối | Build WebView và các project Template Studio từ checkout LSCode. Xác minh gói cài và smoke trên Windows x64. |

Xem [ma trận WebView](docs/specifications/WEBVIEW-MIGRATION.vi.md) và [đặc tả Table Designer](docs/specifications/CATALOG-TABLE-DESIGNER.vi.md) để tra tiêu chí chi tiết. [PLAN.md](PLAN.md) ghi phạm vi còn cần nghiệm thu theo các tài liệu hiện có.
