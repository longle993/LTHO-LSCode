# Chẩn đoán mẫu setSalesServiceInvoice

Đối chiếu template đính kèm với payload ngày 04/11/2026:

1. Payload chưa là JSON hợp lệ: dấu ngoặc kép cong ở Description/JobID, dấu chấm phẩy sau Description. Dùng dấu `"` và dấu phẩy.
2. Tên property phải khớp chính xác: `InvoiceID ` → `InvoiceID`, `TotalDisscountAmount ` → `TotalDisscountAmount`, `TotalDisscountAmount _NT` → `TotalDisscountAmount_NT`. Giữ cách viết Disscount theo template hiện có.
3. Master `from` phải là `$.data[*]`; detail `from` là `$.detail[*]` theo context từng master. `$` hiện tại lấy toàn envelope rồi lấy nguyên master làm detail.
4. Master TotalNetAmount_NT đang trỏ nhầm `$.TotalNetAmount`; sửa thành `$.TotalNetAmount_NT`.
5. Detail có nhiều dòng cùng InvoiceID: không dùng InvoiceID làm khóa duy nhất của detail. Bộ mẫu sửa đánh dấu InvoiceID và RefNumber cùng `key:true` để kiểm tra duy nhất theo cặp, vẫn giữ parentKey InvoiceID → master.InvoiceID. RefNumber phải duy nhất trong mỗi invoice.

Mẫu sửa tại `examples/sales-service-invoice/`: template JSON Preview và payload Test Mapping. Kiểm tra bằng MappingPreviewService thực tế; không xác nhận runtime SQL Engine bên ngoài.

Kết quả: regression test chạy serializer + MappingPreviewService cho ra **1 master, 2 detail, không có lỗi**. InvoiceID/VoucherNo được kế thừa đúng, TotalNetAmount_NT bằng 0. Toàn bộ **155 tests pass**. Import `template.json`, dán `sample.json` vào Test Mapping rồi Run Preview. Không thay đổi dữ liệu project đang mở.
