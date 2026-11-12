# Cài đặt LSCode WebView trên Windows x64

1. Đóng các cửa sổ LSCode đang chạy và mở `LSCode-WebView-Setup-x64.exe`. Khi tạo bộ cài từ mã nguồn, file nằm trong `artifacts/installer/`.
2. Chọn thư mục cài đặt rồi bấm **Install**. Mặc định ứng dụng được cài cho tài khoản Windows hiện tại tại `%LOCALAPPDATA%\Programs\LSCode WebView`. Có thể chọn tạo biểu tượng Desktop trong trình cài đặt.
3. Mở **LSCode WebView** từ menu Start, rồi chọn profile dự án hoặc đồng bộ dự án từ `web.config`.

Bộ cài đã kèm .NET Desktop Runtime. Nếu máy thiếu Microsoft Edge WebView2 Runtime, bộ cài dùng Evergreen Bootstrapper và cần Internet để tải runtime. Có thể cài **Evergreen Standalone Installer x64** trước nếu máy cài đặt không có Internet. [Tải WebView2 từ Microsoft](https://developer.microsoft.com/microsoft-edge/webview2/#download-section)

Để cập nhật, chạy bộ cài mới sau khi đóng ứng dụng. Hồ sơ dự án và thiết lập nằm tại `%LOCALAPPDATA%\LSCode`. Để gỡ, vào **Settings → Apps → Installed apps → LSCode WebView → Uninstall**.

## Tạo lại bộ cài từ mã nguồn

Chuẩn bị .NET SDK, Node.js và Inno Setup. Đặt Microsoft WebView2 Evergreen Bootstrapper tại `artifacts/installer-tools/MicrosoftEdgeWebview2Setup.exe`. Từ thư mục gốc của repo, chạy các lệnh sau. `ISCC.exe` cần có trong PATH hoặc được gọi bằng đường dẫn đầy đủ.

```powershell
npm.cmd --prefix src/LSCode.Web ci
npm.cmd --prefix src/LSCode.Web run build
dotnet publish src/LSCode.Desktop/LSCode.Desktop.csproj -c Release -r win-x64 --self-contained true -o artifacts/installer-publish
ISCC.exe scripts/LSCode.WebView.iss
```

[Tải Inno Setup](https://jrsoftware.org/isdl.php) · [Hướng dẫn build WebView](WEBVIEW-RUNNING.vi.md)
