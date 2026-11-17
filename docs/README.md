# Tài liệu LSCode

Bắt đầu từ [README](../README.md), [bản đồ](../MAP.md) hoặc [tư liệu nguồn](../sources/README.md).

## Hướng dẫn sử dụng

Điểm vào cài đặt, vận hành WebView và các tính năng ERP.

- [Advance Note trong LSCode](guides/ADVANCE-NOTE.vi.md)
- [Cài đặt LSCode WebView trên Windows x64](guides/CAI-DAT-LSCODE.vi.md)
- [F3 — Thông tin và chỉnh sửa menu ERP](guides/ERP-MENU-EDITOR.vi.md)
- [Source Collection trong cây file ERP](guides/SOURCE-COLLECTION.vi.md)
- [LSCode React + WebView2 — hướng dẫn và trạng thái chuyển đổi](guides/WEBVIEW-RUNNING.vi.md)

## Kiến trúc

Ranh giới module, bridge và luồng lệnh.

- [Clean Architecture và MediatR trong LSCode](architecture/CLEAN-ARCHITECTURE-MEDIATR.vi.md)

## Đặc tả tính năng

Yêu cầu và tiêu chí nghiệm thu chi tiết.

- [Tạo danh mục — thiết kế bảng và nhập dữ liệu](specifications/CATALOG-TABLE-DESIGNER.vi.md)
- [LSCode — đặc tả chuyển đổi toàn bộ tính năng sang WebView](specifications/WEBVIEW-MIGRATION.vi.md)

## Lịch sử và ghi chú triển khai

Các quyết định, quy trình và bằng chứng của từng đợt. Đường dẫn publish và kết quả kiểm thử ghi theo thời điểm của từng tài liệu.

- [Thanh icon, controller và cấu trúc dữ liệu — 0.8](history/ACTIVITY-BAR-AND-CONTROLLERS.vi.md)
- [App DB, gợi ý SQL và bố cục — 0.7](history/APP-DB-OBJECTS-AND-LAYOUT.vi.md)
- [Đóng cửa sổ, màn hình nhỏ, tìm kiếm và mở file — 0.12](history/CLOSE-SCREEN-SEARCH-FIXES.vi.md)
- [Compare Text, Compare Folder và Beautify SQL — 0.20](history/COMPARE-AND-BEAUTIFY.vi.md)
- [App/SYS từ web.config — 0.19](history/CONFIG-DATABASES.vi.md)
- [DB Objects và Profiler — bản 0.14](history/DBOBJECTS-PROFILER.vi.md)
- [LS Code — khoảng trống khảo sát và tiêu chí xác minh](history/DISCOVERY-GAPS.vi.md)
- [Đổi theme và thứ tự lối tắt — 0.9](history/FAST-THEME-AND-ICON-ORDER.vi.md)
- [Tô màu SQL và JavaScript nhúng trong XML](history/HIGHLIGHTING.vi.md)
- [LTHO Studio Code (LS Code) — kế hoạch IDE desktop C# cho ERP](history/IDE-BLUEPRINT.vi.md)
- [LS Code 0.1 — trạng thái triển khai](history/IMPLEMENTATION-STATUS.vi.md)
- [Include, menu và Compare Text](history/INCLUDE-PREVIEW-DIFF.vi.md)
- [Menu, DB Object và gợi ý SQL — 0.10](history/LAZY-DB-AND-SQL-FIXES.vi.md)
- [Loading và vùng phóng to — 0.13](history/LOADING-AND-MAXIMIZE.vi.md)
- [Thông báo, thu gọn panel và gợi ý SQL — 0.6](history/NOTIFICATIONS-AND-SQL-SUGGESTIONS.vi.md)
- [SQL Profiler mặc định — 0.21](history/PROFILER-DEFAULTS.vi.md)
- [Profiler bắt được sự kiện thực tế — 0.24](history/PROFILER-LIVE-CAPTURE.vi.md)
- [Đường dẫn Profiler lưu một lần — 0.22](history/PROFILER-PATH.vi.md)
- [Thiết kế nâng cấp danh mục dự án](history/PROJECT-CATALOG-REDESIGN.vi.md)
- [Cập nhật khung đồng bộ và cây menu](history/PROJECT-SYNC-UI-UPDATE.vi.md)
- [Đồng bộ dự án và xem dữ liệu](history/PROJECT-SYNC.vi.md)
- [README trước đợt sắp xếp, gồm các bản WPF](history/README-WPF.vi.md)
- [LSCode tự chứa danh mục chuẩn và Dynamic Template Studio](history/SELF-CONTAINED-PROJECT-CATALOG-AND-TEMPLATE-STUDIO.vi.md)
- [Phím tắt và màu giao diện — bản 0.5](history/SHORTCUTS-AND-COLORS.vi.md)
- [Sửa kết nối SQL Trace — 0.15](history/SQL-TRACE-CONNECTION-FIX.vi.md)
- [Thanh tiêu đề, tìm DB Object và tab gần đây — 0.11](history/TITLE-SEARCH-AND-RECENT-TABS.vi.md)
- [Đề xuất tích hợp Dynamic Template Studio vào LSCode](history/TOOLAPI-INTEGRATION.vi.md)
- [Tinh chỉnh mật độ giao diện và khả năng đọc](history/UI-DENSITY-AND-EDITOR.vi.md)
- [Thiết kế giao diện A — editor làm trung tâm](history/UI-REDESIGN-A.vi.md)
- [Explorer gọn, tab thấp và DB lazy load](history/WEBVIEW-EXPLORER-COMPACT.vi.md)
- [Cập nhật workbench WebView — 10/11/2026](history/WEBVIEW-UPDATE-2026-11-10.vi.md)
- [Tab file, thao tác ERP và sao chép Profiler](history/WORKBENCH-FILE-ACTIONS.vi.md)
- [Cây thư mục ERP, đổi tên và tìm bảng SQL](history/WORKBENCH-FOLDER-TREE.vi.md)
- [Lịch sử file và thao tác dự án](history/WORKBENCH-PROJECT-HISTORY.vi.md)
- [Màu giao diện, lưới SQL và điều hướng](history/WORKBENCH-WORKFLOW.vi.md)

## Kế hoạch đang chuẩn bị

- [Tối ưu tải menu ERP và tích hợp Mobile từ Fcode](specifications/ERP-MENU-PERFORMANCE-MOBILE.vi.md) — đã triển khai cache, giảm round trip, tab Web/Mobile và tra source bằng sysid; còn nghiệm thu ERP/LAN.
