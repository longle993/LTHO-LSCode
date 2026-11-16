# Save, export và phục hồi

## Cấu trúc portable

```text
SalesManagement/
  project.json
  templates/order.import.json
  samples/order.import.sample.json
  .studio/                         Đề xuất; cần chốt policy metadata
    metadata.json
    drafts/
    recovery/
```

`project.json` bám ví dụ prompt §3.2; templates liệt kê code không đuôi `.json`. Runtime templates không chứa trạng thái UI. Samples là tùy chọn. `.studio/` không phải nguồn bắt buộc để đọc lại runtime; mô tả/display name/timestamps không có wire field phải được giữ ở metadata để project round-trip đầy đủ.

Tên project hiển thị không phải quyền tạo đường dẫn tùy ý. Template code cần policy filename Windows: không separator, traversal, reserved filename hoặc collision khác hoa/thường. Resolve/canonicalize đường dẫn cuối và kiểm vẫn nằm trong project; kiểm reparse points/symlink trước ghi/xóa. Không tin path do manifest nhập vào.

## Open

1. Đọc manifest, kiểm schemaVersion và danh sách code trùng.
2. Resolve từng template; thu lỗi missing/invalid JSON/unsupported version theo file.
3. Parse DTO, preserve extension data, tạo domain rồi resolve references.
4. Validate graph/semantic, restore metadata nếu có. Metadata thiếu không được chặn việc mở runtime hợp lệ.
5. Giữ original bytes/snapshot để recovery và compare; không rewrite file chỉ vì vừa mở.
6. File hỏng/không hiểu mở trong trạng thái an toàn, cho sửa qua import có kiểm tra; không autosave đè lên nguồn chưa parse được.

Project có một template lỗi vẫn có thể cho xem các template đọc được cùng bảng lỗi. Không bỏ template lỗi khỏi manifest ngầm định khi Save All.

## Save và autosave

Phân biệt **draft của editor**, **file đã lưu**, **runtime export hợp lệ**. Đề xuất draft riêng trong `.studio/drafts/` cho cấu hình còn lỗi; file runtime chỉ thay khi validate thành công. UI phải nói “Draft saved — validation errors remain”, không chỉ “Saved” gây hiểu nhầm đã sẵn sàng chạy Engine.

Autosave debounce đề xuất 800 ms sau thao tác đã commit; giá trị cấu hình cần đo. Gõ trong cell chưa commit chưa ghi domain. Mỗi project có write queue tuần tự, gộp revision còn chờ. Manual Save flush revision hiện hành; Save All xử lý cả manifest/metadata cần đổi.

Safe write từng file:

1. Chụp snapshot + revision + hash nguồn dự kiến.
2. Serialize UTF-8 vào temp file trên cùng volume/thư mục đích, flush và parse/validate lại.
3. Kiểm file đích chưa đổi ngoài ứng dụng. Có xung đột: giữ bản của người dùng, cho Reload / Save Copy / giải quyết overwrite rõ ràng.
4. Replace/move theo khả năng filesystem, giữ backup có recovery policy. Nếu replace lỗi, không xóa file đích còn tốt.
5. Chỉ báo thành công sau commit; nếu model đã đổi tiếp thì vẫn dirty.

Nhiều file không tự trở thành atomic khi từng file replace atomic. Create/rename/delete/save all cần journal hoặc staging transaction: ghi đủ nội dung mới → lưu journal có transaction ID và checksum → commit file theo kế hoạch, manifest sau cùng → đánh dấu complete. Khi restart phát hiện journal dở, đề xuất rollback/complete dựa trên trạng thái thực, không tự chọn bản không xác định. Fault injection phải bao phủ từng bước.

Đóng app khi còn thay đổi: hiện Save / Discard / Cancel ngay. Dừng debounce khi bắt đầu đóng. Save chờ ghi xong trước khi đóng; Discard đóng cửa sổ ngay và không khởi động lượt ghi mới. Một transaction đã bắt đầu ghi vẫn hoàn tất an toàn trước khi process thoát; không rollback một autosave đã bắt đầu chỉ vì chọn Discard. Cancel giữ cửa sổ và tiếp tục debounce. Host shutdown chạy bất đồng bộ sau khi cửa sổ đóng, không chặn UI thread. Draft phục hồi chỉ restore khi người dùng chọn, không tự ghi đè nguồn.

## Export

- Current Template: một JSON runtime hợp lệ.
- All Templates: toàn bộ runtime templates hợp lệ trong project.
- Project Folder: manifest + templates + samples + metadata được phép, đủ để mở lại chỉnh sửa.
- Draft export, nếu triển khai, dùng thư mục/đuôi và nhãn riêng, không trộn vào `templates/` runtime.

Luôn hiện absolute destination trước khi ghi. Nếu thư mục được chọn chính là project đích, dùng trực tiếp; nếu là container, tạo một thư mục project con. Không suy luận chỉ bằng chuỗi basename: hiển thị đường dẫn cuối cho người dùng thấy. Không tự lồng `SalesManagement/SalesManagement`.

Validate toàn snapshot trước, export sang staging cùng volume rồi commit; file đích có sẵn phải có kế hoạch collision rõ. Copy project phải kiểm đích không nằm trong nguồn để tránh tự copy đệ quy. Export quay lại chính nguồn dùng save transaction, không ghi chồng trong lúc đang đọc.

## Rename, duplicate, delete

Rename template cập nhật filename, manifest, sample và metadata trong một transaction; rename project cần tách tên hiển thị và di chuyển folder, không âm thầm move folder. Duplicate tạo bản độc lập, kiểm code/tên/path collision. Xóa project phải hiện folder chính xác, số template và phạm vi xóa; chỉ xóa tài nguyên project được chọn, ưu tiên khả năng phục hồi. Không coi “Remove from recent” là “Delete Project”.

## Round-trip

So sánh semantic: tables/columns đúng thứ tự, mọi runtime property hợp lệ và extension giữ nguyên giá trị, null/absent không bị đổi vô cớ. Formatting có thể đổi. Khi gặp property chưa hiểu: giữ lại, báo giới hạn khả năng chỉnh/preview; không âm thầm loại bỏ rồi nói đã bảo toàn.
