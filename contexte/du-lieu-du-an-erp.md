# Dữ liệu dự án ERP

- Project picker gọi `project.standard.sync` để đồng bộ danh mục. Provider đọc resource được nhúng trong Infrastructure. Import/export XML dùng file picker và xử lý ở .NET. Xem [thiết kế danh mục tự chứa](../docs/history/SELF-CONTAINED-PROJECT-CATALOG-AND-TEMPLATE-STUDIO.vi.md).
- Catalog dự án được lưu bằng DPAPI theo tài khoản Windows. `LSCODE_DATA_HOME` cho phép tách dữ liệu khi thử nghiệm. Xem [dữ liệu và khả năng quay lại bản cũ](../docs/guides/WEBVIEW-RUNNING.vi.md#dữ-liệu-cũ-và-rollback).
- Cây menu đọc mã cây `wmenu_id` và quan hệ cha `wmenu_id0`; mã command có trường riêng. Quy tắc dựng ba cấp theo FSD Studio nằm trong [ProjectDiscovery](../src/LSCode.Core/ProjectDiscovery.cs). Xem [chỉnh sửa menu](../docs/guides/ERP-MENU-EDITOR.vi.md).
