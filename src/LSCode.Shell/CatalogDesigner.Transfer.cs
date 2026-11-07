using System.Data;
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using LSCode.Core;
using LSCode.Shell.Services;
using Microsoft.Data.SqlClient;

namespace LSCode.Shell;

internal sealed partial class CatalogDesigner
{
    private const string TransferFormat = "LSCode.Catalog.Rows.v1";
    private readonly Button duplicate = new() { Content = "Duplicate template…", IsEnabled = false };
    private TableName? duplicateSource;
    private readonly Dictionary<Field, string> templateOriginalNames = [];

    private sealed record TransferField(string Name, string Type, string Size, bool Required, bool Key,
        bool Unique, bool Identity, string? Default, string[]? Reference, string Seed, string Increment,
        int KeyOrder, string? Collation, bool NullDefault);
    private sealed record TransferPacket(int Version, string Kind, string[] Headers, string[] Types,
        string?[][] Rows, TransferField[] Fields);

    private void InitializeTransfer()
    {
        duplicate.Click += async (_, _) => await DuplicateAsync();
        tables.SelectionChanged += (_, _) => UpdateTransferState();
        modes.SelectionChanged += (_, _) => UpdateTransferState();
        foreach (var grid in new[] { fields, data, existing })
        {
            grid.SelectionMode = DataGridSelectionMode.Extended;
            grid.SelectionUnit = DataGridSelectionUnit.FullRow;
            grid.HeadersVisibility = DataGridHeadersVisibility.All;
            grid.RowHeaderWidth = 36;
            var rowHeader = new Style(typeof(DataGridRowHeader));
            rowHeader.Setters.Add(new Setter(Control.BackgroundProperty, new DynamicResourceExtension("Raised")));
            rowHeader.Setters.Add(new Setter(Control.ForegroundProperty, new DynamicResourceExtension("Text")));
            rowHeader.Setters.Add(new Setter(Control.BorderBrushProperty, new DynamicResourceExtension("GridLine")));
            rowHeader.Setters.Add(new Setter(Control.ToolTipProperty, "Chọn dòng · Shift: chọn dải · Ctrl: chọn nhiều dòng"));
            grid.RowHeaderStyle = rowHeader;
            grid.LoadingRow += (_, e) => e.Row.Header = (e.Row.GetIndex() + 1).ToString(CultureInfo.InvariantCulture);
            var menu = new ContextMenu();
            var copy = new MenuItem { Header = "Sao chép dòng", InputGestureText = "Ctrl+C" };
            var paste = new MenuItem { Header = "Dán dòng…", InputGestureText = "Ctrl+V" };
            copy.Click += (_, _) => CopyGridRows(grid);
            paste.Click += (_, _) => PasteRows();
            menu.Items.Add(copy); menu.Items.Add(paste);
            if (!ReferenceEquals(grid, existing))
            {
                var remove = new MenuItem { Header = "Bỏ dòng nháp đang chọn" };
                remove.Click += (_, _) => RemoveDraftRows(grid);
                menu.Items.Add(remove);
            }
            menu.Opened += (_, _) => { copy.IsEnabled = grid.SelectedItems.Count > 0; paste.IsEnabled = CanPaste(); };
            grid.ContextMenu = menu;
            grid.PreviewKeyDown += (_, e) =>
            {
                if (Keyboard.Modifiers != ModifierKeys.Control || IsEditingCell(grid)) return;
                if (e.Key == Key.C) { CopyGridRows(grid); e.Handled = true; }
                if (e.Key == Key.V) { PasteRows(); e.Handled = true; }
            };
        }
        var previewMenu = new ContextMenu();
        var previewCopy = new MenuItem { Header = "Sao chép dòng", InputGestureText = "Ctrl+C" };
        previewCopy.Click += (_, _) => CopyPreviewRows();
        var previewPaste = new MenuItem { Header = "Dán dòng…", InputGestureText = "Ctrl+V" };
        previewPaste.Click += (_, _) => PasteRows();
        previewMenu.Items.Add(previewCopy); previewMenu.Items.Add(previewPaste);
        previewMenu.Opened += (_, _) => { previewCopy.IsEnabled = preview.SelectedRowIndexes.Count > 0; previewPaste.IsEnabled = CanPaste(); };
        preview.ContextMenu = previewMenu;
        preview.CopyRowsRequested += CopyPreviewRows;
        preview.PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.V && Keyboard.Modifiers == ModifierKeys.Control) { PasteRows(); e.Handled = true; }
        };
        fields.CellEditEnding += (_, e) => { if (!switching && e.EditAction == DataGridEditAction.Commit) dirty = true; };
        data.CellEditEnding += (_, e) => { if (!switching && e.EditAction == DataGridEditAction.Commit) dirty = true; };
    }

    private void UpdateTransferState() => duplicate.IsEnabled = selected != null && !busy && !switching && loading == null;

    private void RemoveDraftRows(DataGrid grid)
    {
        if (busy || switching || loading != null) return;
        grid.CancelEdit(DataGridEditingUnit.Cell); grid.CancelEdit(DataGridEditingUnit.Row);
        var rows = grid.SelectedItems.Cast<object>().ToArray();
        foreach (var row in rows)
        {
            if (ReferenceEquals(grid, fields) && row is Field field) draft.Remove(field);
            if (ReferenceEquals(grid, data) && row is string?[] record) records.Remove(record);
        }
        if (ReferenceEquals(grid, data) && records.Count == 0)
        { data.Visibility = Visibility.Collapsed; previewScroll.Visibility = Visibility.Visible; }
        dirty = selected == null || draft.Count > 0 || records.Count > 0;
        status.Text = $"Đã bỏ {rows.Length} dòng nháp · {draft.Count} trường và {records.Count} bản ghi chưa lưu.";
    }
    private bool CanPaste() => !busy && !switching && loading == null &&
        (modes.SelectedIndex == 0 ? records.Count == 0 && (selected == null || metadata != null) : selected != null && metadata != null && draft.Count == 0);

    private static bool IsEditingCell(DataGrid grid) => TransferDescendants<DataGridCell>(grid).Any(c => c.IsEditing);

    private static IEnumerable<T> TransferDescendants<T>(DependencyObject root) where T : DependencyObject
    {
        for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = System.Windows.Media.VisualTreeHelper.GetChild(root, i);
            if (child is T match) yield return match;
            foreach (var descendant in TransferDescendants<T>(child)) yield return descendant;
        }
    }

    private static Field CloneField(Field field) => new()
    {
        Name = field.Name, Type = field.Type, Size = field.Size, Required = field.Required, Key = field.Key,
        Unique = field.Unique, Identity = field.Identity, Default = field.Default, Reference = field.Reference?.ToArray(),
        IdentitySeed = field.IdentitySeed, IdentityIncrement = field.IdentityIncrement, KeyOrder = field.KeyOrder,
        Collation = field.Collation, NullDefault = field.NullDefault
    };

    private static TransferField PackField(Field field) => new(field.Name, field.Type, field.Size, field.Required,
        field.Key, field.Unique, field.Identity, field.Default, field.Reference, field.IdentitySeed,
        field.IdentityIncrement, field.KeyOrder, field.Collation, field.NullDefault);
    private static Field UnpackField(TransferField field) => new()
    {
        Name = field.Name, Type = field.Type, Size = field.Size, Required = field.Required, Key = field.Key,
        Unique = field.Unique, Identity = field.Identity, Default = field.Default, Reference = field.Reference?.ToArray(),
        IdentitySeed = field.Seed, IdentityIncrement = field.Increment, KeyOrder = field.KeyOrder,
        Collation = field.Collation, NullDefault = field.NullDefault
    };

    private static string UniqueCopyName(string source, ISet<string> used)
    {
        for (int number = 1; ; number++)
        {
            string suffix = number == 1 ? "_copy" : "_copy_" + number.ToString(CultureInfo.InvariantCulture);
            var candidate = source[..Math.Min(source.Length, 128 - suffix.Length)] + suffix;
            if (used.Add(candidate)) return candidate;
        }
    }

    private static string CollationSql(string value)
    {
        if (value.Length > 128 || !Regex.IsMatch(value, @"\A[A-Za-z][A-Za-z0-9_]*\z", RegexOptions.CultureInvariant))
            throw new InvalidOperationException("Tên collation không hợp lệ.");
        return value;
    }

    private async Task DuplicateAsync()
    {
        if (busy || switching || loading != null || selected is not { } source) return;
        if (!await ResolveDraftAsync()) return;
        busy = true; ((UIElement)Content).IsEnabled = false;
        List<Field> copied;
        try
        {
            copied = await ReadTemplateAsync(source);
        }
        catch (Exception ex) { Error(ex); return; }
        finally { busy = false; ((UIElement)Content).IsEnabled = true; UpdateTransferState(); }

        var used = tables.Items.OfType<TableName>().Where(t => t.Schema.Equals(source.Schema, StringComparison.OrdinalIgnoreCase))
            .Select(t => t.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var targetName = new TextBox { Text = UniqueCopyName(source.Name, used), Width = 300, MaxLength = 128 };
        var schemas = new ComboBox { ItemsSource = schema.ItemsSource, SelectedItem = source.Schema, MinWidth = 130 };
        var root = new DockPanel { Margin = new Thickness(16) };
        var dialog = TransferDialog("Duplicate template", root, 860, 570);
        var error = new TextBlock { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 8) };
        var apply = Button("Tạo bản nháp", () =>
        {
            try
            {
                Quote(targetName.Text.Trim());
                var targetSchema = schemas.SelectedItem as string ?? throw new InvalidOperationException("Chọn schema đích.");
                if (tables.Items.OfType<TableName>().Any(t => t.Schema.Equals(targetSchema, StringComparison.OrdinalIgnoreCase) &&
                    t.Name.Equals(targetName.Text.Trim(), StringComparison.OrdinalIgnoreCase)))
                    throw new InvalidOperationException("Tên bảng đã tồn tại trong schema đích.");
                dialog.DialogResult = true;
            }
            catch (Exception ex) { error.Text = ex.Message; }
        });
        apply.SetResourceReference(StyleProperty, "PrimaryButton");
        Footer(root, Button("Hủy", () => dialog.DialogResult = false), apply);
        DockPanel.SetDock(error, Dock.Bottom); root.Children.Add(error);
        Header(root, new TextBlock { Text = "Nguồn: " + source, TextWrapping = TextWrapping.Wrap, MaxWidth = Math.Max(400, dialog.Width - 40) });
        Header(root, Label("Schema"), schemas, Label("Tên bảng mới *"), targetName);
        Header(root, new TextBlock { Text = "Chỉ sao chép cấu trúc. Không sao chép dữ liệu, index phụ, quyền, trigger hoặc thuộc tính mở rộng.\nBốn trường hệ thống luôn được thêm đúng một lần. Chỉ nhấn Lưu mới tạo bảng.", TextWrapping = TextWrapping.Wrap });
        var display = Grid(); display.AutoGenerateColumns = true; display.IsReadOnly = true;
        display.ItemsSource = copied.Select(f => new { f.Name, f.Type, f.Size, f.Required, f.Key, f.Unique,
            f.Identity, f.IdentitySeed, f.IdentityIncrement, f.Default, f.ForeignKey }).ToArray();
        root.Children.Add(display);
        if (dialog.ShowDialog() != true) return;
        dirty = false; // The prior draft has already been resolved before reading the source.
        await SwitchAsync(null);
        if (selected != null) return;
        duplicateSource = source;
        schema.SelectedItem = schemas.SelectedItem;
        name.Text = targetName.Text.Trim();
        foreach (var field in copied) { templateOriginalNames[field] = field.Name; draft.Add(field); }
        dirty = true;
        status.Text = $"Bản nháp từ {source} · {copied.Count} trường nghiệp vụ · không kèm dữ liệu. Nhấn Lưu để tạo bảng.";
        UpdateTransferState();
    }

    private async Task<List<Field>> ReadTemplateAsync(TableName source)
    {
        Task<DataTable> Read(string sql) => ProjectDatabase.ReadAsync(target, sql, CancellationToken.None, new SqlParameter("@table", source.Sql));
        var columns = await Read("""
            SELECT c.name, TYPE_NAME(c.user_type_id) AS type_name, c.max_length, c.precision, c.scale,
                c.is_nullable, c.is_identity, c.is_computed, d.definition,
                CONVERT(varchar(40),ic.seed_value) AS seed, CONVERT(varchar(40),ic.increment_value) AS increment_value,
                c.collation_name, c.generated_always_type, c.is_sparse, c.is_rowguidcol,
                TYPEPROPERTY(TYPE_NAME(c.user_type_id),'IsUserDefined') AS user_type
            FROM sys.columns c LEFT JOIN sys.default_constraints d ON d.object_id=c.default_object_id
            LEFT JOIN sys.identity_columns ic ON ic.object_id=c.object_id AND ic.column_id=c.column_id
            WHERE c.object_id=OBJECT_ID(@table,'U') ORDER BY c.column_id
            """);
        if (columns.Rows.Count == 0) throw new InvalidOperationException("Không đọc được cấu trúc nguồn. Kiểm tra quyền VIEW DEFINITION.");
        var unsupported = await Read("""
            SELECT 'CHECK: '+name AS issue FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID(@table)
            UNION ALL SELECT 'UNIQUE ghép hoặc index khóa không hỗ trợ: '+i.name
            FROM sys.indexes i WHERE i.object_id=OBJECT_ID(@table) AND (i.is_primary_key=1 OR i.is_unique_constraint=1)
              AND (i.is_disabled=1 OR i.has_filter=1 OR i.type<>2 AND i.type<>1 OR
                i.is_unique_constraint=1 AND (SELECT COUNT(*) FROM sys.index_columns k
                    WHERE k.object_id=i.object_id AND k.index_id=i.index_id AND k.key_ordinal>0)>1)
            UNION ALL SELECT 'Khóa ngoại ghép hoặc cascade: '+f.name FROM sys.foreign_keys f
            WHERE f.parent_object_id=OBJECT_ID(@table) AND (f.delete_referential_action<>0 OR f.update_referential_action<>0
                OR f.is_disabled=1 OR f.is_not_trusted=1 OR
                (SELECT COUNT(*) FROM sys.foreign_key_columns k WHERE k.constraint_object_id=f.object_id)>1)
            UNION ALL SELECT 'Bảng temporal, memory-optimized hoặc FILETABLE' FROM sys.tables
            WHERE object_id=OBJECT_ID(@table) AND (temporal_type<>0 OR is_memory_optimized=1 OR is_filetable=1)
            UNION ALL SELECT 'Trường mã hóa/masking/rule/default cũ: '+c.name FROM sys.columns c
            WHERE c.object_id=OBJECT_ID(@table) AND (c.encryption_type IS NOT NULL OR c.rule_object_id<>0 OR
                c.default_object_id<>0 AND NOT EXISTS(SELECT 1 FROM sys.default_constraints d WHERE d.object_id=c.default_object_id)
                OR EXISTS(SELECT 1 FROM sys.masked_columns m WHERE m.object_id=c.object_id AND m.column_id=c.column_id AND m.is_masked=1))
            """);
        if (unsupported.Rows.Count > 0) throw new InvalidOperationException("Không thể nhân bản đầy đủ:\n" +
            string.Join("\n", unsupported.Rows.Cast<DataRow>().Select(r => (string)r[0])));
        var keys = await Read("""
            SELECT c.name, i.is_primary_key, i.is_unique_constraint, k.key_ordinal
            FROM sys.indexes i JOIN sys.index_columns k ON k.object_id=i.object_id AND k.index_id=i.index_id
            JOIN sys.columns c ON c.object_id=k.object_id AND c.column_id=k.column_id
            WHERE i.object_id=OBJECT_ID(@table) AND k.key_ordinal>0 AND (i.is_primary_key=1 OR i.is_unique_constraint=1)
            ORDER BY i.index_id,k.key_ordinal
            """);
        var references = await Read("""
            SELECT c.name, s.name AS target_schema,t.name AS target_table,rc.name AS target_column,TYPE_NAME(rc.user_type_id) AS target_type
            FROM sys.foreign_key_columns f JOIN sys.columns c ON c.object_id=f.parent_object_id AND c.column_id=f.parent_column_id
            JOIN sys.tables t ON t.object_id=f.referenced_object_id JOIN sys.schemas s ON s.schema_id=t.schema_id
            JOIN sys.columns rc ON rc.object_id=f.referenced_object_id AND rc.column_id=f.referenced_column_id
            WHERE f.parent_object_id=OBJECT_ID(@table)
            """);
        var fields = TemplateFields(columns, keys, references);
        foreach (var field in fields.Where(f => f.Reference is { } r && r[0] == source.Schema && r[1] == source.Name))
        {
            var key = fields.SingleOrDefault(f => f.Name == field.Reference![2]);
            if (key == null || !key.Unique && !(key.Key && fields.Count(f => f.Key) == 1))
                throw new InvalidOperationException(field.Name + ": khóa ngoại tự tham chiếu phụ thuộc index chưa được nhân bản.");
        }
        return fields;
    }

    private static List<Field> TemplateFields(DataTable columns, DataTable keys, DataTable references)
    {
        var result = new List<Field>();
        foreach (DataRow row in columns.Rows)
        {
            string columnName = (string)row[0], type = (string)row[1];
            if ((bool)row[7] || Convert.ToInt32(row[12]) != 0 || (bool)row[13] || (bool)row[14] ||
                row[15] != DBNull.Value && Convert.ToInt32(row[15]) != 0 || !Types.Contains(type))
                throw new InvalidOperationException(columnName + ": kiểu/thuộc tính chưa hỗ trợ nhân bản đầy đủ.");
            if (Audit.Any(a => a.Equals(columnName, StringComparison.OrdinalIgnoreCase)))
            {
                string expected = columnName.StartsWith("user_", StringComparison.OrdinalIgnoreCase) ? "int" : "datetime";
                if (type != expected || !(bool)row[5] || (bool)row[6] || row[8] != DBNull.Value ||
                    keys.Rows.Cast<DataRow>().Any(k => (string)k[0] == columnName) || references.Rows.Cast<DataRow>().Any(k => (string)k[0] == columnName))
                    throw new InvalidOperationException(columnName + ": trường hệ thống không khớp hợp đồng (kiểu, NULL hoặc ràng buộc).");
                continue;
            }
            var field = new Field { Name = columnName, Type = type, Required = !(bool)row[5], Identity = (bool)row[6],
                Size = type switch
                {
                    "nvarchar" or "nchar" => Convert.ToInt32(row[2]) == -1 ? "max" : (Convert.ToInt32(row[2]) / 2).ToString(CultureInfo.InvariantCulture),
                    "varchar" or "char" => Convert.ToInt32(row[2]) == -1 ? "max" : Convert.ToString(row[2], CultureInfo.InvariantCulture)!,
                    "decimal" or "numeric" => row[3] + "," + row[4],
                    "datetime2" => Convert.ToString(row[4], CultureInfo.InvariantCulture)!, _ => ""
                },
                IdentitySeed = row[9] == DBNull.Value ? "1" : (string)row[9],
                IdentityIncrement = row[10] == DBNull.Value ? "1" : (string)row[10],
                Collation = row[11] == DBNull.Value ? null : (string)row[11] };
            if (row[8] != DBNull.Value) ReadLiteralDefault(field, (string)row[8]);
            foreach (var key in keys.Rows.Cast<DataRow>().Where(k => (string)k[0] == columnName))
            {
                if ((bool)key[1]) { field.Key = true; field.KeyOrder = Convert.ToInt32(key[3]); }
                else field.Unique = true;
            }
            var foreign = references.Rows.Cast<DataRow>().Where(r => (string)r[0] == columnName).ToArray();
            if (foreign.Length > 1) throw new InvalidOperationException(columnName + ": nhiều khóa ngoại trên cùng một trường chưa được hỗ trợ.");
            if (foreign.Length == 1) field.Reference = foreign[0].ItemArray.Skip(1).Cast<string>().ToArray();
            result.Add(field);
        }
        return result;
    }

    private static void ReadLiteralDefault(Field field, string expression)
    {
        var value = expression.Trim();
        while (value.StartsWith('(') && value.EndsWith(')')) value = value[1..^1].Trim();
        if (value.Equals("NULL", StringComparison.OrdinalIgnoreCase)) { field.NullDefault = true; return; }
        var literal = Regex.Match(value, "^(?:N)?'((?:[^']|'')*)'$", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
        if (literal.Success) value = literal.Groups[1].Value.Replace("''", "'", StringComparison.Ordinal);
        else if (!Regex.IsMatch(value, @"^[+-]?\d+(?:\.\d+)?$", RegexOptions.CultureInvariant))
            throw new InvalidOperationException(field.Name + ": DEFAULT biểu thức chưa được hỗ trợ (" + expression + ").");
        try { ConvertValue(field.Type, value); }
        catch (Exception ex) when (ex is FormatException or OverflowException)
        { throw new InvalidOperationException(field.Name + ": DEFAULT không biểu diễn được an toàn bằng bộ thiết kế."); }
        field.Default = value;
    }

    private Window TransferDialog(string title, DockPanel root, double width = 900, double height = 600) => new()
    {
        Owner = this, Title = title, Content = root, Width = Math.Min(width, Math.Max(480, ActualWidth)),
        Height = Math.Min(height, Math.Max(380, ActualHeight)), MinWidth = 480, MinHeight = 380,
        WindowStartupLocation = WindowStartupLocation.CenterOwner, ShowInTaskbar = false
    };

    private static void Footer(DockPanel root, params Button[] buttons)
    {
        var panel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 12, 0, 0) };
        foreach (var button in buttons) panel.Children.Add(button);
        DockPanel.SetDock(panel, Dock.Bottom); root.Children.Add(panel);
    }
}
