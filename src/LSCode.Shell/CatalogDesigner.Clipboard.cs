using System.Data;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using LSCode.Core;
using LSCode.Shell.Services;
using Microsoft.Data.SqlClient;

namespace LSCode.Shell;

internal sealed partial class CatalogDesigner
{
    private async void CopyGridRows(DataGrid grid)
    {
        if (busy || switching || loading != null) return;
        try
        {
            if (!grid.CommitEdit(DataGridEditingUnit.Cell, true) || !grid.CommitEdit(DataGridEditingUnit.Row, true)) return;
            var selectedRows = grid.Items.Cast<object>().Where(item => grid.SelectedItems.Contains(item)).ToArray();
            if (selectedRows.Length == 0) throw new InvalidOperationException("Chọn nguyên dòng bằng đầu dòng trước khi sao chép.");
            if (selectedRows.Length > CatalogClipboard.MaxRows) throw new InvalidOperationException("Chỉ sao chép tối đa 1.000 dòng mỗi đợt.");
            if (ReferenceEquals(grid, data))
            {
                PutClipboard(DataPacket(selectedRows.Cast<string?[]>().Select(r => r.ToArray()).ToArray()),
                    selectedRows.Cast<string?[]>().Select(r => r.Select(DisplayToken)));
                return;
            }
            List<Field> source;
            HashSet<string> names;
            if (ReferenceEquals(grid, existing))
            {
                if (selected == null) throw new InvalidOperationException("Các trường hệ thống không được sao chép thành trường mới.");
                names = selectedRows.Cast<DataRowView>().Select(r => (string)r[0]).ToHashSet(StringComparer.OrdinalIgnoreCase);
                busy = true; ((UIElement)Content).IsEnabled = false; UpdateTransferState();
                try { source = await ReadTemplateAsync(selected); }
                finally { busy = false; ((UIElement)Content).IsEnabled = true; UpdateTransferState(); }
            }
            else
            {
                source = draft.ToList();
                names = selectedRows.Cast<Field>().Select(f => f.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
            }
            if (names.Any(n => Audit.Contains(n, StringComparer.OrdinalIgnoreCase)))
                if (MessageBox.Show(this, "Các trường hệ thống sẽ không được sao chép. Tiếp tục với các trường nghiệp vụ?",
                    Title, MessageBoxButton.OKCancel) != MessageBoxResult.OK) return;
            var copied = source.Where(f => names.Contains(f.Name) && !Audit.Contains(f.Name, StringComparer.OrdinalIgnoreCase)).Select(CloneField).ToArray();
            if (copied.Length == 0) throw new InvalidOperationException("Không có trường nghiệp vụ để sao chép.");
            if (copied.Any(f => f.Key) && source.Any(f => f.Key && !names.Contains(f.Name)))
                throw new InvalidOperationException("Chọn đủ các trường của khóa chính ghép trước khi sao chép.");
            var packet = new TransferPacket(1, "fields", FieldHeaders, [], [], copied.Select(PackField).ToArray());
            PutClipboard(packet, copied.Select(FieldCells));
        }
        catch (Exception ex) { TransferError(ex); }
    }

    private static readonly string[] FieldHeaders = ["Tên trường", "Kiểu dữ liệu", "Tham số", "Bắt buộc", "Khóa chính", "Không trùng", "Tự tăng", "Mặc định"];
    private static string[] FieldCells(Field field) => [field.Name, field.Type, field.Size, field.Required ? "1" : "0",
        field.Key ? "1" : "0", field.Unique ? "1" : "0", field.Identity ? "1" : "0", field.Default ?? ""];

    private TransferPacket DataPacket(string?[][] values) => new(1, "data",
        metadata!.Rows.Cast<DataRow>().Select(r => (string)r[0]).ToArray(),
        metadata.Rows.Cast<DataRow>().Select(r => (string)r[1]).ToArray(), values, []);

    private void CopyPreviewRows()
    {
        try
        {
            if (metadata == null) return;
            var rows = preview.SelectedRowIndexes.Select(i => preview.RowValues(i)).ToArray();
            if (rows.Length == 0) throw new InvalidOperationException("Chọn dòng ở cột số thứ tự bên trái lưới.");
            var values = rows.Select(r => r.Select(value => value is null or DBNull ? ":null" :
                CatalogClipboard.Literal(FormatClipboardValue(value))).ToArray()).ToArray();
            PutClipboard(DataPacket(values), rows.Select(r => r.Select(FormatClipboardValue)));
        }
        catch (Exception ex) { TransferError(ex); }
    }

    private static string FormatClipboardValue(object? value) => value switch
    {
        null or DBNull => "", DateTime date => date.ToString("yyyy-MM-dd HH:mm:ss.fffffff", CultureInfo.InvariantCulture),
        bool flag => flag ? "1" : "0", _ => Convert.ToString(value, CultureInfo.InvariantCulture) ?? ""
    };
    private static string? DisplayToken(string? value) => value switch
    {
        null or ":default" or ":null" => "", ":empty" => "", _ => CatalogClipboard.DecodeLiteral(value)
    };

    private static string DisplayCellValue(string? value) => value switch
    {
        null or ":default" => "Dùng mặc định", ":null" => "NULL", ":empty" => "",
        _ => CatalogClipboard.DecodeLiteral(value)
    };

    private sealed class PastePreviewColumn(int index, bool internalFormat) : DataGridTextColumn
    {
        protected override FrameworkElement GenerateElement(DataGridCell cell, object item)
        {
            var element = base.GenerateElement(cell, item);
            if (internalFormat && element is TextBlock text && item is DataRowView row)
                text.Text = DisplayCellValue((string)row[index]);
            return element;
        }
    }

    private void PutClipboard(TransferPacket packet, IEnumerable<IEnumerable<string?>> cells)
    {
        var text = CatalogClipboard.Write(cells);
        var json = JsonSerializer.Serialize(packet);
        CatalogClipboard.CheckSize(json);
        var clipboard = new DataObject();
        clipboard.SetData(TransferFormat, json);
        clipboard.SetData(DataFormats.UnicodeText, text);
        Clipboard.SetDataObject(clipboard, true);
        status.Text = $"Đã sao chép {(packet.Kind == "fields" ? packet.Fields.Length : packet.Rows.Length)} dòng.";
    }

    private void TransferError(Exception error)
    {
        if (error is System.Runtime.InteropServices.ExternalException)
            status.Text = "Clipboard đang bận. Thao tác chưa hoàn tất; vui lòng thử lại.";
        else if (error is JsonException or ArgumentException or NullReferenceException)
            status.Text = "Định dạng clipboard không hợp lệ hoặc không tương thích.";
        else Error(error);
    }

    private void PasteRows()
    {
        if (!CanPaste()) { status.Text = "Lưu hoặc bỏ bản nháp khác trước khi dán dòng; chờ bảng tải xong."; return; }
        try
        {
            foreach (var grid in new[] { fields, data })
                if (!grid.CommitEdit(DataGridEditingUnit.Cell, true) || !grid.CommitEdit(DataGridEditingUnit.Row, true)) return;
            var source = Clipboard.GetDataObject();
            if (source == null) throw new InvalidOperationException("Clipboard rỗng.");
            TransferPacket? packet = null;
            if (source.GetDataPresent(TransferFormat) && source.GetData(TransferFormat) is string json)
            {
                CatalogClipboard.CheckSize(json);
                packet = JsonSerializer.Deserialize<TransferPacket>(json);
                if (packet == null || packet.Version != 1 || packet.Headers == null || packet.Types == null ||
                    packet.Rows == null || packet.Fields == null || packet.Headers.Length > 4096 ||
                    packet.Rows.Length > CatalogClipboard.MaxRows || packet.Fields.Length > CatalogClipboard.MaxRows ||
                    packet.Kind is not ("fields" or "data") ||
                    packet.Rows.Any(r => r == null || r.Length != packet.Headers.Length))
                    throw new InvalidOperationException("Định dạng clipboard nội bộ không hợp lệ.");
            }
            bool structure = modes.SelectedIndex == 0;
            if (packet != null && packet.Kind != (structure ? "fields" : "data"))
                throw new InvalidOperationException("Clipboard thuộc chế độ khác. Chọn Cấu trúc hoặc Dữ liệu tương ứng trước khi dán.");
            if (structure && packet != null)
            {
                PasteFields(packet.Fields.Select(UnpackField).ToArray());
                return;
            }
            if (!structure && packet != null)
            {
                ShowDataPaste(packet.Headers, packet.Rows, true);
                return;
            }
            var text = source.GetData(DataFormats.UnicodeText) as string ?? "";
            var rows = CatalogClipboard.Read(text);
            if (rows.Count == 0) throw new InvalidOperationException("Clipboard rỗng.");
            if (structure)
            {
                if (rows.Count > CatalogClipboard.MaxRows) throw new InvalidOperationException("Chỉ dán tối đa 1.000 dòng mỗi đợt.");
                PasteFields(rows.Select((r, i) =>
                {
                    if (r.Length != 8) throw new InvalidOperationException($"Dòng {i + 1}: cần 8 cột: tên, kiểu, tham số, bắt buộc, khóa chính, không trùng, tự tăng, mặc định.");
                    bool Flag(int index) => r[index] switch { "0" or "" => false, "1" => true,
                        _ => throw new InvalidOperationException($"Dòng {i + 1}: cột cờ chỉ nhận 0 hoặc 1.") };
                    return new Field { Name = r[0], Type = r[1], Size = r[2], Required = Flag(3), Key = Flag(4),
                        Unique = Flag(5), Identity = Flag(6), Default = r[7].Length == 0 ? null : r[7] };
                }).ToArray());
            }
            else ShowExternalDataPaste(rows);
        }
        catch (Exception ex) { TransferError(ex); }
    }

    private void PasteFields(Field[] values)
    {
        if (values.Length == 0) throw new InvalidOperationException("Không có dòng trường để dán.");
        var used = draft.Select(f => f.Name).Concat(Audit)
            .Concat(metadata?.Rows.Cast<DataRow>().Select(r => (string)r[0]) ?? [])
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var copies = values.Select(CloneField).ToArray();
        var renames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var field in copies)
        {
            Quote(field.Name);
            if (Audit.Contains(field.Name, StringComparer.OrdinalIgnoreCase))
                throw new InvalidOperationException("Không dán được tên trường hệ thống: " + field.Name);
            string original = field.Name;
            if (!used.Add(original)) field.Name = UniqueCopyName(original, used);
            if (!renames.TryAdd(original, field.Name)) throw new InvalidOperationException("Clipboard có tên trường nguồn trùng nhau.");
        }
        // References to an existing table stay attached to that table. They must not be silently
        // rebound to a pasted column merely because a source field has a similar name.
        var root = new DockPanel { Margin = new Thickness(16) };
        var dialog = TransferDialog("Dán trường · xem trước", root);
        var error = new TextBlock { TextWrapping = TextWrapping.Wrap };
        var grid = Grid(); grid.IsReadOnly = false; grid.ItemsSource = copies;
        grid.Columns.Add(TextColumn("Tên trường", nameof(Field.Name)));
        grid.Columns.Add(new DataGridComboBoxColumn { Header = "Kiểu dữ liệu", ItemsSource = Types, SelectedItemBinding = new Binding(nameof(Field.Type)) });
        grid.Columns.Add(TextColumn("Tham số", nameof(Field.Size)));
        foreach (var property in new[] { nameof(Field.Required), nameof(Field.Key), nameof(Field.Unique), nameof(Field.Identity) })
            grid.Columns.Add(new DataGridCheckBoxColumn { Header = property, Binding = new Binding(property) });
        grid.Columns.Add(TextColumn("Mặc định", nameof(Field.Default)));
        var fk = TextColumn("Khóa ngoại (giữ nguồn)", nameof(Field.ForeignKey)); fk.IsReadOnly = true; grid.Columns.Add(fk);
        var apply = Button("Áp dụng", () => { }); apply.IsEnabled = false;
        apply.SetResourceReference(StyleProperty, "PrimaryButton");
        bool Validate()
        {
            try
            {
                if (!grid.CommitEdit(DataGridEditingUnit.Cell, true) || !grid.CommitEdit(DataGridEditingUnit.Row, true)) return false;
                ValidateFieldCopies(copies);
                error.Text = $"{copies.Length} trường hợp lệ. Áp dụng chỉ thêm bản nháp; nhấn Lưu để ghi database.";
                return true;
            }
            catch (Exception ex) { error.Text = ex.Message; return false; }
        }
        apply.Click += (_, _) => { if (Validate()) dialog.DialogResult = true; };
        grid.BeginningEdit += (_, _) => apply.IsEnabled = false;
        Footer(root, Button("Hủy", () => dialog.DialogResult = false),
            Button("Kiểm tra", () => apply.IsEnabled = Validate()), apply);
        DockPanel.SetDock(error, Dock.Bottom); root.Children.Add(error);
        Header(root, new TextBlock { Text = $"{copies.Length} trường mới · tên trùng được gợi ý hậu tố _copy. Có thể sửa trước khi áp dụng.", TextWrapping = TextWrapping.Wrap });
        root.Children.Add(grid);
        apply.IsEnabled = Validate();
        if (dialog.ShowDialog() != true) return;
        foreach (var field in copies) draft.Add(CloneField(field));
        dirty = true; status.Text = $"Đã dán {copies.Length} trường vào bản nháp. Nhấn Lưu để áp dụng.";
    }

    private void ValidateFieldCopies(Field[] copies)
    {
        foreach (var field in copies)
        {
            if (field.Reference is { } reference && (reference.Length != 4 || reference.Any(string.IsNullOrWhiteSpace)))
                throw new InvalidOperationException(field.Name + ": cấu hình khóa ngoại không hợp lệ.");
            if (field.Collation != null && field.Type is not ("char" or "nchar" or "varchar" or "nvarchar"))
                throw new InvalidOperationException(field.Name + ": collation chỉ áp dụng cho trường văn bản.");
        }
        _ = DefinitionCommands(selected ?? new TableName("dbo", "draft_validation"), selected == null, draft.Concat(copies).ToArray());
    }

    private void ShowExternalDataPaste(List<string[]> rows)
    {
        ShowDataPaste([], rows.Select(r => r.Cast<string?>().ToArray()).ToArray(), false);
    }

    private sealed class PasteMapping
    {
        public string Source { get; set; } = "";
        public string Target { get; set; } = "";
    }
    private const string IgnoreColumn = "(Bỏ qua)";

    private void ShowDataPaste(string[] sourceHeaders, string?[][] sourceRows, bool internalFormat)
    {
        if (metadata == null || selected == null || sourceRows.Length == 0) throw new InvalidOperationException("Chọn bảng đích trước khi dán dữ liệu.");
        var columns = metadata.Rows.Cast<DataRow>().ToArray();
        var writable = columns.Select((r, i) => (Row: r, Index: i)).Where(x => !Managed(x.Row)).ToArray();
        var root = new DockPanel { Margin = new Thickness(16) };
        var dialog = TransferDialog("Dán bản ghi · xem trước", root, 1040, 700);
        var error = new TextBlock { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 8) };
        var header = new CheckBox { Content = "Dòng đầu là tên cột", IsEnabled = !internalFormat, IsChecked = internalFormat };
        var mappingGrid = Grid(); mappingGrid.IsReadOnly = false; mappingGrid.Height = 145;
        mappingGrid.Columns.Add(new DataGridTextColumn { Header = "Cột clipboard", Binding = new Binding(nameof(PasteMapping.Source)), IsReadOnly = true, Width = 230 });
        mappingGrid.Columns.Add(new DataGridComboBoxColumn { Header = "Cột đích", ItemsSource = new[] { IgnoreColumn }.Concat(writable.Select(x => (string)x.Row[0])).ToArray(),
            SelectedItemBinding = new Binding(nameof(PasteMapping.Target)), Width = 240 });
        var grid = Grid(); grid.IsReadOnly = false; grid.AutoGenerateColumns = true;
        DataTable? table = null;
        PasteMapping[] maps = [];
        string?[][]? accepted = null;
        var apply = Button("Áp dụng", () => { }); apply.IsEnabled = false;
        apply.SetResourceReference(StyleProperty, "PrimaryButton");
        void Rebuild()
        {
            apply.IsEnabled = false;
            try
            {
                bool hasHeader = header.IsChecked == true;
                var rows = !internalFormat && hasHeader ? sourceRows.Skip(1).ToArray() : sourceRows;
                if (rows.Length == 0 || rows.Length > CatalogClipboard.MaxRows) throw new InvalidOperationException("Cần từ 1 đến 1.000 dòng dữ liệu mỗi đợt.");
                int count = sourceRows[0].Length;
                if (sourceRows.Any(r => r.Length != count)) throw new InvalidOperationException("Số cột không khớp giữa các dòng. Sửa clipboard trước khi dán lại.");
                var names = internalFormat ? sourceHeaders : hasHeader ? sourceRows[0].Select(v => v ?? "").ToArray() :
                    Enumerable.Range(1, count).Select(i => "Cột " + i).ToArray();
                if (names.Length != count || names.Any(string.IsNullOrWhiteSpace) || names.Distinct(StringComparer.OrdinalIgnoreCase).Count() != names.Length)
                    throw new InvalidOperationException("Header rỗng hoặc trùng tên; sửa header hoặc tắt Dòng đầu là tên cột.");
                maps = names.Select((n, i) => new PasteMapping
                {
                    Source = n,
                    Target = internalFormat || hasHeader ? writable.FirstOrDefault(x => string.Equals((string)x.Row[0], n, StringComparison.OrdinalIgnoreCase)).Row?[0] as string ?? IgnoreColumn :
                        i < writable.Length ? (string)writable[i].Row[0] : IgnoreColumn
                }).ToArray();
                mappingGrid.ItemsSource = maps;
                table = new DataTable();
                for (int i = 0; i < count; i++) table.Columns.Add("C" + i, typeof(string));
                foreach (var row in rows)
                    table.Rows.Add(row.Select(v => (object?)(internalFormat ? v ?? ":default" : v) ?? "").ToArray());
                grid.AutoGenerateColumns = false; grid.Columns.Clear();
                for (int i = 0; i < names.Length; i++) grid.Columns.Add(new PastePreviewColumn(i, internalFormat)
                    { Header = names[i], Binding = new Binding("[C" + i + "]"), Width = 160 });
                grid.ItemsSource = table.DefaultView;
                error.Text = $"{rows.Length} dòng · số theo dạng 1234.56 · ngày yyyy-MM-dd HH:mm:ss.\n" +
                    "Ô không phải văn bản bị trống: nhập :null hoặc :default. Văn bản bắt đầu bằng dấu : sẽ được giữ nguyên; dùng menu ô sau khi áp dụng để đặt NULL/DEFAULT.";
            }
            catch (Exception ex) { table = null; grid.ItemsSource = null; error.Text = ex.Message; }
        }
        bool Validate()
        {
            try
            {
                foreach (var control in new[] { mappingGrid, grid })
                    if (!control.CommitEdit(DataGridEditingUnit.Cell, true) || !control.CommitEdit(DataGridEditingUnit.Row, true)) return false;
                if (table == null) throw new InvalidOperationException("Chưa có dữ liệu hợp lệ để kiểm tra.");
                var active = maps.Select((m, i) => (Map: m, Source: i)).Where(x => x.Map.Target != IgnoreColumn).ToArray();
                if (active.Length == 0) throw new InvalidOperationException("Ánh xạ ít nhất một cột nghiệp vụ.");
                if (active.Select(x => x.Map.Target).Distinct(StringComparer.OrdinalIgnoreCase).Count() != active.Length)
                    throw new InvalidOperationException("Không ánh xạ nhiều cột nguồn vào cùng một cột đích.");
                accepted = table.Rows.Cast<DataRow>().Select((row, r) =>
                {
                    var record = new string?[columns.Length];
                    foreach (var item in active)
                    {
                        int targetIndex = Array.FindIndex(columns, c => string.Equals((string)c[0], item.Map.Target, StringComparison.OrdinalIgnoreCase));
                        if (targetIndex < 0 || Managed(columns[targetIndex])) throw new InvalidOperationException("Ánh xạ cột không hợp lệ.");
                        var value = (string)row[item.Source];
                        if (internalFormat) record[targetIndex] = value;
                        else if ((string)columns[targetIndex][1] is "char" or "nchar" or "varchar" or "nvarchar")
                            record[targetIndex] = value == "" ? ":empty" : CatalogClipboard.Literal(value);
                        else if (value.Length == 0) throw new InvalidOperationException($"Dòng {r + 1}, {item.Map.Target}: chọn :null, :default hoặc nhập giá trị.");
                        else record[targetIndex] = value;
                    }
                    return record;
                }).ToArray();
                ValidateRecordCopies(accepted);
                var ignored = string.Join(", ", maps.Where(m => m.Target == IgnoreColumn).Select(m => m.Source));
                error.Text = $"{accepted.Length} dòng hợp lệ. Chỉ thêm bản nháp; nhấn Lưu để ghi database.\n" +
                    "Cột bỏ qua: " + (ignored.Length == 0 ? "không có" : ignored) + ". Identity/computed/audit do hệ thống cấp lại.";
                return true;
            }
            catch (Exception ex) { accepted = null; error.Text = ex.Message; return false; }
        }
        apply.Click += (_, _) => { if (Validate()) dialog.DialogResult = true; };
        grid.BeginningEdit += (_, _) => apply.IsEnabled = false;
        mappingGrid.BeginningEdit += (_, _) => apply.IsEnabled = false;
        header.Checked += (_, _) => Rebuild(); header.Unchecked += (_, _) => Rebuild();
        Footer(root, Button("Hủy", () => dialog.DialogResult = false), Button("Kiểm tra", () => apply.IsEnabled = Validate()), apply);
        DockPanel.SetDock(error, Dock.Bottom); root.Children.Add(error);
        Header(root, Label("Bảng đích: " + selected), header);
        Header(root, new TextBlock { Text = "Kiểm tra ánh xạ và các ô trước khi áp dụng. Dán luôn thêm dòng mới, không ghi đè dữ liệu đã lưu.", TextWrapping = TextWrapping.Wrap });
        DockPanel.SetDock(mappingGrid, Dock.Top); root.Children.Add(mappingGrid);
        root.Children.Add(grid);
        Rebuild();
        if (dialog.ShowDialog() != true || accepted == null) return;
        AddRecord(); // Initializes the existing editor; replace its placeholder with the accepted batch.
        records.RemoveAt(records.Count - 1);
        foreach (var row in accepted) records.Add(row.ToArray());
        dirty = true; status.Text = $"Đã dán {accepted.Length} bản ghi mới · {records.Count} bản ghi chưa lưu.";
    }

    private void ValidateRecordCopies(string?[][] copies)
    {
        try
        {
            foreach (var record in copies)
                for (int i = 0; i < record.Length; i++)
                {
                    var value = record[i];
                    if ((string)metadata!.Rows[i][1] is not ("date" or "datetime" or "datetime2") || value is null or ":null" or ":default") continue;
                    if (!DateTime.TryParseExact(CatalogClipboard.DecodeLiteral(value),
                        ["yyyy-MM-dd", "yyyy-MM-dd HH:mm:ss", "yyyy-MM-dd HH:mm:ss.FFFFFFF", "yyyy-MM-dd'T'HH:mm:ss", "yyyy-MM-dd'T'HH:mm:ss.FFFFFFF"],
                        CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
                        throw new CellException(record, i, "Dùng định dạng ngày ISO yyyy-MM-dd hoặc yyyy-MM-dd HH:mm:ss, không đoán ngày/tháng.");
                }
            _ = InsertCommands(selected!, records.Concat(copies).ToArray());
        }
        catch (CellException ex)
        {
            int index = Array.IndexOf(copies, (string?[])ex.Row);
            throw new InvalidOperationException($"Dòng {(index < 0 ? "nháp hiện tại" : (index + 1).ToString(CultureInfo.InvariantCulture))}, {metadata!.Rows[ex.Column][0]}: {ex.Message}");
        }
    }

    private static void ValidateValueFits(DataRow column, object value, object record, int index)
    {
        if (value is DateTime day && (string)column[1] == "date" && day.TimeOfDay != TimeSpan.Zero)
            throw new CellException(record, index, "Cột date chỉ nhận ngày, không tự cắt phần giờ.");
        if (value is DateTime date && (string)column[1] == "datetime2" &&
            date.Ticks % (long)Math.Pow(10, 7 - Convert.ToInt32(column[4])) != 0)
            throw new CellException(record, index, "Ngày giờ vượt độ chính xác của cột datetime2; không tự làm tròn.");
        if (value is decimal number && (string)column[1] is "decimal" or "numeric")
        {
            int precision = Convert.ToInt32(column[3]), scale = Convert.ToInt32(column[4]);
            string digits = Math.Abs(number).ToString("0.#############################", CultureInfo.InvariantCulture);
            var parts = digits.Split('.');
            int whole = parts[0] == "0" ? 0 : parts[0].Length;
            int fraction = parts.Length == 1 ? 0 : parts[1].Length;
            if (whole > precision - scale || fraction > scale)
                throw new CellException(record, index, "Số vượt precision/scale; không tự làm tròn.");
        }
        if (value is string text && (string)column[1] is "varchar" or "char" && Convert.ToInt32(column[2]) > 0)
        {
            // SQL Server decides the actual code page. Reject obvious overlength input here;
            // ANSI conversion is verified against the destination collation in the save transaction.
            if (text.Length > Convert.ToInt32(column[2])) throw new CellException(record, index, "Giá trị vượt quá độ dài của cột.");
        }
    }

    private async Task<string[][]> UniqueKeysAsync(TableName table, CancellationToken token)
    {
        var keys = await ProjectDatabase.ReadAsync(target, """
            SELECT i.index_id,c.name FROM sys.indexes i JOIN sys.index_columns k
              ON k.object_id=i.object_id AND k.index_id=i.index_id
            JOIN sys.columns c ON c.object_id=k.object_id AND c.column_id=k.column_id
            WHERE i.object_id=OBJECT_ID(@table) AND i.is_unique=1 AND i.is_disabled=0
              AND i.has_filter=0 AND k.key_ordinal>0 ORDER BY i.index_id,k.key_ordinal
            """, token, new SqlParameter("@table", table.Sql));
        return keys.Rows.Cast<DataRow>().GroupBy(r => Convert.ToInt32(r[0])).Select(g => g.Select(r => (string)r[1]).ToArray()).ToArray();
    }

    private void ValidateBatchKeys(List<Statement> statements, IReadOnlyList<string?[]> pending)
    {
        foreach (var key in uniqueKeyColumns)
        {
            var indexes = key.Select(n => metadata!.Rows.Cast<DataRow>().ToList().FindIndex(c => (string)c[0] == n)).ToArray();
            if (indexes.Any(i => i < 0)) continue;
            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (int row = 0; row < statements.Count; row++)
            {
                var values = indexes.Select(i => statements[row].Parameters.FirstOrDefault(p => p.ParameterName == "@p" + i)).ToArray();
                if (values.Any(p => p == null)) continue; // Server-generated/default values are checked by SQL Server.
                string signature = JsonSerializer.Serialize(values.Select(p => p!.Value is DBNull ? null :
                    p.Value.GetType().Name + ":" + (p.Value is decimal number ? number.ToString("G29", CultureInfo.InvariantCulture) :
                        FormatClipboardValue(p.Value))).ToArray());
                if (!seen.Add(signature)) throw new CellException(pending[row], indexes[0], "Trùng khóa trong các bản ghi chưa lưu: " + string.Join(", ", key));
            }
        }
    }
}
