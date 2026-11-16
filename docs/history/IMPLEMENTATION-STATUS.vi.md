> Cập nhật 0.14: [DB Objects và Profiler](DBOBJECTS-PROFILER.vi.md). Đã có native WPF viewer và bridge SQL Trace/SSMS18; live reader còn lỗi khởi tạo, chưa hoàn thành Profiler. Các trạng thái phía dưới là ghi nhận pilot trước đó.

# LS Code 0.1 — trạng thái triển khai

Plan đã được người dùng duyệt triển khai. Đây là **bản pilot chạy được**, chưa phải bản thay thế toàn bộ Fcode. Source cũ không bị chỉnh sửa và ứng dụng mới không phụ thuộc đường dẫn cài Fcode để khởi động.

## Đã triển khai

- C# WPF/.NET 10; solution `LSCode.sln`, Core độc lập WPF, Shell/editor và services.
- Shell: Explorer lazy folders, activity bar, tab đóng được, split editor dùng chung buffer/undo, palette, Ctrl+P, shortcuts, status/output và Dark/Light/High Contrast.
- SQL/XML/JS/JSON/C#/text editor native AvalonEdit; syntax palette theo theme; find/replace; XML folding, diagnostics, outline và XPath; format XML/JSON và SQL150 bằng ScriptDom. SQL Ctrl+Space gợi ý keywords và metadata bảng/cột theo DB, hỗ trợ alias cơ bản; chưa tương đương đầy đủ IntelliSense SSMS.
- Open/save/save-as; bảo toàn UTF-8/UTF-16/UTF-32 BOM; kiểm tra file đổi bên ngoài trước lưu; ghi trực tiếp không tạo backup/temp cạnh file; thay đổi bên ngoài luôn hiện thông báo OK/Hủy, chỉ tải lại sau OK và cảnh báo nếu tab chưa lưu; recovery dirty buffers mỗi 8 giây. Khôi phục vào tab chưa gắn path để tránh ghi đè bản trên disk.
- SQL connection Windows/SQL auth, password chỉ giữ trong session; thực thi vùng chọn/cả document; GO batch, cùng connection trong một lần chạy; timeout 60 giây, cancel, result sets, messages, CSV export.
- Search workspace có regex/case/cancel, ignore `.git/bin/obj/node_modules/artifacts`; dùng snapshot buffer đang mở, tối đa 2000 kết quả.
- 12 snippet legacy đóng gói nguyên file nguồn, insert/caret offset, tạo/sửa/xóa, tìm, import/export giữ fields/CRLF. Xung đột import theo key/category giữ bản hiện có.
- Text compare hai cột, căn chỉnh thêm/xóa bằng LCS; không trim whitespace, chỉ bỏ khác biệt CRLF/LF.
- Catalog nhiều project: tạo profile, import XML có preview, merge giữ/cập nhật bản trùng, chọn project, xem metadata và export. Giữ các trường XML chưa nhận diện/nested metadata; lưu catalog bằng Windows DPAPI theo tài khoản hiện tại, có backup. Không tự dùng password legacy, không ghi ngược XML cũ. Export để trống các trường nhạy cảm nhận diện theo tên; cần kiểm tra trường tùy biến trước khi chia sẻ.
- Tab SQL giữ server/database và connection riêng trong bộ nhớ khi chuyển project. Tab mới dùng project hiện tại; SQL → Connection có thể đổi target tab đang chọn. Recovery chỉ giữ văn bản, không giữ connection/password.
- Google Dịch: ô nhập hai cột trong ứng dụng (nguồn / bản dịch, đổi chiều, tự dịch khi gõ). Nội dung gửi tới endpoint công khai của Google, không mở trình duyệt.
- Crystal Reports: external launcher với executable cấu hình được.
- SSMS 18/22: hai đường dẫn external Profiler launcher cấu hình riêng. **Chưa có native event viewer hoặc mapping/filter tự động.**
- Agent contracts độc lập provider trong Core; **chưa có agent adapter/chat thực** và không có nút agent giả.

## Giới hạn hiện tại

File editor giới hạn 16 MB, chặn file binary/encoding không xác định thay vì đọc lossy. Analysis XML/folding tắt khi vượt 1 triệu ký tự. DTD/entity bị chặn trong parser; vẫn có thể mở/sửa/lưu XML dạng text. Chưa có XSD autocomplete/design preview hoặc bộ phân tích nghiệp vụ FCodeViewer hoàn chỉnh.

Query mở session mới mỗi lần Execute; không giữ temp table/transaction qua hai lần Execute. Tối đa hiển thị 5000 rows/result và 30 result sets; CSV xuất phần đang hiển thị. Không phải chế độ đọc-only: SQL chạy theo quyền connection đã cấu hình, không hỏi xác nhận trước khi chạy theo yêu cầu người dùng. Đã kiểm tra kết nối, menu sys, schema và SELECT giới hạn dòng trên BACHVIET/FBISP24; chưa kiểm tra transaction hoặc câu lệnh ghi trên DB thật. Không tự chạy snippet khi insert/import.

Search không tìm file chưa có path, binary hoặc file quá lớn. Explorer refresh bằng lệnh, chưa có FileSystemWatcher. Compare giới hạn tích số dòng hai file ở 4 triệu để chặn tăng bộ nhớ không giới hạn.

Catalog giữ nhiều project nhưng Explorer chỉ hiện một thư mục hiện tại. Metadata được bảo toàn trong XML; AppData/Sys DB mappings, QueryDefine và các trường nghiệp vụ chưa được thực thi. **Đồng bộ theo tên dự án** đã triển khai từ CustomerPro/web.config; cây chức năng dùng wcommand/command và cửa sổ bảng hỗ trợ schema/data chỉ đọc. Xem [chi tiết](PROJECT-SYNC.vi.md). File XML gốc được giữ nguyên. Catalog DPAPI không chuyển trực tiếp giữa tài khoản/máy; bản export đã bỏ thông tin đăng nhập cần cấu hình lại khi nhập.

Chưa triển khai decrypt, ERP generators, DB/data/folder compare đầy đủ, native profiler, Crystal data automation, e-invoice, update packages, NoteX, terminal, Git và extension host. Các phần này vẫn thuộc roadmap, không được coi đã hoàn thành.

## Kiểm chứng

- Build Release và publish đã thành công, không warning/error.
- 44 kiểm tra nền (thêm discovery/config/tree mẹ–con/search sysid/SQL suggestions): encoding/BOM byte round-trip, xung đột lưu, Save As, reject lossy decode, GO lexical states/repeat/line offsets, XML diagnostics/XXE/CDATA/space, snippet lossless round-trip, diff alignment/whitespace, project metadata/merge/redacted export, search buffer/ignore/cancel.
- 11 kiểm tra WPF smoke (thêm kiểm tra SELECT và Encrypt legacy): mở window/editor, split buffer/dirty state, XML diagnostics/outline, ba theme, 12 snippets, SQL highlighting/format, DPAPI catalog round-trip/backup, SQL target isolation khi đổi project, save/reopen và render cửa sổ.
- Đã xem ảnh render WPF và sửa nền tab/màu cú pháp trong Dark theme.
- Công cụ Computer Use lỗi `native pipe unavailable` sau retry/reset. Chưa kiểm thử chuột/bàn phím bằng công cụ đó; render/smoke không thay thế acceptance test tương tác đầy đủ.
- Đã kiểm tra tích hợp SQL Server thật với BACHVIET: discovery, cây chức năng, catalog/file explorer, query target, cấu trúc và dữ liệu bảng. Chưa gọi Google Dịch, Crystal hay Profiler thật; chưa có chứng nhận parity với Fcode.

Chạy `./build.ps1 -Test -Smoke -Publish` để tái tạo build và báo cáo. Xem [hướng dẫn chạy](README-WPF.vi.md).

## Tiếp theo theo roadmap đã duyệt

Yêu cầu ngày 03/11/2026: [Tạo danh mục — thiết kế bảng và nhập dữ liệu](../specifications/CATALOG-TABLE-DESIGNER.vi.md). Source màn danh mục và preview đã được thêm ở commit `13967fb`; đây là công cụ riêng với cửa sổ TableBrowser chỉ đọc. Chưa kiểm chứng lại đầy đủ chức năng trong lượt cập nhật tài liệu ngày 05/11/2026. Bổ sung mới **duplicate template** (nhân bản cấu trúc bảng danh mục, không kèm dữ liệu — đã được người dùng xác nhận) và **copy/paste nhiều dòng** ở mục 10–11 của đặc tả: **đã bổ sung source và kiểm chứng riêng WPF/SQL Server cục bộ**; nhân bản/dán tạo bản nháp, chỉ ghi database sau khi Lưu, giữ hợp đồng bốn trường hệ thống. Xem mục 12 để chạy lại và biết giới hạn; smoke tổng thể còn dừng ở kiểm tra loading trong phần UI đang có thay đổi sẵn.

Phân tích sâu `fsystem`/`fcontrol`/`feditor`/FCodeViewer đã bổ sung; mở rộng viewer XML và nghiệp vụ sau đồng bộ; ánh xạ metadata catalog vào hành vi ERP; kiểm thử SQL integration; dịch Google trong IDE qua provider phù hợp; chuyển từng ERP tool bằng fixtures đầu vào/đầu ra. Không yêu cầu source gốc như điều kiện thực hiện.

Cập nhật SYS/App: toolbar riêng từng tab SQL; chọn và lưu App database khi chuỗi còn placeholder; màn hình Projects lúc khởi động với tìm kiếm và truy cập gần nhất; cây menu mặc định SYS, tìm theo tên/sysid. Đã kiểm tra 11 mục tích hợp thật, gồm popup chèn tên bảng và kết nối App. Bản đóng gói mới: artifacts/publish-0.3/LSCode.exe.
