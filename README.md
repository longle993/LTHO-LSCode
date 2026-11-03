# LTHO Studio Code (LS Code)

**Giao diện A mới:** chạy `artifacts/publish-ui-a/LSCode.exe`. [Thiết kế, kiểm chứng và hướng dẫn](docs/UI-REDESIGN-A.vi.md). Một cột File/Chức năng/DB, vùng editor rộng hơn, style dùng chung và ba theme.

Bản Profiler trước lần thiết kế giao diện **`artifacts/publish-profiler18/LSCode.exe`**: [sửa reader và xác nhận bắt được SQL trên BACHVIET App](docs/PROFILER-LIVE-CAPTURE.vi.md). [cấu hình đường dẫn SSMS 18 một lần](docs/PROFILER-PATH.vi.md). [Profiler mặc định và template SQL2014](docs/PROFILER-DEFAULTS.vi.md). Bao gồm [Compare Text, Compare Folder, Beautify SQL](docs/COMPARE-AND-BEAUTIFY.vi.md). Bản này giữ các sửa lỗi [giải quyết %Database, mặc định App và chuyển App/SYS](docs/CONFIG-DATABASES.vi.md). Các bản publish và dữ liệu smoke cũ đã được dọn; tài liệu phiên bản cũ được giữ làm lịch sử.

Tên solution và namespace: `LSCode`; thư mục workspace hiện tại là `LSCode`.

IDE desktop **C# / WPF / .NET 10**, bản pilot 0.1 dựa trên khảo sát Fcode.

## Chạy ứng dụng

Bản giao diện hiện tại: mở `artifacts\publish-ui-a\LSCode.exe`. Build mặc định xuất ra `artifacts\publish\LSCode.exe`. Cần **.NET 10 Desktop Runtime** trên Windows. Mở thư mục `samples\ERP-Demo` để thử SQL/XML, search và split editor; các file mẫu không phải schema nghiệp vụ FAST.

Build từ source với .NET 10 SDK:

```powershell
.\build.ps1 -Test -Smoke -Publish
```

Hoặc mở `LSCode.sln` và đặt `LSCode.Shell` làm startup project. Lần restore đầu cần truy cập NuGet. Script giữ cache build/package trong `artifacts` của workspace.

## Sử dụng

- Ctrl+O mở file; Ctrl+Shift+O mở folder; Ctrl+N tạo file; Ctrl+S lưu; Ctrl+Shift+S Save As.
- Ctrl+P tìm file; Ctrl+Shift+P command palette; Ctrl+\ chia editor.
- Ctrl+F tìm trong file; Ctrl+H thay tất cả; Ctrl+Shift+F tìm workspace, gồm buffer chưa lưu của file đã có path.
- Editor → Format cho SQL150/XML/JSON; XML Outline/Problems nằm panel dưới; Editor → Evaluate XPath hỗ trợ prefix `d` cho namespace mặc định.
- SQL → Connection cấu hình server/database/auth; F5 chạy selection hoặc cả document; Cancel dừng query. Password không được ghi settings.
- Project → Đồng bộ dự án: nhập `BACHVIET`, nhấn Enter. Tìm dưới `\\172.168.5.14\CustomerPro\<nhóm>\<dự án>\<ứng dụng>\web.config`; nếu có nhiều ứng dụng, chọn đúng thư mục. Cây chức năng và cây file xuất hiện cạnh editor.
- Ctrl+Shift+Q mở tab SQL mới. Ctrl+Shift+T mở cấu trúc/dữ liệu bảng với chọn database/bảng, chọn cột, SELECT/WHERE/ORDER BY; Run đọc tối đa 1000 dòng, Dừng hủy truy vấn. Cửa sổ này chỉ đọc.
- Khi mở ứng dụng, chọn dự án đã lưu trong cửa sổ Projects; có tìm kiếm, truy cập gần nhất và Enter/nhấp đúp để kết nối. Project → Chọn project mở lại danh sách này.
- Mỗi tab SQL có lựa chọn **SYS/App**. Lần đầu chọn App nếu còn `%Database`, chọn DB cụ thể khai báo trong connectionStrings; ứng dụng lưu lựa chọn cho dự án. Cây menu luôn đọc SYS và có tìm theo tên/sysid/mã chức năng.
- SQL: **Ctrl+Space** hoặc gõ từ ba chữ cái/dấu chấm để gợi ý từ khóa, tên bảng/view/procedure và cột; hỗ trợ alias đơn giản. **↻ Gợi ý SQL** xóa cache sau khi schema thay đổi.

Cập nhật mới: SQL mở mặc định **App**. Nếu chưa lưu App DB và cấu hình dùng placeholder, ứng dụng chỉ tìm trong các database cụ thể của connectionStrings, cùng tiền tố với SYS (`_S`/`_Sys` → `_A`/`_App`, ưu tiên `_A` khi cả hai tồn tại); không tìm thấy thì hiện danh sách chọn. F5 chạy trực tiếp, không hỏi xác nhận. Toolbar có **Đổi dự án…** và **Ẩn/hiện kết quả**.

Tìm menu: nhập tên/sysid/mã rồi **Enter**, nhấn Enter tiếp để chuyển kết quả kế tiếp; không lọc lại cây mỗi lần gõ. Nhấp đúp menu đọc `Controller` trong `FastBusiness:QueryExtender` của ASPX (tìm cả thư mục `Main`), tạo tab controller tại cột **File dự án**, gồm file theo tên controller và các controller Grid con. Tab **Toàn dự án** vẫn giữ nguyên. **Workspace → Refresh explorer** xóa cache file để đọc lại khi file server thay đổi.
- Project → Import legacy XML xem trước và nhập nhiều project, chọn giữ/cập nhật bản trùng; tạo profile mới, chọn project và xem metadata. Mỗi tab SQL giữ target khi đổi project; SQL → Connection đổi target của tab đang chọn.
- Snippets: 12 mẫu legacy cùng create/edit/delete/import/export. Hint JavaScript từ `Assets/jssnippet.xml` chỉ hiện trong thẻ `<script>`; giữ nguyên `{{...}}`, Tab/Shift+Tab chọn tham số kế tiếp/trước đó. SQL bổ sung hint từ `Assets/sqlsnippet.xml`. Chèn snippet không tự chạy SQL.
- Tools → External tools settings: cấu hình executable Crystal và Profiler cho SSMS 18. Google Dịch hiện mở vùng chọn trong trình duyệt.

Profiler chỉ dùng SSMS 18. Thanh công cụ gồm icon Chạy, Dừng, Xóa danh sách và đổi bố cục bảng/SQL trên-dưới hoặc trái-phải. Bộ lọc bỏ hậu tố `_App`, `_Sys`, `_A`, `_S` để nhận cả Sys/App theo tiền tố tên database.

Settings/snippets/recovery ở `%LocalAppData%\LSCode`; biến `LSCODE_DATA_HOME` dùng để chọn thư mục dữ liệu khác khi kiểm thử. Save ghi trực tiếp vào file, không tạo backup hoặc file tạm trong thư mục nguồn. Kiểm tra thay đổi bên ngoài mỗi 2 giây: hiện thông báo và chỉ tải lại sau khi nhấn OK; Hủy giữ nội dung hiện tại, cảnh báo nếu có chỉnh sửa chưa lưu. Kiểm tra hash trước khi ghi để tránh ghi đè thay đổi bên ngoài.

Catalog project giữ metadata XML, gồm các trường chưa nhận diện, trong `projects.dat` mã hóa bằng Windows DPAPI theo tài khoản hiện tại. Không tự dùng password legacy. Export XML để trống các trường nhận diện là password/token/secret; kiểm tra trường tùy biến trước khi chia sẻ. Catalog mã hóa không dùng để chuyển sang tài khoản/máy khác.

Xem [hướng dẫn đồng bộ và xem bảng](docs/PROJECT-SYNC.vi.md) và [trạng thái triển khai](docs/IMPLEMENTATION-STATUS.vi.md). Đây chưa phải bản thay thế đầy đủ Fcode: decrypt, ERP generators và AI adapters vẫn còn trong lộ trình.

## Tài liệu

- [Đặc tả Tạo danh mục: tạo bảng, nhập dữ liệu và thêm trường](docs/CATALOG-TABLE-DESIGNER.vi.md) — yêu cầu mới, chưa triển khai; bố cục lưới theo ảnh tham chiếu, không có cột trái, bốn trường hệ thống mặc định.
- [Bản kế hoạch và Recommended New IDE Blueprint](docs/IDE-BLUEPRINT.vi.md)
- [Các khoảng trống cần xác minh](docs/DISCOVERY-GAPS.vi.md)
- [Kiểm kê source FCode ban đầu](docs/source-manifest.csv)
- [Kiểm kê source dependency và viewer bổ sung](docs/source-supplement-manifest.csv)
- [Chỉ mục bằng chứng theo file/dòng](docs/source-evidence.md)
- [Chỉ mục dependency API theo module](docs/module-api-index.txt)

Plan đã được người dùng xác nhận triển khai. Source Fcode cũ được giữ nguyên.
