# Tư liệu nguồn

## Khảo sát Fcode

- [Kiểm kê ban đầu](fcode/source-manifest.csv)
- [Kiểm kê dependency và viewer bổ sung](fcode/source-supplement-manifest.csv)
- [Chỉ mục bằng chứng theo tệp/dòng](fcode/source-evidence.md)
- [Chỉ mục API theo module](fcode/module-api-index.txt)

Khảo sát Create List ngày 19/11/2026 trên mã nguồn cục bộ `D:\FAST\Code\LTHO\Fcode`. `FCode/frmMain.cs` (khoảng dòng 3501) đặt chức năng dưới Design, `FCode/clsMain.cs` (279–288) mở một tab. `FCode/urCreateList.cs` (1599–2020, 2110–2164) điều phối thông tin master/detail, khai báo cột, Grid, Filter, Import, bố cục và menu. `FCode/urCreateListGenerate.cs` (280–355, 367–631, 788–856) đọc mẫu từ thư mục cài đặt, thay nội dung theo cấu hình rồi ghi file. Tên template và nhiều chuỗi giao diện bị làm rối trong bản mã nguồn đã khảo sát. Phạm vi đối chiếu là đọc mã, chưa chạy bản cài Fcode. Thiết kế chuyển sang LSCode xem [Tạo danh mục](../docs/specifications/CATALOG-TABLE-DESIGNER.vi.md#sinh-source-erp-trong-webview--19112026).

## Dynamic Template Studio

[Bản sao tài liệu upstream](upstream/DynamicTemplateStudio/README.md) gồm yêu cầu gốc, quyết định, các change request và bằng chứng triển khai trước khi tích hợp. Xem [nguồn gốc](upstream/DynamicTemplateStudio/ORIGIN.md).
