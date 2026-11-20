# LTHO Studio Code (LSCode)

IDE Windows cho việc phát triển và bảo trì dự án FAST ERP. Giao diện chính dùng React, TypeScript và Monaco Editor trong host WPF/WebView2 trên .NET 10. Solution cũng giữ phiên bản WPF/AvalonEdit đời trước.

## Chạy và cài đặt

- Nếu đã có bản publish, mở `artifacts/publish-webview/LSCode.WebView.exe` và giữ các tệp đi kèm. Bản publish mặc định là Windows x64 self-contained nên không cần cài .NET 10 Desktop Runtime; máy chạy vẫn cần Microsoft Edge WebView2 Runtime.
- Từ thư mục gốc, dùng .NET 10 SDK và Node.js để build bản WebView bằng `./build-webview.ps1 -Publish`.
- Xem cách tạo và cài bộ cài Windows x64 trong [hướng dẫn cài đặt](docs/guides/CAI-DAT-LSCODE.vi.md).
- Khi mở app, chọn profile hoặc bấm **Đồng bộ** trong **Dự án của bạn**. Màn này còn có **Tạo profile**, **Import XML** và **Export XML**. Để thử editor mà không cần dữ liệu ERP, mở [ERP-Demo](samples/ERP-Demo/README.md).

## Tài liệu

- [Hướng dẫn WebView và kiểm chứng](docs/guides/WEBVIEW-RUNNING.vi.md)
- [Bản đồ repo và quy tắc lưu tài liệu](MAP.md)
- [Mục tiêu và phạm vi](SPEC.md) · [Kế hoạch và nghiệm thu](PLAN.md)
- [Chỉ mục tài liệu theo nhóm](docs/README.md) · [Tư liệu nguồn](sources/README.md)
