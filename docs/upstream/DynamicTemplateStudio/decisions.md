# Sổ quyết định và điểm còn mở

Ngày lập: 04/11/2026. `Required` = prompt yêu cầu; `Proposed` = mặc định đề xuất; `Open` = cần bằng chứng/quyết định thêm. Không hàng Proposed/Open nào được coi là đã duyệt.

| ID | Trạng thái | Quyết định / vấn đề | Lý do, bước tiếp |
|---|---|---|---|
| D01 | Required | C#/WPF/.NET 10, MVVM/DI/Hosting/System.Text.Json | Theo prompt §2 |
| D02 | Required | Folder portable, runtime JSON dùng trực tiếp Engine | Theo §3,15; parity chưa được xác nhận |
| D03 | Required | Main layout ba vùng; Light mặc định, có Dark | Theo §4; ghi đè mặc định no-dark của skill |
| D04 | Proposed | Flat/Fluent nhẹ; DataGrid custom có virtualization | Chọn công cụ WPF sẵn trước custom grid tốn kém |
| D05 | Proposed | UI English, docs Vietnamese; font Segoe UI/Consolas | Bám nhãn prompt và desktop; review khi duyệt brief |
| D06 | Proposed | Một active project, recent đa project | Prompt chưa yêu cầu simultaneous windows |
| D07 | Proposed | Metadata/draft ở .studio; local preferences theo user | Tách UI khỏi runtime; xác minh Engine và portability |
| D08 | Proposed | Invalid draft không ghi đè valid runtime; autosave 800 ms | Bảo vệ dữ liệu; đo và review trạng thái UI |
| D09 | Open | C01–C07 trong engine-contract.md | Cần schema/parser/fixtures Engine; không bịa wire shape |
| D10 | Open | Phiên bản library UI/editor/validator/test | Xác minh chính thức khi P1; chưa cài dependency |
| D11 | Proposed | xUnit; JSON preview read-only | Đủ mục tiêu visual authoring, tránh hai nguồn model |
| D12 | Open | Windows tối thiểu, x64/ARM64, installer, signing | Chốt trước phát hành; không ảnh hưởng việc viết brief |
| D13 | Proposed | Identifier ASCII, case-insensitive, filename policy | Quy tắc bảo thủ; chốt với Engine trước codec/validator cuối |
| D14 | Open | Wireframe được chọn | Chưa có; không gọi ASCII prompt là toàn bộ UI đã duyệt |
| D15 | Proposed | Recovery/journal cho mutation nhiều file | Replace từng file không đủ bảo đảm transaction project |

## Cập nhật implementation

Theo yêu cầu “thực hiện … luôn”, đã triển khai bố cục ba vùng trực tiếp; D14 không còn chặn việc dựng, nhưng không đồng nghĩa người dùng đã duyệt từng hình. D10 đã chọn/pin MVVM Toolkit 8.4.2, Hosting 10.0.14, AvalonEdit 6.3.1.120, xUnit 2.9.3 qua NuGet restore và build thực tế. D07/D08/D15 có implementation theo mô tả trong [báo cáo triển khai](implementation-status.md). C01–C07 vẫn Open; chưa có Engine parity.

## Rủi ro và cách giảm

| Rủi ro | Hậu quả | Cách kiểm soát |
|---|---|---|
| Studio và Engine khác semantics | Preview đúng nhưng import thật sai | Contract version + fixtures + parity tests |
| Metadata/unknown properties bị mất | Mở lại không đúng template cũ | DTO extension retention, round-trip tests, read-only unsupported |
| Autosave revision cũ hoặc crash giữa nhiều file | Mất dữ liệu/manifest lệch | Queue, revision, staging/journal, fault injection |
| Lookup parent theo business ID | Child gắn sai parent khi key trùng | Context record identity; fixture hai parent trùng ID |
| Grid/editor không virtualize | UI đơ với hàng trăm field | Spike và benchmark trước tùy biến sâu |
| “Auto detect” suy luận quá tự tin | Sai type/key/data | Review proposal, warning mixed types, không tự tick key |

## Cập nhật quyết định

Khi chốt: ghi quyết định cụ thể, nguồn xác nhận, phạm vi ảnh hưởng và docs/tests cần sửa. Không sửa nguồn prompt để làm cho quyết định mới trông như yêu cầu cũ. Khi contract thay đổi, thêm migration/compatibility plan trước khi rewrite file dự án của người dùng.
