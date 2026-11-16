# LSCode tự chứa danh mục chuẩn và Dynamic Template Studio

Trạng thái: đã triển khai ngày 16/11/2026.

Kết quả triển khai:

- Kết nối danh mục chuẩn được đóng gói thành embedded resource của `LSCode.Infrastructure`; runtime không đọc source FSD Studio.
- `DynamicTemplateStudio.Core` và `DynamicTemplateStudio.Infrastructure` nằm trong `src/` và là project của `LSCode.sln`.
- WebView chỉ còn bốn thao tác **Đồng bộ**, **Tạo profile**, **Import XML**, **Export XML**; luồng đồng bộ web.config thủ công đã bị xóa.
- Import/export dùng file picker và `XmlReader`/`XmlWriter`; XML catalog không đi qua textarea hoặc JSON bridge.
- Modal dự án có chiều cao ổn định; danh sách cuộn bên trong.

## 1. Mục tiêu

Sau nâng cấp, checkout và bản build LSCode phải hoạt động độc lập với hai đường dẫn ngoài:

- Không cần `D:\FAST\Code\LTHO\FSDStudioSource\FSDStudio\clsDatabase.cs` để đồng bộ danh mục dự án.
- Không cần `D:\FAST\Code\LTHO\New\lthoToolApi` để build hoặc sử dụng tab Dynamic Template Studio.

Màn **Dự án của bạn** chỉ giữ bốn thao tác ở đầu danh sách: **Đồng bộ**, **Tạo profile**, **Import XML**, **Export XML**. Bỏ hoàn toàn thao tác thủ công **Đồng bộ web.config**. Chiều cao modal ổn định, không phụ thuộc số lượng dự án.

Import/export catalog lớn phải xử lý qua file và stream ở .NET; không chuyển toàn bộ XML vào state React hoặc textarea WebView.

## 2. Hiện trạng trước khi triển khai

### 2.1 Đồng bộ danh mục

`StandardProjectSync.ReadFsdConnection(sourceFile)` hiện mở `clsDatabase.cs`, dùng regex lấy `cVal`, giải mã Base64/UTF-32 rồi tạo `SqlConnectionStringBuilder`. UI hardcode đường dẫn file nguồn FSD và gửi `sourceFile` trong mỗi lệnh `project.standard.*`.

Hệ quả: máy không có checkout FSD Studio không thể đồng bộ dù LSCode đã được build đầy đủ.

### 2.2 Dynamic Template Studio

`LSCode.Infrastructure.csproj` hiện khai báo:

```xml
<TemplateStudioRoot>../../../New/lthoToolApi</TemplateStudioRoot>
<ProjectReference Include="$(TemplateStudioRoot)/src/DynamicTemplateStudio.Infrastructure/DynamicTemplateStudio.Infrastructure.csproj" />
```

Tab React của LSCode gọi trực tiếp model và dịch vụ trong `DynamicTemplateStudio.Core` và `DynamicTemplateStudio.Infrastructure`. Hai project cần khoảng 66 file nguồn; WPF App có thêm 14 file. Bản publish có DLL nên có thể chạy, nhưng restore/build từ checkout sạch vẫn phụ thuộc repo ngoài.

### 2.3 Import/export

`ProjectImporter.Read` tải toàn bộ XML vào `XDocument`; `Export` dựng một `XDocument` mới chứa toàn bộ catalog. Bridge trả chuỗi XML lớn sang JavaScript, React giữ chuỗi trong state và render vào textarea. Import preview còn trả toàn bộ `ProjectInfo[]`.

Với vài nghìn dự án, cùng một dữ liệu bị giữ nhiều bản trong bộ nhớ .NET, JSON bridge, state React và DOM textarea. Đây là nguyên nhân chính khiến import/export chậm và giao diện đứng.

### 2.4 Chiều cao modal

`.modal` chỉ có `max-height`; `.project-dialog-body` cũng chỉ có `max-height`. Không phần tử nào đặt `height` cố định, nên dialog co theo nội dung. Danh sách có hai dự án sẽ làm cửa sổ thấp hơn danh sách dài.

## 3. Đưa kết nối danh mục chuẩn vào LSCode

### 3.1 Phạm vi cần mang sang

Không sao chép toàn bộ `clsDatabase` cũ vì lớp này chứa nhiều trách nhiệm và phụ thuộc riêng của FSD Studio. Chỉ mang phần cần cho chức năng danh mục chuẩn:

1. Dữ liệu kết nối đang nằm trong `cVal`.
2. Quy tắc giải mã tương thích.
3. Cấu hình timeout, encryption compatibility và application name.
4. Lệnh stored procedure `dbo.ns_getListDept @Name`.

Tạo `IStandardCatalogConnectionProvider` và implementation nội bộ `EmbeddedStandardCatalogConnectionProvider` trong Infrastructure. `StandardProjectSync.ReadAsync` chỉ nhận `currentUser` và `CancellationToken`; không còn nhận đường dẫn file.

```text
ProjectDialog
   └─ project.standard.sync(currentUser)
         └─ StandardProjectSync
               └─ IStandardCatalogConnectionProvider
                     └─ embedded resource trong LSCode
```

### 3.2 Lưu dữ liệu kết nối

- Đặt payload tương thích trong embedded resource của `LSCode.Infrastructure`, kèm version và checksum; không ghi chuỗi kết nối đã giải mã vào source UI, log, exception, telemetry hoặc kết quả bridge.
- Chỉ giải mã trong bộ nhớ ngay trước khi mở `SqlConnection`; xóa tham chiếu sau khi dispose connection.
- Tài khoản danh mục nên chỉ có quyền kết nối DB chuẩn và chạy `ns_getListDept`. Nếu tài khoản hiện tại có quyền ghi, tạo tài khoản read-only trước khi phát hành rộng.
- Base64/UTF-32 của FSD chỉ là mã hóa định dạng, không phải cơ chế bảo mật. Embedded resource vẫn có thể bị trích từ binary; mức bảo vệ tương đương ứng dụng FSD hiện tại. Rủi ro này phải được chấp nhận hoặc thay bằng cấp credential qua Windows Credential Manager trong một đợt riêng.

### 3.3 CurrUser

Chuyển `CurrUser` từ `localStorage` sang `StudioSettings` để thuộc cấu hình LSCode, được đọc ở cả native host và WebView. Lần đầu chưa có giá trị thì mở form nhập; các lần sau bấm **Đồng bộ** là chạy ngay. Cho phép bấm nhãn `FSD: <user>` để đổi tên người dùng.

### 3.4 Thay đổi và dọn dẹp

- Bỏ `sourceFile` khỏi payload `project.standard.preview/sync/apply`.
- Xóa regex đọc source C# và kiểm tra file nguồn FSD.
- Xóa đường dẫn hardcode khỏi `ProjectDialog` và `StandardProjectSyncTool`.
- Thay test đọc file FSD thật bằng fixture provider giả; thêm integration test tùy chọn cho DB thật qua biến môi trường, không chứa credential.
- Build và chạy trên máy đã đổi tên hoặc không có thư mục FSDStudioSource.

## 4. Đưa Dynamic Template Studio vào solution LSCode

### 4.1 Cấu trúc đích

```text
LSCode/
├─ src/
│  ├─ DynamicTemplateStudio.Core/
│  ├─ DynamicTemplateStudio.Infrastructure/
│  ├─ LSCode.Infrastructure/
│  └─ LSCode.Web/
├─ tests/
│  └─ Fixtures/DynamicTemplateStudio/
└─ docs/upstream/DynamicTemplateStudio/
```

Import source của `Core` và `Infrastructure` vào repo LSCode và thêm trực tiếp vào solution. Tab WebView là giao diện chính, nên LSCode không cần EXE WPF để chạy chức năng. Source WPF App chỉ giữ dưới `tools/` nếu còn dùng làm chuẩn đối chiếu; không là dependency của build hoặc runtime LSCode.

Chép cùng các tài liệu hợp đồng cần thiết: `requirements.md`, `ui-spec.md`, `engine-contract.md`, format project và sample tối thiểu dùng cho test. Ghi revision nguồn vào `docs/upstream/DynamicTemplateStudio/ORIGIN.md` và giữ license/notice của dự án nguồn.

### 4.2 Project reference

Thay:

```xml
<ProjectReference Include="$(TemplateStudioRoot)/src/DynamicTemplateStudio.Infrastructure/...csproj" />
```

bằng reference tương đối trong cùng repo:

```xml
<ProjectReference Include="../DynamicTemplateStudio.Infrastructure/DynamicTemplateStudio.Infrastructure.csproj" />
```

Xóa property `TemplateStudioRoot` và mọi hướng dẫn yêu cầu checkout `../New/lthoToolApi`. Build CI chỉ checkout LSCode.

### 4.3 Nguyên tắc đồng bộ source về sau

- LSCode sở hữu bản source đã import; thay đổi phục vụ LSCode thực hiện trực tiếp tại đây.
- Nếu cần lấy bản mới từ repo cũ, dùng một script kiểm kê/copy có manifest và review diff, không dùng ProjectReference ngoài repo.
- Không copy `bin`, `obj`, `.vs`, publish output hoặc secrets.
- Kiểm thử tương đương: mở project, sửa field, kéo sắp xếp, quan hệ cha–con, đồng bộ khóa, preview JSON, test mapping, lưu/xuất riêng template và recovery.

## 5. Bỏ đồng bộ thủ công theo web.config

### 5.1 Giao diện

Xóa nút **Đồng bộ web.config**, mode `sync-web`, import và render `SyncTool` trong `ProjectDialog`. Thanh hành động còn:

```text
[Đồng bộ] [Tạo profile] [Import XML] [Export XML]              FSD: LONGLD
```

### 5.2 Backend

- Xóa command WebView `project.discover` và `project.sync` nếu kiểm tra caller cho thấy chỉ `SyncTool` dùng.
- Xóa `SyncTool.tsx` và `StandardProjectSyncTool.tsx` cũ sau khi chuyển hết luồng đồng bộ vào `ProjectDialog`.
- Giữ `ProjectDiscovery.ReadConnections` và `ProjectDatabase`: khi mở một dự án đã đồng bộ, LSCode vẫn cần đọc App/SYS từ `WebConfigPath`. Đây là đọc cấu hình của profile hiện có, không phải chức năng đồng bộ thủ công.
- Luồng WPF legacy `MainWindow.Sync` cần đánh dấu bỏ hoặc xóa riêng sau khi xác nhận không còn là entry point phát hành.

## 6. Import/export catalog lớn

### 6.1 UX mới

**Import XML** mở file picker `.xml`. Native host parse file, trả về một summary nhỏ:

```text
2.998 dự án hợp lệ · 35 dự án mới · 2.940 trùng · 23 lỗi
[Hủy] [Cập nhật dự án trùng] [Import]
```

Chỉ hiển thị tối đa 100 lỗi hoặc mã trùng đầu tiên, có phân trang nếu cần. Không nạp nội dung XML vào textarea.

**Export XML** mở Save As, ghi trực tiếp ra file và báo `Đã xuất 2.998 dự án`. Không mở màn textarea và không gửi XML qua JSON bridge. Thao tác sao chép XML chỉ giữ cho metadata của một profile, không dùng cho toàn catalog.

### 6.2 API đề xuất

| Command | Input | Output |
|---|---|---|
| `project.import.choose` | rỗng | đường dẫn được chọn hoặc null |
| `project.import.previewFile` | path | counts + tối đa 100 vấn đề |
| `project.import.applyFile` | path, overwrite | added/updated/skipped + catalog revision |
| `project.export.choose` | tên mặc định | đường dẫn được chọn hoặc null |
| `project.export.file` | path | projectCount + byteCount |

Mở rộng `IDesktopDialogs` bằng lời gọi Open/Save có filter `LSCode Project Catalog (*.xml)|*.xml` hoặc thêm command bridge chuyên biệt trong Desktop host. Xác thực đường dẫn tuyệt đối và extension trước khi đọc/ghi.

### 6.3 Xử lý dữ liệu

- Thêm `ProjectImporter.ReadAsync(Stream, CancellationToken)` dùng `XmlReader` với DTD bị cấm, giới hạn độ sâu/kích thước và yield từng `<project>`.
- Thêm `ProjectImporter.WriteAsync(Stream, IEnumerable<LegacyProject>, CancellationToken)` dùng `XmlWriter`; sanitize trường nhạy cảm khi ghi từng project.
- Không tạo `XDocument` chứa cả catalog. Không serialize XML lớn qua WebView bridge.
- Lập dictionary mã dự án một lần rồi merge O(n), lưu catalog đúng một lần.
- Import/export chạy ngoài UI thread, có progress theo số project/byte và hỗ trợ Dừng.
- Ghi export vào file `.tmp` cùng thư mục rồi replace nguyên tử khi hoàn tất; hủy/lỗi phải xóa file tạm và giữ file đích cũ.
- Sau import chỉ gửi summary và revision; tải lại danh sách đúng một lần. Không trả cả preview list và catalog trong cùng response.

### 6.4 Mục tiêu hiệu năng

Đo trên fixture 10.000 project có metadata tương đương dữ liệu thật:

- UI vẫn phản hồi khi import/export; thao tác Dừng hoạt động.
- Peak managed memory nhỏ hơn 2 lần kích thước file XML cộng kích thước catalog đang lưu.
- Bridge không có message XML lớn hơn 1 MB.
- Danh sách sau import chỉ render 50 dòng/trang như hiện tại.
- Ghi catalog và refresh danh sách mỗi thao tác đúng một lần.

## 7. Chiều cao cố định của cửa sổ dự án

Ảnh đính kèm được xem là ảnh hiện trạng. Lỗi đến từ việc modal và body chỉ có `max-height`, nên chiều cao thực tế chạy theo nội dung.

Mở rộng `Modal` nhận `className` hoặc variant riêng. Với cửa sổ dự án:

```css
.modal.project-catalog-modal {
  height: min(680px, calc(100vh - 40px));
  display: grid;
  grid-template-rows: auto minmax(0, 1fr);
  overflow: hidden;
}

.project-dialog-body {
  height: 100%;
  min-height: 0;
}

.project-catalog {
  min-height: 0;
  overflow: auto;
}
```

Chiều cao modal không đổi giữa các trạng thái 0, 1, 2, 50 hoặc 3.000 dự án. Danh sách chiếm phần còn lại và cuộn bên trong. Khi màn hình thấp, `calc(100vh - 40px)` quyết định chiều cao; dưới 720 px chiều rộng, modal dùng `height: calc(100vh - 24px)` và giữ padding hiện có.

Không áp chiều cao này cho mọi modal vì form nhỏ và hộp xác nhận không cần cao. Chỉ gắn class cho `ProjectDialog`.

## 8. Bố cục màn dự án sau nâng cấp

```text
┌────────────────────────────────────────────────────────────────────┐
│ Dự án của bạn                                                   × │
├────────────────────────────────────────────────────────────────────┤
│ [Đồng bộ] [Tạo profile] [Import XML] [Export XML]       FSD:LONGLD │
│ [ Tìm mã, tên, đường dẫn, server hoặc database        ]  2 dự án │
│ ────────────────────────────────────────────────────────────────── │
│ SONGBEHRM   CÔNG TY TNHH SÂN GOLF PALM ...              →   🗑  │
│              \\172...\CustomerPro\HRM\SONGBEHRM\SP24S\          │
│ ────────────────────────────────────────────────────────────────── │
│ SONGBE      Công ty TNHH Sân Golf Palm ...              →   🗑  │
│              \\172...\CustomerPro\FBO\SONGBE\R2SP2254\          │
│                                                                    │
│                         vùng trống cố định                         │
└────────────────────────────────────────────────────────────────────┘
```

Giữ phong cách flat, theme sáng/tối và component hiện có. Mã dự án, tên và đường dẫn giữ thứ bậc hiện tại. Việc thay đổi chỉ gồm bỏ một nút và cố định khung; không thêm section mới.

## 9. Trình tự triển khai

1. Import Dynamic Template Studio Core/Infrastructure, tài liệu hợp đồng và fixture vào repo; sửa ProjectReference; build trên checkout không có repo ngoài.
2. Tạo connection provider tự chứa và bỏ `sourceFile` khỏi toàn bộ luồng đồng bộ; chuyển CurrUser sang settings.
3. Bỏ UI/command đồng bộ thủ công web.config, dọn component không còn caller.
4. Thêm file dialog command và importer/exporter streaming; đổi UI sang chọn file + summary.
5. Thêm variant modal dự án và CSS chiều cao cố định; kiểm tra 0/2/50/3.000 project.
6. Cập nhật tài liệu cài đặt, build và release; xóa hướng dẫn cần FSDStudioSource/lthoToolApi.

Mỗi bước phải build độc lập. Bước 1 và 2 cần hoàn thành trước khi xóa đường dẫn ngoài để luôn giữ solution ở trạng thái build được.

## 10. Kiểm thử nghiệm thu

### Tự chứa

- Đổi tên hoặc tháo `FSDStudioSource` và `New/lthoToolApi`; restore, build, test, publish LSCode vẫn thành công.
- Trên máy chỉ có bản publish LSCode, đồng bộ danh mục và mở Dynamic Template Studio đều chạy.
- Không có đường dẫn `D:\FAST\...` trong source build/runtime hoặc UI.

### Đồng bộ

- CurrUser chưa có: nhập một lần rồi chạy; đã có: một lần bấm.
- DB lỗi, timeout và Dừng không làm hỏng catalog hiện tại.
- Log/exception/export không chứa connection string hoặc password.

### Import/export

- Round-trip 0, 1, 3.000 và 10.000 project; metadata được giữ, trường nhạy cảm bị loại khi export.
- File XML hỏng, DTD, project trùng mã, file rất lớn, hủy giữa chừng và đường dẫn chỉ đọc có thông báo rõ.
- Export lỗi không ghi đè file cũ; không còn textarea chứa toàn catalog.
- Đo thời gian, allocation và kích thước bridge message; lưu số đo trong báo cáo test.

### UI

- Modal giữ cùng chiều cao với 0, 2, 50 và 3.000 project.
- Danh sách cuộn trong khung; header, action và tìm kiếm không trôi.
- Không cuộn ngang ở 375 px; kiểm tra theme Light, Dark và High Contrast.
- Bốn nút hành động có trạng thái hover, focus, disabled và loading; không còn nút hoặc route web.config thủ công.

### Dynamic Template Studio

- Mở/tạo project, thêm template, chỉnh/kéo field, sửa khóa cha–con, preview, mapping, lưu/xuất template và project.
- Đóng/mở lại giữ đúng dữ liệu và recovery; test không dùng sample nằm ngoài repo.

## 11. Ngoài phạm vi đợt này

- Không đổi schema project của Dynamic Template Studio.
- Không viết lại UI Studio lần nữa.
- Không xóa khả năng đọc `web.config` khi mở profile đã có.
- Không thay cơ chế DPAPI của catalog LSCode.
- Không thêm cơ chế cập nhật tự động từ repo Dynamic Template Studio cũ.

