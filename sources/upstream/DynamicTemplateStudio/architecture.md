# Kiến trúc đề xuất

## Solution và chiều phụ thuộc

```text
DynamicTemplateStudio.sln
src/
  DynamicTemplateStudio.App/
    Views/ ViewModels/ Controls/ Styles/ Converters/ Resources/
  DynamicTemplateStudio.Core/
    Models/ Enums/ Interfaces/ Validation/ Services/
  DynamicTemplateStudio.Infrastructure/
    FileSystem/ Serialization/ JsonPath/ ProjectPersistence/
tests/
  DynamicTemplateStudio.Tests/
    Unit/ Integration/ Fixtures/
```

App → Core; Infrastructure → Core; App tham chiếu Infrastructure chỉ ở composition root để đăng ký DI. Core không phụ thuộc WPF, filesystem cụ thể hoặc DI container. Tests tham chiếu Core/Infrastructure; kiểm thử WPF cần môi trường Windows/STA phù hợp.

- **App:** XAML, ViewModel dùng CommunityToolkit.Mvvm, commands, selection, dialog/navigation, Dispatcher và resource dictionaries. Hosting quản lý vòng đời dịch vụ; đóng app phải giải quyết pending writes trước khi dừng host.
- **Core:** domain models, validation, mapping context, graph operations, editing commands/undo, interface persistence và serializer.
- **Infrastructure:** UTF-8 JSON DTO/serializer bằng System.Text.Json, file I/O, manifest, export staging/recovery, parser/evaluator JSONPath theo subset được chốt.
- **Tests:** fixture và expected output không cần SQL Server, failure injection cho persistence, acceptance automation nếu phù hợp.

Đã restore/build và pin package version trong project và lock files; xem README. AvalonEdit đã dùng cho read-only JSON highlighting. SQLite chưa có nhu cầu; filesystem là mặc định. Khi đổi package tiếp tục xác minh tương thích trước khi cập nhật.

## Ba lớp dữ liệu

1. **Runtime DTO:** bám DSL Engine, trường JSON có đúng tên/casing, không chứa selection, pane width hay ID UI.
2. **Domain:** ProjectDefinition → TemplateDefinition → danh sách TableDefinition → danh sách ColumnDefinition. Dùng danh sách có thứ tự; `columns` ở runtime là object nên serializer phải chủ động giữ thứ tự ghi.
3. **Editor state:** stable IDs, selected table/field, dirty revision, undo history, lỗi nhập chưa commit, trạng thái panel. Display name/description/timestamps không có hợp đồng runtime phải ở metadata thiết kế.

ParentKey trong domain nên hỗ trợ danh sách cặp mapping ngay từ đầu; codec runtime cho một cặp theo ví dụ hiện có. Không tự chọn JSON shape nhiều cặp trước khi giải quyết C01 trong hợp đồng Engine.

Mapping source nên là discriminated model (Current/Parent/Ancestor/NamedAncestor/Constant) thay vì UI tự nối chuỗi rải rác. Default là fallback có ngữ nghĩa riêng, không thay thế mapping source ngầm định.

Stable ID giúp rename không làm mất selection và references. Khi đọc runtime, resolve tên thành ID; khi export chuyển ID về tên đã normalize. Unknown JSON properties được giữ bằng vùng extension data tại từng cấp DTO; không dùng Dictionary/dynamic thay toàn bộ domain.

## Dịch vụ và trách nhiệm

| Interface dự kiến | Trách nhiệm |
|---|---|
| IProjectRepository | Open/save project, manifest và metadata; trả diagnostics, không hiển thị dialog |
| ITemplateSerializer | DTO ↔ domain, preserve extension data và thứ tự, format preview |
| ITemplateValidator | Syntax/structural/semantic validation; không phụ thuộc View |
| IMappingPreviewService | Map snapshot + sample, cancellation, kết quả theo bảng và record |
| ISampleInferenceService | Đề xuất bảng/field/type, cảnh báo mơ hồ, không tự sửa project |
| IProjectExportService | Validate snapshot, dựng export package, commit/recovery |
| IUndoRedoService | Nhóm thao tác semantic thành undo unit; restore graph nhất quán |
| IAutosaveCoordinator | Debounce, revision queue, serialized writes, failure state |

Tên là đề xuất kiến trúc, chưa phải API đã tồn tại. Khi triển khai mỗi service cần contract input/output/error rõ ràng, tránh interface chung làm tất cả.

## Luồng state

UI edit → validate input → semantic command → domain revision tăng → dirty → cập nhật diagnostics/preview từ snapshot → autosave queue. Save thành công chỉ đánh dấu sạch revision đã ghi; sửa mới hơn trong lúc ghi vẫn dirty.

Undo/redo cũng là mutation, tăng revision; không ghi ngược một snapshot cũ vào disk chỉ vì stack index trùng saved index. Rename table/field và sửa reference là một transaction undo. Confirm Generate Tables là một transaction. Cancel editor không tạo undo entry.

Mapping/serialization dài chạy ngoài UI thread trên immutable snapshot; kết quả gửi về Dispatcher theo revision. Kết quả cũ được bỏ hoặc ghi “Out of date”; không ghi đè model mới. I/O async, CancellationToken đi xuyên service. ObservableCollection chỉ cập nhật trên UI thread; tránh Task.Run chạm binding objects.

## Dữ liệu ngoài runtime

Đề xuất `.studio/` chứa metadata và draft/recovery bên cạnh project. Đây là phần mở rộng cần kiểm tra Engine có bỏ qua được hay không. Project export dùng để sửa tiếp phải mang metadata thiết kế; template export runtime chỉ mang DSL. Không có `.studio/` vẫn phải mở được template và khôi phục tất cả dữ liệu runtime.

Settings theo máy như recent paths/theme/window size để trong user profile, không ghi absolute path cá nhân vào runtime. Đường dẫn metadata chỉ tương đối trong project; không tin tên template như một path tùy ý.
