# Kiến trúc LSCode

| Điểm vào | Trách nhiệm |
| --- | --- |
| [LSCode.Web](../src/LSCode.Web/) | React/TypeScript, Monaco và component theo tính năng trong `src/features`. |
| [Desktop/StudioWindow.cs](../src/LSCode.Desktop/StudioWindow.cs) | Host WPF/WebView2, bridge và vòng đời cửa sổ. |
| [Contracts](../src/LSCode.Contracts/) | Giao thức thông điệp qua bridge. |
| [Application](../src/LSCode.Application/) | Lệnh MediatR, handler và các interface ứng dụng. |
| [Domain](../src/LSCode.Domain/) | Kiểu và quy tắc domain. |
| [Infrastructure](../src/LSCode.Infrastructure/) | File, SQL, catalog, Source Collection, Advance Note và adapter Template Studio. Tái sử dụng Core và một số source qua Compile Link. |
| [Core](../src/LSCode.Core/), [Shell](../src/LSCode.Shell/), [TraceHost](../src/LSCode.TraceHost/) | Logic dùng chung, ứng dụng WPF trước WebView và tiến trình Trace. |
| [DynamicTemplateStudio.Core](../src/DynamicTemplateStudio.Core/), [DynamicTemplateStudio.Infrastructure](../src/DynamicTemplateStudio.Infrastructure/) | Model, validation, mapping và persistence của Studio, được build trong repo. |

Chi tiết luồng request ở [Clean Architecture và MediatR](../docs/architecture/CLEAN-ARCHITECTURE-MEDIATR.vi.md).
