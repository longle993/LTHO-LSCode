# Hợp đồng DSL và mapping

## CR05: Studio allowed-values extension

Text columns may include `"allowedValues": ["1", "2", "0"]`. An absent/empty list is unrestricted. Studio validates converted non-null values with exact, case-sensitive comparison, including defaults, constants and inherited business keys. Draft metadata preserves editable CSV; runtime JSON stores an array only. This is a Studio extension: external Engine enforcement has not been confirmed.

**Chưa có source/schema hoặc bộ fixture chuẩn của Dynamic API Engine trong workspace.** Ví dụ trong prompt là baseline để lập kế hoạch, chưa đủ chứng minh mọi tính năng tương thích. Không tự công bố một schema mở rộng là schema chính thức.

## Phần đã được mô tả

| Thành phần | Wire shape theo prompt | Ngữ nghĩa |
|---|---|---|
| Template | `template`, `version` khi có, `tables`, `execute` | Code và procedure được cấu hình |
| Table | `name`, `from`, `mode`, `rowKey`, `parent` khi có | Tên tạm có một `#`, Object/Array, nguồn relative |
| Columns | Object `columns` keyed bằng tên field | Mỗi value có `path`, `type`, `key` khi dùng |
| Parent Key | `parentKey: {strategy,column,references}` | Ví dụ một cặp, rowKey hoặc businessKey |
| Project | `projectName`, `description`, `schemaVersion`, `templates` | templates là danh sách code, không phải DTO nhúng |

Baseline đầy đủ nằm ở prompt §11. `required`, `default`, `references` và description field có yêu cầu về model, nhưng chi tiết wire semantics phải xác minh với Engine. Không dùng một sample thiếu trường để kết luận Engine hỗ trợ mọi thuộc tính mở rộng.

## Ngữ nghĩa mapping phải giữ

| Cú pháp | Context cần dùng |
|---|---|
| Root table `from: $.orders[*]` | JSON root đầu vào |
| Child table `from: $.details[*]` | Object của **record cha hiện tại**, không JSON root |
| Field `$.field` | Object của record hiện tại |
| `@parent.field` | Object JSON của cha trực tiếp |
| `@ancestor(2).field` | Object JSON của ông/bà, hai cấp cha |
| `@#Header.OrderNo` | Giá trị **đã mapping** của Header trong ancestor chain hiện tại |
| Constant | Giá trị typed độc lập input; wire shape chưa định nghĩa |
| Default | Fallback khi thiếu/null theo policy cần chốt; không tự đồng nhất missing với null |

Các prefix `@...` là DSL riêng. Parser phải nhận diện chúng trước khi chuyển phần path còn lại vào evaluator; không quảng cáo là JSONPath chuẩn đầy đủ.

Đề xuất subset đầu tiên: `$`, `.property`, `['property']` cho tên đặc biệt, `[index]`, `[*]`. Filter, recursive descent, functions và union không mặc nhiên hỗ trợ. Input ngoài subset báo “Unsupported expression” với vị trí; tập cú pháp cuối phải khớp Engine.

## Thuật toán Mapping Preview

1. Validate graph, names/types và dependency; chặn chu trình trước khi duyệt input.
2. Tạo snapshot template/sample và context stack. Duyệt root tables, sau đó child tables theo mỗi record cha. Không dùng điều kiện hardcode theo số cấp.
3. Resolve `from` theo context. Object/Array sai hình dạng phát sinh diagnostic có input location; chính sách missing/null cần C03.
4. Tạo record output; nếu bật RowKey thì cấp `long` từ bộ đếm riêng của table trong lần chạy này. RowKey được cấp trước khi đi xuống con.
5. Map các field với nguồn JSON/current/ancestor hoặc mapped ancestor; validate convert/required/default theo policy.
6. Gán Parent Key từ **record cha hiện hành**. Không lookup parent chỉ bằng business ID.
7. Kiểm key null/uniqueness trên phạm vi đã chốt; ghi lỗi không âm thầm bỏ record.
8. Duyệt con với context chứa input object, mapped values, table ID, record ordinal, RowKey và parent context.
9. Trả kết quả theo table, diagnostics và revision. Cancel/truncated phải hiện rõ, không báo full validation passed.

Không giới hạn nghiệp vụ ở 2 hoặc 4 cấp. Vẫn cần giới hạn tài nguyên cấu hình (độ sâu parse, số record, kích thước file) để tránh cạn bộ nhớ; vượt giới hạn báo lỗi rõ, không cắt im lặng. Đề xuất dùng stack duyệt tường minh cho hierarchy sâu.

## Keys và quan hệ

CR04 bổ sung: child phải có cột cho từng Business Key của parent, kể cả khi join bằng RowKey. Cột mặc định dùng `@#Parent.KeyField`, cùng type/length, Required và không tự tick Key. Business-key pairs phải trỏ đến cột child thực sự tồn tại; technical Parent RowKey tiếp tục do parentKey sinh. Không đưa field thường trùng tên cột kỹ thuật vào runtime. Xem [CR04](change-request-04.md).

- `rowKey` default false. `_RowKey` là bigint duy nhất trong table và một run, không phải Business ID.
- Child có `rowKey:false` vẫn được nhận `_HeaderRowKey` khi parent có RowKey.
- Theo yêu cầu cập nhật: khi parent bật RowKey, Studio tự đặt rowKey strategy, references=_RowKey và child column `_<Parent>RowKey`. Nếu parent tắt RowKey, chuyển businessKey và cho chọn key trong dropdown. Thao tác người dùng bật/tắt parent và các cập nhật quan hệ thuộc cùng lịch sử undo.
- Composite business key cần đủ cặp, không trùng cột, type tương thích. Ngữ nghĩa uniqueness toàn bảng hay theo parent chưa rõ.
- Cấm user field đụng `_RowKey` và tên technical column được relationship sinh; cần danh sách reserved của Engine.
- Rename thay references trên model đã parse, không find/replace chuỗi tùy ý. Xử lý cả parent, key pairs, field references và named ancestor tokens; nếu gặp extension chưa hiểu có tham chiếu tiềm ẩn thì cảnh báo/chặn mutation gây mất nghĩa.

## Identifier và type policy

Normalization table: trim ngoài → bỏ tối đa một `#` đầu → validate → thêm đúng một `#` khi serialize; `##abc` phải lỗi, không trim hết dấu #. Đề xuất tên ngắn ASCII `[A-Za-z_][A-Za-z0-9_]*` và so trùng không phân biệt hoa thường, nhưng giới hạn chiều dài/case/collation cần chốt với Engine. Không tự suy ra giới hạn từ tên SQL chung.

Procedure chỉ nhận schema và procedure identifier đã validate; đề xuất `schema.name`, không nhận statement, `;`, comment hoặc expression. Hỗ trợ bracket/Unicode/ba phần nếu cần phải nằm trong contract, không nới parser tự do.

Theo yêu cầu cập nhật, lựa chọn mới là SQL char/varchar/nvarchar/smalldatetime/bit/numeric/int; numeric mặc định numeric(19,5), string length trong type declaration như varchar(50). Các type string/bigint/decimal/bool/datetime/date/guid cũ vẫn được đọc và giữ khi round-trip. Property editor chuyển tên thuần VoucherID thành $.VoucherID; không đổi nghĩa DSL nhập sẵn. Xem [change-request-02](change-request-02.md) cho normalization và parent RowKey tự động. Mọi lỗi chuyển kiểu giữ nguyên input, không ép sai dữ liệu thành zero/null.

## Các điểm phải chốt bằng Engine/fixture

| ID | Thiếu gì | Tác động và xử lý trước khi chốt |
|---|---|---|
| C01 | JSON shape composite parentKey và field references | Domain giữ danh sách cặp; chưa phát minh runtime JSON nhiều cặp |
| C02 | Constant/default/required/string length và transaction wire format | UI/spec có yêu cầu; chưa export shape đoán |
| C03 | Missing vs null, Object/Array rỗng/sai dạng, conversion và default precedence | Preview cần policy; fixture phải phân biệt từng trường hợp |
| C04 | Key uniqueness scope, case/collation, type compatibility | Không tự cho rằng key luôn global hoặc parent-scoped |
| C05 | JSONPath subset và ancestor edge cases, mapped dependencies | Không đưa thư viện JSONPath toàn phần thay thế DSL riêng |
| C06 | Identifier limits/reserved names/field rename semantics | Chặn expression; chốt limits trước validator runtime |
| C07 | Engine chấp nhận unknown property/version, vị trí metadata | Giữ unknown khi round-trip; không đánh dấu hỗ trợ nếu không hiểu |

Điều kiện xác nhận tương thích: có version Engine được nhắm tới, schema hoặc parser contract, bộ input-template-expected-output; chạy parity tests với artifact chuẩn. Trong lúc chờ vẫn triển khai shell/domain/persistence abstraction, nhưng đánh dấu vùng chưa hỗ trợ và không quảng cáo export những vùng đó là Engine-compatible.
