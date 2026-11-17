# PROMPT: XÂY DỰNG ỨNG DỤNG WPF DYNAMIC API TEMPLATE DESIGNER

## 1. Vai trò và mục tiêu

Bạn là Senior .NET Desktop Architect, chuyên về WPF, MVVM, UX/UI và xây dựng các ứng dụng thiết kế cấu hình trực quan.

Hãy xây dựng một ứng dụng Windows Desktop hoàn chỉnh có tên **Dynamic Template Studio**, sử dụng C# và WPF.

Ứng dụng dùng để thiết kế, chỉnh sửa, quản lý và xuất các JSON Template phục vụ Dynamic API Engine. Template định nghĩa cách mapping JSON đầu vào thành các SQL Server Temporary Table, sau đó gọi Stored Procedure xử lý nghiệp vụ.

**Yêu cầu quan trọng nhất:** Người dùng thiết kế template bằng giao diện trực quan giống Lark Base/Airtable, không cần chỉnh sửa JSON bằng tay.

Ứng dụng phải chạy được thực tế, không chỉ tạo mockup hoặc giao diện demo.

## 2. Công nghệ

Sử dụng:

- C# và .NET 10 (WPF).
- WPF + XAML.
- MVVM với CommunityToolkit.Mvvm.
- Microsoft.Extensions.DependencyInjection.
- Microsoft.Extensions.Hosting.
- System.Text.Json.
- JSON Schema validation hoặc hệ thống validation tương đương.
- AvalonEdit cho JSON Editor nếu cần.
- SQLite chỉ dùng nếu thật sự cần lưu metadata/cache; ưu tiên filesystem để dễ di chuyển dự án.
- Fluent Design hoặc Modern WPF styling.

Không sử dụng WinForms, Electron, Blazor Hybrid hoặc web application.

Tổ chức solution thành các project:

```text
DynamicTemplateStudio.sln
│
├── DynamicTemplateStudio.App
│   ├── Views
│   ├── ViewModels
│   ├── Controls
│   ├── Styles
│   ├── Converters
│   └── Resources
│
├── DynamicTemplateStudio.Core
│   ├── Models
│   ├── Enums
│   ├── Interfaces
│   ├── Validation
│   └── Services
│
├── DynamicTemplateStudio.Infrastructure
│   ├── FileSystem
│   ├── Serialization
│   ├── JsonPath
│   └── ProjectPersistence
│
└── DynamicTemplateStudio.Tests
```

Áp dụng SOLID, dependency injection, async/await và separation of concerns.

---

# 3. Quản lý dự án

Ứng dụng phải hỗ trợ nhiều dự án.

Mỗi Project chứa nhiều Template. Mỗi Template chứa nhiều bảng tạm.

## 3.1. Màn hình Projects

Hiển thị danh sách dự án dạng card hoặc table, bao gồm:

- Project Name
- Description
- Location
- Number of Templates
- Last Modified
- Open Project

Có các thao tác:

- New Project
- Open Project Folder
- Recent Projects
- Rename Project
- Duplicate Project
- Delete Project (phải xác nhận)
- Export Project

Khi tạo Project, người dùng nhập tên, mô tả và chọn vị trí lưu.

Khi chọn Open Project Folder, ứng dụng đọc toàn bộ cấu trúc dự án và khôi phục các template vào giao diện chỉnh sửa.

Không cần cơ sở dữ liệu riêng để mở lại các dự án đã export.

## 3.2. Cấu trúc thư mục kết xuất

Ví dụ dự án có tên `SalesManagement`.

```text
SalesManagement/
│
├── project.json
│
├── templates/
│   ├── order.import.json
│   ├── order.update.json
│   ├── customer.import.json
│   └── stock.transfer.json
│
└── samples/
    ├── order.import.sample.json
    └── customer.import.sample.json
```

`project.json` chứa metadata của dự án:

```json
{
  "projectName": "SalesManagement",
  "description": "Sales API templates",
  "schemaVersion": 1,
  "templates": [
    "order.import",
    "order.update",
    "customer.import",
    "stock.transfer"
  ]
}
```

Các file trong `templates` phải là JSON template có thể dùng trực tiếp bởi Dynamic API Engine.

Khi Open Project Folder:

1. Kiểm tra file project.json.
2. Đọc danh sách template.
3. Kiểm tra file template tồn tại.
4. Parse JSON và validate schema.
5. Khôi phục toàn bộ bảng, cột, mapping và quan hệ.
6. Hiển thị lỗi rõ ràng nếu template không hợp lệ.
7. Cho phép chỉnh sửa và lưu lại.

Không tự ý ghi đè hoặc sửa dữ liệu hỏng mà chưa được người dùng đồng ý.

---

# 4. Thiết kế giao diện giống Lark Base

Đây là phần ưu tiên cao nhất của ứng dụng.

Giao diện cần hiện đại, tối giản, nhiều khoảng trắng, thao tác nhanh như Lark Base, Airtable hoặc Notion Database.

Không sử dụng DataGrid mặc định với giao diện Windows cổ điển.

Có thể sử dụng WPF DataGrid được custom sâu hoặc xây dựng bảng bằng custom control.

## 4.1. Main Layout

Thiết kế giao diện gồm 3 khu vực:

**Sidebar bên trái:**

- Project Name
- Danh sách Template
- Nút `+ New Template`
- Tìm kiếm Template
- Menu ngữ cảnh để Rename, Duplicate, Delete, Export

**Workspace ở giữa:**

- Template Name
- Tab chứa các bảng tạm
- Bảng dữ liệu cấu hình column mapping
- Nút `+` để thêm bảng mới
- Nút `+` ở header để thêm trường mới

**Properties Panel bên phải:**

- Thuộc tính bảng được chọn
- Thuộc tính field được chọn
- JSONPath
- Data Type
- Key
- RowKey
- References
- Validation

Panel bên phải có thể thu gọn.

## 4.2. Mockup bố cục

```text
┌──────────────────────────────────────────────────────────────────────────┐
│ Dynamic Template Studio        SalesManagement       Save   Export       │
├──────────────────────┬───────────────────────────────────────────────────┤
│ PROJECT              │ order.import                                      │
│ SalesManagement      │                                                   │
│                      │ [Header] [Detail] [Serial] [+ Add Table]          │
│ TEMPLATES            ├───────────────────────────────────────────────────┤
│                      │ Table: Header                                     │
│ ● order.import       │ Source: $.orders[*]            RowKey: [✓]        │
│   order.update       │                                                   │
│   customer.import    │ ┌──────────────┬────────────┬──────────┬─────┐    │
│   stock.transfer     │ │ Field        │ JSON Path  │ Type     │ Key │ +  │
│                      │ ├──────────────┼────────────┼──────────┼─────┤    │
│ + New Template       │ │ OrderNo      │ $.orderNo  │ string   │ ✓   │    │
│                      │ │ CustomerID   │ $.customerId│ int     │     │    │
│                      │ │ CreatedDate  │ $.date     │ datetime │     │    │
│                      │ └──────────────┴────────────┴──────────┴─────┘    │
│                      │                                                   │
│                      │ [+ Add Field]                                     │
│                      │                                                   │
│                      │ [+ Add Table]                                     │
│                      │                                                   │
├──────────────────────┴───────────────────────────────────────────────────┤
│ Ready                         4 Templates                  JSON Valid   │
└──────────────────────────────────────────────────────────────────────────┘
```

Mockup chỉ để minh họa layout; ứng dụng thực tế cần thiết kế đẹp và hiện đại hơn.

## 4.3. Yêu cầu UI/UX

- Light Mode mặc định, có Dark Mode.
- Sidebar có thể resize.
- Tabs hỗ trợ kéo thả để sắp xếp.
- Hover highlight.
- Inline editing.
- Context menu.
- Tooltip.
- Keyboard shortcuts.
- Undo/Redo.
- Unsaved changes indicator.
- Autosave có debounce và cơ chế ghi file an toàn.
- Search/filter field.
- Drag-and-drop reorder columns.
- Không khóa UI khi mở dự án lớn.
- Hỗ trợ DPI scaling.
- Ưu tiên hiệu năng khi một template có hàng trăm field.

Không sử dụng MessageBox liên tục cho các thao tác nhỏ; ưu tiên inline validation và toast notification.

---

# 5. Chức năng thiết kế bảng tạm

Mỗi Template có thể chứa N bảng tạm.

Người dùng thêm bảng bằng nút `+ Add Table`.

Khi thêm bảng:

- Nhập Table Name.
- Chọn Parent Table nếu có.
- Khai báo JSONPath nguồn.
- Chọn Mode: Object hoặc Array.
- Bật/tắt RowKey.

## 5.1. Quy tắc Table Name

Người dùng chỉ cần nhập:

`Header`

Hoặc:

`abc`

Ứng dụng tự động hiểu tên SQL Temporary Table là:

`#Header`

Hoặc:

`#abc`

**Không yêu cầu người dùng nhập ký tự `#`.**

Giao diện có thể hiển thị tên ngắn `Header`, nhưng bản JSON export phải sử dụng `#Header`.

Nếu người dùng gõ `#Header`, phải normalize để tránh tạo `##Header`.

Ví dụ:

```text
Input: abc
Normalized: #abc

Input: #abc
Normalized: #abc

Input: ##abc
Result: Validation Error
```

Sử dụng quy tắc đặt tên an toàn:

- Không cho phép tên trống.
- Không cho phép trùng tên bảng trong cùng template.
- Không cho phép dấu cách hoặc ký tự SQL không hợp lệ.
- Không cho phép nhập SQL expression.
- Không cho phép vượt quá giới hạn tên identifier đã quy định.
- Cảnh báo khi rename bảng đang được các bảng con tham chiếu.

Khi rename bảng, ứng dụng phải cập nhật references, parent, join mapping và các tham chiếu theo tên bảng một cách nhất quán.

---

# 6. Thiết kế Field dạng bảng

Người dùng thêm Field bằng dấu `+` tại header của grid hoặc nút `+ Add Field`.

Mỗi field gồm các thuộc tính:

| Property | Description |
|---|---|
| Name | Tên cột SQL |
| Path | JSONPath mapping |
| Type | Kiểu dữ liệu |
| Required | Bắt buộc |
| Key | Business Key |
| Default | Giá trị mặc định |
| References | Tham chiếu bảng khác |
| Description | Mô tả |

Các kiểu dữ liệu hỗ trợ:

- string
- int
- bigint
- decimal(p,s)
- bool
- datetime
- date
- guid

Nên có Type Picker dạng dropdown.

Với decimal, hỗ trợ cấu hình Precision và Scale.

Với string, hỗ trợ Max Length.

Không cho phép tùy ý nhập kiểu SQL ngoài danh sách được Engine hỗ trợ.

## 6.1. Các loại Mapping

Field có thể lấy dữ liệu từ:

- Current JSON object: `$.field`
- Parent JSON object: `@parent.field`
- Ancestor: `@ancestor(2).field`
- Named ancestor mapped value: `@#Header.OrderNo`
- Constant value
- Default value

Cho phép lựa chọn mapping type bằng UI thay vì bắt buộc nhập cú pháp thủ công.

---

# 7. Business Key và RowKey

Đây là tính năng cốt lõi.

Ứng dụng phải hỗ trợ cả Business Key và Generated RowKey.

## 7.1. Business Key

Người dùng tick `Key` ở một hoặc nhiều field.

Ví dụ:

```json
"OrderNo": {
  "path": "$.orderNo",
  "type": "string",
  "key": true
}
```

Nếu nhiều field được đánh dấu Key, Engine hiểu là Composite Business Key theo quy ước schema của template.

UI phải hỗ trợ hiển thị nhiều field được chọn làm khóa.

## 7.2. Generated RowKey

Mỗi bảng có tùy chọn:

`Generate _RowKey`

Khi bật:

```json
"rowKey": true
```

Engine sẽ tự sinh trường `_RowKey` cho mỗi record.

Khi tắt:

```json
"rowKey": false
```

Engine không sinh `_RowKey`.

Mặc định là false.

`_RowKey` là khóa kỹ thuật dùng trong một lần import, không phải Business ID.

Không cho phép người dùng tạo field thường trùng với các cột kỹ thuật do Engine sinh.

## 7.3. Parent Key

Nếu bảng có Parent, ứng dụng hiển thị Relation Editor.

Cho phép chọn:

- Business Key
- Generated RowKey

Ví dụ:

```json
"parentKey": {
  "strategy": "rowKey",
  "column": "_HeaderRowKey",
  "references": "_RowKey"
}
```

Hoặc:

```json
"parentKey": {
  "strategy": "businessKey",
  "column": "OrderNo",
  "references": "OrderNo"
}
```

Trong trường hợp Business Key gồm nhiều cột, cần hỗ trợ mapping nhiều cặp cột.

Nếu người dùng chọn RowKey nhưng bảng cha chưa bật RowKey, hiển thị validation error và nút đề xuất bật RowKey ở bảng cha.

Nếu bảng hiện tại không cần RowKey nhưng cần tham chiếu RowKey của cha, vẫn phải hỗ trợ.

Không được nhầm RowKey của bảng hiện tại với Parent RowKey.

---

# 8. Hỗ trợ JSON nhiều cấp (N-Level Nested JSON)

Đây là yêu cầu bắt buộc.

Ví dụ:

```json
{
  "orders": [
    {
      "orderNo": "SO001",
      "details": [
        {
          "productId": 101,
          "serials": [
            {
              "serialNo": "SN001",
              "attributes": [
                {
                  "name": "Color",
                  "value": "Red"
                }
              ]
            }
          ]
        }
      ]
    }
  ]
}
```

Ứng dụng có thể thiết kế:

```text
Header
  └── Detail
       └── Serial
            └── Attribute
```

Mỗi bảng có Parent Table.

JSONPath của bảng con phải được hiểu tương đối với từng object thuộc bảng cha.

Ví dụ:

```text
Header    → $.orders[*]
Detail    → $.details[*]
Serial    → $.serials[*]
Attribute → $.attributes[*]
```

Nếu không có Parent, `from` được tính từ JSON root.

Cần xây dựng Mapping Context để hỗ trợ truy cập các cấp cha.

Không giới hạn số cấp bằng các điều kiện hardcode.

Phải phát hiện và ngăn vòng lặp Parent Reference.

---

# 9. Relation Designer

Bổ sung một giao diện trực quan để quản lý quan hệ.

Có thể có thêm tab `Relationships` hoặc panel riêng.

Ví dụ:

```text
┌──────────────────┐
│ Header           │
│ Key: OrderNo     │
│ RowKey: Enabled  │
└─────────┬────────┘
          │
          │ _HeaderRowKey
          ▼
┌──────────────────┐
│ Detail           │
│ RowKey: Enabled  │
└─────────┬────────┘
          │
          │ _DetailRowKey
          ▼
┌──────────────────┐
│ Serial           │
│ RowKey: Disabled │
└──────────────────┘
```

Cho phép:

- Chọn parent table.
- Chọn join strategy.
- Mapping foreign key.
- Hiển thị quan hệ 1-N.
- Validate quan hệ.
- Tự cập nhật khi đổi tên field/table.

Relation Designer chỉ tạo metadata, không tự thay đổi JSON input.

---

# 10. Import Sample JSON và Auto Detect

Đây là tính năng quan trọng để thao tác nhanh.

Người dùng có thể:

1. Paste JSON sample.
2. Load JSON sample từ file.
3. Nhấn `Generate Tables`.
4. Ứng dụng phân tích JSON.
5. Nhận diện object và array.
6. Đề xuất bảng tạm.
7. Đề xuất field, JSONPath và data type.
8. Nhận diện cấu trúc cha–con.
9. Cho phép chỉnh sửa trước khi xác nhận.

Ví dụ:

```json
{
  "orders": [
    {
      "orderNo": "SO001",
      "details": [
        {
          "productId": 101,
          "quantity": 2
        }
      ]
    }
  ]
}
```

Ứng dụng đề xuất:

```text
#Orders
    OrderNo

#Details
    ProductID
    Qty
```

Đây chỉ là đề xuất. Người dùng được quyền đổi tên bảng/cột trước khi sinh template.

Không tự động giả định một trường là Business Key nếu không đủ bằng chứng.

Cho phép chọn RowKey cho từng bảng trong bước Generate.

---

# 11. JSON Template Preview

Ứng dụng cần có nút:

`Preview JSON`

Hiển thị JSON template được tạo từ giao diện.

Preview phải cập nhật từ cùng một model đang được chỉnh sửa, không duy trì một nguồn dữ liệu độc lập dễ bị lệch.

Có các chức năng:

- Format JSON
- Syntax Highlighting
- Copy JSON
- Validate JSON
- Export Template
- Compare với bản đã lưu (nếu khả thi)

JSON phải tuân theo cấu trúc DSL sau:

```json
{
  "template": "order.import",
  "tables": [
    {
      "name": "#Header",
      "from": "$.orders[*]",
      "mode": "array",
      "rowKey": true,
      "columns": {
        "OrderNo": {
          "path": "$.orderNo",
          "type": "string",
          "key": true
        },
        "CustomerID": {
          "path": "$.customerId",
          "type": "int"
        }
      }
    },
    {
      "name": "#Detail",
      "parent": "#Header",
      "from": "$.details[*]",
      "mode": "array",
      "rowKey": true,
      "parentKey": {
        "strategy": "rowKey",
        "column": "_HeaderRowKey",
        "references": "_RowKey"
      },
      "columns": {
        "ProductID": {
          "path": "$.productId",
          "type": "int"
        },
        "Qty": {
          "path": "$.quantity",
          "type": "decimal(18,2)"
        }
      }
    }
  ],
  "execute": "dbo.usp_ImportOrder"
}
```

Lưu ý:

- `rowKey` là khóa kỹ thuật được sinh bởi Engine.
- `parentKey` mô tả cách bảng con liên kết với bảng cha.
- `key` là khóa nghiệp vụ của field.
- Các cú pháp `@parent`, `@ancestor` và `@#Table.Column` là DSL riêng, không phải JSONPath chuẩn.

Cần tài liệu hóa ngữ nghĩa của các cú pháp này để JSON export tương thích với API Engine.

---

# 12. Template Settings

Mỗi Template có các thuộc tính:

- Template Code
- Display Name
- Description
- Version
- Stored Procedure
- Transaction Enabled
- Tables
- Created At
- Modified At

Ví dụ:

```json
{
  "template": "order.import",
  "version": 1,
  "execute": "dbo.usp_ImportOrder"
}
```

Trong giao diện có thể nhập:

```text
Procedure: dbo.usp_ImportOrder
```

Lưu ý phải validate tên procedure theo định dạng identifier hợp lệ, không cho phép nhập SQL script tùy ý.

---

# 13. Validation Engine

Cần kiểm tra:

### Project validation

- Project name hợp lệ.
- Không trùng Template Code.
- Không thiếu file template.
- Template JSON hợp lệ.

### Table validation

- Table Name hợp lệ.
- Không trùng Table Name.
- Parent tồn tại.
- Không có vòng lặp parent.
- JSONPath hợp lệ theo DSL được hỗ trợ.

### Field validation

- Field Name hợp lệ.
- Không trùng field trong cùng bảng.
- Data Type hợp lệ.
- JSONPath hợp lệ.
- Key không được null ở các record cần kiểm tra uniqueness.
- Default value đúng kiểu dữ liệu.

### Relationship validation

- Reference Table tồn tại.
- Reference Column tồn tại.
- Data Type tương thích.
- RowKey strategy yêu cầu parent có RowKey.
- Business Key strategy yêu cầu Parent Business Key hợp lệ.
- Composite Key phải mapping đầy đủ các cột.

### Export validation

Không cho phép export template không hợp lệ trừ khi người dùng chọn một cơ chế export draft riêng, không dùng cho runtime.

Hiển thị lỗi chính xác tại table/field tương ứng.

---

# 14. Test Mapping / Data Preview

Cần có tab `Test Mapping`.

Người dùng paste JSON đầu vào, chọn template, nhấn `Run Preview`.

Ứng dụng sẽ:

1. Parse JSON.
2. Duyệt các object/array theo định nghĩa `from`.
3. Mapping field.
4. Sinh RowKey nếu bật.
5. Sinh Parent Key nếu khai báo.
6. Kiểm tra Business Key.
7. Kiểm tra dữ liệu thiếu hoặc sai type.
8. Hiển thị kết quả thành các bảng preview.

Ví dụ:

```text
[Header] [Detail] [Serial]

Header
--------------------------------------------------
_RowKey  OrderNo    CustomerID
1        SO001      1001

Detail
--------------------------------------------------
_RowKey  _HeaderRowKey  ProductID  Qty
1        1              101        2
2        1              102        5
```

Không cần kết nối SQL Server để chạy Test Mapping.

Đây phải là Engine mô phỏng mapping thực tế, không phải hiển thị dữ liệu JSON trực tiếp dưới dạng bảng.

Phải giữ ngữ nghĩa xử lý tương thích với API Engine.

---

# 15. Save, Export, Import và Project Persistence

## Save

- Ctrl+S lưu template hiện tại.
- Ctrl+Shift+S lưu toàn bộ project.
- Đánh dấu trạng thái chưa lưu.
- Hỗ trợ Undo/Redo.
- Khi đóng app, cảnh báo nếu có thay đổi chưa lưu.

## Export

Cho phép:

- Export Current Template
- Export All Templates
- Export Project Folder

Khi export project:

```text
OutputFolder/
    SalesManagement/
        project.json
        templates/
            order.import.json
            order.update.json
            customer.import.json
```

Không tự tạo folder lồng trùng tên project nếu người dùng đã chọn đúng folder đích là thư mục project.

Ghi file theo phương pháp an toàn: tạo file tạm, validate, sau đó thay thế file đích; có cơ chế phục hồi nếu thất bại.

## Open Project Folder

Cho phép mở một project đã export để chỉnh sửa trực tiếp.

Khi chọn folder:

- Đọc project.json.
- Khôi phục templates.
- Khôi phục columns.
- Khôi phục Parent References.
- Khôi phục RowKey Settings.
- Khôi phục Business Keys.
- Khôi phục mappings.
- Khôi phục Stored Procedure Settings.

Sau khi chỉnh sửa, có thể Save hoặc Export lại.

Phải đảm bảo round-trip:

`Template JSON → UI Model → Edit → Template JSON`

Không làm mất các thuộc tính hợp lệ của template.

---

# 16. Gợi ý UI nâng cao

Bổ sung nếu phù hợp:

- Command Palette (Ctrl+K).
- Duplicate Field.
- Duplicate Table.
- Bulk Edit Fields.
- Field Type Icons.
- Sticky Header.
- Template Search.
- Table Search.
- JSONPath Autocomplete.
- Table Reference Dropdown.
- Recent Projects.
- Drag-and-drop Field Order.
- Breadcrumb.
- Status Bar.
- Context Menu.
- Confirmation Dialog.
- Keyboard Navigation.

Các tính năng này không được làm trì hoãn việc hoàn thiện luồng chức năng cốt lõi.

---

# 17. Kiến trúc Data Model

Tạo các model tối thiểu:

```text
ProjectDefinition
    ProjectName
    Description
    SchemaVersion
    Templates[]

TemplateDefinition
    TemplateCode
    Version
    Execute
    Tables[]

TableDefinition
    Name
    Parent
    From
    Mode
    RowKey
    ParentKey
    Columns[]

ColumnDefinition
    Name
    Path
    Type
    Required
    Key
    Default
    References

ParentKeyDefinition
    Strategy
    Column
    References
```

Dùng strongly typed models, không xây dựng toàn bộ ứng dụng dựa trên Dictionary hoặc dynamic object.

Có DTO cho serialization nếu model hiển thị UI khác với model export.

Bảo toàn thứ tự Table và Column khi serialize.

Không serialize các property chỉ dành cho giao diện vào runtime template nếu DSL không định nghĩa chúng.

---

# 18. Quy tắc sinh RowKey

Khi Test Mapping:

- Mỗi bảng có thể có bộ đếm RowKey riêng.
- RowKey chỉ cần duy nhất trong bảng đó và trong một lần mapping.
- Sử dụng kiểu bigint.
- Không phụ thuộc vào ID nghiệp vụ.
- RowKey được sinh trước khi xử lý các bảng con.
- Parent Key tham chiếu chính xác RowKey của record cha hiện tại.
- Không lấy RowKey của một record cha khác chỉ vì Business ID giống nhau.

Ví dụ hai đơn hàng có OrderNo trùng nhau vẫn phải có hai RowKey khác nhau.

---

# 19. Unit Tests

Viết unit tests cho các trường hợp:

1. Create Project.
2. Open Existing Project.
3. Save Template.
4. Export Project.
5. Normalize Table Name.
6. Prevent Duplicate Table Name.
7. Rename Table and Update References.
8. Map JSON Object.
9. Map JSON Array.
10. Nested JSON 2 Levels.
11. Nested JSON 4 Levels.
12. Generate RowKey.
13. Map Parent RowKey.
14. Business Key Mapping.
15. Composite Business Key.
16. Mixed RowKey and Business Key.
17. Missing Required Field.
18. Invalid JSONPath.
19. Invalid Parent Reference.
20. Prevent Cyclic Parent.
21. Validate Invalid Decimal.
22. Preserve Template After Reload.
23. Large JSON Mapping Preview.
24. Export and Reopen Project Without Data Loss.

Sử dụng xUnit hoặc NUnit.

---

# 20. Yêu cầu triển khai

Không chỉ trả lời bằng mô tả kiến trúc.

Hãy thực sự tạo solution, project, source code, XAML, ViewModel, Model, Service và Unit Tests.

Triển khai theo các giai đoạn:

**Phase 1 — Application Foundation**
- Tạo solution và project.
- Thiết lập MVVM/DI.
- Tạo MainWindow.
- Thiết kế theme.
- Tạo Navigation.

**Phase 2 — Project Management**
- Create Project.
- Open Project Folder.
- Save Project.
- Manage Templates.

**Phase 3 — Visual Template Designer**
- Table Tabs.
- Add Table.
- Rename Table.
- Add Field.
- Edit Field.
- Property Panel.
- RowKey and Business Key.

**Phase 4 — Advanced Mapping**
- Nested JSON.
- Parent References.
- Relation Designer.
- Composite Business Key.
- Data Type Validation.

**Phase 5 — JSON Generator**
- Serialize/Deserialize.
- JSON Preview.
- Project Export.
- Import Existing Templates.

**Phase 6 — Mapping Preview**
- Load Sample JSON.
- Execute Mapping.
- Generate RowKey.
- Preview Temp Tables.
- Report Validation Errors.

**Phase 7 — Quality**
- Unit Tests.
- UI Refinement.
- Error Handling.
- Undo/Redo.
- Performance Optimization.

Sau mỗi Phase, đảm bảo solution build thành công và các chức năng đã triển khai hoạt động được.

Không để các nút chức năng quan trọng chỉ hiển thị mà không có hành động thực tế.

Nếu một tính năng chưa được triển khai, phải thể hiện rõ trạng thái chưa hỗ trợ, không giả vờ đã hoàn thành.

---

# 21. Tiêu chí nghiệm thu

Ứng dụng được xem là hoàn thành khi có thể thực hiện trọn vẹn luồng sau:

1. Mở ứng dụng.
2. Tạo project `SalesManagement`.
3. Thêm template `order.import`.
4. Nhấn `+` thêm bảng `Header`, export thành `#Header`.
5. Khai báo JSON source `$.orders[*]`.
6. Nhấn `+` thêm field `OrderNo`.
7. Đánh dấu `OrderNo` là Business Key.
8. Bật Generate RowKey cho Header.
9. Thêm bảng `Detail`, chọn Header làm Parent.
10. Khai báo source `$.details[*]`.
11. Tạo các field `ProductID`, `Qty`.
12. Chọn quan hệ bằng Parent RowKey.
13. Xem JSON Template Preview.
14. Paste JSON mẫu và chạy Test Mapping.
15. Kiểm tra `_HeaderRowKey` liên kết đúng từng Header.
16. Export toàn bộ project ra folder `SalesManagement`.
17. Đóng ứng dụng.
18. Mở lại ứng dụng.
19. Chọn Open Project Folder `SalesManagement`.
20. Tất cả template, bảng, cột, key và quan hệ phải được khôi phục chính xác.
21. Chỉnh sửa một field.
22. Save và Export lại thành công.
23. JSON export sau khi mở lại phải tương thích với DSL ban đầu.

## Kết quả cần bàn giao

- Source code WPF hoàn chỉnh.
- Solution build được.
- Giao diện hiện đại theo phong cách Lark Base.
- Project & Template Management.
- Visual Table/Field Designer.
- JSON Template Generator.
- Nested JSON Mapping Preview.
- RowKey/Business Key Support.
- Folder-based Import/Export.
- Unit Tests.
- README hướng dẫn build, chạy và sử dụng.

**Ưu tiên cao nhất:** Trải nghiệm thao tác trực quan, dễ thêm bảng và field bằng dấu `+`, sửa cấu hình nhanh, tự động chuẩn hóa tên bảng tạm bằng dấu `#`, và khả năng mở lại project đã export để chỉnh sửa mà không mất dữ liệu.

Hãy bắt đầu bằng việc tạo solution và triển khai chức năng thực tế, không dừng lại ở bản thiết kế hoặc mockup.