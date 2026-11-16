# Clean Architecture và MediatR trong LSCode

## Luồng thực thi

```text
LSCode.Web
    │ JSON bridge
    ▼
LSCode.Desktop (Presentation / composition root)
    │ ISender.Send(ExecuteStudioCommand)
    ▼
LSCode.Application
    ├─ Commands/ExecuteStudioCommand
    ├─ Handlers/ExecuteStudioCommandHandler
    └─ Abstractions/
       ├─ IDocumentWorkspace
       ├─ IDeveloperTools
       └─ IDesktopDialogs
           │
           ▼
LSCode.Infrastructure
    ├─ DocumentWorkspace : IDocumentWorkspace
    ├─ DeveloperTools : IDeveloperTools
    └─ DependencyInjection
```

Dependency chỉ đi vào trong: Desktop tham chiếu Application và Infrastructure; Infrastructure tham chiếu Application; Application không tham chiếu Infrastructure hoặc Desktop; Domain không phụ thuộc các lớp bên ngoài.

## Trách nhiệm từng lớp

- **Domain** giữ model và quy tắc lõi, không biết MediatR, UI hay SQL.
- **Application** nhận command, chứa handler use case và khai báo các port cần dùng. `ExecuteStudioCommandHandler` giữ nguyên protocol method/payload hiện tại để WebView không phải đổi đồng thời.
- **Infrastructure** hiện thực IO, SQL, file system, profiler và developer tools. `AddLSCodeInfrastructure` là nơi ánh xạ interface Application sang implementation.
- **Desktop** là composition root. Mỗi cửa sổ có một service provider riêng, đăng ký Application, Infrastructure và dialog adapter gắn với owner window; khi đóng cửa sổ, provider được dispose bất đồng bộ để dừng profiler và giải phóng tài nguyên.

## Đăng ký dịch vụ

```csharp
var services = new ServiceCollection();
services.AddLogging();
services.AddLSCodeApplication();
services.AddLSCodeInfrastructure();
services.AddSingleton<IDesktopDialogs>(new DesktopDialogs(window));
await using var provider = services.BuildServiceProvider();
```

Application dùng MediatR 14.3.0 và tự quét handler trong assembly Application. Presentation chỉ phụ thuộc `ISender`; không khởi tạo `DeveloperTools` hoặc `DocumentWorkspace` trực tiếp.

## Thêm use case mới

1. Tạo command/query trong `LSCode.Application/Commands`.
2. Tạo handler trong `LSCode.Application/Handlers` và chỉ phụ thuộc abstraction hoặc domain.
3. Nếu cần IO, khai báo port trong `LSCode.Application/Abstractions`, hiện thực trong Infrastructure và đăng ký tại `AddLSCodeInfrastructure`.
4. Presentation chuyển input thành command rồi gọi `ISender.Send`.
5. Giữ DTO transport trong Contracts khi dữ liệu phải dùng chung giữa WebView/Desktop; không để Infrastructure type rò sang Application.

## Tương thích

Tên method bridge, JSON payload, kết quả trả về, cancellation token và vòng đời `DeveloperTools` được giữ nguyên. Các thao tác rename và developer tools vẫn chạy nền như dispatcher cũ.
