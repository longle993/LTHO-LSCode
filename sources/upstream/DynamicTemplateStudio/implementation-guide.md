# Hướng dẫn bắt đầu triển khai

## Thứ tự đọc bắt buộc

1. `README.md` và `AGENTS.md` để biết trạng thái repo.
2. `brief/product-brief.md` và `brief/ux-brief.md` để hiểu ai dùng, việc chính và giới hạn.
3. `docs/requirements.md` và `docs/decisions.md` để biết điều gì bắt buộc, điều gì chưa chốt.
4. `docs/implementation-plan.md` để chọn phase; không tự coi phase trước đã hoàn tất.
5. Tài liệu theo task: UI → `ui-spec.md`; model/mapping → `architecture.md`, `engine-contract.md`; file → `persistence.md`; validation nghiệm thu → `testing.md`.
6. Đối chiếu mục nguồn trong `docs/source/original-prompt.md` nếu diễn giải chưa rõ.

## Trước khi viết code

- Audit lại workspace và môi trường thực tế: repo có thể đã thay đổi kể từ bộ tài liệu này.
- Kiểm `dotnet --info`, SDK .NET 10 và khả năng build WPF trên Windows; chưa có thì báo điều kiện thiếu cụ thể.
- Tra tài liệu chính thức để chốt package/API đang dùng, pin version; không cài frontend stack vì ví dụ skill dùng web.
- Kiểm trạng thái review UI. Khi áp dụng ui-ux để dựng màn mới, thực hiện U1/U2 và các gate còn thiếu; không tự ghi Approved.
- Xác định contract nào đủ evidence, feature nào cần để trạng thái unsupported cho tới khi xác minh.

## Mẫu giao việc

```text
Task: P2-02 — Repository lưu project an toàn
Đọc: brief/product-brief.md; docs/architecture.md;
      docs/persistence.md; docs/testing.md; docs/decisions.md
Yêu cầu: R03, R16
Phụ thuộc: P2-01 serializer và typed model đã hoàn tất
Đầu ra: repository, transaction/recovery, lỗi có cấu trúc
Kiểm chứng: save/reopen, failure injection, nguồn cũ còn nguyên khi lỗi
Không thuộc task: SQL execution, cloud sync, thay DSL chưa được chốt
```

## Trong khi làm

Implement theo một lát cắt chạy thật: model/service → test → ViewModel/command → UI → chạy flow. Không tô hết màn rồi để button trống. Service không gọi MessageBox; View nhận lỗi có cấu trúc và hiển thị. Giữ một nguồn model cho designer/preview/export.

Không log sample JSON đầy đủ mặc định; lỗi dùng file/table/field/location để tránh đưa dữ liệu nghiệp vụ vào log không cần thiết. Không thêm kết nối SQL/API khi task là Test Mapping cục bộ.

Mỗi thay đổi hợp đồng/UX/persistence phải cập nhật nguồn đặc tả tương ứng, không ghi các quy tắc mới rải trong README. Chỉ thêm dependency khi có nhu cầu cụ thể và đã kiểm tương thích.

## Trước khi kết thúc task

- Chạy build và tests thích hợp cho hành vi vừa đổi; báo commands/results thật.
- Nếu đổi UI: chạy WPF thật, xem các state liên quan ở Light/Dark và DPI/kích thước cần thiết. Web probe không thay native verification.
- Kiểm save/export/reopen khi động vào domain/serializer/persistence.
- Ghi yêu cầu nào hoàn tất, phần nào unsupported/not run, quyết định nào còn mở và bước kế tiếp.
- Cập nhật trạng thái phase dựa trên evidence, không dựa trên số file tạo được.

## Prompt dùng cho lượt triển khai sau

> Triển khai Dynamic Template Studio theo AGENTS.md. Đọc README, hai brief và docs/implementation-guide.md trước; kiểm trạng thái hiện tại rồi bắt đầu task đầu tiên đủ phụ thuộc trong docs/implementation-plan.md. Giữ WPF/.NET 10, không thay runtime DSL bằng phỏng đoán. Với quyết định chưa chốt, làm tiếp phần độc lập và nêu rõ điểm cần xác minh. Mỗi phase phải build/test và demo flow đã triển khai, cập nhật docs cùng kết quả thật.
