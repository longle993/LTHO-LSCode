# LS Code — khoảng trống khảo sát và tiêu chí xác minh

Không xem việc quét được toàn bộ file là đã hiểu toàn bộ hành vi. Bản source có làm rối. Người dùng đã bổ sung source dịch ngược của ba dependency và FCodeViewer; không có source gốc. Các mục dưới đây là giới hạn cụ thể, không phải kết luận rằng chương trình thiếu tính năng.

| ID | Chưa rõ | Bằng chứng hiện có | Cách xác minh / ảnh hưởng |
|---|---|---|---|
| G01 | Đã xác nhận chỉ có source dịch ngược | Người dùng không có source gốc | Không chờ/yêu cầu source gốc. Phân tích source dịch ngược, đối chiếu binary, phục dựng phần cần thiết và golden tests; build recipe vẫn cần xác minh |
| G02 | Đã nhận source dịch ngược; còn phân tích semantics | Đã kiểm tra `Fcode/fcontrol/fcontrol.csproj`, `Fcode/feditor/feditor.csproj`, `Fcode/fsystem/fsystem.csproj`, đều net461 | Đọc tiếp Cmd/Dbs/Reg/Sec/clsConnection, editor/control contracts; chưa coi nhận source là đã chứng minh tương thích .NET 10 |
| G03 | Đã nhận source viewer dịch ngược; còn inventory behavior | `Fcode/FcodeViewer/FCodeViewer.csproj` net462; entry UI `FCodeViewer/frmMain.cs:25` | Phân tích editor/design/navigation/XML rules từ source mới; chuyển SQL/XML vào editor native LS Code, không dùng viewer ngoài làm editor mới |
| G04 | Local SQL decrypt query | `urDecryptSQLObject.xAwR38Nm8a` tạo query bằng chuỗi làm rối, nhánh catch có Dbs.Execute/retry | Xác minh exact SQL, object types, server versions/quyền/side effects và expected output trước implement |
| G05 | Host decrypt protocol | Async state machine HTTP; ResultValue status/data/msg; inputs password/machine/version/object | Contract endpoint/auth/response/error/timeout; không dùng network/live credentials để dò |
| G06 | Đã chốt Google Dịch; còn cơ chế tích hợp | Người dùng chọn Google Dịch; tìm thấy `fsystem/fsystem/Cmd.cs:2440` (Translator) | Dùng Google Dịch qua ITranslationService; xác minh cách truy cập/xác thực và mapping dictionary/EN/Standard, không tự giả định API có phí hay scraping |
| G07 | Đã xác nhận luồng nhập tên dự án → Đồng bộ | Người dùng xác nhận có thể bổ sung bằng cách nhập tên dự án và nhấn đồng bộ; source có one/all/override | Giữ luồng này; tiếp tục xác minh schema, identity, overwrite và save path. Chưa kết luận sync dự án đồng thời tải snippet |
| G08 | Đã xác nhận bộ hiện tại chỉ có 12 mẫu | Người dùng nhắc 12 mẫu trong phản hồi G07; embedded snippet có 12 mẫu | Dùng 12 mẫu làm baseline, không yêu cầu thêm bộ mẫu. Tiếp tục test body/position/param và xác minh liên hệ với project sync nếu có |
| G09 | Registry/settings complete mapping | Reg wrappers và Registry.CurrentUser; MySettings không có properties | Chỉ xuất danh sách keys liên quan từ helper/source, che secret; không dump registry máy |
| G10 | SQL batch/helper/history semantics | QueryDefine 16 records; selected text, DataSet, Cancel, ScriptDom | Fixture GO/comments/transactions/commands/selection; chưa khẳng định history bền vững |
| G11 | Đã cho phép mở Crystal Reports bằng ứng dụng riêng | Người dùng không yêu cầu nhúng cửa sổ Crystal trong IDE | Dùng external launcher với executable cấu hình được; vẫn xác minh runtime và dữ liệu/XSD/report automation/FEC. Ngoại lệ này không áp dụng cho SQL/XML editor |
| G12 | ERP templates và data variants | Generators dùng Cmd.GetFileContent, version XML, App/Sys DB | Bộ mẫu list/voucher/filter/report/retrieve/update/mobile, output chuẩn từ bản cũ; quyết định cần exact bytes hay semantic equality |
| G13 | Đã chốt mapping theo SSMS 18 và SSMS 22 | Yêu cầu người dùng; legacy dùng Process.Start + SetParent và template tdf | Lập mapping/test matrix riêng từng bản SSMS; tiếp tục xác minh phiên bản SQL Server, events, filters và quyền. Chưa coi hai bản SSMS là hai phiên bản database engine |
| G14 | Những executable ngoài được gọi thực tế | Binary folder có Viewer/Translator/Design/Capture/etc; nhiều Process.Start giấu tên | Tìm call chain và resolve config/strings; không gán chức năng chỉ vì executable tồn tại |
| G15 | Reachability/stubs | urChangeColumnWidth chỉ layout; GenerateRetrieveCalc rỗng | Xác nhận dùng thực tế và call chain của các nhánh CreateRetrieve/ReportPrint; không coi tên class là feature hoàn chỉnh |
| G16 | Performance và compatibility runtime | Chưa chạy/build ứng dụng | Benchmark/acceptance sau duyệt trên môi trường thử; chưa có số đo hoặc chứng nhận parity |

## Những việc đã thực hiện an toàn trong khảo sát

Snapshot ban đầu: đọc source/config; kiểm kê và hash 178 file; lập chỉ mục nội dung của mọi C# file; xem cấu trúc XML/resx và các dependency/call sites; kiểm tra sự tồn tại DLL theo HintPath và SDK cài sẵn. Không sửa Fcode, không chạy executable legacy, không load DLL để thực thi, không gọi SQL/HTTP/SMTP, không giải mã thông tin đăng nhập, không thay registry.

File PNG/ICO/TDF/blob được kiểm kê tài nguyên; không xem hash là phân tích ngữ nghĩa của nội dung binary. Resx và dữ liệu XML đã được đọc/quét, nhưng chưa dựng lại UI từ resource. Không có AGENTS.md tìm được trong cây `D:\FAST\Code\LTHO` và các thư mục cha đã kiểm tra.

## Thông tin nên bổ sung khi triển khai

Không yêu cầu source gốc nữa. Tiếp tục phân tích bốn cây source dịch ngược đã nhận. Thông tin hữu ích còn lại: một project ERP mẫu đã loại secret, phiên bản SQL Server/Windows và 3–5 workflow thường dùng. Không chờ các thông tin này để thiết kế shell/editor, nhưng cần baseline trước khi cam kết tương thích nghiệp vụ.

## Source bổ sung đã kiểm tra

Các đường dẫn tuyệt đối:

- `D:\FAST\Code\LTHO\Fcode\fcontrol`: 154 file, 133 C#; net461.
- `D:\FAST\Code\LTHO\Fcode\feditor`: 88 file, 85 C#; net461.
- `D:\FAST\Code\LTHO\Fcode\fsystem`: 128 file, 78 C#; net461.
- `D:\FAST\Code\LTHO\Fcode\FcodeViewer`: 44 file, 17 C#; net462.

Tổng bổ sung 414 file, 313 C#. Đã xác minh project files và vị trí class quan trọng; chưa thực hiện phân tích đầy đủ các thân hàm mới. [Manifest bổ sung](source-supplement-manifest.csv) tách riêng để giữ nguyên snapshot/bằng chứng ban đầu. `source-manifest.csv`, `source-evidence.md` và `module-api-index.txt` trước đó vẫn chỉ mô tả phạm vi khảo sát ban đầu.

## Tên sản phẩm

Tên chính thức: **LTHO Studio Code**. Viết tắt: **LS Code**. Solution, assembly, namespace dự kiến: `LSCode`. Workspace trên disk giữ `D:\FAST\Code\LTHO\KappaCode`; không cần di chuyển thư mục để đổi tên sản phẩm.