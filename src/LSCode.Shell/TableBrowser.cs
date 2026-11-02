using System.Data;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using LSCode.Core;
using LSCode.Shell.Services;
using Microsoft.Data.SqlClient;
using Microsoft.SqlServer.TransactSql.ScriptDom;

namespace LSCode.Shell;

public sealed class TableBrowser : Window
{
    private readonly ProgressBar loading = new() { IsIndeterminate = true, Height = 4, Visibility = Visibility.Collapsed };
    private SqlTarget target;
    private readonly CancellationTokenSource lifetime = new();
    private CancellationTokenSource? operation;
    private readonly TextBox tableFilter = new() { MinWidth = 240, ToolTip = "Gõ tên bảng hoặc view. Danh sách hiện tối đa 10 kết quả." };
    private readonly ListBox tableHits = new() { MaxHeight = 220 };
    private readonly List<int> hitIndexes = [];
    private int selectedTable = -1;
    private readonly ComboBox databases = new() { MinWidth = 170 };
    private readonly TextBox select = new() { Text = "*", MinWidth = 150 };
    private readonly TextBox where = new() { MinWidth = 180 };
    private TextBox completionInput = null!;
    private readonly ListBox whereHits = new() { MaxHeight = 220, MinWidth = 300, DisplayMemberPath = "Text" };
    private readonly Popup wherePopup = new() { Placement = PlacementMode.Bottom, StaysOpen = true, AllowsTransparency = true };
    private readonly List<string> columnNames = [];
    private readonly TextBox order = new() { MinWidth = 120 };
    private readonly StackPanel columns = new();
    private readonly DataGrid rows = new() { IsReadOnly = true, CanUserAddRows = false, AutoGenerateColumns = true, EnableRowVirtualization = true, EnableColumnVirtualization = false };
    private readonly TextBlock status = new() { Margin = new Thickness(8), TextWrapping = TextWrapping.Wrap };
    private readonly Button run = new() { Content = "▶ Run (tối đa 1000 dòng)" };
    private readonly List<(string Schema, string Name)> tableNames = [];
    private bool initializing;
    private readonly TaskCompletionSource ready = new();

    private IReadOnlyList<SqlTarget> configuredTargets = [];
    public TableBrowser(Window owner, SqlTarget sqlTarget, IReadOnlyList<SqlTarget>? targets = null) : this(owner, sqlTarget, true) { configuredTargets = targets ?? [sqlTarget]; }
    private TableBrowser(Window owner, SqlTarget sqlTarget, bool initialize)
    {
        FontFamily = new System.Windows.Media.FontFamily("Segoe UI"); FontSize = 14; UseLayoutRounding = true;
        Owner = owner; target = sqlTarget; Width = 1200; Height = 760; MinWidth = 850; MinHeight = 500;
        SetResourceReference(BackgroundProperty, "Surface"); SetResourceReference(ForegroundProperty, "Text");
        Title = "Cấu trúc và dữ liệu — " + target.Server + " / " + target.Database;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        var dock = new DockPanel { Margin = new Thickness(20) }; Content = dock;
        var heading = new TextBlock { Text = "Cấu trúc và dữ liệu", FontSize = 22, FontWeight = FontWeights.SemiBold, Margin = new Thickness(4, 0, 4, 16) }; DockPanel.SetDock(heading, Dock.Top); dock.Children.Add(heading);
        run.SetResourceReference(StyleProperty, "PrimaryButton");
        loading.SetResourceReference(ForegroundProperty, "Accent"); loading.SetResourceReference(BackgroundProperty, "Raised"); DockPanel.SetDock(loading, Dock.Top); dock.Children.Add(loading);
        var top = new Grid(); top.ColumnDefinitions.Add(new()); top.ColumnDefinitions.Add(new() { Width = new GridLength(1, GridUnitType.Star) });
        ResultGrids.Prepare(rows, compact: true);
        var filters = new StackPanel { Margin = new Thickness(0, 0, 16, 16) }; var source = new StackPanel { Margin = new Thickness(0, 0, 0, 16) }; Grid.SetColumn(source, 1);
        top.Children.Add(filters); top.Children.Add(source);
        void Field(Panel panel, string label, UIElement value) { panel.Children.Add(new TextBlock { Text = label, Margin = new Thickness(4, 3, 4, 0) }); panel.Children.Add(value); }
        Field(filters, "SELECT", select); Field(filters, "WHERE (không bắt buộc)", where); Field(filters, "ORDER BY (không bắt buộc)", order);
        select.TabIndex = 0; tableFilter.TabIndex = 1; where.TabIndex = 2; order.TabIndex = 3; databases.TabIndex = 4;
        completionInput = where; wherePopup.Child = whereHits;
        foreach (var input in new[] { select, where, order })
        {
        input.GotKeyboardFocus += (_, _) => { wherePopup.IsOpen = false; completionInput = input; wherePopup.PlacementTarget = input; };
        input.SelectionChanged += (_, _) => ShowWhereMatches();
        input.PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Space && Keyboard.Modifiers == ModifierKeys.Control) { ShowWhereMatches(true); e.Handled = true; }
            else if (wherePopup.IsOpen && e.Key is Key.Down or Key.Up) { whereHits.SelectedIndex = Math.Clamp(whereHits.SelectedIndex + (e.Key == Key.Down ? 1 : -1), 0, whereHits.Items.Count - 1); whereHits.ScrollIntoView(whereHits.SelectedItem); e.Handled = true; }
            else if (wherePopup.IsOpen && e.Key is Key.Enter or Key.Tab) { InsertWhereMatch(); e.Handled = true; }
            else if (e.Key == Key.Escape) { wherePopup.IsOpen = false; e.Handled = true; }
        };
        input.LostKeyboardFocus += (_, _) => Dispatcher.BeginInvoke(new Action(() => { if (!completionInput.IsKeyboardFocusWithin && !whereHits.IsKeyboardFocusWithin) wherePopup.IsOpen = false; }));
        }
        whereHits.PreviewMouseLeftButtonUp += (_, _) => InsertWhereMatch();
        whereHits.LostKeyboardFocus += (_, _) => Dispatcher.BeginInvoke(new Action(() => { if (!completionInput.IsKeyboardFocusWithin && !whereHits.IsKeyboardFocusWithin) wherePopup.IsOpen = false; }));
        Deactivated += (_, _) => wherePopup.IsOpen = false;
        Field(source, "DATABASE", databases); Field(source, "FROM — gõ tên bảng / view", tableFilter); source.Children.Add(tableHits);
        var buttons = new WrapPanel(); buttons.Children.Add(run); var cancel = new Button { Content = "Dừng" }; buttons.Children.Add(cancel); source.Children.Add(buttons);
        source.Children.Add(new TextBlock { Text = "Chỉ đọc • Chọn cột bên trái hoặc nhập biểu thức SELECT", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(5) });
        DockPanel.SetDock(top, Dock.Top); dock.Children.Add(top);
        DockPanel.SetDock(status, Dock.Bottom); dock.Children.Add(status);
        var body = new Grid(); body.ColumnDefinitions.Add(new() { Width = new GridLength(260), MinWidth = 150 }); body.ColumnDefinitions.Add(new() { Width = new GridLength(5) }); body.ColumnDefinitions.Add(new());
        body.Children.Add(new ScrollViewer { Content = columns, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto });
        var splitter = new GridSplitter { Width = 5, HorizontalAlignment = HorizontalAlignment.Stretch }; Grid.SetColumn(splitter, 1); body.Children.Add(splitter);
        Grid.SetColumn(rows, 2); body.Children.Add(rows); dock.Children.Add(body);
        run.Click += async (_, _) => await RunAsync(); cancel.Click += (_, _) => operation?.Cancel();
        tableFilter.TextChanged += (_, _) => { if (!initializing) { operation?.Cancel(); selectedTable = -1; columnNames.Clear(); columns.Children.Clear(); wherePopup.IsOpen = false; ShowMatches(); } };
        tableFilter.GotKeyboardFocus += (_, _) => { if (selectedTable < 0) ShowMatches(); };
        tableFilter.LostKeyboardFocus += (_, _) => Dispatcher.BeginInvoke(new Action(async () =>
        {
            if (tableFilter.IsKeyboardFocusWithin || tableHits.IsKeyboardFocusWithin) return;
            tableHits.Visibility = Visibility.Collapsed;
            var exact = ResolveTable();
            if (exact >= 0 && exact != selectedTable) await SelectTableAsync(exact);
        }));
        tableFilter.KeyDown += async (_, e) => { if (e.Key == Key.Escape) { tableHits.Visibility = Visibility.Collapsed; e.Handled = true; return; } if (e.Key != Key.Enter) return; e.Handled = true; var exact = ResolveTable(); if (exact >= 0) await SelectTableAsync(exact); else if (hitIndexes.Count > 0) await SelectTableAsync(hitIndexes[0]); };
        var tabOrder = new[] { select, tableFilter, where, order };
        foreach (var input in tabOrder) input.PreviewKeyDown += (_, e) =>
        {
            if (e.Handled || e.Key != Key.Tab || (Keyboard.Modifiers & (ModifierKeys.Control | ModifierKeys.Alt)) != 0) return;
            var next = Array.IndexOf(tabOrder, input) + (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) ? -1 : 1);
            if (next >= 0 && next < tabOrder.Length) { tabOrder[next].Focus(); e.Handled = true; }
        };
        tableHits.SelectionChanged += async (_, _) => { if (!initializing) await ChooseHitAsync(); };
        databases.SelectionChanged += async (_, _) =>
        {
            if (initializing || databases.SelectedItem is not SqlTarget selectedTarget) return;
            target = selectedTarget; await LoadTablesAsync();
        };
        Loaded += async (_, _) => { try { if (initialize) await InitializeAsync(); } finally { ready.TrySetResult(); } };
        Closed += (_, _) => { wherePopup.IsOpen = false; lifetime.Cancel(); operation?.Cancel(); };
    }
    private async Task<bool> Work(Func<CancellationToken, Task> action, bool silent = false)
    {
        operation?.Cancel();
        using var current = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token); operation = current;
        run.IsEnabled = false; loading.Visibility = Visibility.Visible; status.Text = "LS · Đang tải…";
        try { await action(current.Token); current.Token.ThrowIfCancellationRequested(); return true; }
        catch (OperationCanceledException) { if (operation == current) status.Text = "Đã hủy."; }
        catch (Exception ex)
        {
            // Display errors in status text instead of throwing popups
            if (operation == current)
            {
                var msg = ex.Message;
                if (msg.Length > 300) msg = msg[..300] + "…";
                status.Text = "Lỗi: " + msg;
            }
        }
        finally { if (operation == current) { operation = null; run.IsEnabled = true; loading.Visibility = Visibility.Collapsed; } }
        return false;
    }
    private async Task InitializeAsync()
    {
        initializing = true;
        databases.DisplayMemberPath = "Database";
        databases.ItemsSource = configuredTargets;
        var selected = configuredTargets.FirstOrDefault(t => t.Server.Equals(target.Server, StringComparison.OrdinalIgnoreCase) && t.Database.Equals(target.Database, StringComparison.OrdinalIgnoreCase)) ?? configuredTargets.FirstOrDefault();
        databases.SelectedItem = selected; initializing = false;
        if (selected == null) { status.Text = "connectionStrings không có database cụ thể."; run.IsEnabled = false; return; }
        target = selected;
        if (!lifetime.IsCancellationRequested) await LoadTablesAsync();
    }
    private async Task LoadTablesAsync()
    {
        selectedTable = -1; columnNames.Clear(); columns.Children.Clear(); wherePopup.IsOpen = false; tableHits.Visibility = Visibility.Collapsed;
        var loaded = await Work(async token =>
        {
            // Load table names - limit to 5000 tables to avoid metadata overload
            var result = await ProjectDatabase.ReadAllAsync(target, "SELECT TABLE_SCHEMA,TABLE_NAME FROM INFORMATION_SCHEMA.TABLES ORDER BY TABLE_SCHEMA,TABLE_NAME", token);
            token.ThrowIfCancellationRequested(); rows.ItemsSource = null; columns.Children.Clear(); tableNames.Clear();
            tableNames.AddRange(result.Rows.Cast<DataRow>().Select(r => ((string)r[0], (string)r[1])));
            initializing = true;
            // Don't auto-select a table - let user type to search
            selectedTable = -1;
            tableFilter.Text = "";
            ShowMatches();
            initializing = false;
            Title = "Cấu trúc và dữ liệu — " + target.Server + " / " + target.Database;
            status.Text = $"{tableNames.Count} bảng/view • Gõ tên bảng để tìm kiếm, chọn từ danh sách đề xuất.";
        });
        // Don't auto-load columns - wait for user to select a table
    }
    private async Task LoadColumnsAsync()
    {
        if (selectedTable < 0 || selectedTable >= tableNames.Count) return;
        var table = tableNames[selectedTable];
        await Work(async token =>
        {
            var result = await ProjectDatabase.ReadAsync(target, "SELECT COLUMN_NAME,DATA_TYPE,CHARACTER_MAXIMUM_LENGTH,IS_NULLABLE FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA=@schema AND TABLE_NAME=@table ORDER BY ORDINAL_POSITION", token,
                new SqlParameter("@schema", table.Schema), new SqlParameter("@table", table.Name));
            token.ThrowIfCancellationRequested(); columns.Children.Clear(); columnNames.Clear(); rows.ItemsSource = null; select.Text = "*"; where.Text = ""; order.Text = "";
            PopulateColumnChoices(result);
            status.Text = $"{result.Rows.Count} cột • Nhập bộ lọc rồi nhấn Run. Kết nối: {target.Server} / {target.Database}";
        });
    }
    private void PopulateColumnChoices(DataTable result)
    {
        columns.Children.Clear(); columnNames.Clear();
            foreach (DataRow row in result.Rows)
            {
                var name = (string)row[0]; var type = (string)row[1];
                columnNames.Add(name);
                if (row[2] != DBNull.Value) type += "(" + ((int)row[2] == -1 ? "max" : row[2].ToString()) + ")";
                var box = new CheckBox { Content = new TextBlock { Text = name + " [" + type + "]" }, Tag = name, Margin = new Thickness(4), ToolTip = "Nullable: " + row[3] };
                void Changed(object sender, RoutedEventArgs e) { var chosen = columns.Children.OfType<CheckBox>().Where(c => c.IsChecked == true).Select(c => ProjectDatabase.Quote((string)c.Tag)); select.Text = chosen.Any() ? string.Join(", ", chosen) : "*"; }
                box.Checked += Changed; box.Unchecked += Changed; columns.Children.Add(box);
            }

    }
    private void ShowMatches()
    {
        tableHits.Visibility = tableFilter.IsKeyboardFocusWithin ? Visibility.Visible : Visibility.Collapsed;
        var term = tableFilter.Text.Trim();
        hitIndexes.Clear();
        var labels = new List<string>();
        // Only show suggestions when user has typed something
        if (term.Length == 0)
        {
            tableHits.ItemsSource = labels;
            return;
        }
        var ranked = tableNames.Select((t, i) => (Name: t.Name, Label: t.Schema + "." + t.Name, Index: i))
            .Select(x => (x.Label, x.Index, Rank: x.Name.StartsWith(term, StringComparison.OrdinalIgnoreCase) ? 0 : x.Label.StartsWith(term, StringComparison.OrdinalIgnoreCase) ? 1 : x.Label.Contains(term, StringComparison.OrdinalIgnoreCase) ? 2 : 3))
            .Where(x => x.Rank < 3).OrderBy(x => x.Rank).ThenBy(x => x.Label, StringComparer.OrdinalIgnoreCase).Take(10);
        foreach (var item in ranked) { hitIndexes.Add(item.Index); labels.Add(item.Label); }
        tableHits.ItemsSource = labels;
    }
    private int ResolveTable()
    {
        var term = tableFilter.Text.Trim();
        var exact = tableNames.FindIndex(t => t.Name.Equals(term, StringComparison.OrdinalIgnoreCase) || (t.Schema + "." + t.Name).Equals(term, StringComparison.OrdinalIgnoreCase));
        if (exact >= 0) return exact;
        return -1;
    }
    private async Task ChooseHitAsync()
    {
        if (initializing) return;
        if (tableHits.SelectedIndex < 0 || tableHits.SelectedIndex >= hitIndexes.Count)
        {
            // No valid selection - don't load columns
            return;
        }
        var index = hitIndexes[tableHits.SelectedIndex];
        await SelectTableAsync(index);
    }
    private async Task SelectTableAsync(int index)
    {
        initializing = true;
        tableFilter.Text = tableNames[index].Schema + "." + tableNames[index].Name;
        tableHits.Visibility = Visibility.Collapsed;
        initializing = false;
        if (index == selectedTable && columns.Children.Count > 0) return;
        columnNames.Clear(); columns.Children.Clear(); wherePopup.IsOpen = false;
        selectedTable = index;
        await LoadColumnsAsync();
    }
    internal static IReadOnlyList<SqlSuggestion> FilterSuggestions(string prefix, IEnumerable<string> names) =>
        names.Select(n => new SqlSuggestion(n, ProjectDatabase.Quote(n), "Cột"))
            .Concat(new[] { "AND", "OR", "NOT", "LIKE", "IN", "BETWEEN", "IS NULL", "IS NOT NULL", "EXISTS" }.Concat(SqlSuggestions.Keywords).Select(k => new SqlSuggestion(k, k, "SQL")))
            .DistinctBy(s => s.Text, StringComparer.OrdinalIgnoreCase)
            .Where(s => s.Text.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)).Take(40).ToList();
    private int WhereTokenStart()
    {
        var start = completionInput.CaretIndex;
        while (start > 0 && (char.IsLetterOrDigit(completionInput.Text[start - 1]) || completionInput.Text[start - 1] is '_' or '$' or '#')) start--;
        return start;
    }
    private void ShowWhereMatches(bool force = false)
    {
        if (!completionInput.IsKeyboardFocusWithin || (completionInput != select && (selectedTable < 0 || columnNames.Count == 0))) { wherePopup.IsOpen = false; return; }
        var start = WhereTokenStart(); var prefix = completionInput.Text[start..completionInput.CaretIndex];
        // An odd number of quote delimiters means the caret is inside a SQL string;
        // doubled quotes contribute an even count and do not change that state.
        if (completionInput.Text[..completionInput.CaretIndex].Count(c => c == '\'') % 2 != 0 || (!force && prefix.Length == 0)) { wherePopup.IsOpen = false; return; }
        whereHits.ItemsSource = FilterSuggestions(prefix, columnNames).Concat(completionInput == order ? new[] { "ASC", "DESC" }.Where(k => k.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)).Select(k => new SqlSuggestion(k, k, "ORDER BY")) : Enumerable.Empty<SqlSuggestion>()).ToList(); whereHits.SelectedIndex = 0;
        wherePopup.IsOpen = whereHits.Items.Count > 0;
    }
    private void InsertWhereMatch()
    {
        if (whereHits.SelectedItem is not SqlSuggestion suggestion) return;
        var start = WhereTokenStart(); var end = completionInput.CaretIndex;
        while (end < completionInput.Text.Length && (char.IsLetterOrDigit(completionInput.Text[end]) || completionInput.Text[end] is '_' or '$' or '#')) end++;
        if (start > 0 && completionInput.Text[start - 1] == '[') { start--; if (end < completionInput.Text.Length && completionInput.Text[end] == ']') end++; }
        completionInput.Select(start, end - start); completionInput.SelectedText = suggestion.InsertText; completionInput.CaretIndex = start + suggestion.InsertText.Length;
        wherePopup.IsOpen = false; completionInput.Focus();
    }
    internal static string BuildQuery(string schema, string table, string selection, string filter, string sorting)
    {
        var sql = $"SELECT TOP (1000) {selection} FROM {ProjectDatabase.Quote(schema)}.{ProjectDatabase.Quote(table)}";
        if (!string.IsNullOrWhiteSpace(filter)) sql += " WHERE " + filter;
        if (!string.IsNullOrWhiteSpace(sorting)) sql += " ORDER BY " + sorting;
        var parser = new TSql150Parser(true); var fragment = parser.Parse(new StringReader(sql), out var errors);
        if (errors.Count > 0 || fragment is not TSqlScript script || script.Batches.Count != 1 || script.Batches[0].Statements.Count != 1 || script.Batches[0].Statements[0] is not SelectStatement statement || statement.Into != null || statement.QueryExpression is not QuerySpecification)
            throw new InvalidOperationException("Chỉ chấp nhận một SELECT hợp lệ, không có INTO hoặc lệnh ghi dữ liệu.");
        return sql;
    }
    internal static void SmokeInputControls(Window owner)
    {
        var browser = new TableBrowser(owner, new SqlTarget("fixture", "App", ""), false);
        try
        {
            browser.tableNames.Add(("dbo", "orders")); browser.selectedTable = 0; browser.columnNames.AddRange(["ma_dvcs", "ngay_ct"]);
            var columnFixture = new DataTable(); columnFixture.Columns.Add("name"); columnFixture.Columns.Add("type"); columnFixture.Columns.Add("length", typeof(int)); columnFixture.Columns.Add("nullable");
            columnFixture.Rows.Add("ma_dvcs", "char", 16, "YES"); columnFixture.Rows.Add("ma_bp", "char", 16, "YES"); columnFixture.Rows.Add("ten_bp", "nvarchar", 128, "YES");
            browser.PopulateColumnChoices(columnFixture);
            if (browser.columns.Children.OfType<CheckBox>().Single(c => Equals(c.Tag, "ma_bp")).Content is not TextBlock { Text: "ma_bp [char(16)]" }) throw new InvalidOperationException("Column checkbox consumes underscore as access key");
            browser.Show(); browser.Activate(); browser.UpdateLayout();
            browser.select.Focus(); browser.select.Text = "ma_"; browser.select.CaretIndex = 3; browser.ShowWhereMatches(true);
            if (!browser.wherePopup.IsOpen || browser.whereHits.Items.Cast<SqlSuggestion>().All(s => s.Text != "ma_dvcs")) throw new InvalidOperationException("SELECT column suggestions");
            browser.whereHits.SelectedItem = browser.whereHits.Items.Cast<SqlSuggestion>().First(s => s.Text == "ma_dvcs"); browser.InsertWhereMatch();
            if (browser.select.Text != "[ma_dvcs]") throw new InvalidOperationException("SELECT insertion preserves underscore");
            var fields = new[] { browser.select, browser.tableFilter, browser.where, browser.order };
            for (var i = 0; i < fields.Length - 1; i++)
            {
                fields[i].Focus();
                fields[i].RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(browser), 0, Key.Tab) { RoutedEvent = Keyboard.PreviewKeyDownEvent });
                if (!fields[i + 1].IsKeyboardFocusWithin) throw new InvalidOperationException($"SELECT/FROM/WHERE/ORDER tab order at {i}; focus={Keyboard.FocusedElement}; nextVisible={fields[i + 1].IsVisible}");
            }
            browser.order.Focus(); browser.order.Text = "ma_"; browser.order.CaretIndex = 3; browser.ShowWhereMatches(true);
            if (!browser.wherePopup.IsOpen || browser.whereHits.Items.Cast<SqlSuggestion>().All(s => s.Text != "ma_dvcs")) throw new InvalidOperationException("ORDER column suggestions");
            browser.whereHits.SelectedItem = browser.whereHits.Items.Cast<SqlSuggestion>().First(s => s.Text == "ma_dvcs"); browser.InsertWhereMatch();
            if (browser.order.Text != "[ma_dvcs]") throw new InvalidOperationException("ORDER insertion");
            browser.order.Text = "[ma_dvcs] DE"; browser.order.CaretIndex = browser.order.Text.Length; browser.ShowWhereMatches(true);
            if (browser.whereHits.Items.Cast<SqlSuggestion>().All(s => s.Text != "DESC")) throw new InvalidOperationException("ORDER direction suggestion");
            var table = new DataTable();
            table.Columns.Add("internal1").ExtendedProperties["OriginalName"] = "same";
            table.Columns.Add("internal2").ExtendedProperties["OriginalName"] = "same";
            table.Columns.Add("internal3").ExtendedProperties["OriginalName"] = "";
            table.Rows.Add("a", "b", "c"); browser.rows.ItemsSource = table.DefaultView; browser.UpdateLayout();
            if (!browser.rows.Columns.Select(c => Convert.ToString(c.Header)).SequenceEqual(new[] { "same", "same", "" })) throw new InvalidOperationException("Original result headers changed");
            var wide = new DataTable();
            wide.Columns.Add("active", typeof(bool));
            for (int i = 0; i < 150; i++) wide.Columns.Add("column_" + i);
            for (int i = 0; i < 1000; i++) { var row = wide.NewRow(); row[0] = i % 3 == 0 ? DBNull.Value : i % 2 == 0; row[1] = "long text\nsecond line"; wide.Rows.Add(row); }
            browser.rows.ItemsSource = wide.DefaultView; browser.UpdateLayout();
            browser.rows.ScrollIntoView(wide.DefaultView[999], browser.rows.Columns[^1]); browser.UpdateLayout();
            browser.rows.ScrollIntoView(wide.DefaultView[0], browser.rows.Columns[0]); browser.UpdateLayout();
            if (browser.rows.Columns[0] is not DataGridTextColumn || !browser.rows.EnableColumnVirtualization || browser.rows.RowHeight != 32) throw new InvalidOperationException("SQL bit / wide grid regression");
            MainWindow.SaveDesignImage(browser, "ui-a-table-browser");
        }
        finally { browser.Close(); owner.Activate(); }
    }
    internal async Task SmokeAsync(string imagePath)
    {
        await ready.Task;
        if (columns.Children.Count == 0) throw new InvalidOperationException("No table columns loaded: " + status.Text);
        if (columns.Children.OfType<CheckBox>().Any(c => string.Equals((string)c.Tag, "controller", StringComparison.OrdinalIgnoreCase))) where.Text = "controller LIKE 'GLDetail'";
        await RunAsync();
        if (rows.ItemsSource == null) throw new InvalidOperationException("No data result: " + status.Text);
        UpdateLayout();
        var bitmap = new System.Windows.Media.Imaging.RenderTargetBitmap((int)ActualWidth, (int)ActualHeight, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
        bitmap.Render(this);
        var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder(); encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));
        using var stream = File.Create(imagePath); encoder.Save(stream);
    }
    private async Task RunAsync()
    {
        var resolved = ResolveTable();
        if (resolved >= 0 && resolved != selectedTable)
        {
            selectedTable = resolved;
            await LoadColumnsAsync();
        }
        selectedTable = ResolveTable();
        if (selectedTable < 0 || selectedTable >= tableNames.Count) return;
        var table = tableNames[selectedTable];
        await Work(async token =>
        {
            var sql = BuildQuery(table.Schema, table.Name, select.Text, where.Text, order.Text);
            var result = await ProjectDatabase.ReadAsync(target, sql, token); token.ThrowIfCancellationRequested(); rows.ItemsSource = result.DefaultView;
            status.Text = $"{result.Rows.Count} dòng (giới hạn 1000) • {target.Server} / {target.Database} • Chỉ đọc";
        });
    }
}







