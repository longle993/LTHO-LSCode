using System.Collections.ObjectModel;
using System.Data;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using LSCode.Shell.Services;
using Microsoft.Data.SqlClient;

namespace LSCode.Shell;

public partial class MainWindow
{
    private async void Catalog_Click(object sender, RoutedEventArgs e) => await Guard(async () =>
    {
        SqlTarget? target;
        if (Active is { Language: ".sql" } document)
        {
            await document.TargetReady;
            target = document.SqlTarget;
        }
        else
        {
            var project = projects.FirstOrDefault(p => p.Id == settings.ActiveProjectId);
            if (project == null || string.IsNullOrWhiteSpace(project.Field("SysConnectionString")))
                throw new InvalidOperationException("Chọn dự án có App DB hoặc mở tab SQL đã chọn kết nối.");
            var context = new EditorTab("catalog.sql", "") { Project = project };
            try { target = appSqlTarget ?? await ResolveAppTargetAsync(context, false); }
            finally { context.Dispose(); }
        }
        if (target == null || string.IsNullOrWhiteSpace(target.Database))
            throw new InvalidOperationException("Chưa chọn database cho danh mục.");
        new CatalogDesigner(target, () =>
        {
            SqlSchemaCompletion.Clear();
            foreach (var tab in tabs.Where(tab => tab.SqlTarget == target)) tab.InvalidateCompletionCache();
            if (appSqlTarget == target)
            {
                contentSearchCache.Clear();
                objectCancellation?.Cancel();
                DbObjects.Items.Clear();
                if (DbObjectTab.IsSelected) _ = LoadDbObjectsAsync();
            }
        }) { Owner = this }.Show();
    });
}

internal sealed class CatalogDesigner : Window
{
    private sealed record TableName(string Schema, string Name)
    {
        public string Sql => Quote(Schema) + "." + Quote(Name);
        public override string ToString() => Schema + "." + Name;
    }

    private static readonly string[] Types = ["nvarchar", "varchar", "nchar", "char", "int", "bigint",
        "smallint", "tinyint", "decimal", "numeric", "bit", "date", "datetime", "datetime2", "uniqueidentifier"];
    private static readonly string[] Audit = ["user_id0", "user_id2", "datetime0", "datetime2"];
    private readonly SqlTarget target;
    private readonly Action changed;
    private readonly ComboBox tables = new() { Width = 260, IsTextSearchEnabled = true };
    private readonly ComboBox schema = new() { Width = 130 };
    private readonly TextBox name = new() { Width = 220 };
    private readonly Button save = new() { Content = "Lưu", Width = 100 };
    private readonly TextBlock status = new() { TextWrapping = TextWrapping.Wrap };
    private readonly TabControl modes = new();
    private readonly DataGrid fields = Grid();
    private readonly DataGrid data = Grid();
    private readonly CatalogPreview preview = new();
    private readonly ScrollViewer previewScroll = new()
    {
        CanContentScroll = true, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
        VerticalScrollBarVisibility = ScrollBarVisibility.Auto, IsDeferredScrollingEnabled = true
    };
    private readonly ObservableCollection<Field> draft = [];
    private readonly ObservableCollection<string?[]> records = [];
    private readonly DataGrid existing = Grid();
    private TableName? selected;
    private DataTable? metadata;
    private CancellationTokenSource? loading;
    private bool busy, switching, dirty, uncertain;
    private int page;
    private DataGridCell? highlightedCell;

    public sealed class Field
    {
        public string Name { get; set; } = "";
        public string Type { get; set; } = "nvarchar";
        public string Size { get; set; } = "128";
        public bool Required { get; set; }
        public bool Key { get; set; }
        public bool Unique { get; set; }
        public bool Identity { get; set; }
        public string? Default { get; set; }
        internal string[]? Reference { get; set; }
        public string ForeignKey => Reference is { } reference ? string.Join(".", reference.Take(3)) : "";
    }

    public CatalogDesigner(SqlTarget target, Action changed)
    {
        this.target = target;
        this.changed = changed;
        Title = "Danh mục";
        Width = 1080;
        Height = 720;
        MinWidth = 480;
        MinHeight = 380;
        var root = new DockPanel { Margin = new Thickness(16) };
        Content = root;
        Header(root, Label("Danh mục"), Label(target.Server + " / " + target.Database),
            Label("Chọn bảng"), tables, Button("Tạo bảng mới", async () => await SwitchAsync(null)));
        Header(root, Label("Schema"), schema, Label("Tên bảng *"), name, save);
        DockPanel.SetDock(status, Dock.Bottom);
        root.Children.Add(status);
        root.Children.Add(modes);
        modes.Items.Add(new TabItem { Header = "Cấu trúc", Content = Structure() });
        var rows = new DockPanel();
        Header(rows, Button("Thêm bản ghi", AddRecord), Button("+ Thêm trường", AddField),
            Button("Trang trước", async () => await PageAsync(-1)), Button("Trang sau", async () => await PageAsync(1)));
        previewScroll.Content = preview;
        var dataHost = new System.Windows.Controls.Grid();
        dataHost.Children.Add(previewScroll);
        dataHost.Children.Add(data);
        data.Visibility = Visibility.Collapsed;
        rows.Children.Add(dataHost);
        modes.Items.Add(new TabItem { Header = "Dữ liệu", Content = rows, ToolTip = "Lưu bảng trước khi nhập dữ liệu" });
        save.Click += async (_, _) => await SaveAsync();
        name.TextChanged += (_, _) => { if (!switching && selected == null) dirty = true; };
        schema.SelectionChanged += (_, _) => { if (!switching && selected == null) dirty = true; };
        tables.SelectionChanged += async (_, _) =>
        {
            if (!switching && tables.SelectedItem is TableName table) await SwitchAsync(table);
        };
        Loaded += async (_, _) =>
        {
            await SwitchAsync(null);
            try
            {
                var schemas = await ProjectDatabase.ReadAsync(target, "SELECT name FROM sys.schemas ORDER BY name", CancellationToken.None);
                switching = true;
                schema.ItemsSource = schemas.Rows.Cast<DataRow>().Select(r => (string)r[0]).ToArray();
                if (selected == null) schema.SelectedItem = "dbo";
                switching = false;
                await RefreshTablesAsync();
            }
            catch (Exception ex) { Error(ex); }
        };
        Closing += async (_, e) =>
        {
            if (busy || switching) { e.Cancel = true; return; }
            if (!dirty) { loading?.Cancel(); return; }
            e.Cancel = true;
            if (await ResolveDraftAsync()) { dirty = false; Close(); }
        };
    }

    private UIElement Structure()
    {
        var panel = new DockPanel();
        Header(panel, Button("Thêm trường", AddField), Button("Chọn khóa ngoại…", async () => await ChooseReferenceAsync()), Button("Bỏ trường nháp", () =>
        {
            if (fields.SelectedItem is Field field) draft.Remove(field);
        }));
        existing.IsReadOnly = true;
        existing.AutoGenerateColumns = true;
        existing.MaxHeight = 220;
        DockPanel.SetDock(existing, Dock.Top);
        panel.Children.Add(existing);
        fields.IsReadOnly = false;
        fields.ItemsSource = draft;
        fields.Columns.Add(TextColumn("Tên trường *", nameof(Field.Name)));
        fields.Columns.Add(new DataGridComboBoxColumn { Header = "Kiểu dữ liệu", ItemsSource = Types,
            SelectedItemBinding = new Binding(nameof(Field.Type)), Width = 145 });
        fields.Columns.Add(TextColumn("Tham số", nameof(Field.Size)));
        foreach (var (title, property) in new[] { ("Bắt buộc", nameof(Field.Required)), ("Khóa chính", nameof(Field.Key)),
            ("Không trùng", nameof(Field.Unique)), ("Tự tăng", nameof(Field.Identity)) })
            fields.Columns.Add(new DataGridCheckBoxColumn { Header = title, Binding = new Binding(property), Width = 115 });
        fields.Columns.Add(TextColumn("Mặc định", nameof(Field.Default)));
        var foreignKey = TextColumn("Khóa ngoại", nameof(Field.ForeignKey));
        foreignKey.IsReadOnly = true;
        fields.Columns.Add(foreignKey);
        panel.Children.Add(fields);
        return panel;
    }

    private static DataGrid Grid()
    {
        var grid = new DataGrid { AutoGenerateColumns = false, EnableColumnVirtualization = true,
            EnableRowVirtualization = true, CanUserAddRows = false, CanUserDeleteRows = false,
            CanUserSortColumns = false, CanUserReorderColumns = false, ColumnWidth = 150 };
        grid.SetResourceReference(DataGrid.CellStyleProperty, "CatalogGridCell");
        grid.AutoGeneratingColumn += (_, e) =>
        {
            e.Column.Width = 150;
            if (e.Column is DataGridBoundColumn { Binding: Binding binding })
                binding.Mode = BindingMode.OneTime;
        };
        VirtualizingPanel.SetVirtualizationMode(grid, VirtualizationMode.Recycling);
        ScrollViewer.SetCanContentScroll(grid, true);
        // Thumb drags can cross hundreds of columns in one gesture. Realize the final viewport on release.
        ScrollViewer.SetIsDeferredScrollingEnabled(grid, true);
        return grid;
    }

    private static DataGridTextColumn TextColumn(string title, string property) => new()
    {
        Header = title, Binding = new Binding(property), Width = 150
    };

    private static TextBlock Label(string value) => new()
    {
        Text = value, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0)
    };

    private static Button Button(string title, Action action)
    {
        var button = new Button { Content = title };
        button.Click += (_, _) => action();
        return button;
    }

    private static void Header(DockPanel dock, params UIElement[] controls)
    {
        var row = new WrapPanel { Margin = new Thickness(0, 0, 0, 8) };
        foreach (var control in controls) row.Children.Add(control);
        DockPanel.SetDock(row, Dock.Top);
        dock.Children.Add(row);
    }

    private async Task RefreshTablesAsync()
    {
        var result = await ProjectDatabase.ReadAsync(target, """
            SELECT s.name,t.name FROM sys.tables t JOIN sys.schemas s ON s.schema_id=t.schema_id
            ORDER BY s.name,t.name
            """, CancellationToken.None);
        var previous = switching;
        switching = true;
        tables.ItemsSource = result.Rows.Cast<DataRow>().Select(r => new TableName((string)r[0], (string)r[1])).ToArray();
        tables.SelectedItem = selected;
        switching = previous;
    }

    private async Task<bool> ResolveDraftAsync()
    {
        if (!dirty) return true;
        var answer = MessageBox.Show(this, "Có thay đổi chưa lưu. Có: Lưu · Không: Bỏ thay đổi · Hủy: Ở lại",
            Title, MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
        return answer == MessageBoxResult.No || answer == MessageBoxResult.Yes && await SaveAsync();
    }

    private async Task SwitchAsync(TableName? table)
    {
        if (busy || switching) return;
        switching = true;
        try
        {
            if (!await ResolveDraftAsync()) { tables.SelectedItem = selected; return; }
            loading?.Cancel();
            ClearCellError();
            selected = table;
            tables.SelectedItem = table;
            schema.SelectedItem = table?.Schema ?? "dbo";
            schema.IsEnabled = table == null;
            name.Text = table?.Name ?? "";
            name.IsReadOnly = table != null;
            metadata = null;
            draft.Clear();
            records.Clear();
            data.ItemsSource = null;
            data.Columns.Clear();
            preview.SetPage([], []);
            data.Visibility = Visibility.Collapsed;
            previewScroll.Visibility = Visibility.Visible;
            existing.ItemsSource = null;
            dirty = false;
            uncertain = false;
            page = 0;
            ((TabItem)modes.Items[1]).IsEnabled = table != null;
            modes.SelectedIndex = table == null ? 0 : 1;
            if (table == null)
            {
                var system = new DataTable();
                system.Columns.Add("Tên trường");
                system.Columns.Add("Kiểu dữ liệu");
                system.Columns.Add("Ràng buộc");
                for (int i = 0; i < Audit.Length; i++) system.Rows.Add(Audit[i], i < 2 ? "int" : "datetime", "Hệ thống");
                existing.ItemsSource = system.DefaultView;
                status.Text = "Nhập tên bảng và khai báo trường.";
            }
        }
        finally { switching = false; }
        if (table != null) await LoadAsync(table);
    }

    private Task<DataTable> MetadataAsync(TableName table, CancellationToken token) =>
        ProjectDatabase.ReadAsync(target, """
            SELECT c.name AS [Tên trường], TYPE_NAME(c.user_type_id) AS [Kiểu dữ liệu],
                   c.max_length AS [Độ dài], c.precision, c.scale, c.is_nullable,
                   c.is_identity, c.is_computed, d.definition AS [Mặc định], c.generated_always_type,
                   c.system_type_id, c.column_id, c.object_id,
                   CAST(CASE WHEN marker.name IS NOT NULL AND c.name IN ('user_id0', 'user_id2', 'datetime0', 'datetime2')
                       THEN 1 ELSE 0 END AS bit) AS [Hệ thống]
            FROM sys.columns c LEFT JOIN sys.default_constraints d ON d.object_id=c.default_object_id
            LEFT JOIN sys.extended_properties marker ON marker.class=1 AND marker.major_id=c.object_id
                AND marker.minor_id=0 AND marker.name=N'LSCode.Catalog'
            WHERE c.object_id=OBJECT_ID(@table, 'U') ORDER BY c.column_id
            """, token, new SqlParameter("@table", table.Sql));

    private async Task LoadAsync(TableName table)
    {
        loading?.Cancel();
        using var request = new CancellationTokenSource();
        loading = request;
        status.Text = "Đang tải bảng…";
        save.IsEnabled = false;
        var offset = page * 100;
        try
        {
            var columnsTask = MetadataAsync(table, request.Token);
            var rowsTask = ProjectDatabase.ReadAsync(target,
                "SELECT * FROM " + table.Sql + " ORDER BY (SELECT NULL) OFFSET @offset ROWS FETCH NEXT 100 ROWS ONLY",
                request.Token, new SqlParameter("@offset", offset));
            await Task.WhenAll(columnsTask, rowsTask);
            var columns = await columnsTask;
            if (columns.Rows.Count == 0) throw new InvalidOperationException("Không đọc được cấu trúc bảng.");
            var result = await rowsTask;
            request.Token.ThrowIfCancellationRequested();
            await preview.PreparePageAsync(result.Columns.Cast<DataColumn>()
                .Select(c => (string?)c.ExtendedProperties["OriginalName"] ?? c.ColumnName).ToArray(),
                result.Rows.Cast<DataRow>().Select(row => row.ItemArray).ToArray(), request.Token);
            metadata = columns;
            existing.ItemsSource = columns.DefaultView;
            data.ItemsSource = null;
            data.Columns.Clear();
            data.Visibility = Visibility.Collapsed;
            previewScroll.Visibility = Visibility.Visible;
            status.Text = result.Rows.Count == 0 ? "Chưa có bản ghi trên trang này." : $"Trang {page + 1} · {result.Rows.Count} bản ghi";
        }
        catch (OperationCanceledException) when (request.IsCancellationRequested) { }
        catch (Exception ex) { if (!request.IsCancellationRequested) Error(ex); }
        finally
        {
            if (ReferenceEquals(loading, request)) { loading = null; save.IsEnabled = !busy; }
        }
    }

    private async Task PageAsync(int step)
    {
        if (busy || switching || selected == null || page + step < 0 || !await ResolveDraftAsync()) return;
        records.Clear();
        draft.Clear();
        dirty = false;
        page += step;
        await LoadAsync(selected);
    }

    private void AddField()
    {
        if (busy || loading != null || selected != null && metadata == null) return;
        if (records.Count != 0) { status.Text = "Lưu bản ghi trước khi thêm trường."; return; }
        modes.SelectedIndex = 0;
        var field = new Field();
        draft.Add(field);
        dirty = true;
        fields.ScrollIntoView(field);
        status.Text = $"{draft.Count} trường mới chưa lưu";
    }

    private async Task ChooseReferenceAsync()
    {
        if (busy || fields.SelectedItem is not Field field) return;
        if (!fields.CommitEdit(DataGridEditingUnit.Cell, true) || !fields.CommitEdit(DataGridEditingUnit.Row, true)) return;
        try
        {
            var candidates = await ProjectDatabase.ReadAsync(target, """
                SELECT DISTINCT s.name, t.name, c.name, TYPE_NAME(c.user_type_id)
                FROM sys.indexes i
                JOIN sys.tables t ON t.object_id=i.object_id
                JOIN sys.schemas s ON s.schema_id=t.schema_id
                JOIN sys.index_columns k ON k.object_id=i.object_id AND k.index_id=i.index_id AND k.key_ordinal=1
                JOIN sys.columns c ON c.object_id=k.object_id AND c.column_id=k.column_id
                WHERE i.is_unique=1 AND i.has_filter=0 AND i.is_disabled=0 AND i.is_hypothetical=0
                  AND NOT EXISTS (SELECT 1 FROM sys.index_columns x WHERE x.object_id=i.object_id
                      AND x.index_id=i.index_id AND x.key_ordinal>1)
                ORDER BY s.name,t.name,c.name
                """, CancellationToken.None);
            if (!IsLoaded || !draft.Contains(field) || busy) return;
            var options = candidates.Rows.Cast<DataRow>().Where(r => (string)r[3] == field.Type)
                .Select(r => r.ItemArray.Cast<string>().ToArray()).ToArray();
            var chooser = new ComboBox { MinWidth = 280, ItemsSource = options.Select(r =>
                string.Join(".", r.Take(3))).ToArray(), IsTextSearchEnabled = true };
            var panel = new DockPanel { Margin = new Thickness(16) };
            var dialog = new Window { Title = "Khóa ngoại · " + field.Name, Owner = this,
                SizeToContent = SizeToContent.WidthAndHeight, WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Content = panel };
            Header(panel, Label(options.Length == 0 ? "Không có cột khóa tương thích." : "Cột đích"), chooser);
            Header(panel, Button("Áp dụng", () =>
            {
                if (chooser.SelectedIndex < 0) return;
                field.Reference = options[chooser.SelectedIndex];
                dialog.DialogResult = true;
            }), Button("Bỏ khóa ngoại", () => { field.Reference = null; dialog.DialogResult = true; }));
            if (dialog.ShowDialog() == true)
            {
                fields.Items.Refresh();
                dirty = true;
                status.Text = field.Reference is { } reference
                    ? field.Name + " → " + string.Join(".", reference.Take(3)) : "Đã bỏ khóa ngoại khỏi trường nháp.";
            }
        }
        catch (Exception ex) { Error(ex); }
    }

    private sealed class CellException(object row, int column, string message, bool structure = false) : Exception(message)
    {
        public object Row { get; } = row;
        public int Column { get; } = column;
        public bool Structure { get; } = structure;
    }

    private sealed class ValueColumn(string type, bool nullable, bool hasDefault) : DataGridTextColumn
    {
        protected override FrameworkElement GenerateEditingElement(DataGridCell cell, object item)
        {
            if (type == "bit")
            {
                var values = new List<string> { "0", "1" };
                if (nullable) values.Add(":null");
                if (hasDefault) values.Add(":default");
                var choice = new ComboBox { ItemsSource = values };
                choice.SetBinding(ComboBox.SelectedItemProperty, Binding);
                return choice;
            }

            FrameworkElement editor;
            DependencyProperty property;
            if (type == "date" && item is string?[] row && row[cell.Column.DisplayIndex] is not (":null" or ":default"))
            {
                var date = new DatePicker { Language = System.Windows.Markup.XmlLanguage.GetLanguage("en-US") };
                property = DatePicker.TextProperty;
                date.SetBinding(property, Binding);
                editor = date;
            }
            else
            {
                editor = base.GenerateEditingElement(cell, item);
                property = TextBox.TextProperty;
            }

            var menu = new ContextMenu();
            void AddOption(string label, string value)
            {
                var option = new MenuItem { Header = label };
                option.Click += (_, _) =>
                {
                    if (editor is DatePicker && item is string?[] record)
                    {
                        var index = cell.Column.DisplayIndex;
                        var grid = ItemsControl.ItemsControlFromItemContainer(
                            DataGridRow.GetRowContainingElement(cell)) as DataGrid;
                        grid?.CancelEdit(DataGridEditingUnit.Cell);
                        record[index] = value;
                        grid?.Items.Refresh();
                    }
                    else
                    {
                        editor.SetCurrentValue(property, value);
                        editor.GetBindingExpression(property)?.UpdateSource();
                    }
                };
                menu.Items.Add(option);
            }
            if (nullable) AddOption("Đặt NULL", ":null");
            if (hasDefault) AddOption("Dùng mặc định", ":default");
            if (type is "char" or "nchar" or "varchar" or "nvarchar") AddOption("Chuỗi rỗng", ":empty");
            if (menu.Items.Count != 0) editor.ContextMenu = menu;
            if (type is "datetime" or "datetime2") editor.ToolTip = "Nhập ngày giờ theo dạng yyyy-MM-dd HH:mm:ss.";
            return editor;
        }
    }

    private void AddRecord()
    {
        if (busy || loading != null || metadata == null) return;
        if (draft.Count != 0) { status.Text = "Lưu trường mới trước khi thêm bản ghi."; return; }
        if (records.Count == 0)
        {
            data.ItemsSource = null;
            data.AutoGenerateColumns = false;
            data.Columns.Clear();
            foreach (DataRow column in metadata.Rows)
            {
                int i = metadata.Rows.IndexOf(column);
                var editor = new ValueColumn((string)column[1], (bool)column[5], column[8] != DBNull.Value)
                {
                    Header = (string)column[0], Binding = new Binding($"[{i}]"), Width = 150
                };
                editor.IsReadOnly = Managed(column);
                data.Columns.Add(editor);
            }
            data.IsReadOnly = false;
            previewScroll.Visibility = Visibility.Collapsed;
            data.Visibility = Visibility.Visible;
            data.ItemsSource = records;
        }
        records.Add(new string?[metadata.Rows.Count]);
        dirty = true;
        data.ScrollIntoView(records[^1]);
        status.Text = $"{records.Count} bản ghi mới chưa lưu · NULL: :null · DEFAULT: :default · Chuỗi rỗng: :empty";
    }

    private async Task<bool> SaveAsync()
    {
        if (busy || loading != null || !dirty) return !dirty;
        if (uncertain)
        {
            status.Text = "Kết quả lưu chưa xác định. Kiểm tra trên SQL Server trước khi bỏ bản nháp và mở lại bảng.";
            return false;
        }
        foreach (var grid in new[] { fields, data })
            if (!grid.CommitEdit(DataGridEditingUnit.Cell, true) || !grid.CommitEdit(DataGridEditingUnit.Row, true))
                return false;
        ClearCellError();
        var table = selected;
        busy = true;
        ((UIElement)Content).IsEnabled = false;
        save.Content = "Đang lưu…";
        try
        {
            var creating = table == null;
            table ??= new TableName(schema.SelectedItem as string ?? "", name.Text.Trim());
            var commands = records.Count > 0 ? InsertCommands(table) : DefinitionCommands(table, creating);
            if (commands.Count == 0) { dirty = false; return true; }
            if (!creating)
            {
                var actual = await MetadataAsync(table, CancellationToken.None);
                if (metadata == null || !actual.Rows.Cast<DataRow>().Select(Signature)
                    .SequenceEqual(metadata.Rows.Cast<DataRow>().Select(Signature)))
                    throw new InvalidOperationException("Cấu trúc bảng đã thay đổi. Mở lại bảng để đối chiếu.");
            }
            await WriteAsync(commands);
            dirty = false;
            draft.Clear();
            records.Clear();
            selected = table;
            name.IsReadOnly = true;
            schema.IsEnabled = false;
            ((TabItem)modes.Items[1]).IsEnabled = true;
            modes.SelectedIndex = 1;
            changed();
            try
            {
                await RefreshTablesAsync();
                await LoadAsync(table);
                status.Text = (creating ? "Đã tạo bảng " : "Đã lưu ") + table;
            }
            catch (Exception ex) { status.Text = "Đã lưu. Không tải lại được danh sách."; System.Diagnostics.Debug.WriteLine(ex.GetType()); }
            return true;
        }
        catch (CellException ex)
        {
            modes.SelectedIndex = ex.Structure ? 0 : 1;
            status.Text = ex.Message;
            var grid = ex.Structure ? fields : data;
            grid.ScrollIntoView(ex.Row, grid.Columns[ex.Column]);
            grid.CurrentCell = new DataGridCellInfo(ex.Row, grid.Columns[ex.Column]);
            MarkCell(grid, ex.Row, ex.Column, ex.Message);
            return false;
        }
        catch (UncertainCommitException ex) { uncertain = true; status.Text = ex.Message; return false; }
        catch (Exception ex) { Error(ex); return false; }
        finally
        {
            busy = false;
            ((UIElement)Content).IsEnabled = true;
            save.IsEnabled = true;
            save.Content = "Lưu";
        }
    }

    private void ClearCellError()
    {
        if (highlightedCell == null) return;
        highlightedCell.Unloaded -= ClearUnloadedCell;
        highlightedCell.ClearValue(ToolTipProperty);
        highlightedCell.ClearValue(Control.BorderBrushProperty);
        highlightedCell.ClearValue(Control.BorderThicknessProperty);
        highlightedCell = null;
    }

    private void ClearUnloadedCell(object sender, RoutedEventArgs e) => ClearCellError();

    private void MarkCell(DataGrid grid, object row, int column, string message)
    {
        grid.UpdateLayout();
        DependencyObject? element = grid.Columns[column].GetCellContent(row);
        while (element != null && element is not DataGridCell)
            element = System.Windows.Media.VisualTreeHelper.GetParent(element);
        if (element is not DataGridCell cell) return;
        highlightedCell = cell;
        cell.Unloaded += ClearUnloadedCell;
        cell.ToolTip = message;
        cell.BorderBrush = SystemColors.HighlightBrush;
        cell.BorderThickness = new Thickness(2);
    }

    private static string Signature(DataRow row) => string.Join("\u001f", row.ItemArray);

    private sealed class UncertainCommitException() : Exception(
        "Chưa xác nhận được kết quả lưu. Kiểm tra trên SQL Server trước khi thử lại.");

    private sealed record Statement(string Sql, SqlParameter[] Parameters);

    private async Task WriteAsync(List<Statement> statements)
    {
        await using var connection = new SqlConnection(target.ConnectionString);
        await connection.OpenAsync().ConfigureAwait(false);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync().ConfigureAwait(false);
        foreach (var statement in statements)
        {
            await using var command = new SqlCommand(statement.Sql, connection, transaction);
            command.Parameters.AddRange(statement.Parameters);
            await command.ExecuteNonQueryAsync().ConfigureAwait(false);
        }
        try { await transaction.CommitAsync().ConfigureAwait(false); }
        catch (Exception)
        {
            throw new UncertainCommitException();
        }
    }

    private List<Statement> DefinitionCommands(TableName table, bool creating)
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var columns = new List<string>();
        var keys = new List<string>();
        if (creating) names.UnionWith(Audit);
        if (draft.Count(f => f.Identity) > 1) throw new InvalidOperationException("Mỗi bảng chỉ có một trường tự tăng.");
        foreach (var field in draft)
        {
            var name = field.Name.Trim();
            string id;
            try { id = Quote(name); }
            catch (InvalidOperationException ex) { throw new CellException(field, 0, ex.Message, true); }
            if (!names.Add(name)) throw new CellException(field, 0, "Tên trường bị trùng: " + name, true);
            if (field.Identity && (field.Type is not ("int" or "bigint" or "smallint" or "tinyint") || field.Default != null))
                throw new CellException(field, 6, name + ": tự tăng cần kiểu số nguyên và không có DEFAULT.", true);
            string sqlType;
            try { sqlType = SqlType(field); }
            catch (InvalidOperationException ex) { throw new CellException(field, 2, ex.Message, true); }
            var definition = id + " " + sqlType + (field.Identity ? " IDENTITY(1,1)" : "") +
                (field.Required || field.Key ? " NOT NULL" : " NULL") + (field.Unique ? " UNIQUE" : "");
            if (field.Default != null)
            {
                object value;
                try { value = ConvertValue(field.Type, field.Default); }
                catch (Exception ex) when (ex is FormatException or OverflowException)
                { throw new CellException(field, 7, "Giá trị mặc định không hợp lệ.", true); }
                definition += " DEFAULT N'" + (value is DateTime date ? date.ToString("yyyy-MM-ddTHH:mm:ss.fffffff", System.Globalization.CultureInfo.InvariantCulture) :
                    Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture)!)
                    .Replace("'", "''", StringComparison.Ordinal) + "'";
            }
            if (field.Reference is { } reference)
            {
                if (!string.Equals(reference[3], field.Type, StringComparison.Ordinal))
                    throw new CellException(field, 8, name + ": kiểu khóa ngoại phải khớp với cột đích.", true);
                definition += " REFERENCES " + Quote(reference[0]) + "." + Quote(reference[1]) + "(" + Quote(reference[2]) + ")";
            }
            columns.Add(definition);
            if (field.Key) keys.Add(id);
        }
        if (creating)
            for (int i = 0; i < Audit.Length; i++) columns.Add(Quote(Audit[i]) + (i < 2 ? " int NULL" : " datetime NULL"));
        if (keys.Count > 0) columns.Add("PRIMARY KEY (" + string.Join(", ", keys) + ")");
        if (columns.Count == 0) return [];
        var list = string.Join(", ", columns);
        if (!creating) return [new Statement($"ALTER TABLE {table.Sql} ADD {list}", [])];
        return [
            new Statement($"CREATE TABLE {table.Sql} ({list})", []),
            new Statement("""
                EXEC sys.sp_addextendedproperty @name=N'LSCode.Catalog', @value=1,
                    @level0type=N'SCHEMA', @level0name=@schema, @level1type=N'TABLE', @level1name=@table
                """, [new SqlParameter("@schema", table.Schema), new SqlParameter("@table", table.Name)])
        ];
    }

    private List<Statement> InsertCommands(TableName table)
    {
        var result = new List<Statement>();
        foreach (var record in records)
        {
            var columns = new List<string>();
            var values = new List<string>();
            var parameters = new List<SqlParameter>();
            for (int i = 0; i < record.Length; i++)
            {
                var column = metadata!.Rows[i];
                var value = record[i];
                if ((bool)column[13])
                {
                    columns.Add(Quote((string)column[0]));
                    values.Add(((string)column[0]).StartsWith("datetime", StringComparison.Ordinal) ? "@auditTime" : "NULL");
                    continue;
                }
                if (Managed(column)) continue;
                if (value is null or ":default")
                {
                    if (column[8] == DBNull.Value && !(bool)column[5])
                        throw new CellException(record, i, "Trường này bắt buộc nhập.");
                    continue;
                }
                var parameter = "@p" + i;
                object converted;
                try { converted = value == ":null" ? DBNull.Value : ConvertValue((string)column[1], value == ":empty" ? "" : value); }
                catch (Exception ex) when (ex is FormatException or OverflowException)
                { throw new CellException(record, i, "Giá trị không hợp lệ cho " + column[1] + "."); }
                if (converted == DBNull.Value && !(bool)column[5])
                    throw new CellException(record, i, "Trường này bắt buộc nhập.");
                if (converted is DateTime date && (string)column[1] == "datetime" && date.Year < 1753)
                    throw new CellException(record, i, "Kiểu datetime chỉ nhận ngày từ năm 1753.");
                if (converted is string text && (string)column[1] is "nchar" or "nvarchar" &&
                    (short)column[2] is > 0 and var maxBytes && text.Length > maxBytes / 2)
                    throw new CellException(record, i, "Giá trị vượt quá độ dài của cột.");
                columns.Add(Quote((string)column[0]));
                values.Add(parameter);
                var argument = new SqlParameter(parameter, converted);
                switch ((string)column[1])
                {
                    case "bit": argument.SqlDbType = SqlDbType.Bit; break;
                    case "date": argument.SqlDbType = SqlDbType.Date; break;
                    case "datetime": argument.SqlDbType = SqlDbType.DateTime; break;
                    case "datetime2":
                        argument.SqlDbType = SqlDbType.DateTime2;
                        argument.Scale = (byte)column[4];
                        break;
                }
                parameters.Add(argument);
            }
            var sql = columns.Count == 0 ? $"INSERT INTO {table.Sql} DEFAULT VALUES" :
                $"INSERT INTO {table.Sql} ({string.Join(", ", columns)}) VALUES ({string.Join(", ", values)})";
            if (values.Contains("@auditTime")) sql = "DECLARE @auditTime datetime = GETDATE(); " + sql;
            result.Add(new Statement(sql, parameters.ToArray()));
        }
        return result;
    }

    private static bool Managed(DataRow column) => (bool)column[13] || (bool)column[6] || (bool)column[7] ||
        Convert.ToInt32(column[9]) != 0 || Convert.ToInt32(column[10]) == 189;

    private static object ConvertValue(string type, string text)
    {
        var culture = System.Globalization.CultureInfo.InvariantCulture;
        return type switch
        {
            "int" => int.Parse(text, culture),
            "bigint" => long.Parse(text, culture),
            "smallint" => short.Parse(text, culture),
            "tinyint" => byte.Parse(text, culture),
            "decimal" or "numeric" => decimal.Parse(text, System.Globalization.NumberStyles.AllowLeadingSign |
                System.Globalization.NumberStyles.AllowDecimalPoint, culture),
            "bit" => text switch { "0" => 0, "1" => 1, _ => throw new FormatException("Bit chỉ nhận 0 hoặc 1.") },
            "date" or "datetime" or "datetime2" => DateTime.Parse(text, culture),
            "uniqueidentifier" => Guid.Parse(text),
            _ => text
        };
    }

    private static string SqlType(Field field)
    {
        if (!Types.Contains(field.Type)) throw new InvalidOperationException("Kiểu dữ liệu không hợp lệ.");
        var size = field.Size.Trim();
        if (field.Type is "nvarchar" or "varchar" or "nchar" or "char")
        {
            if (size.Equals("max", StringComparison.OrdinalIgnoreCase) && field.Type is "nvarchar" or "varchar")
                return field.Type + "(max)";
            if (int.TryParse(size, out int length) && length > 0 &&
                length <= (field.Type.StartsWith('n') ? 4000 : 8000)) return $"{field.Type}({length})";
        }
        else if (field.Type is "decimal" or "numeric")
        {
            var parts = size.Split(',');
            if (parts.Length == 2 && int.TryParse(parts[0], out int precision) && precision is >= 1 and <= 38 &&
                int.TryParse(parts[1], out int scale) && scale >= 0 && scale <= precision)
                return $"{field.Type}({precision},{scale})";
        }
        else if (field.Type == "datetime2")
        {
            if (int.TryParse(size, out int precision) && precision is >= 0 and <= 7) return $"datetime2({precision})";
        }
        else return field.Type;
        throw new InvalidOperationException(field.Name + ": tham số kiểu không hợp lệ.");
    }

    private static string Quote(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length > 128 || name.Any(char.IsControl))
            throw new InvalidOperationException("Tên schema, bảng hoặc trường không hợp lệ.");
        return "[" + name.Replace("]", "]]", StringComparison.Ordinal) + "]";
    }

    private void Error(Exception error)
    {
        status.Text = error switch
        {
            SqlException sql => $"SQL Server báo lỗi {sql.Number}. Kiểm tra quyền, kết nối hoặc ràng buộc của bảng.",
            InvalidOperationException or FormatException or OverflowException => error.Message,
            _ => "Không hoàn tất được thao tác danh mục."
        };
    }
}
