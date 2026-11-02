using System.Data;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using System.Xml;
using System.Xml.XPath;
using System.Xml.Linq;
using LSCode.Core;
using LSCode.Shell.Services;
using Microsoft.Win32;

namespace LSCode.Shell;
public partial class MainWindow
{
    private static readonly HttpClient translateClient = CreateTranslateClient();
    private static readonly TranslationClient translator = new(translateClient);
    private Window? translatorWindow;
    private static HttpClient CreateTranslateClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
        client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "Mozilla/5.0");
        return client;
    }
    private async void ConfigureConnection()
    {
        SqlTarget target;
        try { if (Active is { Language: ".sql" } doc) { await doc.TargetReady; target = doc.SqlTarget ?? throw new InvalidOperationException("Chưa chọn App DB."); } else target = await DefaultAppTargetAsync(); }
        catch (Exception ex) { Dialogs.Notify(this, ex.Message, "SQL Connection"); return; }
        var current = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(target.ConnectionString);
        var panel = new StackPanel { Margin = new Thickness(20) };
        TextBox Field(string label, string value) { panel.Children.Add(new TextBlock { Text = label, Margin = new Thickness(3, 6, 3, 0) }); var input = new TextBox { Text = value }; panel.Children.Add(input); return input; }
        var server = Field("SQL Server / instance", current.DataSource);
        var database = Field("Database", current.InitialCatalog);
        var integrated = new CheckBox { Content = "Windows authentication", IsChecked = current.IntegratedSecurity }; panel.Children.Add(integrated);
        var user = Field("SQL user", current.UserID);
        panel.Children.Add(new TextBlock { Text = "Password (chỉ giữ trong phiên hiện tại)" });
        var password = new PasswordBox { Password = current.Password }; panel.Children.Add(password);
        var trust = new CheckBox { Content = "Trust server certificate (máy chủ thử nghiệm)", IsChecked = current.TrustServerCertificate }; panel.Children.Add(trust);
        panel.Children.Add(new TextBlock { Text = "Mỗi lần Execute mở một session mới; GO dùng chung session trong lần chạy. Timeout 60s.", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(3, 8, 3, 8) });
        var save = new Button { Content = "Lưu cấu hình kết nối", IsDefault = true }; panel.Children.Add(save);
        var window = Dialogs.Create(this, "SQL Connection", panel, 530, 540);
        save.Click += (_, _) =>
        {
            if (string.IsNullOrWhiteSpace(server.Text) || string.IsNullOrWhiteSpace(database.Text)) return;
            settings.Server = server.Text.Trim(); settings.Database = database.Text.Trim(); settings.SqlUser = user.Text.Trim();
            settings.IntegratedSecurity = integrated.IsChecked == true; settings.TrustCertificate = trust.IsChecked == true; sqlPassword = password.Password;
            current.DataSource = settings.Server; current.InitialCatalog = settings.Database; current.UserID = settings.SqlUser; current.Password = sqlPassword;
            current.IntegratedSecurity = settings.IntegratedSecurity; current.TrustServerCertificate = settings.TrustCertificate;
            configuredSqlTarget = new(settings.Server, settings.Database, current.ConnectionString);
            if (Active is { Language: ".sql" } doc) doc.SqlTarget = configuredSqlTarget;
            SettingsStore.Save(settings); window.DialogResult = true; UpdateStatus();
        };
        window.ShowDialog();
    }
    private async Task ExecuteAsync()
    {
        if (Active is not { } doc) return;
        if (doc.Language != ".sql") throw new InvalidOperationException("Chạy SQL chỉ khả dụng cho tab .sql.");
        await doc.TargetReady;
        if (sqlCancellation != null) { Log("Query đang chạy; dùng Cancel trước lần chạy mới."); return; }
        var selected = !string.IsNullOrWhiteSpace(doc.ActiveEditor.SelectedText);
        var selectionStart = selected ? doc.ActiveEditor.SelectionStart : 0;
        var startLocation = doc.Buffer.GetLocation(selectionStart);
        var sql = selected ? doc.ActiveEditor.SelectedText : doc.Buffer.Text;
        if (string.IsNullOrWhiteSpace(sql)) return;
        var target = doc.SqlTarget ?? throw new InvalidOperationException("Chưa chọn database cho tab SQL. Chọn SYS hoặc App trước khi chạy.");
        var server = target.Server; var database = target.Database;
        var connection = target.ConnectionString;
        var cts = new CancellationTokenSource(); sqlCancellation = cts;
        Problems.Items.Clear();
        Status.Text = $"Executing · {server} / {database}"; Log($"Execute {doc.Name} → {server} / {database}");
        try
        {
            var result = await Task.Run(() => SqlRunner.ExecuteAsync(connection, sql, cts.Token), cts.Token);
            Results.Items.Clear();
            if (BottomPanel.Visibility != Visibility.Visible) ToggleBottomPanel();
            foreach (var table in result.Tables)
            {
                var resultGrid = new DataGrid { ItemsSource = table.DefaultView, IsReadOnly = true, CanUserAddRows = false };
                ResultGrids.Prepare(resultGrid);
                Results.Items.Add(new TabItem { Header = $"{table.TableName} · {table.Rows.Count} rows", Tag = table, Content = resultGrid });
            }
            Results.SelectedIndex = 0; BottomPanel.SelectedIndex = result.Tables.Count == 0 ? 0 : 2;
            foreach (var message in result.Messages) Log(message);
            Log($"Completed in {result.Duration.TotalMilliseconds:N0} ms · {result.Tables.Count} result sets · {server}/{database}");
            if (result.Truncated) Log("Giới hạn hiển thị: 5000 rows / result, tối đa 30 result sets. CSV chỉ xuất rows đang hiển thị.");
        }
        catch (OperationCanceledException) { }
        catch (SqlExecutionException ex)
        {
            Problems.Items.Clear();
            foreach (var error in ex.Errors)
            {
                if (error.Line <= 0 || !string.IsNullOrEmpty(error.Procedure))
                    Problems.Items.Add($"SQL {error.Procedure} · dòng {error.Line}: {error.Message}");
                else
                {
                    var location = SqlBatches.ErrorLocation(1, error.Line, startLocation.Line, startLocation.Column);
                    Problems.Items.Add(new ProblemEntry(doc, new XmlDiagnostic(error.Message,
                        location.Line, location.Column)));
                }
            }
            if (BottomPanel.Visibility != Visibility.Visible) ToggleBottomPanel();
            BottomPanel.SelectedIndex = 1;
        }
        catch (Exception ex)
        {
            Problems.Items.Clear();
            Problems.Items.Add("SQL: " + ex.Message);
            if (BottomPanel.Visibility != Visibility.Visible) ToggleBottomPanel();
            BottomPanel.SelectedIndex = 1;
        }
        finally { sqlCancellation = null; cts.Dispose(); UpdateStatus(); }
    }
    private async Task ExportResultAsync()
    {
        if (Results.SelectedItem is not TabItem { Tag: DataTable table }) return;
        var dialog = new SaveFileDialog { FileName = "result.csv", Filter = "CSV|*.csv" };
        if (dialog.ShowDialog(this) != true) return;
        static string Cell(object? value) => "\"" + Convert.ToString(value)?.Replace("\"", "\"\"") + "\"";
        var builder = new StringBuilder(); builder.AppendLine(string.Join(",", table.Columns.Cast<DataColumn>().Select(c => Cell(c.ColumnName))));
        foreach (DataRow row in table.Rows) builder.AppendLine(string.Join(",", row.ItemArray.Select(Cell)));
        await File.WriteAllTextAsync(dialog.FileName, builder.ToString(), new UTF8Encoding(true)); Log("CSV exported: " + dialog.FileName);
    }
    private void RefreshSnippets()
    {
        if (SnippetList == null) return;
        var query = SnippetFilter?.Text ?? "";
        SnippetList.ItemsSource = snippets.Where(s => (s.Key + s.Category + s.Description).Contains(query, StringComparison.OrdinalIgnoreCase)).ToList();
    }
    private void PersistSnippets()
    {
        Directory.CreateDirectory(SettingsStore.Root);
        var temp = SnippetsPath + ".tmp"; File.WriteAllText(temp, SnippetImporter.Write(snippets), new UTF8Encoding(false)); File.Move(temp, SnippetsPath, true); RefreshSnippets();
    }
    private void InsertSnippet()
    {
        if (SnippetList.SelectedItem is not Snippet snippet) return;
        if (Active == null) NewDocument();
        var editor = Active!.ActiveEditor; var offset = editor.SelectionStart;
        editor.Document.Replace(offset, editor.SelectionLength, snippet.Body);
        editor.CaretOffset = offset + Math.Clamp(snippet.Position, 0, snippet.Body.Length); editor.Focus();
    }
    private void EditSnippet(bool create)
    {
        var existing = create ? null : SnippetList.SelectedItem as Snippet;
        if (!create && existing == null) return;
        var name = Dialogs.Text(this, "Tên snippet", existing?.Key ?? "New snippet"); if (string.IsNullOrWhiteSpace(name)) return;
        var category = Dialogs.Text(this, "Nhóm snippet", existing?.Category ?? "User"); if (category == null) return;
        var body = Dialogs.Text(this, "Nội dung snippet", existing?.Body ?? "", true); if (body == null) return;
        var item = new Snippet(name, category, body, existing?.Description ?? "", existing?.Position ?? 0, existing?.Param ?? "");
        if (existing != null) snippets[snippets.IndexOf(existing)] = item; else snippets.Add(item);
        PersistSnippets();
    }
    private async Task ImportSnippetsAsync()
    {
        var dialog = new OpenFileDialog { Filter = "Legacy snippets XML|*.xml" };
        if (dialog.ShowDialog(this) != true) return;
        var imported = SnippetImporter.Read(await File.ReadAllTextAsync(dialog.FileName));
        if (Dialogs.Notify(this, $"Thêm {imported.Count} snippets? Trùng tên/nhóm sẽ giữ bản hiện có.", "Import snippets", MessageBoxButton.OKCancel) != MessageBoxResult.OK) return;
        foreach (var item in imported) if (!snippets.Any(s => s.Key == item.Key && s.Category == item.Category)) snippets.Add(item);
        PersistSnippets(); Log($"Imported snippets; catalog now has {snippets.Count} entries.");
    }
    private async Task ExportSnippetsAsync()
    {
        var dialog = new SaveFileDialog { FileName = "snippets.xml", Filter = "Legacy snippets XML|*.xml" };
        if (dialog.ShowDialog(this) == true) await File.WriteAllTextAsync(dialog.FileName, SnippetImporter.Write(snippets));
    }
    private async Task CompareAsync()
    {
        if (Active is not { } doc) return;
        var dialog = new OpenFileDialog { Title = "So sánh document hiện tại với file" }; if (dialog.ShowDialog(this) != true) return;
        var other = await DocumentStore.OpenAsync(dialog.FileName);
        var currentText = doc.Buffer.Text;
        CompareTools.Text(this, currentText, other.Text).Show();
    }
    private void ShowTranslator()
    {
        var selected = Active?.ActiveEditor.SelectedText?.Trim() ?? "";
        if (translatorWindow is { IsVisible: true } open)
        {
            if (open.Content is DockPanel { Tag: TextBox source } && selected.Length > 0) source.Text = selected;
            open.Activate(); return;
        }
        var languages = new[]
        {
            new LanguageChoice("auto", "Phát hiện ngôn ngữ"), new LanguageChoice("vi", "Tiếng Việt"), new LanguageChoice("en", "English"),
            new LanguageChoice("ja", "日本語"), new LanguageChoice("ko", "한국어"), new LanguageChoice("zh-CN", "中文 (简体)"), new LanguageChoice("zh-TW", "中文 (繁體)"),
            new LanguageChoice("fr", "Français"), new LanguageChoice("de", "Deutsch"), new LanguageChoice("th", "ไทย"), new LanguageChoice("lo", "ລາວ"), new LanguageChoice("ru", "Русский")
        };
        var sourceLang = new ComboBox { ItemsSource = languages, DisplayMemberPath = "Name", SelectedIndex = 0, MinWidth = 180 };
        var targetLang = new ComboBox { ItemsSource = languages.Skip(1).ToArray(), DisplayMemberPath = "Name", MinWidth = 180, SelectedIndex = LooksVietnamese(selected) ? 1 : 0 };
        var sourceBox = new TextBox { Text = selected, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, MinHeight = 220 };
        var targetBox = new TextBox { IsReadOnly = true, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, MinHeight = 220 };
        var status = new TextBlock { Text = "Nhấn Dịch để gửi văn bản. Không tự gửi khi gõ. Tối đa 4500 ký tự mỗi lần.", Margin = new Thickness(4), Foreground = (System.Windows.Media.Brush)FindResource("Muted"), TextWrapping = TextWrapping.Wrap };
        var translate = new Button { Content = "Dịch" };
        var copy = new Button { Content = "Sao chép" };
        var browser = new Button { Content = "Mở Google Dịch" };
        var swap = new Button { Content = "⇄", ToolTip = "Đổi chiều", Padding = new Thickness(10, 4, 10, 4) };
        var root = new DockPanel { Margin = new Thickness(12), Tag = sourceBox };
        DockPanel.SetDock(status, Dock.Bottom); root.Children.Add(status);
        var buttons = new WrapPanel { Margin = new Thickness(0, 8, 0, 0) }; buttons.Children.Add(translate); buttons.Children.Add(copy); buttons.Children.Add(browser); DockPanel.SetDock(buttons, Dock.Bottom); root.Children.Add(buttons);
        var body = new Grid(); body.ColumnDefinitions.Add(new()); body.ColumnDefinitions.Add(new() { Width = GridLength.Auto }); body.ColumnDefinitions.Add(new());
        UIElement Column(int index, ComboBox language, TextBox box)
        {
            var stack = new DockPanel { Margin = new Thickness(4) };
            DockPanel.SetDock(language, Dock.Top); stack.Children.Add(language); stack.Children.Add(box);
            Grid.SetColumn(stack, index); return stack;
        }
        body.Children.Add(Column(0, sourceLang, sourceBox));
        swap.VerticalAlignment = VerticalAlignment.Center; Grid.SetColumn(swap, 1); body.Children.Add(swap);
        body.Children.Add(Column(2, targetLang, targetBox));
        root.Children.Add(body);
        var window = Dialogs.Create(this, "Google Dịch", root, 920, 520);
        window.MinWidth = 720; window.MinHeight = 420; translatorWindow = window;
        var run = 0; CancellationTokenSource? request = null;
        async Task RunAsync()
        {
            if (request != null) return;
            var text = sourceBox.Text; if (string.IsNullOrWhiteSpace(text)) { targetBox.Text = ""; return; }
            var from = ((LanguageChoice)sourceLang.SelectedItem).Code; var to = ((LanguageChoice)targetLang.SelectedItem).Code;
            if (from == to) { targetBox.Text = text; return; }
            var id = ++run; using var current = new CancellationTokenSource(); request = current;
            translate.IsEnabled = false;
            status.Text = "Đang dịch…";
            try
            {
                var translated = await translator.TranslateAsync(text, from, to, current.Token);
                if (id != run) return;
                targetBox.Text = translated;
                status.Text = "Đã dịch.";
            }
            catch (OperationCanceledException) { }
            catch (TranslationRateLimitException ex) { if (id == run) status.Text = $"{ex.Message} Chờ khoảng {Math.Max(1, Math.Ceiling((ex.RetryAt - DateTimeOffset.UtcNow).TotalSeconds))} giây."; }
            catch (Exception ex) { if (id == run) { targetBox.Text = ""; status.Text = "Không dịch được: " + ex.Message; } }
            finally { request = null; translate.IsEnabled = true; }
        }
        void Changed() { ++run; request?.Cancel(); targetBox.Clear(); status.Text = "Nhấn Dịch để dịch nội dung hiện tại."; }
        sourceBox.TextChanged += (_, _) => Changed();
        sourceLang.SelectionChanged += (_, _) => Changed();
        targetLang.SelectionChanged += (_, _) => Changed();
        translate.Click += async (_, _) => await RunAsync();
        browser.Click += (_, _) =>
        {
            try
            {
                var url = TranslationClient.BrowserUrl(((LanguageChoice)sourceLang.SelectedItem).Code, ((LanguageChoice)targetLang.SelectedItem).Code, sourceBox.Text);
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            }
            catch (Exception ex) { status.Text = "Không mở được trình duyệt: " + ex.Message; }
        };
        copy.Click += (_, _) => { if (targetBox.Text.Length > 0) { Clipboard.SetText(targetBox.Text); status.Text = "Đã sao chép bản dịch."; } };
        swap.Click += (_, _) =>
        {
            var from = (LanguageChoice)sourceLang.SelectedItem; var to = (LanguageChoice)targetLang.SelectedItem;
            var translated = targetBox.Text; var original = sourceBox.Text;
            sourceLang.SelectedItem = languages.First(l => l.Code == to.Code);
            targetLang.SelectedItem = languages.Skip(1).First(l => l.Code == (from.Code == "auto" ? "vi" : from.Code));
            sourceBox.Text = translated.Length > 0 ? translated : original;
        };
        window.Closed += (_, _) => { ++run; request?.Cancel(); if (translatorWindow == window) translatorWindow = null; };
        window.Show();
    }
    private static bool LooksVietnamese(string text) => text.Any(c => c is >= '\u00C0' and <= '\u1EF9');
    private sealed record LanguageChoice(string Code, string Name)
    {
        public override string ToString() => Name;
    }
    private void ConfigureExternalTools()
    {
        var panel = new StackPanel { Margin = new Thickness(16) };
        var window = Dialogs.Create(this, "External tools settings", panel, 780, 420);
        TextBox PathField(string label, string value)
        {
            panel.Children.Add(new TextBlock { Text = label, Margin = new Thickness(4, 8, 4, 0) });
            var row = new DockPanel(); panel.Children.Add(row);
            var browse = new Button { Content = "Chọn file…" }; DockPanel.SetDock(browse, Dock.Right); row.Children.Add(browse);
            var input = new TextBox { Text = value, Margin = new Thickness(4) }; row.Children.Add(input);
            browse.Click += (_, _) => { var picker = new OpenFileDialog { Filter = "Executable (*.exe)|*.exe", FileName = input.Text.Trim().Trim('"') }; if (picker.ShowDialog(window) == true) input.Text = picker.FileName; };
            return input;
        }
        var profiler18 = PathField("SQL Profiler — SSMS 18 (PROFILER.EXE)", settings.Profiler18Path);
        panel.Children.Add(new TextBlock { Text = "LS Code tự lấy thư viện SQL Trace từ thư mục Common7/IDE cạnh Profiler. Chỉ cần lưu một lần; dùng chung cho mọi dự án và các lần mở ứng dụng sau.", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(4) });
        var crystal = PathField("Crystal Reports", settings.CrystalPath);
        var status = new TextBlock { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(4) }; panel.Children.Add(status);
        var save = new Button { Style = (Style)FindResource("PrimaryButton"), HorizontalAlignment = HorizontalAlignment.Right, MinHeight = 40, Content = "Lưu cấu hình", IsDefault = true }; panel.Children.Add(save);
        save.Click += (_, _) =>
        {
            try
            {
                var path = profiler18.Text.Trim().Trim('"');
                if (path.Length > 0) { SqlTraceSession.ValidateProfilerPath(path); path = Path.GetFullPath(path); }
                settings.Profiler18Path = path; settings.CrystalPath = crystal.Text.Trim().Trim('"');
                SettingsStore.Save(settings); window.DialogResult = true;
            }
            catch (Exception ex) { status.Text = ex.Message; }
        };
        window.ShowDialog();
    }
    private Task EvaluateXPathAsync()
    {
        if (Active is not { Language: ".xml" or ".xaml" or ".config" } doc) throw new InvalidOperationException("Chọn tab XML trước khi chạy XPath.");
        var expression = Dialogs.Text(this, "XPath (namespace mặc định dùng prefix d)", "//*");
        if (string.IsNullOrWhiteSpace(expression)) return Task.CompletedTask;
        var xml = XmlTools.Parse(doc.Buffer.Text);
        var namespaces = new XmlNamespaceManager(new NameTable());
        if (xml.Root != null)
        {
            foreach (var attr in xml.Root.Attributes().Where(a => a.IsNamespaceDeclaration)) namespaces.AddNamespace(attr.Name.LocalName == "xmlns" ? "d" : attr.Name.LocalName, attr.Value);
        }
        var result = xml.XPathEvaluate(expression, namespaces);
        if (result is System.Collections.IEnumerable items && result is not string)
        {
            var count = 0;
            foreach (var item in items)
            {
                if (count++ >= 200) { Log("XPath: dừng hiển thị ở 200 kết quả."); break; }
                Log("XPath: " + item?.ToString());
            }
            Log($"XPath: {Math.Min(count, 200)} kết quả hiển thị.");
        }
        else Log("XPath: " + result);
        BottomPanel.SelectedIndex = 0;
        return Task.CompletedTask;
    }
    private void LaunchTool(string path, string? file = null)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) throw new IOException("Chưa cấu hình executable hợp lệ. Mở Tools → External tools settings.");
        var info = new ProcessStartInfo(path) { UseShellExecute = true };
        if (file != null) info.ArgumentList.Add(file);
        Process.Start(info); Log("Opened external tool: " + Path.GetFileName(path));
    }
    public async Task RunSmokeAsync()
    {
        var report = new List<string>();
        try
        {
            EmbeddedHighlightingChecks.Run(); report.Add("PASS embedded SQL / JavaScript token colors in three themes");
            foreach (var workArea in new[] { new Rect(0, 0, 1366, 728), new Rect(0, 0, 910, 480), new Rect(-1280, 0, 1280, 680) })
                if (!workArea.Contains(FitStartupBounds(workArea, new Size(1420, 900)))) throw new Exception("Startup bounds outside work area");
            var nameSearchWatch = Stopwatch.StartNew();
            var nameMatches = FilterObjectNames(Enumerable.Range(0, 20000).Select(i => new SqlSymbol("dbo", "table" + i, null, "table")).Append(new("dbo", "dmbp", null, "table")), "dmbp");
            nameSearchWatch.Stop(); if (nameMatches.Count != 1 || nameMatches[0].Table != "dmbp") throw new Exception("Cached object name search");
            report.Add($"PASS laptop/high-DPI/negative-monitor bounds; local 20001-object name filter: {nameSearchWatch.ElapsedMilliseconds} ms");
            var closeProbe = new MainWindow();
            var probeClosed = new TaskCompletionSource(); closeProbe.Closed += (_, _) => probeClosed.TrySetResult();
            closeProbe.Show(); closeProbe.AddDocument("close-test.sql", "unsaved");
            var cancelProbe = new TaskCompletionSource();
            _ = Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() =>
            {
                try
                {
                    var dialog = closeProbe.OwnedWindows.Cast<Window>().Single(w => w.Title == "Đóng file");
                    var actions = ((StackPanel)((Border)dialog.Content).Child).Children.OfType<StackPanel>().Single();
                    actions.Children.OfType<Button>().Single(b => Equals(b.Content, "Hủy")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    cancelProbe.SetResult();
                }
                catch (Exception ex) { cancelProbe.SetException(ex); }
            }));
            closeProbe.Close(); await cancelProbe.Task.WaitAsync(TimeSpan.FromSeconds(5)); await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
            if (!closeProbe.IsVisible || closeProbe.closeRequested || !closeProbe.recoveryTimer.IsEnabled) throw new Exception("Cancel close did not restore timers/window");
            foreach (var probeDoc in closeProbe.tabs) probeDoc.Buffer.Text = "";
            closeProbe.Close(); await probeClosed.Task.WaitAsync(TimeSpan.FromSeconds(5)); Activate();
            report.Add("PASS deferred close, cancel unsaved dialog, restart timers, clean close without recursion");
            if (WindowStyle != WindowStyle.None || System.Windows.Shell.WindowChrome.GetWindowChrome(this) == null) throw new Exception("Custom title chrome missing");
            var searchParameters = new List<Microsoft.Data.SqlClient.SqlParameter>();
            var contentPredicate = DbSearchPredicate("exec abc", true, searchParameters);
            if (!contentPredicate.Contains("sys.sql_modules") || !contentPredicate.Contains("o.type IN ('P','PC','FN','IF','TF','FS','FT')") || searchParameters.Count != 2 || !Equals(searchParameters[1].Value, "abc")) throw new Exception("Content search scope/tokens");
            DbSearchMode.SelectedIndex = 1; DbObjectSearch.Text = "exec abc";
            if (dbSearchTimer.IsEnabled) throw new Exception("Content search must wait for Enter");
            DbObjectSearch.Text = ""; DbSearchMode.SelectedIndex = 0; dbSearchTimer.Stop();
            if (DistinctFileLabel(@"D:\Project\Grid\dmbp.xml", [@"D:\Project\Filter\dmbp.xml"]) != "Grid/dmbp.xml" || DistinctFileLabel(@"D:\One\Grid\dmbp.xml", [@"D:\Two\Grid\dmbp.xml"]) != "One/Grid/dmbp.xml") throw new Exception("Duplicate filename labels");
            var gridDoc = AddDocument("dmbp.xml", ""); gridDoc.Path = @"D:\fixture\Grid\dmbp.xml";
            var filterDoc = AddDocument("dmbp.xml", ""); filterDoc.Path = @"D:\fixture\Filter\dmbp.xml"; RefreshDocumentHeaders();
            if (((StackPanel)gridDoc.Tab.Header).Children.OfType<TextBlock>().First().Text != "Grid/dmbp.xml" || ((StackPanel)filterDoc.Tab.Header).Children.OfType<TextBlock>().First().Text != "Filter/dmbp.xml") throw new Exception("Actual duplicate tab headers");
            await CloseDocumentAsync(filterDoc);
            if (((StackPanel)gridDoc.Tab.Header).Children.OfType<TextBlock>().First().Text != "dmbp.xml") throw new Exception("Closed duplicate header not refreshed");
            await CloseDocumentAsync(gridDoc);
            report.Add("PASS custom title chrome, procedure/function search terms and Enter-only gating, duplicate filename suffixes");
            await LoadDbObjectsAsync();
            if (DbObjects.Items.Count != 4 || DbObjects.Items.OfType<TreeViewItem>().Any(i => i.IsExpanded || i.Tag is not DbGroupState { Loaded: false, Target: null })) throw new Exception("DB Object must defer connections and queries until expanded");
            DbObjects.Items.Clear();
            var references = SqlSchemaCompletion.ReferencedNames("SELECT b.ma_bp FROM [dbo].[dmbp] b JOIN customer c ON 1=1 -- FROM fake\n WHERE b.x='FROM hidden'");
            if (!references.Contains(("dbo", "dmbp")) || !references.Contains(("", "customer")) || references.Count != 2) throw new Exception("Lazy metadata reference extraction");
            report.Add("PASS lazy DB groups make no initial query; column metadata targets referenced tables only");
            var menuFixtureRoot = Path.Combine(SettingsStore.Root, "menu-fixture");
            Directory.CreateDirectory(Path.Combine(menuFixtureRoot, "Main")); Directory.CreateDirectory(Path.Combine(menuFixtureRoot, "App_Data", "Controllers"));
            var menuPage = Path.Combine(menuFixtureRoot, "Main", "department.aspx");
            var controllerXml = Path.Combine(menuFixtureRoot, "App_Data", "Controllers", "dmbp.xml");
            var controllerFile = Path.ChangeExtension(controllerXml, ".f");
            await File.WriteAllTextAsync(menuPage, "<FastBusiness:QueryExtender Controller='dmbp' />");
            await File.WriteAllTextAsync(controllerXml, "<dir />"); await File.WriteAllTextAsync(controllerFile, "select 1;");
            await OpenControllerFilesAsync(menuFixtureRoot, new CommandEntry("fixture", "", "Department", "department.aspx", "", ""));
            if (tabs.Any(t => new[] { menuPage, controllerXml, controllerFile }.Contains(t.Path)) || ProjectFileTabs.Items.Count < 2) throw new Exception("Menu should only populate project files");
            await OpenProjectFileAsync(controllerXml);
            var selectedMenuDoc = tabs.Single(t => t.Path == controllerXml);
            if (((StackPanel)selectedMenuDoc.Tab.Header).Children.OfType<TextBlock>().First().Text != "Controllers/dmbp.xml" || tabs.Any(t => t.Path == controllerFile || t.Path == menuPage)) throw new Exception("Selected project file prefix/open scope");
            foreach (var menuDoc in tabs.Where(t => t.Path?.StartsWith(menuFixtureRoot) == true).ToList()) { Documents.Items.Remove(menuDoc.Tab); tabs.Remove(menuDoc); menuDoc.Dispose(); }
            while (ProjectFileTabs.Items.Count > 1) ProjectFileTabs.Items.RemoveAt(1);
            report.Add("PASS menu populates file tree only; selected file opens alone with parent prefix");
            var themeFixtures = Enumerable.Range(0, 40).Select(i => new EditorTab("theme-" + i + ".xml", "<root/>" )).ToList();
            try
            {
                foreach (var fixtureTab in themeFixtures) fixtureTab.RefreshHighlighting("Dark");
                if (themeFixtures.Any(t => !ReferenceEquals(t.Primary.SyntaxHighlighting, themeFixtures[0].Primary.SyntaxHighlighting))) throw new Exception("Highlighting definitions not shared");
                tabs.AddRange(themeFixtures);
                var previousTheme = settings.Theme;
                var themeWatch = Stopwatch.StartNew();
                foreach (var theme in new[] { "Light", "High Contrast", "Dark", "Light", "Dark" }) { settings.Theme = theme; ApplyTheme(); }
                themeWatch.Stop();
                settings.Theme = previousTheme; ApplyTheme();
                if (themeFixtures.Any(t => !ReferenceEquals(t.Primary.SyntaxHighlighting, themeFixtures[0].Primary.SyntaxHighlighting))) throw new Exception("Hidden editors refreshed unexpectedly");
                var visible = themeFixtures[0]; Documents.Items.Add(visible.Tab); Documents.SelectedItem = visible.Tab; settings.Theme = "Light"; ApplyTheme(); UpdateLayout();
                var expected = themeFixtures[1]; expected.RefreshHighlighting("Light");
                if (!ReferenceEquals(visible.Primary.SyntaxHighlighting, expected.Primary.SyntaxHighlighting)) throw new Exception("Lazy visible theme refresh failed");
                Documents.Items.Remove(visible.Tab); settings.Theme = previousTheme; ApplyTheme();
                report.Add($"PASS shared highlighting + lazy tab theme; five theme updates with 40 hidden editors: {themeWatch.ElapsedMilliseconds} ms (excludes render)");
            }
            finally { foreach (var fixtureTab in themeFixtures) { tabs.Remove(fixtureTab); fixtureTab.Dispose(); } }
            var reorderPanel = new StackPanel(); var firstRow = new DockPanel(); var secondRow = new DockPanel(); reorderPanel.Children.Add(firstRow); reorderPanel.Children.Add(secondRow);
            MoveActivityRow(reorderPanel, secondRow, -1);
            if (reorderPanel.Children[0] != secondRow) throw new Exception("Move activity up");
            MoveActivityRow(reorderPanel, secondRow, -1); MoveActivityRow(reorderPanel, secondRow, 1);
            if (reorderPanel.Children[1] != secondRow || ActivityButtons.Children.OfType<Button>().Any(b => b.ToolTip == null || ToolTipService.GetInitialShowDelay(b) != 100)) throw new Exception("Activity order or tooltip timing");
            report.Add("PASS activity up/down ordering and fast named tooltips");
            var fieldSuggestions = TableBrowser.FilterSuggestions("ma_", ["ma_dvcs", "ma_kh", "ten_kh"]);
            if (fieldSuggestions.Count != 2 || fieldSuggestions[0].InsertText != "[ma_dvcs]" || !TableBrowser.FilterSuggestions("IS N", []).Any(s => s.Text == "IS NULL")) throw new Exception("WHERE completion filtering");
            if (TableBrowser.FilterSuggestions("odd", ["odd]name"]).Single().InsertText != "[odd]]name]") throw new Exception("WHERE identifier quoting");
            report.Add("PASS WHERE column/keyword filtering and identifier escaping");
            TableBrowser.SmokeInputControls(this);
            report.Add("PASS SELECT/FROM/WHERE/ORDER focus, ORDER suggestions and original duplicate/empty result headers");
            var previousActivity = settings.ActivityCommands;
            settings.ActivityCommands = []; RenderActivityBar();
            if (ActivityButtons.Children.Count != 0) throw new Exception("Remove activity commands");
            settings.ActivityCommands = ["sql.new", "theme.switch"]; RenderActivityBar(); SettingsStore.Save(settings);
            if (ActivityButtons.Children.Count != 2 || !SettingsStore.Load().ActivityCommands!.SequenceEqual(settings.ActivityCommands)) throw new Exception("Activity command persistence");
            settings.ActivityCommands = previousActivity; SettingsStore.Save(settings); RenderActivityBar();
            var previousBusy = busyCount;
            UpdateLayout();
            var editorOrigin = Documents.TranslatePoint(new Point(), this);
            var editorSize = Documents.RenderSize;
            using (BeginBusy("LS · kiểm tra loading")) {
                UpdateLayout();
                if (LoadingStrip.Visibility != Visibility.Visible || busyCount != previousBusy + 1) throw new Exception("Loading UI");
                if (Documents.TranslatePoint(new Point(), this) != editorOrigin || Documents.RenderSize != editorSize) throw new Exception("Loading shifted editor layout");
            }
            using (ShowLoadingOverlay(WindowLayers, "Đang kết nối dự án…")) {
                UpdateLayout();
                if (WindowLayers.Children.Count != 2 || MainDock.IsEnabled) throw new Exception("Connection overlay missing");
                if (Documents.TranslatePoint(new Point(), this) != editorOrigin) throw new Exception("Overlay shifted editor");
            }
            if (WindowLayers.Children.Count != 1 || !MainDock.IsEnabled) throw new Exception("Overlay cleanup");
            if (WindowState != WindowState.Maximized) throw new Exception("Startup not maximized");
            var screen = new MonitorInfo { Size = System.Runtime.InteropServices.Marshal.SizeOf<MonitorInfo>() };
            if (GetMonitorInfo(MonitorFromWindow(new System.Windows.Interop.WindowInteropHelper(this).Handle, 2), ref screen)) {
                var bottom = Status.PointToScreen(new Point(0, Status.ActualHeight));
                var top = MainDock.PointToScreen(new Point());
                if (bottom.Y > screen.Work.Bottom + 1 || top.Y < screen.Work.Top - 1) throw new Exception("Maximized content outside monitor work area");
            }
            report.Add("PASS loading preserves layout, overlay lifecycle and maximized visible status bar");
            if (busyCount != previousBusy) throw new Exception("Loading scope not released");
            report.Add("PASS customizable icon bar persistence and loading lifecycle");
            ExpandProjectPanel();
            ToggleProjectPanelWidth();
            if (ProjectPanel.Visibility != Visibility.Collapsed || ProjectRestore.Visibility != Visibility.Visible) throw new Exception("Project collapse rail");
            ToggleProjectPanelWidth();
            ToggleExplorerPanelWidth();
            if (SidePanelContent.Visibility != Visibility.Collapsed || ExplorerRestore.Visibility != Visibility.Visible) throw new Exception("Explorer collapse rail");
            ShowSide(ExplorerPanel);
            if (SidePanelContent.Visibility != Visibility.Visible || ExplorerRestore.Visibility != Visibility.Collapsed) throw new Exception("Explorer restore");
            report.Add("PASS panel arrow collapse/restore");
            var pinnedHeader = new StackPanel(); pinnedHeader.Children.Add(new TextBlock { Text = "Controller đang chọn" });
            var pinnedTab = new TabItem { Header = pinnedHeader, Tag = "smoke-controller", Content = new TextBlock { Text = "Controller files" } };
            ProjectFileTabs.Items.Add(pinnedTab); ProjectFileTabs.SelectedItem = pinnedTab; UpdateLayout(); pinnedTab.ApplyTemplate();
            if (PinnedController.Text != "Controller đang chọn" || pinnedTab.Template.FindName("HeaderBorder", pinnedTab) is not Border { Visibility: Visibility.Collapsed }) throw new Exception("Pinned controller duplicated in tab list");
            ClosePinnedController_Click(this, new RoutedEventArgs());
            if (ProjectFileTabs.Items.Contains(pinnedTab)) throw new Exception("Pinned controller close failed");
            report.Add("PASS selected controller pinned separately and removed from other headers");
            _ = Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.ApplicationIdle, new Action(() =>
            {
                var notification = OwnedWindows.Cast<Window>().Single(w => w.Title == "Kiểm tra thông báo riêng");
                notification.UpdateLayout();
                var preview = new System.Windows.Media.Imaging.RenderTargetBitmap((int)notification.ActualWidth, (int)notification.ActualHeight, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
                preview.Render(notification);
                var png = new System.Windows.Media.Imaging.PngBitmapEncoder(); png.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(preview));
                Directory.CreateDirectory(SettingsStore.Root);
                using (var stream = File.Create(Path.Combine(SettingsStore.Root, "notification.png"))) png.Save(stream);
                var panel = (StackPanel)((Border)notification.Content).Child;
                panel.Children.OfType<StackPanel>().Single().Children.OfType<Button>().Single(b => Equals(b.Content, "Không")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            }));
            if (Dialogs.Notify(this, "Lưu thay đổi trong tài liệu?\nHộp thoại dùng màu giao diện LS Code và các nút riêng.", "Kiểm tra thông báo riêng", MessageBoxButton.YesNoCancel) != MessageBoxResult.No) throw new Exception("Custom notification result");
            report.Add("PASS custom notification render and No result");
            var completionDoc = AddDocument("first-open.sql", "");
            var targetGate = new TaskCompletionSource();
            completionDoc.SqlTarget = null; completionDoc.TargetReady = targetGate.Task;
            completionDoc.CompletionLoader = async () => { await targetGate.Task; await Task.Delay(30); return new[] { new SqlSymbol("dbo", "freecolumns", null, "table") }; };
            Activate(); UpdateLayout(); completionDoc.Primary.Focus();
            var completionCheck = completionDoc.SmokeCompletionAsync();
            completionDoc.SqlTarget = new SqlTarget("fixture", "app", "Server=fixture;Database=app;Integrated Security=true");
            targetGate.SetResult(); await completionCheck;
            await completionDoc.SmokeNarrowingCompletionAsync();
            report.Add("PASS dmbp completion after narrowing 501 object matches");
            tabs.Remove(completionDoc); Documents.Items.Remove(completionDoc.Tab); completionDoc.Dispose();
            report.Add("PASS first SQL completion waits for App target and inserts metadata suggestion");
            var scriptDoc = AddDocument("snippet-check.xml", "");
            UpdateLayout(); scriptDoc.Primary.Focus();
            await scriptDoc.SmokeScriptSnippetsAsync();
            tabs.Remove(scriptDoc); Documents.Items.Remove(scriptDoc.Tab); scriptDoc.Dispose();
            report.Add("PASS JS member snippet insertion, argument selection and script-only scope");
            var newSql = commands.Single(c => c.Id == "sql.new");
            if (!ShortcutValues(newSql).SequenceEqual(new[] { "Ctrl+Shift+Q", "Ctrl+N" })) throw new Exception("SQL shortcut defaults");
            foreach (var command in commands)
                foreach (var value in ShortcutValues(command).Where(v => v.Length > 0))
                    if (!TryGesture(value, out _)) throw new Exception("Invalid default shortcut: " + value);
            settings.Shortcuts["sql.new"] = ["Ctrl+Alt+Q", "Ctrl+Alt+N"];
            SettingsStore.Save(settings);
            if (!SettingsStore.Load().Shortcuts["sql.new"].SequenceEqual(ShortcutValues(newSql))) throw new Exception("Shortcut persistence");
            settings.Shortcuts.Clear();
            SettingsStore.Save(settings);
            report.Add("PASS two shortcut slots, default gesture parsing and settings round-trip");
            var doc = AddDocument("smoke.xml", "<root><item name=\"Tiếng Việt\" /></root>");
            doc.ToggleSplit();
            if (!ReferenceEquals(doc.Primary.Document, doc.Secondary!.Document)) throw new Exception("Split document mismatch");
            doc.Buffer.Insert(doc.Buffer.TextLength, "\n");
            if (!doc.Dirty || doc.Secondary.Text != doc.Primary.Text) throw new Exception("Dirty/split synchronization failed");
            report.Add("PASS split buffer + dirty state + Unicode");
            ValidateActive(); if (Problems.Items.Count != 0 || Outline.Items.Count != 1) throw new Exception("XML diagnostics/outline failed");
            report.Add("PASS XML diagnostics + outline");
            foreach (var theme in new[] { "Dark", "Light", "High Contrast" }) { settings.Theme = theme; ApplyTheme(); }
            report.Add("PASS theme resource switching");
            var loadedSnippets = SnippetImporter.Read(await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "Assets", "snippets.xml")));
            if (loadedSnippets.Count != 12) throw new Exception("Expected 12 legacy snippets");
            report.Add("PASS 12 legacy snippets");
            var sql = AddDocument("query.sql", "select 1 as id;\nGO\nselect N'Tiếng Việt' as message;");
            if (sql.Primary.SyntaxHighlighting == null) throw new Exception("SQL syntax highlighting missing");
            if (!SqlLanguageService.Format(sql.Buffer.Text).Contains("SELECT")) throw new Exception("SQL format failed");
            report.Add("PASS SQL highlighting + ScriptDom formatting");
            TableBrowser.BuildQuery("dbo", "freecolumns", "*", "controller LIKE 'GLDetail'", "controller");
            foreach (var fields in new[] { ("*", "1=1; DELETE FROM dbo.freecolumns"), ("* INTO dbo.bad", ""), ("*", "1=1 UNION SELECT * FROM dbo.freecolumns") })
            {
                bool rejected = false;
                try { TableBrowser.BuildQuery("dbo", "freecolumns", fields.Item1, fields.Item2, ""); } catch (InvalidOperationException) { rejected = true; }
                if (!rejected) throw new Exception("Table browser accepted invalid read-only input");
            }
            report.Add("PASS table query validation rejects extra statements / INTO / UNION");
            var legacyTarget = ProjectDatabase.Target("Server=fixture;Database=fixture;Integrated Security=true");
            var encryptedTarget = ProjectDatabase.Target("Server=fixture;Database=fixture;Integrated Security=true;Encrypt=True");
            if (new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(legacyTarget.ConnectionString).Encrypt != Microsoft.Data.SqlClient.SqlConnectionEncryptOption.Optional || new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(encryptedTarget.ConnectionString).Encrypt != Microsoft.Data.SqlClient.SqlConnectionEncryptOption.Mandatory)
                throw new Exception("web.config encryption settings not preserved");
            report.Add("PASS legacy Encrypt default and explicit Encrypt preservation");
            var originalTarget = sql.SqlTarget;
            var fixture = ProjectImporter.Create("Smoke ERP", "smoke-server", "smoke-database", SettingsStore.Root);
            ProjectStore.Save([fixture]);
            var stored = ProjectStore.Load();
            if (stored.Count != 1 || stored[0] != fixture) throw new Exception("Encrypted project round-trip failed");
            var catalogPath = Path.Combine(SettingsStore.Root, "projects.dat");
            if (System.Text.Encoding.UTF8.GetString(await File.ReadAllBytesAsync(catalogPath)).Contains("smoke-server")) throw new Exception("Project catalog stored plaintext");
            ProjectStore.Save(stored);
            if (!File.Exists(catalogPath + ".bak")) throw new Exception("Project catalog backup missing");
            report.Add("PASS DPAPI project catalog round-trip + backup");
            ActivateProject(fixture);
            var nextSql = AddDocument("project-query.sql", "select 2;");
            if (sql.SqlTarget != originalTarget || nextSql.SqlTarget?.Server != fixture.Server || nextSql.SqlTarget?.Database != fixture.Database)
                throw new Exception("SQL tab target isolation failed");
            report.Add("PASS project switching preserves existing SQL targets");
            tabs.Remove(nextSql); Documents.Items.Remove(nextSql.Tab); nextSql.Dispose();
            tabs.Remove(sql); Documents.Items.Remove(sql.Tab); sql.Dispose(); Documents.SelectedItem = doc.Tab;
            var path = Path.Combine(SettingsStore.Root, "smoke.xml"); Directory.CreateDirectory(SettingsStore.Root);
            doc.Snapshot = await DocumentStore.SaveAsync(path, doc.Buffer.Text, null); doc.Path = path;
            var reopened = await DocumentStore.OpenAsync(path); if (reopened.Text != doc.Buffer.Text) throw new Exception("Save/reopen failed");
            report.Add("PASS save/reopen");
            using (var reader = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                doc.Buffer.Text = "<root>edited XML — Tiếng Việt</root>";
                if (!await SaveAsync(doc) || doc.Dirty || (await DocumentStore.OpenAsync(path)).Text != doc.Buffer.Text)
                    throw new Exception("XML editor save with shared reader failed");
                doc.Buffer.Text = "<root>second save</root>";
                if (!await SaveAsync(doc) || doc.Dirty || (await DocumentStore.OpenAsync(path)).Text != doc.Buffer.Text)
                    throw new Exception("Repeated XML editor save failed");
            }
            report.Add("PASS XML editor edit/save twice with reader denying delete");
            await File.WriteAllTextAsync(path, "<root>external edit</root>");
            var contentBeforeNotice = doc.Buffer.Text;
            var noticedBeforeReload = false;
            var noticeTimer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };
            noticeTimer.Tick += (_, _) =>
            {
                var notice = OwnedWindows.Cast<Window>().FirstOrDefault(w => w.Title == "File thay đổi bên ngoài");
                if (notice?.Content is not Border { Child: StackPanel panel }) return;
                noticedBeforeReload = doc.Buffer.Text == contentBeforeNotice;
                noticeTimer.Stop();
                panel.Children.OfType<StackPanel>().Last().Children.OfType<Button>()
                    .Single(b => Equals(b.Content, "OK")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            };
            noticeTimer.Start();
            try { await CheckExternalFilesAsync(); }
            finally { noticeTimer.Stop(); }
            if (!noticedBeforeReload) throw new Exception("XML reload must wait for notification OK");
            if (doc.Buffer.Text != "<root>external edit</root>" || doc.Dirty)
                throw new Exception("XML tab did not reload external edit after OK");
            if (Directory.EnumerateFiles(SettingsStore.Root, "smoke.xml.lscode*").Any())
                throw new Exception("XML save created a backup or temporary file");
            report.Add("PASS external XML edit waits for OK before reload; no backup or temporary files");
            AddDocument("embedded-language.xml", EmbeddedHighlightingChecks.Fixture);
            foreach (var name in new[] { "zrptLoanContractBalance3", "zrptLoanContractDepositObj", "zrptLoanContractDetail" })
            {
                var header = new DockPanel(); var close = new Button { Content = "×", Width = 22, Height = 22, Padding = new Thickness(0) }; DockPanel.SetDock(close, Dock.Right); header.Children.Add(close);
                header.Children.Add(new TextBlock { Text = name, TextTrimming = TextTrimming.CharacterEllipsis });
                var controller = new TabItem { Header = header, Tag = name, Content = new TreeView { Items = { new TreeViewItem { Header = CreateTreeHeader(name + ".aspx", false) }, new TreeViewItem { Header = CreateTreeHeader("web.config", false) }, new TreeViewItem { Header = CreateTreeHeader("report.xlsx", false) }, new TreeViewItem { Header = CreateTreeHeader("report.rpt", false) }, new TreeViewItem { Header = CreateTreeHeader("schema.xsd", false) } } } };
                ProjectFileTabs.Items.Add(controller); ProjectFileTabs.SelectedItem = controller;
            }
            if (RecentControllers.Children.Count != 2 || controllerHistory.Count != ProjectFileTabs.Items.Count || controllerHistory[0] != ProjectFileTabs.SelectedItem) throw new Exception("Recent controller limit/full list/order");
            report.Add("PASS controller MRU shows two alternatives and retains all open controllers");
            settings.Theme = "Dark"; ApplyTheme();
            var selectedNode = new TreeViewItem { Header = "Dòng chọn vẫn đọc được khi mất focus", IsSelected = true };
            CommandTree.Items.Add(selectedNode);
            ExpandProjectPanel(); ProjectViews.SelectedIndex = 0;
            Active!.Primary.Focus(); UpdateLayout(); selectedNode.ApplyTemplate();
            if (selectedNode.Template.FindName("Selection", selectedNode) is not Border selection ||
                selection.Background != (System.Windows.Media.Brush)FindResource("Raised")) throw new Exception("Inactive tree selection theme");
            var darkBitmap = new System.Windows.Media.Imaging.RenderTargetBitmap((int)ActualWidth, (int)ActualHeight, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
            darkBitmap.Render(this);
            var darkEncoder = new System.Windows.Media.Imaging.PngBitmapEncoder(); darkEncoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(darkBitmap));
            using (var stream = File.Create(Path.Combine(SettingsStore.Root, "smoke-dark.png"))) darkEncoder.Save(stream);
            report.Add("PASS dark tree selection retains themed background without focus");
            settings.Theme = "Light"; ApplyTheme(); UpdateLayout();
            var bitmap = new System.Windows.Media.Imaging.RenderTargetBitmap((int)ActualWidth, (int)ActualHeight, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
            bitmap.Render(this);
            var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder(); encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));
            using (var stream = File.Create(Path.Combine(SettingsStore.Root, "smoke-window.png"))) encoder.Save(stream);
            report.Add("PASS WPF layout / window startup / render");
            await CheckObjectAndProfilerAsync(report);
            await CheckCompareToolsAsync(report);
            await CheckResultGridScrollingAsync(report);
            await CheckFileActionsAndEmbeddedProfilerAsync(report);
            CheckRedesignedWorkspace(report);
        }
        catch (Exception ex) { report.Add("FAIL " + ex); }
        Directory.CreateDirectory(SettingsStore.Root); await File.WriteAllLinesAsync(Path.Combine(SettingsStore.Root, "smoke-result.txt"), report);
        closing = true; Close();
    }
    private void Connection_Click(object s, RoutedEventArgs e) => ConfigureConnection();
    private async void Execute_Click(object s, RoutedEventArgs e) => await Guard(ExecuteAsync);
    private void Cancel_Click(object s, RoutedEventArgs e) => sqlCancellation?.Cancel();
    private async void Export_Click(object s, RoutedEventArgs e) => await Guard(ExportResultAsync);
    private void SnippetFilter_Changed(object s, TextChangedEventArgs e) => RefreshSnippets();
    private void InsertSnippet_Click(object s, RoutedEventArgs e) => InsertSnippet();
    private void Snippet_DoubleClick(object s, MouseButtonEventArgs e) => InsertSnippet();
    private void NewSnippet_Click(object s, RoutedEventArgs e) => EditSnippet(true);
    private void EditSnippet_Click(object s, RoutedEventArgs e) => EditSnippet(false);
    private void DeleteSnippet_Click(object s, RoutedEventArgs e) { if (SnippetList.SelectedItem is Snippet item && Dialogs.Notify(this, $"Xóa snippet {item.Key}?", "Snippets", MessageBoxButton.YesNo) == MessageBoxResult.Yes) { snippets.Remove(item); PersistSnippets(); } }
    private async void ImportSnippets_Click(object s, RoutedEventArgs e) => await Guard(ImportSnippetsAsync);
    private async void ExportSnippets_Click(object s, RoutedEventArgs e) => await Guard(ExportSnippetsAsync);
    private async void Compare_Click(object s, RoutedEventArgs e) => await Guard(CompareAsync);
    private void Translate_Click(object s, RoutedEventArgs e) => ShowTranslator();
    private void ToolsSettings_Click(object s, RoutedEventArgs e) => ConfigureExternalTools();
    private async void Crystal_Click(object s, RoutedEventArgs e) => await Guard(() => { var d = new OpenFileDialog { Filter = "Crystal Report|*.rpt" }; if (d.ShowDialog(this) == true) LaunchTool(settings.CrystalPath, d.FileName); return Task.CompletedTask; });
    private async void Profiler18_Click(object s, RoutedEventArgs e) => await Guard(OpenProfilerAsync);
    private async void ImportProject_Click(object s, RoutedEventArgs e) => await Guard(ImportProjectAsync);
    private async void XPath_Click(object s, RoutedEventArgs e) => await Guard(EvaluateXPathAsync);
}








