# Mục lục và quy ước tài liệu

| Tài liệu | Dùng khi |
|---|---|
| [Prompt gốc](source/original-prompt.md) | Tra nguyên văn 21 mục yêu cầu |
| [Yêu cầu](requirements.md) | Chọn phạm vi, tra ID và tiêu chí hoàn tất |
| [Kiến trúc](architecture.md) | Tạo solution, project, model, service, state |
| [Hợp đồng Engine](engine-contract.md) | Viết DTO, serializer, validator, mapping |
| [Persistence](persistence.md) | Save/autosave/export/import/recovery |
| [UI spec](ui-spec.md) | Wireframe, XAML, theme, control, keyboard |
| [Testing](testing.md) | Unit/integration/UI/performance/acceptance |
| [Kế hoạch](implementation-plan.md) | Chọn phase và thứ tự phụ thuộc |
| [Quyết định](decisions.md) | Kiểm tra giả định và câu hỏi chưa chốt |
| [Hướng dẫn triển khai](implementation-guide.md) | Bắt đầu một lượt implementation |
| [Trạng thái triển khai](implementation-status.md) | Xem phần đã chạy, kết quả tests và giới hạn còn lại |
| [Đợt thay đổi UX/SQL](change-request-02.md) | 7 yêu cầu mới, đặc tả cập nhật trước source và kết quả kiểm chứng |
| [Inline rename và tick](change-request-03.md) | Icon bút, sửa tên bảng bằng Enter/Esc và checkbox một click |
| [Field rename và parent keys](change-request-04.md) | Sửa field tại chỗ, tự sinh đủ khóa cha và validation quan hệ |
| [Selection và allowed values](change-request-05.md) | Không nhảy bảng khi thao tác; danh sách giá trị được chấp nhận cho chuỗi |

Nguồn có thứ tự ưu tiên: yêu cầu mới trực tiếp của người dùng → prompt nguồn → quyết định đã được xác nhận → đặc tả đề xuất. Nếu Engine thực tế mâu thuẫn yêu cầu, ghi chênh lệch để giải quyết, không âm thầm đổi output.

Từ khóa: **bắt buộc** là yêu cầu trong prompt; **đề xuất** là thiết kế chưa duyệt; **cần xác minh** là thiếu bằng chứng, không được tuyên bố tương thích. Các đặc tả giữ phạm vi mục tiêu; trạng thái thực tế và kết quả build/test ở implementation-status.md.
- [CR06 — parent-key validation và rename khi rời ô](change-request-06.md)
