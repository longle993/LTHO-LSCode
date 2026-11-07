# LTHO Studio Code (LS Code) — kế hoạch IDE desktop C# cho ERP

Ngày khảo sát: 25/09/2026. Source tham chiếu: `D:\FAST\Code\LTHO\Fcode`. Workspace mới: `D:\FAST\Code\LTHO\KappaCode`.

**Trạng thái: đã được người dùng duyệt triển khai; bản pilot 0.1 đang được phát triển.** Xem [trạng thái implementation](IMPLEMENTATION-STATUS.vi.md) để phân biệt chức năng đã có và roadmap. Tên sản phẩm đã chốt: **LTHO Studio Code**, viết tắt **LS Code**; solution/namespace dự kiến dùng `LSCode`. Thư mục workspace hiện tại vẫn là `KappaCode`.

## 1. Executive Summary

Đề xuất xây IDE **C# + WPF + .NET 10**, editor WPF tích hợp, kiến trúc modular monolith. SQL, XML, JavaScript và text mở trong cùng ứng dụng; không dùng FCodeViewer làm editor của IDE mới. Core chỉ biết hợp đồng workspace, document, command, tool và agent; shell lắp ghép module qua dependency injection.

Fcode hiện tại không chỉ là editor. Đây là bộ công cụ phát triển ERP: quản lý project/kết nối database; query SQL; menu/command nghiệp vụ; sinh source chứng từ, danh mục, filter, processing, báo cáo; so sánh; update package; thiết kế report; dịch nhãn; các tiện ích database và file. Thay thế bằng một editor đơn giản sẽ làm mất nghiệp vụ.

Bản cung cấp là C# mang nhiều dấu vết dịch ngược từ VB.NET: `DesignerGenerated`, `NewLateBinding`, `VB_0024StateMachine`, `base._002Ector()` và giải mã chuỗi `gnxpDSNRMCT`. Snapshot khảo sát ban đầu của riêng FCode có **178 file, 128 file C#, 163.917 dòng C#**, tổng 9.929.867 byte; một `.csproj`, không có `.sln`. Số liệu này chưa bao gồm bốn thư mục dependency/viewer mới được bổ sung. Không nên coi đây là source sạch có thể copy vào solution mới rồi build.

Người dùng xác nhận không có source gốc, chỉ có source dịch ngược. Đã bổ sung và kiểm tra project tại `Fcode/fcontrol`, `Fcode/feditor`, `Fcode/fsystem` (net461) và `Fcode/FcodeViewer` (net462). Các DLL cũng tồn tại ở `D:\FAST\FCode`. Tiếp tục phân tích source dịch ngược và đối chiếu binary; không yêu cầu source gốc như điều kiện triển khai. Chưa cam kết tái sử dụng trực tiếp trên .NET 10.

**Giới hạn bằng chứng:** đã kiểm kê mọi file; quét toàn bộ nội dung 128 file C# để lập chỉ mục class, entry point, dependency và API; đọc sâu các luồng trọng yếu. Đây không phải chứng nhận đã hiểu từng nhánh của 163.917 dòng hoặc toàn bộ chương trình ngoài. Các chuỗi SQL/đường dẫn bị mã hóa, nội dung DLL và hành vi runtime còn cần đối chiếu. Không suy diễn tên executable, schema hay nghiệp vụ từ tên file đơn thuần.

Tài liệu đi kèm:

- [source-manifest.csv](source-manifest.csv): toàn bộ file, kích thước, SHA-256.
- [source-evidence.md](source-evidence.md): chỉ mục bằng chứng theo file/dòng; chuỗi làm rối được thay bằng ký hiệu, không phải source dùng để build.
- [module-api-index.txt](module-api-index.txt): API phụ thuộc của từng module FCode.
- [DISCOVERY-GAPS.vi.md](DISCOVERY-GAPS.vi.md): thông tin chưa thể kết luận và cách đóng khoảng trống.

## 2. Current IDE Inventory

Đường dẫn trong bảng dưới tương đối với `Fcode/FCode/`, trừ khi ghi khác. `clsMain.AddTab…` là điểm tạo tool xác minh trực tiếp. Mức **A**: có thân hàm/call site chứng minh hành vi chính; **B**: có luồng/call site nhưng phần nghiệp vụ bị làm rối hoặc nằm trong DLL; **S**: phần khung chưa có logic. Không có mục nào được xem là đã kiểm thử runtime.

| Nhóm/chức năng | Implementation, entry point và bằng chứng | Input → output / dependency | Mức |
|---|---|---|---|
| Shell và tab công cụ | `clsMain.cs:50–537`, `frmMain.cs:3001`, `zurInheritsControl.cs:472` | Action → control/tab; refresh, add script, add note, remove; WinForms/fcontrol | A |
| Chọn/sửa project và server | `clsMain.ChooseServerName`, `frmServerName.cs:265`, `frmServerName_Update.cs:1264` | Project row/XML → kết nối và đường dẫn; `clsConnection`, `clsGlobal` | B |
| Sync project | `frmServerName.cs:271` → `frmServerName_SyncProject.cs:374` | Connection registry, một project/all/override → cập nhật DataTable; `Dbs.GetTable`, `Sec` | A/B |
| Chuyển App/Sys DB | `clsMain.cs:920–969`, `urMainControl.SetConnection` | Connection + database → context hiện hành | A |
| Tree lookup và command | `urMainControl.Init/RefreshControl`, `urMainLookup.LoadObject`, `urMainCommand.LoadCommand`, `urMainWCommand.LoadWCommand`, `urMainDvCommand.LoadDvCommand` | SQL metadata → tree; nhiều nhánh ERP, không phải chỉ tree filesystem | B |
| Sửa/nhân bản menu nghiệp vụ | `urMainCommand_Menu.RefreshData`, `urMainWCommand_Menu.RefreshData`, `urMainDVCommand_Menu.RefreshData`, `urMainWCommand.cs:832` | Form/menu fields → `Dbs.SQLInsert/Update/Delete`; dịch nhãn qua `Cmd.AddLabelTranslate` | B |
| File lookup/reference | `clsMain.AddTabFileLookup/AddTabFileReference`; `urFileLookup.cs:4238`, `urFileReference.cs:1279` | Folder/pattern → tree, nội dung, tham chiếu; có mở file qua process | A/B |
| Thêm source và WFile theo version | `frmAddSource.cs:662`, `frmAddWFile.cs:661`; `frmSourceVersionConfig.cs:629`, `frmWFileVersionConfig.cs:630` | Version/template/path → source/config; XML ngoài hoặc embedded fallback | B |
| SQL query | `clsMain.AddTabQuery` → `urQuery`; `urQuery.cs:4051` | SQL + connection → DataSet kết quả và lỗi có số dòng; SqlClient, feditor/Scintilla | A |
| Dừng query, kết quả nhiều bảng | `urQuery.cs:4056`, `urQuery.cs:5397`, result tab code | Danh sách command → `Cancel`; DataSet → các bảng kết quả; cần test batch semantics | A/B |
| Query helper/filter | `urQuery.cs:5131/6726`, `urFilterGrid.GetKeyString`, `urFilterWrapper.LoadKey`, `FCode.QueryDefine.xml` | Fields/operator/parameters → SQL hoặc filter; 16 định nghĩa embedded | A/B |
| SQL formatting | `urQuery.cs:4908`, `urStringBeauty.SQLBeautyFormat:1124` | T-SQL → parser SQL150 + generator; có parser SQL120 riêng trong decrypt | A |
| SQL/editor cũ thứ hai | `SQLEditor.cs:13`, `Parser.Parse`, `RtfHtmlBuilder`, `urChangeOwner.cs:518` | RichTextBox + RTF parser, undo/redo, comment/uncomment; khác engine của query | A |
| Snippet SQL | `urQuery.cs:4380–4505`, `FCode.snippet.xml` | DataSet XML → menu theo nhóm → chèn và đặt caret; 12 snippet mẫu | A/B |
| Script collection | `clsMain.AddToScript/ViewScript/SaveScript`, `frmScript.FindAll/HighlightWord`; `FCode.script.xml` | Script từ tool → buffer/dialog/save/clipboard; 23 bản ghi embedded | A |
| SQL object/table navigation | `urLookup.OnRefresh/OnAddScript`, `urTables.LoadColumns`, `urMainLookup.LoadObject` | Metadata DB → object list/columns/script; `Dbs`, Scintilla | B |
| Cấu trúc/cột/mô tả bảng | `urTables.cs:3058/3123/4699`, `frmTableEditColumn.LoadColumns`, `frmDescriptionColumns.cs:642` | Table/schema → chỉnh cột, kiểm tra cấu trúc, lưu mô tả XML | A/B |
| Xem/sửa dữ liệu | `urViewTableData.LoadColumns`, call `Dbs.ExecuteUpdate`; `urTables` | Table/filter → grid và update DB; cần xác minh keys/concurrency | B |
| Backup/restore/attach/info | `clsMain.cs:1357–1380` → `frmBackup/Restore/Attach/DBInfo`, `Dbs.Execute` | Database/path/options → thao tác server; không được đồng nhất với backup file local | B |
| Đổi owner và clear tables | `clsMain.AddTabChangeOwner/AddTabClearTables`; `urChangeOwner.OnAddScript`, `urClearTables.OnAddScript`, `Dbs.SQLDelete` | DB/table selection → script/thao tác; `FCode.cleartable.xml` có 4 bản ghi | A/B |
| Decrypt SQL local/host | `clsMain.AddTabDecryptSQLObject` → `urDecryptSQLObject.cs:1800–1999` | Server, DB, object, credentials/proxy → SQL text; nhánh local query DB, nhánh host HTTP/JSON | A/B |
| Text compare | `clsMain.AddTabCompareText` → `urCompareText.cs:1161` | Hai text/file → `DiffPlex.SideBySideDiffBuilder`; hiện gọi `Trim()` trước compare | A |
| Compare control/query | `urControlCompareText.CompareText:469`, `urQueryCompare.Init:855` | Hai chuỗi/SQL objects → diff tích hợp; search qua `urSearch` từ dependency | A |
| Compare database | `clsMain.AddTabCompareDatabase`; `urCompareDatabase.cs:1461/1587` | Hai connection → object khác/thiếu và script; tái dùng compare control | B |
| Compare data rows | `clsMain.AddTabCompareData`; `urCompareDataRow.CheckValueDiff:2059` | Hai bảng/dataset → khác/thiếu và script; xác minh null/key/collation | B |
| Compare folder | `clsMain.AddTabCompareFolder`; `urCompareFolder.CheckDataFile:1796` | Hai folder → so file/hash/text, mở compare, copy; `Cmd.HashFile`, JSON | A/B |
| SQL Profiler | `clsMain.RunSQLProfiler/RunSQLProfilerSingle`; `urPRofiler.cs:199/265` | Connection/filter → external process + `Api.SetParent`; không phải profiler native | A |
| SQL Management Studio launcher | `clsMain.RunSQLSMS:1134`, `Process.Start:1186` | Connection/options → external process; executable path ghép từ cấu hình | A/B |
| XML và liên kết Viewer | `clsMain.OpenFCodeViewer:753`; `frmMain.cs:4814/4834`; nhiều `Cmd.OpenFileWithNotePad`/`Process.Start` | Mở viewer riêng; đích một số nhánh XML bị giấu trong chuỗi/DLL; source viewer dịch ngược đã có, chưa hoàn tất phân tích | A/B |
| XML/JSON/JS beautify | `urStringBeauty.XmlBeauty:1083/JsonBeauty:1063`, `fsystem.JSBeautyLib` | Text → formatted text; XML dùng XmlDocument/XmlTextWriter | A/B |
| JS/CSS minifier | `urMinifier`, `Microsoft.Ajax.Utilities`, file đọc tại `:712` | Nội dung/file → minified output; cần test options/version | B |
| Translation | `frmMain.cs:4272` → `frmTranslatorServerSetting.cs:866–936` | Link/port/XPath và text → `Cmd.Translator`, `Data[].EN/Standard`; settings registry | A/B |
| Sinh list/danh mục | `clsMain.AddTabCreateList`; `urCreateList` → `urCreateListGenerate.GenerateList:280` | Khai báo fields/filter/grid/design/template → source | A/B |
| Sinh grid detail/filter/processing | `urCreateGridDetailGenerate.GenerateGridDetail`, `urCreateFilterGenerate.GenerateSource`, `urCreateProcessingGenerate.GenerateSource` | Wizard + template → file qua `clsGenSource`/helper | A/B |
| Sinh report/report print | `urCreateReport` và `urCreateReportPrint` → `urCreateReportGenerate.GenerateReport:789` | Filter/grid/print/drilldown/design → source/report artifacts | A/B |
| Sinh voucher | `urCreateVoucher.ReadXML:1799`, `urCreateVoucherGenerate.GenScript/GenerateVoucher`, SQL/Source/Reference subcontrols | Voucher nguồn, App/Sys DB, references → source và SQL; nghiệp vụ phải giữ nguyên | A/B |
| Source retrieve | `urSourceRetrieve.AnalysisCode:2020` → Condition/Form/Grid/Lookup/Generate | XML/file/metadata → cấu hình retrieve và source được sinh | A/B |
| Thiết kế view/grid/field | `urGenerateViewDesign.GenerateXML/GenerateView/GenerateDetail`, `urGridDesign.GenGrid/GenItem`, `urListDeclearField.GenView/GenGrid` | Bố cục + fields → XML; có logic dịch/header/category | A/B |
| Crystal reports và XSD | `urCreateFileRpt.cs:3029/3783`, `urCheckRpt.cs:811` → `urCheckRPTData.Check` | XSD/RPT/param/formula/source → report/schema/diagnostics; CrystalDecisions | A/B |
| Rpt FEC/export | `urRptFEC.cs:915–916`, `OfficeOpenXml`, CrystalDecisions | Report/options → xuất report/Excel; quy tắc cụ thể còn cần baseline | B |
| Import/export Excel ERP | `clsMain.AddTabImportExportExcel` → `urImportExportExcel` | File/template/XML/grid config → artifacts import/export; không kết luận chỉ là nhập XLSX | B |
| E-invoice setup | `clsMain.AddTabSetupEInvoice`; `urSetupEInvoice.cs:1232`, HTTP/JSON, `FastEInvoice.CryptoExt`, `Dbs.Execute` | Cấu hình/key → service result/script/DB update; protocol nằm một phần trong DLL | B |
| Update package | `clsMain.AddTabGenUpdate`; `urUpdate.GetSource/GetSourceMobile`, `:5023/5118/5294`, `frmUpdateTopBottomScript` | File/SQL/note/metadata → XML và bộ update; có copy/delete thư mục | A/B |
| Note/NoteX | `urNote.Save/OnControlRemove`, `urNoteX.Save:4199/GetSource/GetSourceMobile` | Note/file/source metadata → text/XML with schema; NoteX rộng hơn ghi chú | A/B |
| Library và commander | `urLibrary` có File.Copy/Process.Start; `urExploreCommander` tạo commander; `urCommander.CreateNewFolder` | Folder/library → browse/copy/move/delete/open; settings registry | A/B |
| Change header | `urChangeHeader.cs:431`, regex, `File.Copy` | File/text → nội dung header được chuyển đổi; cần giải mã mẫu để giữ ngữ nghĩa | B |
| Kiểm tra mail | `urCheckMail`, System.Net.Mail, XML/settings registry | SMTP/mail settings → thao tác mail; không gọi mạng trong khảo sát | B |
| Tiện ích phụ | `frmGetResourceDLL` dùng Reflection; `frmCheckKeyCode`; `frmQuickAccess`; `frmViewConfigTables`; `frmMain.cs:4803` capture launcher | Resource/config/key/shortcut → tool tương ứng; kiểm chứng thêm protocol capture | B |
| Khung chưa hoàn thiện | `urChangeColumnWidth.cs` chỉ layout; `urCreateRetrieveCalc.GenerateRetrieve:52` rỗng | Không xem là chức năng đã hoàn thành; `urCreateRetrieveGenerate` chủ yếu UI | S |

### Snippet và dữ liệu embedded

`FCode.snippet.xml` dùng `<NewDataSet><snippet>` với `type`, `key`, `position`, `param`, `description`, `code` và CDATA. Có nhóm và caret offset; chưa có bằng chứng về tab-stop kiểu `${1:...}`, editor CRUD snippet riêng, scope project hoặc tìm kiếm snippet chuyên dụng. Không được tự chuyển `param` sang engine placeholder mới trước khi xác minh semantics.

`FCode.config.xml` có `servername`, `User`, `Password`, `df_Database`, `AppData`, `FolderName`, `WLoginLink`, `ProgramPath`, `SourcePath`, `WorkingPath`, `RegistryName`. Các file `table.xml` (45 records), `tableSave.xml` (8), `QueryDefine.xml` (16), `script.xml` (23), `cleartable.xml` (4) là dữ liệu nghiệp vụ cần đưa vào migration. Số đếm là dữ liệu embedded của snapshot, không phải dữ liệu user tại runtime.

### Chức năng chưa được xác minh là đang tồn tại

Không có đủ bằng chứng để khẳng định IDE cũ có workspace nhiều root, command palette, Git integration, terminal tương tác, plugin SDK, AI agent, XML language service đầy đủ, SQL execution plan viewer hay query history bền vững. Những mục này là đề xuất mới, không phải inventory đã chứng minh.

## 3. Current Architecture

`FCode.csproj` đặt WinExe, WinForms, `net461`, `Prefer32Bit=true`, unsafe enabled. `LangVersion=14.0` là nội dung project được cung cấp; không dùng nó để kết luận phiên bản ngôn ngữ của ứng dụng gốc. `app.config` xác nhận .NET Framework 4.6.1 và Crystal assembly redirects. Project chỉ định `FCode.exe.config` nhưng trong cây source có `app.config`; cần sửa mapping nếu phục dựng build legacy.

Entry point đọc được ở `OVGykjfLYGfCVNwSTTa/mR6aHYf0Y9ldHD1I9ah.cs:86`, tạo `frmMain` tại dòng 151. `clsMain` là factory/orchestrator tạo nhiều control trực tiếp. `zurInheritsControl` gom connection, lifecycle và hook script/note. `clsGlobal` giữ trạng thái dùng chung; form vừa thao tác UI vừa truy vấn DB, đọc/ghi file, gọi registry/process.

Coupling cụ thể: generator giữ `step1` trỏ ngược đến wizard; tool đọc `clsGlobal.CurrentConnectionRow`; `Cmd` gộp style, XML, translation, file/process; `Dbs` thực thi SQL trong UI handler. Một engine query dựa feditor/Scintilla cùng tồn tại với `SQLEditor : RichTextBox`. Nhiều màn hình compare có logic tương tự nhau.

Technical debt nếu mở rộng nguyên trạng: khó unit test ngoài UI, state project/connection có thể ảnh hưởng nhiều tab, I/O đồng bộ và `Thread.Sleep`, SQL timeout vô hạn (`urQuery.cs:4052`), đường dẫn/settings lẫn trong registry/startup folder, error catch không luôn ghi log, dependency x86/Crystal và binary chưa rõ version. Đây là rủi ro từ call site đã thấy; chưa đo profiling ứng dụng.

## 4. Current Dependency Map

```text
Entry point (mR6…Main)
  └─ frmMain + clsMain + urMainControl
      ├─ urQuery / Lookup / Tables / ViewTableData
      │   ├─ feditor / ScintillaNET; một số tool: RichTextBox SQLEditor
      │   └─ System.Data.SqlClient + fsystem.Dbs / clsConnection
      ├─ urMainCommand / WCommand / DvCommand + menu editors
      ├─ ERP wizards → fields/design → generators → XML / SQL / files
      ├─ Compare → DiffPlex + file / DB helpers
      ├─ Report → CrystalDecisions; RptFEC → EPPlus
      ├─ Format → ScriptDom / Newtonsoft / JSBeautyLib; minify → AjaxMin
      ├─ Decrypt → SQL local hoặc HTTP host + Sec
      ├─ Translation → Cmd.Translator (fsystem/fsystem/Cmd.cs:2440, mới bổ sung)
      └─ External launches → Viewer / Profiler / SSMS / file associations
Mọi nhánh UI: fcontrol + fsystem.Cmd/Reg/clsGlobal + WinForms
```

Dependency trực tiếp từ project: fcontrol, fsystem, feditor, DiffPlex, Newtonsoft.Json, ScriptDom, AjaxMin, EPPlus, Crystal Engine/Shared, Microsoft.VisualBasic và framework assemblies. ScintillaNET xuất hiện trong `using`/types nhưng không có `<Reference Include="ScintillaNET">` độc lập; phải kiểm tra binary mapping trước khi phục dựng. Không kết luận DLL merge chỉ từ dấu hiệu này.

Thư mục binary có thêm FCodeViewer, FCodeTranslator, FTranslator, FDecryptSQLObject, FDesign, FCapture, chromedriver, ffmpeg… **Tồn tại file không có nghĩa mọi file đều được gọi bởi source này.** Các call site giấu chuỗi cần đối chiếu executable thực sự.

## 5. Feature Migration Matrix

P0 = phải có trước dùng thử; P1 = workflow thường ngày; P2 = đạt parity nghiệp vụ; P3 = nâng cao. Không có chức năng đang dùng nào mặc định bị loại bỏ.

| Module | New IDE / strategy | Lý do, phần giữ lại và rủi ro | Ưu tiên |
|---|---|---|---|
| Shell/forms/global state | Rewrite UI + context services | WinForms gắn UI/DB; giữ flow và command catalog; chi phí trung bình, rủi ro chuyển context | P0 |
| Project/config/sync | Adapt schema, rewrite persistence/UI | Giữ App/Sys/source/working paths và merge policy; cần giải mã mapping; chi phí cao | P0/P1 |
| Text/SQL engine | Replace bằng AvalonEdit + language services | Giữ shortcut/query helper; bỏ coupling Scintilla/RichTextBox; test selection/undo/IME | P0 |
| XML viewer flow | Replace launch bằng native editor | Giữ byte format khi save; migrate logic viewer sau có source/baseline; rủi ro cao | P0/P2 |
| Query execution | Adapt behavior, rewrite async service | Giữ batches/results/errors; per-document session tránh global state | P0 |
| XML/snippet/templates embedded | Reuse dữ liệu + importer | Bảo toàn code, CDATA, key, category, caret, unknown fields; không chạy script khi import | P0 |
| Formatting | Adapt parser options | Giữ SQL120/150 compatibility; format là explicit command; chi phí thấp-trung bình | P0/P1 |
| Compare text/query/folder | Adapt DiffPlex + rewrite presenters | Giữ lựa chọn whitespace legacy; engine dùng chung; chi phí trung bình | P1 |
| Compare DB/data | Adapt domain rules + rewrite I/O | Cần baseline keys/types/null/collation; sinh script preview trước apply; chi phí cao | P2 |
| Decrypt | Adapter có điều kiện; chưa rewrite thuật toán | Phải biết local query, host contract, quyền và output; chi phí chưa thể ước lượng đáng tin | P2 gate |
| Profiler | Replace bằng native event viewer/provider | Hiện reparent external window; map theo SSMS 18 và SSMS 22; phiên bản SQL Server/quyền vẫn cần xác minh; chi phí cao | P2 |
| Translation | Adapt protocol qua ITranslationService | Provider mới dùng Google Dịch theo yêu cầu; fsystem/Cmd.cs đã có source dịch ngược; xác minh mapping kết quả/dictionary và cơ chế kết nối | P2 |
| ERP generators/designers | Adapt logic thuần + rewrite UI | Giữ mẫu và mapping; golden tests từng variant; chi phí cao nhất | P2 |
| Crystal/XSD/RptFEC | Mở Crystal Reports bằng ứng dụng riêng; adapter cho dữ liệu/XSD | Người dùng không yêu cầu nhúng cửa sổ Crystal. Giữ sinh/kiểm tra dữ liệu, XSD và các tác vụ FEC/Excel trong scope; xác minh executable/runtime | P2 |
| Update/NoteX/library/Excel | Adapt formats + rewrite services | Không gộp NoteX vào note text; giữ source/mobile/update artifacts; chi phí cao | P2 |
| DB admin/owner/clear/e-invoice/mail | Adapt và kiểm thử tác động | Phụ thuộc quyền/server/protocol; dry run/preview cho thao tác thay đổi dữ liệu | P2 |
| SQLEditor parser/RTF helpers | Reuse thuật toán khi cần, replace UI | Chỉ port phần có test; không kéo WinForms vào Core | P1 |
| Stub/obfuscation plumbing | Không port mặc định | Stub chưa phải feature hoàn chỉnh; runtime làm rối không cần trong core mới; xác nhận nhu cầu trước remove | P3 |

## 6. New IDE Architecture

Modular monolith, MVVM ở presentation. Bốn hướng phụ thuộc: Shell → modules/contracts; Modules → Core contracts; Infrastructure triển khai contracts; Core không tham chiếu WPF, SQL client hay provider AI.

Core models: WorkspaceId, ProjectId, DocumentId, ConnectionProfileId, DocumentSnapshot(version), Diagnostic, TextEdit, CommandDescriptor, ToolDescriptor, OperationResult. Không truyền Control/DataGridView/SqlConnection toàn cục giữa module.

DocumentService sở hữu buffer, encoding, newline, version, dirty state; EditorView chỉ là view. Hai split có thể dùng chung DocumentId và undo stack. WorkspaceService chịu trách nhiệm project lifecycle. Mỗi query giữ connection profile riêng và snapshot lúc bắt đầu chạy. Module giao tiếp qua interface và sự kiện có kiểu; tránh event bus chứa mọi logic.

## 7. UI/UX Architecture

Yêu cầu bổ sung: [Tạo danh mục — thiết kế bảng và nhập dữ liệu](CATALOG-TABLE-DESIGNER.vi.md). Công cụ WPF dùng lưới theo ảnh tham chiếu, bỏ cột danh sách trái; chọn bảng trên thanh đầu, nhập tên bảng trong vùng nội dung, cấu hình trường/kiểu/ràng buộc rồi Lưu để tạo bảng. Hỗ trợ nhập bản ghi và thêm trường cho bảng đã có. Bốn trường mặc định bắt buộc: `user_id0 int`, `user_id2 int`, `datetime0 datetime`, `datetime2 datetime`. Đã có source công cụ danh mục; đặc tả bổ sung ngày 05/11/2026 gồm duplicate template (nhân bản cấu trúc bảng danh mục, không kèm dữ liệu — đã được người dùng xác nhận) và copy/paste nhiều dòng cấu trúc/dữ liệu, đã triển khai trong source và kiểm chứng riêng bằng WPF cùng SQL Server cục bộ (chi tiết mục 12 của đặc tả). Không thay thế phạm vi ERP source generator.

```text
Menu   Project ▾    Search / Command Palette           Window controls
Activity | Explorer/Search/ERP/DB | Editor tabs         | Agent / Tools
         | project tree          | SQL/XML/JS/Text      | chat + context
         |                       | split editor        | review changes
         |                       |---------------------|--------------
         |                       | Problems / Output / SQL Results /
         |                       | Terminal / Logs / Agent activity
Status: project · connection/database · line:column · encoding · theme
```

Explorer có Files và ERP Objects, không ép metadata menu DB thành file giả. Double-click mở editor, single-click preview nếu bật; file dirty không bị tab preview ghi đè. Tab có close/dirty indicator; đóng nhiều file gom lựa chọn save/discard/cancel. Tool có thể pin vào panel hoặc tab, SQL results gắn với query/run ID.

Activity Bar: Explorer, Search, Database, ERP Tools, Snippets, Agent; Source Control chỉ bật khi có Git provider. Settings và theme ở đáy. Phím: Ctrl+P mở file; Ctrl+Shift+P palette; Ctrl+Shift+F search; Ctrl+H replace; Ctrl+S save; Ctrl+W close; Ctrl+\ split; F5 query khi context SQL. Shortcut xung đột và layout bàn phím phải cấu hình được.

## 8. Editor Architecture

Editor registration theo extension và content type: SQL/XML/JS/JSON/Text; unknown text mở plain text, binary hiện preview/metadata phù hợp. `IEditorProvider` tạo view; `IDocumentService` quản buffer; `ILanguageService` cung cấp diagnostics, format, completion, outline, navigation. Cancellation và snapshot version ngăn diagnostics cũ ghi đè nội dung mới.

**SQL:** AvalonEdit cho text, syntax và folding; ScriptDom parse/format theo dialect của profile; completion keywords/snippets trước, schema cache sau. Không quảng cáo autocomplete hoàn chỉnh chỉ nhờ syntax highlighting. SqlClient chạy async, selected SQL/all document, `GO` batch splitter có lexical state (comment/string), result sets riêng, row count/messages/errors, cancel/timeout. Temp tables và transaction cần session rõ ràng; reconnect phải báo mất session. Parameter UI dùng SqlParameter cho giá trị; tên object phải được quote/validate riêng. History lưu có opt-in và lọc secret; CSV export trước, XLSX sau khi chọn license. Explain/actual plan và schema object navigation ở phase nâng cao.

**XML:** mở trực tiếp trong tab, edit/save, search/replace, syntax, well-formed diagnostics có line/column; outline chọn node điều hướng; XPath có namespace mapping và kết quả giới hạn. Parser với resolver bị vô hiệu hóa mặc định; hỗ trợ ERP entity/include chỉ qua allowlist trong workspace sau khi xác minh format. XSD completion/validation chỉ khi có schema tương ứng. Không chạy script khi preview. Formatting là lệnh chủ động và có preview với file nghiệp vụ nhạy whitespace. Save bình thường giữ BOM/encoding/newlines và không serialize toàn bộ DOM.

Folding/attribute completion/navigation tới file ERP references đi qua language service riêng. Design preview phải tái hiện quy tắc `urGenerateViewDesign`, `urGridDesign` và viewer đã kiểm chứng; không dựng preview giả rồi coi là tương thích.

**File lifecycle:** canonical path + identity, reopen focus tab cũ; load async; save qua temp cùng volume và replace/backup phù hợp, detect external edits bằng version/hash; lựa chọn reload/compare/keep buffer; recovery journal cho unsaved buffers. Rename/delete cập nhật editor, tìm dirty tab trước. Unicode tiếng Việt, UTF-8 BOM/no BOM, UTF-16 và encoding legacy xác minh bằng fixture.

## 9. Project & Workspace Architecture

Workspace chứa nhiều Project và roots; project gồm ERP type/version, App/Sys DB profile IDs, ProgramPath/SourcePath/WorkingPath, web login link, template versions và metadata. Explorer lazy-load, ignore cấu hình được. FileSystemWatcher chỉ là thông báo thay đổi: debounce/coalesce và full rescan khi overflow.

Luồng người dùng đã xác nhận: nhập tên dự án rồi nhấn **Đồng bộ** để bổ sung dự án. Không mặc định thao tác này tải thêm snippet; cần đối chiếu mapping trong source. Sync tách `ReadRemote → Map → Diff → Review → ApplyLocal`, hỗ trợ một project/all/override; không hiểu sync là copy source tree. Import config đọc file người dùng chọn, bảo toàn field chưa biết trong legacy extras. Metadata có schema version, migration idempotent, backup và rollback. Không tự động kết nối database khi mở workspace chưa tin cậy.

## 10. Tool Architecture

`IToolModule` đăng ký descriptor, commands và view factory; domain service của tool độc lập view. Shell không switch theo tên từng tool. Built-in modules: Compare, DatabaseAdmin, Decrypt, Profiler, Translation, ErpGeneration, Reports, Deployment, Notes, Files, Mail, EInvoice.

Translation dùng Google Dịch theo quyết định của người dùng. Provider đặt sau `ITranslationService`; chưa mặc định dùng API có phí, scraping hay mở trình duyệt. Cách xác thực/kết nối cần xác minh lúc triển khai; giữ văn bản gốc và báo lỗi nếu dịch thất bại.

Crystal Reports được mở bằng ứng dụng riêng, đường dẫn executable cấu hình được; SQL/XML vẫn mở native. Profiler mapping hỗ trợ SSMS 18 và SSMS 22, kiểm thử từng bản; số phiên bản SSMS không thay thế phiên bản SQL Server.

Job dài dùng progress/cancel; output có artifact path, diagnostics và operation id. Sinh file/SQL theo mô hình preview → chọn target → apply, có log và backup. Decrypt host/local là hai implementation cùng contract, không thay thuật toán chỉ vì tên chức năng giống nhau. Tool chưa đủ dependency có trạng thái unavailable với lý do rõ ràng, không hiển thị kết quả giả.

## 11. Snippet Architecture

Bộ mẫu hiện tại được người dùng xác nhận chỉ có 12 mẫu; không yêu cầu cung cấp một bộ snippet bổ sung. Thông tin này ghi tại G08 dù phản hồi gọi G07; đồng bộ theo tên dự án được ghi riêng tại G07.

Snippet model: id, title/key, language, scopes, category, body, description, parameters, cursor/tab stops, shortcut, source/version. Scope: built-in/global/user/workspace/project và tags ERP/SQL/XML/JS. Quy tắc override xác định theo ID, không chỉ title; collision có preview.

Importer legacy giữ nguyên code và thêm `legacy.position/param/type`; export legacy chỉ khi chuyển đổi không mất dữ liệu, nếu không xuất format mới kèm cảnh báo phần không tương thích. UI có list/search/filter, create/edit/delete, import/export, preview insertion và shortcut/context. Placeholder mới là tính năng mở rộng, không suy diễn từ trường `param` cũ. Chèn snippet là một undo transaction, không thực thi SQL.

## 12. AI Agent Architecture

`IAgentProvider`/`IAgentSession`/`IAgentTransport` độc lập provider; descriptor khai báo capabilities, cancellation, streaming events, tool request/result. Codex adapter, Claude adapter và adapter tương lai nằm ngoài Core. Transport CLI/process hoặc API chọn theo tài liệu/version thực tế ở phase AI; chưa chốt lệnh CLI/protocol hay cam kết tính năng provider trong khảo sát này.

ContextBuilder gom workspace/project metadata, tree giới hạn, current file/selection, open buffers (kể cả chưa save), search results, docs, AGENTS.md và project rules. Mỗi phần có provenance, path, document version, token/size limit. Quy tắc ưu tiên minh bạch; nội dung file/tool output là dữ liệu, không tự nâng quyền. Không gửi toàn workspace mặc định; UI cho xem context và loại trừ secrets/generated/binary.

Tool gateway: read/search/create/edit/delete file, project structure, XML/SQL analyze, command và SQL execution. Agent đề xuất patch gắn hash/version; Review UI hiển thị diff trước apply; thay đổi cạnh tranh yêu cầu rebase/review. Quyền đọc không hàm ý upload đến provider. Session log chứa tool names/status và metadata được lọc secrets.

MVP AI ban đầu: provider abstraction + context selection + chat/explain + đề xuất patch. Agent tự chạy shell/SQL là phase sau khi permission enforcement đã có tests.

## 13. Theme Architecture

Theme tokens dùng WPF ResourceDictionary và semantic keys: surface/sidebar/editor/tab/selection/text/error/border/accent/status. Built-in Dark, Light, High Contrast; editor syntax palette lấy từ cùng theme model. Không hard-code màu ở từng control. Theme switch cập nhật editor, grid, XML tree, dialogs, terminal và icons; đảm bảo contrast, focus và DPI. Custom theme chỉ dữ liệu có schema, không load XAML/code tùy ý từ workspace.

## 14. Command Architecture

CommandDescriptor gồm ID/name/category/shortcut/context predicate/handler/capabilities. Registry chung cấp menu, toolbar, palette, keybindings; can-execute tính theo active document/tool, không theo text tên tab. Async command có trạng thái running/cancel/error.

IDs dự kiến: `workspace.open`, `project.sync`, `file.open/save`, `search.workspace`, `document.format`, `sql.execute/executeSelection/cancel`, `compare.files`, `sql.decrypt`, `profiler.open`, `translation.open`, `agent.open`, `theme.select`, `settings.open`. Permission được kiểm tra trong service gateway nữa, không chỉ disable nút.

## 15. Search Architecture

Provider cho file path, text, XML attributes và SQL objects. Search text streaming background, cancel, regex timeout, include/exclude/globs, case/whole word, current project/all roots. Result giữ path/span/version/snippet để click đi đúng vị trí. Ctrl+P ưu tiên MRU/fuzzy names.

Replace toàn workspace bắt buộc preview tập thay đổi và phát hiện file đổi giữa search/apply; batch write có journal/rollback. File mở dùng buffer hiện tại thay cho bản trên disk. SQL object search chỉ truy vấn profile được chọn và không tự decrypt object. Index cache có thể xóa và dựng lại; không xem FileSystemWatcher là nguồn sự thật duy nhất.

## 16. Settings Architecture

User settings tại `%AppData%\LSCode`, cache/log/recovery tại `%LocalAppData%\LSCode`, workspace/project settings dạng JSON versioned. Thứ tự defaults → user → workspace → project; permission/secret là ngoại lệ, workspace không được tự cấp quyền hoặc thay credential store.

Settings Center: Appearance, Editor, SQL, XML, Project, Snippet, Terminal, AI/Agents, Shortcuts, Tools, Security, Performance. Validate trước lưu, migration version, reset từng nhóm, export đã lọc secret. Windows credential storage/DPAPI service giữ secret; JSON chỉ tham chiếu credential ID.

Legacy dùng cả `Reg.GetRegistryKey/SetRegistryKey`, XML startup/fallback resources; `MySettings` chỉ thấy wrapper, không có settings properties khai báo. Không coi `MySettings` là nơi lưu chính. Import registry phải có mapping xác minh, preview và backup, không đọc/xuất hàng loạt registry không liên quan.

## 17. Plugin/Extension Architecture

Phase đầu chỉ built-in modules đăng ký lúc khởi động; chưa xây marketplace/hot reload. Contracts nhỏ, versioned: commands, editors, tools, languages, themes, agents. Module dependencies khai báo rõ và phát hiện cycle khi composition.

Sau MVP: extension manifest id/version/contractVersion/capabilities/entry point. Plugin .NET chạy in-process có quyền của process, không thể xem AssemblyLoadContext là security sandbox. Plugin không tin cậy phải out-of-process qua IPC có message schema, size/time limits, broker permissions; giao diện UI qua declarative contributions. Core không tự tải code do workspace khai báo.

## 18. Security & Permission Model

Workspace Trust và AgentPermission là hai lớp. Permission theo operation + canonical root/path + command arguments/cwd + connection/database + provider/session. Allow once/session/persisted và deny có UI quản lý/revoke; persistent grant cần xác nhận riêng, không lưu vào project có thể chỉnh bởi agent.

Chặn path traversal, junction/symlink escape, thay target giữa validate/write; áp dụng cả create/delete/rename. Agent edits đi qua document service để giữ undo và kiểm tra version. Shell process chạy có working directory rõ ràng, arguments tách, timeout/cancel/process tree cleanup. Nếu cần sandbox thực sự, dùng OS isolation/helper account; UI approval tự nó không phải sandbox.

SQL dùng quyền tài khoản DB làm lớp enforcement chính, không chỉ kiểm tra prefix SELECT. Review SQL/procedure call và target, yêu cầu riêng cho write/admin/decrypt; không mặc định tự commit production. Logs redaction connection strings/tokens/passwords/query parameters. Không đưa secret vào command-line nếu protocol cho giải pháp khác. XML không tự fetch external entities; AI không tự nhận quyền từ AGENTS.md.

## 19. Technology Selection

| Lựa chọn | Phù hợp | Chi phí/rủi ro và quyết định |
|---|---|---|
| C# WPF/.NET 10 | Windows desktop, MVVM, resource themes, native controls | Chọn; máy đã có SDK 10.0.301. Không phải cross-platform |
| WinForms hiện đại | Gần UI cũ, có thể bridge control | Không chọn làm shell mới vì yêu cầu layout/theme/editor modular; giữ bridge tùy compatibility |
| WinUI 3 | Windows UI hiện đại | Cần PoC editor/docking/interop thêm; chưa có lợi ích đủ lớn cho repo này |
| Avalonia | Có hướng đa nền tảng | Chưa có yêu cầu ngoài Windows, trong khi ERP/Crystal/registry mang tính Windows; đánh giá lại nếu scope đổi |
| AvalonEdit | Editor WPF mở rộng được | Chọn PoC; phải tự bổ sung SQL/XML language services, completion và diagnostics |
| Monaco trong WebView2 | Editor web nhúng desktop, nhiều khả năng có sẵn | Phương án dự phòng nếu AvalonEdit không đạt benchmark/IME; thêm JS bridge/runtime và state synchronization |
| Scintilla/feditor legacy | Gần behavior cũ | Dependency/source/version chưa rõ; không đưa trực tiếp vào Core |

Thành phần đề xuất: System.Xml/XmlReader/XDocument cho XML; Microsoft.SqlServer.TransactSql.ScriptDom cho T-SQL; Microsoft.Data.SqlClient cho SQL Server; DiffPlex cho diff (pin version sau kiểm tra license/API); System.Text.Json cho settings; Microsoft.Extensions DI/logging cho composition và log; FileSystemWatcher + indexer cho search. Search MVP viết service giới hạn phạm vi, chưa cần search server. Terminal tương tác cần Windows pseudoconsole adapter; Output process không được gọi là terminal đầy đủ.

Docking MVP dùng layout WPF có splitter và tab groups do shell quản lý; chỉ thêm docking library sau PoC license/maintenance/accessibility. Crystal mở ứng dụng riêng qua launcher cấu hình được, không nhúng cửa sổ; các tác vụ dữ liệu/report automation vẫn cần kiểm tra compatibility. Excel chọn sau compatibility gate; không tự nâng EPPlus binary cũ sang package mới mà chưa kiểm tra điều kiện sử dụng.

Cơ sở kỹ thuật chính thức đã đối chiếu: [WPF overview](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/overview/) xác nhận WPF là UI Windows; [AvalonEdit](https://avalonedit.net/) mô tả editor WPF; [ScriptDom](https://github.com/microsoft/SqlScriptDOM) cung cấp parser/AST T-SQL; [Microsoft.Data.SqlClient](https://learn.microsoft.com/en-us/sql/connect/ado-net/) là provider SQL Server. Các lựa chọn kiến trúc trên là đề xuất cho project, chưa phải kết quả benchmark. Version package sẽ pin khi PoC và build; không dùng website search cache để suy diễn version mới nhất.

## 20. Source Code Structure

```text
LSCode.sln
src/
  LSCode.Core/          Models, Contracts, Commands, Permissions
  LSCode.Workspaces/    Projects, Discovery, Indexing, Import
  LSCode.Documents/     Buffers, Storage, Recovery, History
  LSCode.Shell/         App, Composition, Views, ViewModels, Themes
  LSCode.Editors/       AvalonEdit integration, LanguageServices
  LSCode.Sql/           Execution, Connections, Schema, Diagnostics
  LSCode.Xml/           Parsing, Outline, XPath, ERP references
  LSCode.Snippets/      Catalog, Expansion, LegacyImport
  LSCode.Tools/         Compare, Formatting, Notes, Files
  LSCode.Erp/           Metadata, Generators, Deployment, Tools
  LSCode.Agents/        Context, Sessions, ToolGateway
  LSCode.AgentAdapters/ Provider-specific transports
  LSCode.Infrastructure/Settings, Credentials, Logging, Processes
  LSCode.LegacyBridge/  Optional isolated compatibility host
tests/
  Core.Tests/ Documents.Tests/ Sql.Tests/ Xml.Tests/
  LegacyGolden.Tests/ Integration.Tests/ Desktop.SmokeTests/
fixtures/                 Sanitized baseline inputs and outputs
docs/                     Discovery, ADRs, migration/runbook
```

Đây là module boundaries dự kiến. Phase 1 tạo Core, Shell, Infrastructure và tests cần thiết trước, tránh tạo hàng loạt project rỗng. Agents/Erp/bridge chỉ thêm khi bắt đầu phase tương ứng. Source mới không tham chiếu ngược về thư mục source cũ.

## 21. Migration Strategy

Chạy song song hai IDE cho đến khi từng workflow đạt parity; không sửa/xóa Fcode. Lập baseline input/output từ bản đang sử dụng trên database thử nghiệm và bản sao project. Import tạo báo cáo: đọc gì, chuyển gì, field giữ nguyên, conflict, số records, hash artifacts, lỗi và rollback.

| Current behavior | New behavior | Reason / compatibility impact | Migration solution |
|---|---|---|---|
| XML mở tool ngoài ở một số luồng | Open native tab | Trải nghiệm một ứng dụng; viewer-specific behavior chưa biết | Golden files + inventory viewer; không bật editor association toàn Windows mặc định |
| Global project/DB state | Context theo workspace/document | Ngăn chạy query nhầm DB khi đổi project | Import profile IDs; status bar và target snapshot trên mỗi run |
| Compare gọi Trim | Mặc định exact và tùy chọn Legacy Trim | Exact diff hữu ích nhưng thay kết quả so sánh | Setting theo project, parity mode và test whitespace |
| SQL CommandTimeout=0 | Timeout cấu hình, có tùy chọn unlimited chủ động | Tránh query treo; có thể ngắt workflow cũ | Import tùy chọn/hiển thị rõ, baseline query dài |
| Registry/startup XML | JSON + credential references | Dễ quản trị, không để secrets cùng project | Import read-only, preserve unknown fields, backup và rollback |
| Profiler process + SetParent | Native event viewer | Loại phụ thuộc cửa sổ ngoài; event/filter không mặc nhiên tương đương | Mapping trace → provider được hỗ trợ; giữ legacy song song đến khi đạt parity |
| XML beautify serialize DOM | Save raw buffer; format explicit | Bảo toàn ERP whitespace/CDATA/entity | No-op round-trip tests; compare trước chấp nhận format |

Không auto-deploy source sinh ra, không auto-run SQL import/snippet, không tự update schema database để “khớp IDE”. Decrypt giữ nguyên contract đã xác minh; nếu chưa xác minh thì chưa thay thế tool cũ.

## 22. Development Roadmap

P0 discovery/compatibility → P1 shell → P2 documents/editors → P3 workspace/SQL/snippets MVP → P4 developer tools → P5 ERP parity → P6 AI → P7 terminal/extensions/performance → P8 migration rollout. Có thể làm AI sau MVP trước một số ERP tool nếu người dùng đổi ưu tiên, nhưng không gọi đó là đạt parity.

Chưa đặt deadline cố định: thiếu baseline viewer, decrypt protocol, ERP templates và compatibility của dữ liệu/report automation làm ước lượng sai lệch lớn. Sau P0 và editor PoC mới ước lượng theo workflow/fixture và số người thực hiện.

## 23. Detailed Phase Plan

### P0 — Discovery và compatibility gates

- Goal: có contract và baseline đủ để implement không đoán nghiệp vụ.
- Tasks: bổ sung source/dependency map; xác minh registry/schema/strings; lập fixture legacy; PoC .NET 10 editor/IME/SQL connection và Crystal external launcher; chọn version/license.
- Dependencies: bộ chương trình cũ và source dịch ngược đã cung cấp, project/database mẫu đã loại secret.
- Create: `docs/adr`, `docs/legacy-contracts`, `fixtures` và harness chỉ sau duyệt plan.
- Change: inventory, gap register, migration matrix.
- Risks: không phục dựng được full build; phụ thuộc binary/platform/licensing.
- Testing: baseline do app cũ tạo trên bản sao; hash file trước/sau, so output/golden.
- DoD: mỗi workflow được nhận làm scope có entry/input/output/dependency/fixture; phần thiếu đánh dấu gate, không coi là đã hiểu. Báo cáo này chưa tự đóng toàn bộ P0.

### P1 — Desktop shell

- Goal: app WPF khởi động và shell sử dụng được.
- Tasks: DI/MVVM, activity/sidebar, tab groups/splitter, status/output, command palette, settings và ba theme.
- Dependencies: duyệt kiến trúc; SDK/runtime/package pins.
- Create: Core, Shell, Infrastructure, Desktop smoke tests.
- Change: solution, command registrations.
- Risks: focus/keyboard, high DPI, accessibility.
- Testing: launch/close, navigation bàn phím, 100/150/200% DPI, theme/focus.
- DoD: mở/đóng app ổn định; palette chạy command thực; layout/settings restore; không nút chức năng giả.

### P2 — Documents, SQL/XML/Text editors

- Goal: mở, sửa, lưu SQL/XML/JS/JSON/text native.
- Tasks: buffers, file identity, dirty prompts, atomic save/recovery, undo, search/replace, split shared document, highlighting, XML diagnostics/outline/folding, format commands.
- Dependencies: P1 + editor PoC; fixtures encoding/XML.
- Create: Documents, Editors, Xml, language services và tests.
- Change: Shell editor host/commands/status.
- Risks: mất encoding/CDATA; overwrite thay đổi ngoài; IME tiếng Việt.
- Testing: byte round-trip, crash recovery, simultaneous external edit, invalid XML, large file, split synchronization.
- DoD: double-click XML vào IDE không gọi FCodeViewer; save/reopen đúng nội dung; dirty close/cancel/undo chuẩn; diagnostics clickable; editor không block UI khi parse.

### P3 — Workspace, SQL execution và snippet MVP

- Goal: làm việc project ERP cơ bản với query thật và snippet import.
- Tasks: project importer preview, Explorer file/ERP metadata read-only ban đầu, search workspace, connection profiles, async query selected/all, multi-result/messages/cancel/export CSV, snippet CRUD/import/export.
- Dependencies: P2; config/snippet contract; SQL Server test target xác định version/auth.
- Create: Workspaces, Sql, Snippets, import reports/integration fixtures.
- Change: Shell DB panel, result grid, search/settings, command contexts.
- Risks: target DB sai, batch/session semantics, config collision, secret leak.
- Testing: null/decimal/date/binary, GO trong string/comment, temp tables/transaction, cancellation, auth failure; snippet caret/Unicode; import twice.
- DoD: workflow open project → edit XML → run SQL → view results → save hoạt động; 12 embedded snippets import đủ và nguyên body; profiles không lưu plain secrets; lỗi có thông tin và không mất buffer.

### P4 — Developer tools

- Goal: compare, format, translation, decrypt, profiler theo contract đã đóng.
- Tasks: shared compare engine, folder diff, Google Dịch service/settings (cơ chế truy cập sẽ xác minh khi triển khai), decrypt adapters, profiler provider/event grid với mapping SSMS 18/22, query history/parameters/schema completion.
- Dependencies: P3; host protocol/decrypt baseline; mapping SSMS 18/22 và phiên bản SQL Server/quyền profiler.
- Create: tool domain services/views, provider adapters, fixtures.
- Change: Tools registry, Sql metadata services và capability checks.
- Risks: profiler không tương đương trace cũ; decrypt/translation phụ thuộc service ngoài.
- Testing: text whitespace modes, folder encodings, network timeout, permission denied, SQL object kinds, disconnect/cancel.
- DoD: mỗi tool có fixture parity pass; profiler hiển thị events native đúng filters với mapping được kiểm thử riêng cho SSMS 18 và SSMS 22; không thay thuật toán decrypt khi chưa hiểu. Tool chưa đạt gate chưa được công bố thay thế legacy.

### P5 — ERP workflow parity

- Goal: giữ nghiệp vụ sinh code, update, report, database và các tiện ích hiện có.
- Tasks: port từng wizard cùng fields/design; voucher/list/report/retrieve; DB/data compare/admin; Excel, Crystal/FEC, e-invoice, mail, NoteX/library/commander; viewer-specific navigation/preview.
- Dependencies: P4 và golden fixture từng ERP/version; Crystal external launcher và data compatibility PoC.
- Create: Erp module, optional LegacyBridge, fixtures per workflow.
- Change: tool registrations, project metadata, settings importer.
- Risks: đây là phase chi phí cao nhất; schema/version/template không đầy đủ; x86/runtime.
- Testing: input/output exact + semantic SQL/XML comparison, rollback trên test DB, report render/data binding, package replay trên sandbox.
- DoD: từng dòng inventory đang dùng có test và mapping mới hoặc quyết định giữ legacy rõ ràng; không đánh dấu parity nếu còn chức năng thiếu. Stub chỉ implement nếu có đặc tả.

### P6 — AI agent

- Goal: agent nhiều provider có context và permission thật.
- Tasks: đọc docs provider hiện hành, adapters, chat stream/cancel, context inspector, patch review, permission broker, file/command/SQL tools có audit.
- Dependencies: P3 trở lên, document versions và security gateway; credentials người dùng cấu hình riêng.
- Create: Agents, AgentAdapters, protocol fixtures và security tests.
- Change: right panel, command registry, settings, recovery integration.
- Risks: protocol churn, stale patch, prompt injection, upload ngoài dự kiến.
- Testing: giả lập provider/tool requests, denied permissions, path escape/junction, race writes, secret redaction, cancel/timeouts; smoke với provider thật khi được cấu hình.
- DoD: thêm provider không sửa Core; context hiển thị đúng; deny chặn được ở service; patch review/undo có version; không tự chạy command/SQL vượt scope.

### P7 — Advanced platform

- Goal: terminal thực, source control tùy chọn, extensions và hiệu năng ổn định.
- Tasks: ConPTY host, process lifecycle, Git provider, out-of-process extension host nếu cần, advanced search/replace/index, query plan viewer.
- Dependencies: P1–P6 contracts ổn định; đánh giá nhu cầu plugin thật.
- Create: Terminal, Extensions và benchmark fixtures.
- Change: Shell bottom panel, security broker, diagnostic pipeline.
- Risks: terminal handles/process leaks, protocol boundary, huge results.
- Testing: interactive shell/resize/Unicode/cancel/dispose; untrusted extension; stress search/result paging.
- DoD: không giả terminal bằng textbox output; plugin lỗi không làm mất document; đạt performance budgets đã thống nhất.

### P8 — Migration và rollout

- Goal: chuyển sử dụng có rollback, giữ toàn bộ dữ liệu.
- Tasks: read-only legacy import, schema upgrades, package installer/uninstaller, runbook, pilot với workflows thực; đối chiếu coverage inventory.
- Dependencies: phases cần cho scope pilot pass; full replacement chỉ sau parity P5.
- Create: installer, compatibility report, user guide, rollback checklist.
- Change: release configuration/version, migration mappings theo dữ liệu pilot.
- Risks: profile khác máy, permission thư mục/registry, thiếu runtime/library.
- Testing: clean Windows install, upgrade/reinstall, offline launch, import twice, rollback, user acceptance trên project copy.
- DoD: user xác nhận workflows; số snippet/config/artifacts khớp; legacy giữ nguyên; không bug mất dữ liệu còn mở; tài liệu nêu rõ phần chưa hỗ trợ.

## 24. Risks & Mitigations

| Risk | Cách xử lý / điều kiện chặn |
|---|---|
| Source dịch ngược/làm rối | Không copy nguyên control; source dịch ngược + contract + golden tests; ghi uncertainty |
| Dependency và FCodeViewer dịch ngược | Source đã có; cần phân tích body/call chain và đối chiếu binary, không coi việc nhận source là đã đạt parity |
| XML ERP khác XML chuẩn | Fixtures entities/includes/CDATA; parser policy theo project, save buffer nguyên bản |
| Decrypt có side effects | Nhánh lỗi local có `Dbs.Execute` rồi retry; chưa rõ SQL, không chạy tự động; gate protocol/permission |
| Profiler/Crystal/legacy x86 | PoC trước; helper process chỉ khi cần, Crystal được mở ứng dụng riêng theo yêu cầu; SQL/XML vẫn native trong IDE; không hứa in-process compatibility |
| Schema DB/version ERP khác nhau | Adapter theo version và snapshot metadata; không tự migrate DB |
| Tính năng ngầm trong QueryDefine/Cmd/Sec | Inventory data-driven commands và dependency source; không chỉ migrate menu thấy được |
| Security/credentials | Secret store, grants scope hẹp, no automatic upload/execute, redacted logs |
| Scope lớn | MVP là pilot có giới hạn; full replacement là milestone riêng, không âm thầm bỏ tool |

## 25. Testing Strategy

Unit tests tập trung logic có nguy cơ: encoding/atomic-save/conflict, batch parser, snippet conversion, config merge, versioned edits, permission canonicalization. Golden tests cho generator/formatter/import/export bằng fixture có nguồn gốc rõ. Integration tests chạy SQL Server test DB có versions được chấp nhận, translation/decrypt contract mock trước và môi trường test sau.

Desktop smoke/acceptance: file open/save/dirty close, split, keyboard/IME, result grid, search navigation, theme/high contrast và diagnostics. Test negative cho malformed XML, file access denied/locked, disconnect, cancellation, giant query result, credential expiration. Không dùng production DB làm test hoặc chạy các snippets embedded chỉ để “xác nhận hoạt động”.

Trước release phải có regression matrix nối feature ID → fixture → expected output → result → known differences. Không gọi một module “ported” chỉ vì giao diện đã xuất hiện.

## 26. Performance Strategy

Đề xuất budgets cho pilot, cần chốt máy tham chiếu: cold start ≤3 giây; mở file text 1 MB ≤500 ms sau warm-up; phản hồi gõ p95 ≤50 ms; search 20.000 file text ≤5 giây trên SSD local với dataset ghi rõ. Đây là tiêu chí mục tiêu, chưa phải số đo.

Lazy tree, virtualized lists/grids, background parse/index, debounce 250–400 ms, cancel stale requests; cap context/result rows và streaming export. Large file mode tắt một số analysis/folding có thông báo; không đọc mọi file vào RAM. SQL execute không materialize kết quả không giới hạn thành DataSet như hướng cũ; paging/bounded streaming và fetch thêm chủ động. Log rotation, cache invalidation, dispose handles/subscriptions trên close; benchmark memory sau mở/đóng nhiều tab.

## 27. Definition of Done tổng thể

IDE mới chỉ được coi là **thay thế Fcode** khi mọi workflow thực sự được sử dụng trong inventory có implementation/test hoặc quyết định scope được người dùng chấp nhận; các khoảng trống source không bị che bằng giả định. XML/SQL mở/sửa trực tiếp; project/config/snippet không mất dữ liệu; query và tool giữ behavior đã cam kết; themes/commands/search hoạt động; agent quyền được enforce; package cài được trên máy sạch; recovery/rollback được kiểm chứng.

Done của bản plan khác Done của sản phẩm: plan này cung cấp inventory bằng chứng tĩnh, kiến trúc, migration, phases và danh sách cần xác minh. Đã có executable pilot mới; chưa build legacy, chưa thử database/API hoặc chạy ứng dụng cũ.

## 28. Recommended MVP

**MVP để dùng thử:** shell WPF C#, Dark/Light/High Contrast, Explorer, project profiles/import preview, tabs/split, command palette/shortcuts/settings; SQL/XML/JS/JSON/text native với safe save/recovery; XML diagnostics/outline/format; SQL selected/all execution, multi-result/messages/cancel/CSV; workspace search; snippet legacy import + CRUD; text compare; Output/Problems/logs.

Giữ kiến trúc sẵn cho tools/agents nhưng không tạo placeholder đánh lừa là chức năng đã chạy. AI, full XML designer, profiler native, decrypt/translation parity, Crystal/ERP generators và full deployment là milestones sau MVP. Chúng vẫn nằm trong scope thay thế cuối cùng; trong thời gian pilot vẫn sử dụng Fcode cho workflow chưa migrate.

Thứ tự code sau duyệt: giải quyết gates tối thiểu P0 → shell P1 → native file/XML editor P2 → SQL/project/snippet P3 → compare + đóng gói bản pilot. Không chờ đủ mọi source legacy mới được tạo shell, nhưng không viết lại nghiệp vụ chưa hiểu.

## 29. Recommended New IDE Blueprint

```text
Người dùng
  → LS Code WPF Shell (commands, keyboard, themes, panels)
  → Workspace / ProjectContext / ConnectionProfile
  → Explorer (files + ERP metadata) / Search
  → DocumentService (buffer/version/encoding/recovery)
      ├─ SQL editor → language service → execution session → results
      ├─ XML editor → diagnostics / outline / XPath / ERP references
      ├─ JS / JSON / Text editors
      └─ split views dùng chung buffer
  → ToolRegistry
      ├─ Compare / Formatting / Snippets
      ├─ Decrypt / Translation / Profiler providers
      ├─ ERP generators / Reports / Database tools / Deployment
      │    └─ Crystal Reports → ứng dụng riêng qua launcher
      └─ Notes / Files / Mail / EInvoice
  → AgentHost → provider adapters
      └─ ContextBuilder → PermissionBroker → reviewed edits / tools
  → Infrastructure (settings, credentials, logs, filesystem, processes)
```

Kiến trúc đã được duyệt: **C# WPF/.NET 10, editor native AvalonEdit qua PoC, modular monolith, MVP như mục 28 và migration song song theo các gates.** Implementation đang thực hiện tại workspace `D:\FAST\Code\LTHO\KappaCode`; không chỉnh chương trình Fcode hiện tại.
