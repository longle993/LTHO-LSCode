# Bản đồ LSCode

| Đường dẫn | Nội dung và nơi lưu |
| --- | --- |
| [README.md](README.md) | Giới thiệu dự án, cách chạy và các điểm vào tài liệu. |
| [AGENTS.md](AGENTS.md) | Thông tin người dùng và quy tắc làm việc. Đọc cùng bản đồ trước khi trả lời ở đầu hội thoại. |
| [SPEC.md](SPEC.md), [PLAN.md](PLAN.md) | Mục tiêu, tiêu chí nghiệm thu và các bước thực hiện. |
| [contexte/](contexte/) | Kiến thức lâu dài theo chủ đề. Xem [kiến trúc](contexte/kien-truc.md), [dữ liệu dự án ERP](contexte/du-lieu-du-an-erp.md) và [quy ước tài liệu](contexte/to-chuc-tai-lieu.md). |
| [docs/](docs/README.md) | Chỉ mục từng tài liệu trong guides, architecture, specifications và history. |
| [sources/](sources/README.md) | Đầu vào, khảo sát Fcode và tài liệu gốc Dynamic Template Studio. |
| [livrables/](livrables/README.md) | Sản phẩm bàn giao được duy trì cùng repo. |
| [src/](src/) | Mã Web, host Desktop, Contracts, Domain, Application, Infrastructure, Core, Shell, TraceHost và Dynamic Template Studio. Tra điểm vào theo [bản đồ kiến trúc](contexte/kien-truc.md). |
| [tests/](tests/) | Hai console runner .NET cho Core và WebView, cùng phạm vi tích hợp SQL được mô tả ở hướng dẫn WebView. |
| [scripts/](scripts/) | Preview WebView, kiểm tra tương tác và đóng gói Inno Setup. Các entry point build và dọn ở gốc repo. |
| [samples/](samples/) · [assets/](assets/) | Dữ liệu demo và tài nguyên phân phối. |
| `artifacts/` | Đầu ra build, publish, bộ cài, cache và báo cáo kiểm thử. Thư mục sinh tự động, được Git bỏ qua. |
| [.codex/](.codex/) | Hai tệp JSON khởi tạo cho thiết lập chung và thiết lập riêng. Các thư mục `rules/`, `skills/`, `workflows/` và `output-styles/` có `.gitkeep` để lưu cấu trúc trong Git. |

Triển khai mới: [tối ưu menu và tích hợp Mobile](docs/specifications/ERP-MENU-PERFORMANCE-MOBILE.vi.md); kiến thức và phạm vi kiểm chứng lưu cùng [kiến trúc](contexte/kien-truc.md#tải-menu-erp-và-mobile).
