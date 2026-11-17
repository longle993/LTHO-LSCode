# Kế hoạch triển khai

Đã bắt đầu triển khai theo yêu cầu tiếp theo của người dùng. Xem [trạng thái thực tế](implementation-status.md) cho từng phase; bảng bên dưới vẫn là kế hoạch và điều kiện đích, không phải tuyên bố đã đạt mọi tiêu chí. Duy trì 7 phase của prompt, thêm P0 chuẩn bị hợp đồng; tests chạy xuyên suốt, không đợi P7.

## Thứ tự phụ thuộc

P0 → P1 → P2 → P3 → P4 → P5 → P6 → P7.

Serializer tối thiểu phải có ở P2 để Save/Open chạy thật; P5 hoàn thiện generator/import/export thay vì đợi P5 mới có persistence. Undo command và revision được thiết kế từ P1/P3; P7 làm cứng và tối ưu, không retrofit toàn ứng dụng.

| Phase | Công việc và đầu ra | Điều kiện qua phase |
|---|---|---|
| P0 — Brief & contract | Review brief; chuẩn bị/chọn wireframe; lấy contract Engine và fixture; chốt C01–C07 hoặc ghi phần chưa hỗ trợ; spike WPF grid/editor | Có hướng UI được chọn và baseline DSL có evidence cho phần sẽ triển khai; chưa rõ điểm nào thì khoanh đúng điểm đó |
| P1 — Foundation | Solution 4 project, MVVM/DI/Hosting; MainWindow/navigation; ResourceDictionary Light/Dark; component styles; exception/logging boundary; revision/commands nền | Restore/build/test smoke; app mở/đóng, đổi theme, resize, điều hướng; package version ghi lại |
| P2 — Projects | Typed model + serializer tối thiểu; create/open/recent/rename/duplicate/delete; template CRUD; safe writes; manifest errors; Save current/all | T01–T04 và open malformed/failure tests; tạo→save→restart→open chạy được, không mất file lỗi |
| P3 — Visual Designer | Table tabs/add/rename; field grid/add/edit; inspector; type picker; source picker UI; Business Key/RowKey; Settings; search/reorder; undo units | R04,R06,R07,R13 được demo; không handler giả; T05–T07, reserved names/type checks; grid hàng trăm field dùng được |
| P4 — Advanced Mapping | Parent graph/context models/parser; relation designer và key pairs; nested sources; ancestor DSL; composite key; semantic validation | T10,T11,T14–T16,T18–T21 theo contract; cycle/reference/type errors dẫn đúng control |
| P5 — Generator & Export | Complete DTO mapping, JSON preview/highlight/copy/validate, import template, export current/all/project, metadata/extension retention, conflict handling | T22,T24; export→open→edit→export không mất dữ liệu; không xuất shape chưa được Engine hỗ trợ |
| P6 — Mapping Preview | Paste/load sample; auto detect + review/apply; traverse/convert/keys; result tabs/errors/cancel; snapshot revision | T08–T18,T21,T23, inferred ambiguous cases; mapping không SQL; parity theo fixtures |
| P7 — Quality & Release | Hoàn thiện autosave/recovery/undo failures; performance; UI keyboard/DPI/dark; full acceptance; packaging; hướng dẫn chạy | Toàn bộ R01–R18 có evidence; 23 bước acceptance; release trên Windows sạch, README chính xác |

## Backlog mở đầu có thể giao độc lập

| Task | Phụ thuộc | Đầu ra review được |
|---|---|---|
| P0-01 Chuẩn hóa contract | Engine source/schema/fixture khi có | C01–C07 có câu trả lời và mẫu output |
| P0-02 Thiết kế UI | Brief được review | Wireframe + state matrix + lựa chọn ghi nhận |
| P1-01 Khởi tạo solution | Bắt đầu implementation được yêu cầu | Solution compile và tests smoke |
| P1-02 Theme/control foundation | P0-02,P1-01 | Gallery WPF controls Light/Dark, screenshot DPI |
| P2-01 Models/DTO serializer | Baseline contract | Round-trip tests và unknown retention |
| P2-02 Repository an toàn | P2-01 | Save/reopen/failure tests |
| P2-03 Project UI | P1-02,P2-02 | Flow create/open/rename/duplicate/delete hoạt động |
| P3-01 Command + grid edits | P2-03 | Add/edit/reorder/undo qua domain model |

Không cần chờ toàn bộ câu hỏi Engine để làm shell/token/persistence abstraction. Chỉ chặn implementation/export thuộc contract chưa rõ, không giả lập tương thích để vượt gate.

## Cách báo cáo cuối mỗi phase

Ghi: mã R/T hoàn tất, thay đổi, build/test commands và kết quả thật, màn đã chạy, screenshot nếu có, limitation, quyết định mới và task tiếp theo. Không dùng số phần trăm cảm tính. Nếu một test chưa chạy vì môi trường thì ghi Not run cùng lý do.

## Release tối thiểu

Chốt target Windows/architecture và dạng package ở P7 sau kiểm tra môi trường đích. Tạo artifact cài/chạy trên máy sạch, hướng dẫn .NET runtime nếu cần, quyền ghi project, mở project mẫu. Không đưa mẫu chứa dữ liệu thật vào distribution. Tùy chọn nâng cao chỉ nhận sau khi luồng end-to-end đã pass.
