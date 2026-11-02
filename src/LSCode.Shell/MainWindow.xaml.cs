using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using System.Xml;
using System.Xml.Linq;
using LSCode.Core;
using LSCode.Shell.Services;
using Microsoft.Win32;

namespace LSCode.Shell;

public partial class MainWindow : Window
{
    private readonly StudioSettings settings = SettingsStore.Load();
    private readonly List<EditorTab> tabs = [];
    private readonly List<IdeCommand> commands = [];
    private List<Snippet> snippets = [];
    private List<LegacyProject> projects = [];
    private CancellationTokenSource? searchCancellation, sqlCancellation;
    private readonly Dictionary<string, Task> openingFiles = new(StringComparer.OrdinalIgnoreCase);
    private readonly DispatcherTimer diagnosticsTimer = new() { Interval = TimeSpan.FromMilliseconds(450) };
    private readonly DispatcherTimer recoveryTimer = new() { Interval = TimeSpan.FromSeconds(8) };
    private readonly DispatcherTimer externalFileTimer = new() { Interval = TimeSpan.FromSeconds(2) };
    private string sqlPassword = "";
    private bool closing;
    private readonly Dictionary<string, (DateTime LastWriteUtc, long Length)> externalFileStamps = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> externalWarnings = new(StringComparer.OrdinalIgnoreCase);
    private EditorTab? Active => Documents.SelectedItem is TabItem { Tag: EditorTab doc } ? doc : null;
    private string RecoveryPath => Path.Combine(SettingsStore.Root, "recovery.json");
    private string SnippetsPath => Path.Combine(SettingsStore.Root, "snippets.xml");
    private sealed record RecoveryItem(string Name, string Text);
    private sealed record ProblemEntry(EditorTab Document, XmlDiagnostic Diagnostic)
    {
        public override string ToString() => $"{Document.Name}:{Diagnostic.Line}:{Diagnostic.Column}  {Diagnostic.Message}";
    }

    public MainWindow()
    {
        InitializeComponent(); ApplyTheme(); RegisterCommands(); FillShortcutMenu(); InitializeActivityBar(); InitializeControllerHistory(); InitializeDbSearch(); ShowSide(ExplorerPanel); ProjectViews.SelectionChanged += (_, _) => UpdateNavigationSelection(); SourceInitialized += (_, _) => FitWindowToScreen();
        diagnosticsTimer.Tick += (_, _) => { diagnosticsTimer.Stop(); ValidateActive(); };
        recoveryTimer.Tick += (_, _) => { try { SaveRecovery(); } catch (Exception ex) { Log("Recovery: " + ex.Message); } };
        recoveryTimer.Start(); PreviewKeyDown += HandleShortcut; Closing += OnClosing;
        externalFileTimer.Tick += async (_, _) => await CheckExternalFilesAsync();
        externalFileTimer.Start();
        Loaded += async (_, _) =>
        {
            using var startup = ShowLoadingOverlay(WindowLayers, "Đang khởi động LS Code…");
            try
            {
                snippets = SnippetImporter.Read(await File.ReadAllTextAsync(File.Exists(SnippetsPath) ? SnippetsPath : Path.Combine(AppContext.BaseDirectory, "Assets", "snippets.xml")));
                RefreshSnippets();
                try { projects = ProjectStore.Load(); }
                catch (Exception ex) { projectCatalogAvailable = false; Log("Không đọc được project catalog; file được giữ nguyên: " + ex.Message); }
                var smoke = Environment.GetCommandLineArgs().Any(a => a is "--smoke" or "--project-smoke");
                startup.Dispose();
                if (!smoke) await SelectProjectAsync();
                if (smoke && Directory.Exists(settings.Workspace)) LoadWorkspace(settings.Workspace!);
                if (smoke && projects.FirstOrDefault(p => p.Id == settings.ActiveProjectId) is { } activeProject)
                {
                    WorkspaceName.Text = activeProject.Name;
                    if (!string.IsNullOrWhiteSpace(activeProject.Field("SysConnectionString"))) projectSqlTarget = ProjectDatabase.Target(activeProject.Field("SysConnectionString"));
                    _ = RefreshCommandsAsync();
                }
                if (File.Exists(RecoveryPath))
                    foreach (var item in JsonSerializer.Deserialize<List<RecoveryItem>>(await File.ReadAllTextAsync(RecoveryPath)) ?? [])
                        AddDocument(item.Name, item.Text);
                Log($"LS Code 0.1 · {snippets.Count} snippets · SQL/XML native editors ready.");
            }
            catch (Exception ex) { Log(ex.Message); }
        };
    }

    private void Log(string message)
    {
        Output.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");
        if (Output.Text.Length > 100000) Output.Text = Output.Text[^70000..];
        Output.ScrollToEnd();
    }
    private async Task Guard(Func<Task> action)
    {
        using var busy = BeginBusy("Đang xử lý…");
        try { await action(); }
        catch (OperationCanceledException) { Log("Đã hủy thao tác."); }
        catch (Exception ex) { Log(ex.Message); Dialogs.Notify(this, ex.Message, "LS Code", MessageBoxButton.OK, MessageBoxImage.Warning); }
    }
    private void RegisterCommands()
    {
        commands.AddRange([
            new("file.open", "File: Mở file", "Ctrl+O", OpenFileDialogAsync),
            new("window.new", "Window: Mở cửa sổ LS Code mới", "Ctrl+Shift+N", OpenNewWindow),
            new("workspace.open", "Workspace: Mở thư mục", "Ctrl+Shift+O", () => { OpenFolder(); return Task.CompletedTask; }),
            new("project.select", "Project: Chọn project", "", SelectProjectAsync),
            new("project.sync", "Project: Đồng bộ từ CustomerPro", "", SyncProjectAsync),
            new("sql.new", "SQL: Mở ô nhập SQL", "Ctrl+Shift+Q", () => { NewDocument(); return Task.CompletedTask; }),
            new("sql.tables", "SQL: Cấu trúc và dữ liệu bảng", "Ctrl+Shift+T", OpenTableBrowserAsync),
            new("project.import", "Project: Import cấu hình legacy", "", ImportProjectAsync),
            new("file.save", "File: Lưu", "Ctrl+S", async () => { if (Active is { } d) await SaveAsync(d); }),
            new("file.close", "File: Đóng tab", "Ctrl+W", async () => { if (Active is { } d) await CloseDocumentAsync(d); }),
            new("editor.split", "Editor: Bật / tắt split", "Ctrl+\\", () => { Active?.ToggleSplit(); return Task.CompletedTask; }),
            new("editor.format", "Editor: Format SQL / XML / JSON", "", FormatAsync),
            new("workspace.search", "Search: Tìm toàn workspace", "Ctrl+Shift+F", () => { ShowSide(SearchPanel); SearchInput.Focus(); return Task.CompletedTask; }),
            new("sql.connection", "SQL: Cấu hình kết nối", "", () => { ConfigureConnection(); return Task.CompletedTask; }),
            new("sql.execute", "SQL: Execute selection / document", "F5", ExecuteAsync),
            new("sql.cancel", "SQL: Dừng query", "", () => { sqlCancellation?.Cancel(); return Task.CompletedTask; }),
            new("theme.switch", "Appearance: Đổi theme", "", () => { CycleTheme(); return Task.CompletedTask; }),
            new("tools.translate", "Tools: Google Dịch", "", () => { ShowTranslator(); return Task.CompletedTask; }),
            new("editor.shortcuts", "Editor: Phím tắt", "", () => { ShowShortcuts(); return Task.CompletedTask; }),
            new("snippet.open", "Snippets: Mở thư viện", "", () => { ShowSide(SnippetPanel); return Task.CompletedTask; }),
            new("tools.compare-text", "Tools: Compare Text", "", CompareTextAsync),
            new("tools.compare-folder", "Tools: Compare Folder", "", CompareFolderAsync),
            new("tools.beautify-sql", "Tools: Beautify SQL (Beauty SQL)", "", BeautifySqlAsync),
            new("compare.file", "Compare: So sánh với file", "", CompareAsync)
        ]);
    }

    private EditorTab AddDocument(string name, string text, FileSnapshot? snapshot = null, SqlTarget? sqlTarget = null)
    {
        var doc = new EditorTab(name, text) { Path = snapshot?.Path, Snapshot = snapshot };
        if (doc.Language == ".sql") doc.SqlTarget = sqlTarget ?? CurrentSqlTarget();
        if (doc.Language == ".sql") AddSqlToolbar(doc, sqlTarget != null);
        var header = new StackPanel { Orientation = Orientation.Horizontal };
        var label = new TextBlock { VerticalAlignment = VerticalAlignment.Center };
        var close = new Button { Content = "×", Style = (Style)FindResource("IconButton"), Padding = new Thickness(0), Margin = new Thickness(12, 0, 0, 0), ToolTip = "Close tab" };
        header.Children.Add(label); header.Children.Add(close); doc.Tab.Header = header;
        void UpdateHeader() => RefreshDocumentHeaders();
        doc.Changed += () => { UpdateHeader(); diagnosticsTimer.Stop(); diagnosticsTimer.Start(); };
        doc.CaretChanged += UpdateStatus;
        close.Click += async (_, _) => await Guard(async () => { await CloseDocumentAsync(doc); });
        tabs.Add(doc); Documents.Items.Add(doc.Tab); Documents.SelectedItem = doc.Tab;
        Welcome.Visibility = Visibility.Collapsed; UpdateHeader(); doc.Primary.Focus(); UpdateStatus(); ValidateActive();
        return doc;
    }
    private void NewDocument() => AddDocument($"Untitled-{tabs.Count + 1}.sql", "");
    private Task OpenNewWindow()
    {
        var executable = Environment.ProcessPath;
        if (!string.IsNullOrWhiteSpace(executable)) Process.Start(new ProcessStartInfo(executable) { UseShellExecute = true });
        return Task.CompletedTask;
    }
    private async Task OpenFileAsync(string path, int line = 1, int column = 1)
    {
        path = Path.GetFullPath(path);
        var doc = tabs.FirstOrDefault(d => string.Equals(d.Path, path, StringComparison.OrdinalIgnoreCase));
        if (doc != null) { FocusDocument(doc, line, column); return; }
        if (openingFiles.TryGetValue(path, out var pending))
        {
            try { await pending; } catch (Exception ex) { Log(ex.Message); }
            doc = tabs.FirstOrDefault(d => string.Equals(d.Path, path, StringComparison.OrdinalIgnoreCase));
            if (doc != null) FocusDocument(doc, line, column);
            return;
        }
        var gate = new TaskCompletionSource();
        openingFiles[path] = gate.Task;
        try
        {
            var snapshot = await DocumentStore.OpenAsync(path);
            doc = tabs.FirstOrDefault(d => string.Equals(d.Path, path, StringComparison.OrdinalIgnoreCase)) ?? AddDocument(Path.GetFileName(path), snapshot.Text, snapshot);
            FocusDocument(doc, line, column);
            settings.RecentFiles.RemoveAll(p => string.Equals(p, path, StringComparison.OrdinalIgnoreCase));
            settings.RecentFiles.Insert(0, path); settings.RecentFiles = settings.RecentFiles.Take(30).ToList();
            if (File.Exists(path)) { var info = new FileInfo(path); externalFileStamps[path] = (info.LastWriteTimeUtc, info.Length); }
        }
        finally { openingFiles.Remove(path); gate.TrySetResult(); }
    }
    private void FocusDocument(EditorTab doc, int line, int column)
    {
        Documents.SelectedItem = doc.Tab; Welcome.Visibility = Visibility.Collapsed; Activate();
        var actualLine = Math.Clamp(line, 1, doc.Buffer.LineCount);
        var actualColumn = Math.Clamp(column, 1, doc.Buffer.GetLineByNumber(actualLine).Length + 1);
        doc.ActiveEditor.ScrollTo(actualLine, actualColumn);
        doc.ActiveEditor.TextArea.Caret.Line = actualLine; doc.ActiveEditor.TextArea.Caret.Column = actualColumn;
        doc.ActiveEditor.Focus();
    }
    private void FillShortcutMenu()
    {
        var item = new MenuItem { Header = "Xem và chỉnh sửa phím tắt…" };
        item.Click += (_, _) => ShowShortcuts();
        ShortcutMenu.Items.Add(item);
        RegisterExtraShortcuts();
    }
    private async Task OpenFileDialogAsync()
    {
        var dialog = new OpenFileDialog { Multiselect = true, Filter = "Code / text|*.sql;*.xml;*.js;*.json;*.cs;*.txt;*.md;*.config;*.xaml|All files|*.*" };
        if (dialog.ShowDialog(this) == true) foreach (var file in dialog.FileNames) await OpenFileAsync(file);
    }
    private async Task<bool> SaveAsync(EditorTab doc, bool saveAs = false)
    {
        var path = doc.Path;
        if (saveAs || path == null)
        {
            var dialog = new SaveFileDialog { FileName = doc.Name, Filter = "All files|*.*", OverwritePrompt = true };
            if (dialog.ShowDialog(this) != true) return false;
            path = dialog.FileName;
            if (tabs.Any(d => d != doc && string.Equals(d.Path, path, StringComparison.OrdinalIgnoreCase)))
                throw new IOException("File này đang mở trong tab khác.");
        }
        var text = doc.Buffer.Text;
        FileSnapshot snapshot;
        try
        {
            try { snapshot = await DocumentStore.SaveAsync(path, text, doc.Snapshot); }
            catch (UnauthorizedAccessException) when (File.Exists(path) && (File.GetAttributes(path) & FileAttributes.ReadOnly) != 0)
            {
                var answer = Dialogs.Notify(this,
                    $"File đang có thuộc tính chỉ đọc (Read-only):\n{path}\n\nBạn có muốn ghi thay đổi vào file này? Thuộc tính chỉ đọc sẽ được khôi phục sau khi lưu.\nChọn Không để lưu sang file khác.",
                    "Lưu file chỉ đọc", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
                if (answer == MessageBoxResult.Cancel) return false;
                if (answer == MessageBoxResult.No) return await SaveAsync(doc, true);
                snapshot = await DocumentStore.SaveAsync(path, text, doc.Snapshot, overwriteReadOnly: true);
            }
        }
        catch (UnauthorizedAccessException ex)
        {
            Log($"Save denied: {ex.Message}");
            var answer = Dialogs.Notify(this,
                $"Không có quyền ghi file tại:\n{path}\n\nNội dung chỉnh sửa vẫn được giữ trong tab. Bạn có muốn lưu sang vị trí khác?\n\nChi tiết: {ex.Message}",
                "Không thể lưu file", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            return answer == MessageBoxResult.Yes && await SaveAsync(doc, true);
        }
        doc.Snapshot = snapshot; doc.Path = path; doc.Name = Path.GetFileName(path);
        if (File.Exists(path)) { var info = new FileInfo(path); externalFileStamps[path] = (info.LastWriteTimeUtc, info.Length); externalWarnings.Remove(path); }
        doc.RefreshHighlighting(settings.Theme);
        RefreshDocumentHeaders();
        doc.Tab.ToolTip = path; Log("Saved " + path); SaveRecovery(); UpdateStatus(); return true;
    }
    private async Task<bool> CloseDocumentAsync(EditorTab doc)
    {
        if (doc.Dirty)
        {
            var answer = Dialogs.Notify(this, $"Lưu thay đổi trong {doc.Name}?", "Đóng file", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
            if (answer == MessageBoxResult.Cancel) return false;
            if (answer == MessageBoxResult.Yes && !await SaveAsync(doc)) return false;
        }
        tabs.Remove(doc); Documents.Items.Remove(doc.Tab); doc.Dispose(); RefreshDocumentHeaders(); Welcome.Visibility = Documents.Items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        SaveRecovery(); return true;
    }
    private bool closeRequested;
    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (closing) return;
        e.Cancel = true;
        if (closeRequested) return;
        closeRequested = true;
        // Leave WPF's Closing event before opening save dialogs or closing again.
        _ = Dispatcher.BeginInvoke(new Action(async () => await CompleteCloseAsync()));
    }
    private async Task CompleteCloseAsync()
    {
        diagnosticsTimer.Stop(); recoveryTimer.Stop(); externalFileTimer.Stop(); dbSearchTimer.Stop();
        try
        {
            foreach (var doc in tabs.ToList()) if (!await CloseDocumentAsync(doc)) return;
            SettingsStore.Save(settings); SaveRecovery();
            sqlCancellation?.Cancel(); searchCancellation?.Cancel(); objectCancellation?.Cancel(); controllerNavigation?.Cancel(); commandCancellation?.Cancel(); syncCancellation?.Cancel();
            objectScriptCancellation?.Cancel();
            await CloseProfilerTabAsync();
            closing = true; Close();
        }
        catch (Exception ex) { Log("Không thể đóng: " + ex.Message); Dialogs.Notify(this, ex.Message, "Không thể đóng ứng dụng"); }
        finally
        {
            closeRequested = false;
            if (!closing) { recoveryTimer.Start(); externalFileTimer.Start(); diagnosticsTimer.Start(); }
        }
    }
    private void SaveRecovery()
    {
        Directory.CreateDirectory(SettingsStore.Root);
        var entries = tabs.Where(d => d.Dirty).Select(d => new RecoveryItem(d.Name, d.Buffer.Text)).ToList();
        var temp = RecoveryPath + ".tmp"; File.WriteAllText(temp, JsonSerializer.Serialize(entries)); File.Move(temp, RecoveryPath, true);
    }
    private void UpdateStatus()
    {
        RefreshProjectWebLink();
        if (Active is not { } doc) { CaretStatus.Text = "LS Code"; return; }
        CaretStatus.Text = $"Ln {doc.ActiveEditor.TextArea.Caret.Line}, Col {doc.ActiveEditor.TextArea.Caret.Column}   ·   {doc.Snapshot?.Encoding.WebName ?? "utf-8"}   ·   {doc.Snapshot?.Newline ?? "LF"}   ·   {doc.Language}";
        var target = doc.SqlTarget;
        if (doc.TargetLabel != null) doc.TargetLabel.Text = target?.Server + " / " + target?.Database;
        Status.Text = $"{Path.GetFileName(settings.Workspace) ?? "No workspace"}   ·   {target?.Server ?? settings.Server} / {target?.Database ?? settings.Database}";
    }
    private void OpenFolder()
    {
        var dialog = new OpenFolderDialog { Title = "Mở workspace LS Code" };
        if (dialog.ShowDialog(this) == true) LoadWorkspace(dialog.FolderName);
    }
    private void LoadWorkspace(string path)
    {
        settings.Workspace = Path.GetFullPath(path); WorkspaceName.Text = Path.GetFileName(path);
        Explorer.Items.Clear(); AddFolderItems(Explorer.Items, path); SettingsStore.Save(settings); ShowSide(ExplorerPanel); UpdateStatus();
        Log("Workspace: " + path);
    }
    private void OpenWorkspaceExplorer()
    {
        if (settings.Workspace is { } path && Directory.Exists(path))
            Process.Start(new ProcessStartInfo("explorer.exe", "\"" + path + "\"") { UseShellExecute = true });
        else Log("Chưa có thư mục dự án.");
    }
    private static UIElement CreateTreeHeader(string name, bool folder)
    {
        var row = new DockPanel { LastChildFill = true };
        var extension = Path.GetExtension(name).ToLowerInvariant();
        var glyph = folder ? "\uE8B7" : extension switch { ".sql" => "\uE968", ".xml" or ".xsd" or ".f" or ".aspx" or ".js" => "\uE943", ".config" => "\uE713", ".xlsx" or ".xls" or ".csv" => "\uE80A", _ => "\uE8A5" };
        var icon = new TextBlock { Text = glyph, FontFamily = new FontFamily("Segoe MDL2 Assets"), Width = 20, FontSize = 14, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) };
        icon.SetResourceReference(TextBlock.ForegroundProperty, "Muted");
        DockPanel.SetDock(icon, Dock.Left); row.Children.Add(icon);
        row.Children.Add(new TextBlock { Text = name, TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center, ToolTip = name });
        return row;
    }
    private void AddFolderItems(ItemCollection items, string folder)
    {
        try
        {
            foreach (var dir in Directory.EnumerateDirectories(folder).OrderBy(Path.GetFileName))
            {
                if (WorkspaceSearch.IgnoredDirectories.Contains(Path.GetFileName(dir)) || (File.GetAttributes(dir) & FileAttributes.ReparsePoint) != 0) continue;
                var node = new TreeViewItem { Header = CreateTreeHeader(Path.GetFileName(dir), true), Tag = dir, ToolTip = dir }; node.Items.Add("…");
                node.Expanded += (_, e) => { if (e.OriginalSource != node) return; if (node.Items.Count == 1 && node.Items[0] is string) { node.Items.Clear(); AddFolderItems(node.Items, dir); } };
                items.Add(node);
            }
            foreach (var file in Directory.EnumerateFiles(folder).OrderBy(Path.GetFileName))
                if ((File.GetAttributes(file) & FileAttributes.ReparsePoint) == 0)
                    items.Add(FileNode(file));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { Log(ex.Message); }
    }
    private void ShowSide(UIElement selected)
    {
        SetPanelCollapsed(false, false);
        ExplorerPanel.Visibility = selected == ExplorerPanel ? Visibility.Visible : Visibility.Collapsed;
        SearchPanel.Visibility = selected == SearchPanel ? Visibility.Visible : Visibility.Collapsed;
        SnippetPanel.Visibility = selected == SnippetPanel ? Visibility.Visible : Visibility.Collapsed;
        UpdateNavigationSelection();
    }
    private bool checkingExternalFiles;
    private async Task CheckExternalFilesAsync()
    {
        if (checkingExternalFiles) return;
        checkingExternalFiles = true;
        try
        {
            foreach (var doc in tabs.ToList())
            {
                if (doc.Path is not { } path || !File.Exists(path)) continue;
                try
                {
                    var info = new FileInfo(path);
                    var stamp = (info.LastWriteTimeUtc, info.Length);
                    if (externalFileStamps.TryGetValue(path, out var previous) && stamp == previous) continue;
                    var originalSnapshot = doc.Snapshot;
                    var snapshot = await DocumentStore.OpenAsync(path);
                    if (!tabs.Contains(doc) || doc.Path != path || doc.Snapshot != originalSnapshot) continue;
                    if (snapshot.Hash == doc.Snapshot?.Hash)
                    {
                        externalFileStamps[path] = stamp;
                        continue;
                    }
                    var message = $"File đã được thay đổi từ nơi khác:\n{path}\n\nNhấn OK để tải lại nội dung từ đĩa.";
                    if (doc.Dirty) message += "\n\nTab này có chỉnh sửa chưa lưu. Tải lại sẽ thay thế nội dung đang sửa. Chọn Hủy để giữ lại.";
                    var reload = Dialogs.Notify(this, message,
                        "File thay đổi bên ngoài", MessageBoxButton.OKCancel, MessageBoxImage.Warning, "OK") == MessageBoxResult.OK;
                    if (reload)
                    {
                        doc.ReloadFromDisk(snapshot);
                        RefreshDocumentHeaders();
                        UpdateStatus();
                        Log("Đã tải lại file thay đổi bên ngoài: " + path);
                    }
                    externalFileStamps[path] = stamp;
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Text.DecoderFallbackException)
                {
                    // An external editor may still hold the file. Retry on the next tick.
                    Log("Chưa tải lại được file: " + ex.Message);
                }
            }
        }
        finally { checkingExternalFiles = false; }
    }
    private async Task SearchAsync()
    {
        if (settings.Workspace == null) { OpenFolder(); if (settings.Workspace == null) return; }
        searchCancellation?.Cancel(); var cts = new CancellationTokenSource(); searchCancellation = cts;
        SearchSummary.Text = "Đang tìm trong workspace và buffer đang mở…";
        try
        {
            var buffers = tabs.Where(d => d.Path != null).ToDictionary(d => d.Path!, d => d.Buffer.Text, StringComparer.OrdinalIgnoreCase);
            var results = await WorkspaceSearch.FindAsync(settings.Workspace, SearchInput.Text, RegexSearch.IsChecked == true, CaseSearch.IsChecked == true, cts.Token, buffers);
            if (searchCancellation != cts) return;
            SearchResults.ItemsSource = results; SearchSummary.Text = $"{results.Count} kết quả (tối đa 2000) · gồm buffer file đang mở";
        }
        finally { if (cts.IsCancellationRequested && searchCancellation == cts) SearchSummary.Text = "Đã dừng tìm kiếm"; cts.Dispose(); if (searchCancellation == cts) searchCancellation = null; }
    }
    private void ValidateActive()
    {
        Problems.Items.Clear(); Outline.Items.Clear();
        if (Active is not { Language: ".xml" or ".xaml" or ".config" } doc) return;
        if (doc.Buffer.TextLength > 1_000_000) { Log("XML analysis tạm tắt với file > 1 triệu ký tự; nội dung vẫn sửa/lưu được."); return; }
        doc.UpdateFoldings();
        var diagnostic = XmlTools.Validate(doc.Buffer.Text);
        if (diagnostic != null) { Problems.Items.Add(new ProblemEntry(doc, diagnostic)); return; }
        try
        {
            var parsed = XmlTools.Parse(doc.Buffer.Text);
            if (parsed.Root != null) Outline.Items.Add(XmlNode(parsed.Root, 0));
        }
        catch (XmlException) { }
    }
    private static TreeViewItem XmlNode(XElement element, int depth)
    {
        var info = (IXmlLineInfo)element;
        var node = new TreeViewItem { Header = $"<{element.Name}> " + string.Join(" ", element.Attributes().Take(3).Select(a => $"{a.Name}=\"{a.Value}\"")), Tag = info.LineNumber, IsExpanded = depth < 1 };
        if (depth < 40) foreach (var child in element.Elements().Take(300)) node.Items.Add(XmlNode(child, depth + 1));
        return node;
    }
    private Task FormatAsync()
    {
        if (Active is { } doc)
        {
            string text = doc.Language switch
            {
                ".xml" or ".xaml" or ".config" => XmlTools.Format(doc.Buffer.Text),
                ".sql" => SqlLanguageService.Format(doc.Buffer.Text),
                ".json" => JsonSerializer.Serialize(JsonSerializer.Deserialize<JsonElement>(doc.Buffer.Text), new JsonSerializerOptions { WriteIndented = true }),
                _ => throw new InvalidOperationException("Bản pilot hỗ trợ format SQL (dialect SQL150), XML và JSON.")
            };
            doc.Buffer.Replace(0, doc.Buffer.TextLength, text);
        }
        return Task.CompletedTask;
    }
    private void CycleTheme()
    {
        settings.Theme = settings.Theme switch { "Dark" => "Light", "Light" => "High Contrast", _ => "Dark" }; ApplyTheme(); SettingsStore.Save(settings);
    }
    private void ApplyTheme()
    {
        string[] colors = settings.Theme switch
        {
            "Light" => ["#FAFBFD", "#EFF2F6", "#DFE7F0", "#192738", "#53657C", "#167D6B", "#CAD3DF", "#CAD3DF", "#E5EBF2", "#FFFFFF"],
            "High Contrast" => ["#000000", "#000000", "#151515", "#FFFFFF", "#FFFFFF", "#FFFF00", "#FFFFFF", "#A0A0A0", "#252525", "#000000"],
            _ => ["#141820", "#1C222D", "#283241", "#E0E6EE", "#A1AFC2", "#63D9BB", "#354254", "#354254", "#222B38", "#102820"]
        };
        var keys = new[] { "Surface", "Panel", "Raised", "Text", "Muted", "Accent", "Border", "GridLine", "Hover", "OnAccent" };
        for (var i = 0; i < keys.Length; i++) Application.Current.Resources[keys[i]] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(colors[i]));
        Application.Current.Resources["ThemeName"] = settings.Theme;
        foreach (var doc in tabs.Where(d => d.Primary.IsVisible || d.Secondary?.IsVisible == true))
        {
            doc.RefreshHighlighting(settings.Theme);
            foreach (var editor in new[] { doc.Primary, doc.Secondary }.Where(e => e != null)) editor!.LineNumbersForeground = (Brush)FindResource("Muted");
        }
    }
    private async void HandleShortcut(object sender, KeyEventArgs e)
    {
        if (e.Handled) return;
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        var command = commands.FirstOrDefault(c => ShortcutValues(c).Any(value =>
            TryGesture(value, out var gesture) && gesture!.Key == key && gesture.Modifiers == Keyboard.Modifiers));
        if (command != null) { e.Handled = true; await Guard(command.Execute); }
        else if (key == Key.F && Keyboard.Modifiers == ModifierKeys.Control) e.Handled = true;
    }
    private async Task ShowPaletteAsync() { if (Dialogs.Palette(this, commands.Select(c => c with { Shortcut = string.Join(" / ", ShortcutValues(c).Where(v => v.Length > 0)) })) is { } command) await command.Execute(); }
    private async Task QuickOpenAsync()
    {
        if (settings.Workspace == null) { await OpenFileDialogAsync(); return; }
        var paths = await Task.Run(() => WorkspaceSearch.Files(settings.Workspace).Take(20000).ToList());
        var choices = paths.Select(p => new IdeCommand(p, Path.GetRelativePath(settings.Workspace, p), "", () => OpenFileAsync(p)));
        if (Dialogs.Palette(this, choices, "Go to File") is { } command) await command.Execute();
    }
    private Task ReplaceAsync()
    {
        if (Active is not { } doc) return Task.CompletedTask;
        var panel = new DockPanel { Margin = new Thickness(14) };
        var find = new TextBox { Text = doc.ActiveEditor.SelectedText, MinWidth = 360 };
        var replace = new TextBox { MinWidth = 360 };
        var matchCase = new CheckBox { Content = "Phân biệt hoa thường", Margin = new Thickness(4, 8, 4, 8) };
        void Field(string label, TextBox input)
        {
            panel.Children.Add(new TextBlock { Text = label, Margin = new Thickness(4, 4, 4, 0) });
            panel.Children.Add(input);
        }
        Field("Tìm", find); Field("Thay thế bằng", replace); panel.Children.Add(matchCase);
        var buttons = new WrapPanel { Margin = new Thickness(0, 8, 0, 0) };
        var next = new Button { Content = "Tìm tiếp" }; var one = new Button { Content = "Thay thế" }; var all = new Button { Content = "Thay thế tất cả" };
        buttons.Children.Add(next); buttons.Children.Add(one); buttons.Children.Add(all);
        panel.Children.Add(buttons);
        var window = Dialogs.Create(this, "Tìm và thay thế", panel, 560, 250);
        StringComparison Comparison() => matchCase.IsChecked == true ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        void FindNext()
        {
            if (find.Text.Length == 0) return;
            var start = Math.Min(doc.ActiveEditor.SelectionStart + doc.ActiveEditor.SelectionLength, doc.Buffer.TextLength);
            var index = doc.Buffer.Text.IndexOf(find.Text, start, Comparison());
            if (index < 0) index = doc.Buffer.Text.IndexOf(find.Text, 0, Comparison());
            if (index >= 0) { doc.ActiveEditor.Select(index, find.Text.Length); doc.ActiveEditor.Focus(); }
        }
        next.Click += (_, _) => FindNext();
        one.Click += (_, _) =>
        {
            if (doc.ActiveEditor.SelectionLength > 0 && string.Equals(doc.ActiveEditor.SelectedText, find.Text, Comparison()))
            {
                doc.Buffer.Replace(doc.ActiveEditor.SelectionStart, doc.ActiveEditor.SelectionLength, replace.Text);
                FindNext();
            }
            else FindNext();
        };
        all.Click += (_, _) =>
        {
            if (find.Text.Length == 0) return;
            var comparison = Comparison();
            var text = doc.Buffer.Text; var position = 0; var count = 0;
            while (position < text.Length)
            {
                var index = text.IndexOf(find.Text, position, comparison);
                if (index < 0) break;
                text = text.Remove(index, find.Text.Length).Insert(index, replace.Text);
                position = index + replace.Text.Length; count++;
            }
            if (count > 0) doc.Buffer.Replace(0, doc.Buffer.TextLength, text);
            window.Title = $"Tìm và thay thế — {count} thay đổi";
        };
        window.Loaded += (_, _) => { find.Focus(); find.SelectAll(); };
        window.Show();
        return Task.CompletedTask;
    }

    private async void Open_Click(object s, RoutedEventArgs e) => await Guard(OpenFileDialogAsync);
    private void New_Click(object s, RoutedEventArgs e) => NewDocument();
    private void OpenFolder_Click(object s, RoutedEventArgs e) => OpenFolder();
    private void OpenWorkspaceExplorer_Click(object s, RoutedEventArgs e) => OpenWorkspaceExplorer();
    private async void Save_Click(object s, RoutedEventArgs e) => await Guard(async () => { if (Active is { } d) await SaveAsync(d); });
    private async void SaveAs_Click(object s, RoutedEventArgs e) => await Guard(async () => { if (Active is { } d) await SaveAsync(d, true); });
    private async void Close_Click(object s, RoutedEventArgs e) => await Guard(async () => { if (Active is { } d) await CloseDocumentAsync(d); });
    private async void Palette_Click(object s, RoutedEventArgs e) => await Guard(ShowPaletteAsync);
    private void Theme_Click(object s, RoutedEventArgs e) => CycleTheme();
    private void BottomToggle_Click(object s, RoutedEventArgs e) => ToggleBottomPanel();
    
    private void Split_Click(object s, RoutedEventArgs e) => Active?.ToggleSplit();
    private async void Format_Click(object s, RoutedEventArgs e) => await Guard(FormatAsync);
    private void Find_Click(object s, RoutedEventArgs e) { if (Active is { } d) { d.ActiveEditor.Focus(); ApplicationCommands.Find.Execute(null, d.ActiveEditor); } }
    private async void Replace_Click(object s, RoutedEventArgs e) => await Guard(ReplaceAsync);
    private void ExplorerPanel_Click(object s, RoutedEventArgs e) => ShowSide(ExplorerPanel);
    private GridLength savedNavigationWidth = new(264);
    private void ProjectPanel_Click(object s, RoutedEventArgs e) => ExpandProjectPanel();
    private void ToggleProjectPanel_Click(object s, RoutedEventArgs e) => ToggleProjectPanelWidth();
    private void ToggleExplorerPanel_Click(object s, RoutedEventArgs e) => ToggleExplorerPanelWidth();
    private void ToggleProjectPanelWidth() => SetPanelCollapsed(true, NavigationPane.Visibility == Visibility.Visible);
    private void ExpandProjectPanel() => SetPanelCollapsed(true, false);
    private void ToggleExplorerPanelWidth() => SetPanelCollapsed(false, NavigationPane.Visibility == Visibility.Visible);
    private void ToggleNavigation_Click(object s, RoutedEventArgs e) => SetPanelCollapsed(ProjectPanel.Visibility == Visibility.Visible, true);
    private void FilesNavigation_Click(object s, RoutedEventArgs e) => ShowSide(ExplorerPanel);
    private void FunctionsNavigation_Click(object s, RoutedEventArgs e) { ExpandProjectPanel(); ProjectViews.SelectedIndex = 0; UpdateNavigationSelection(); }
    private void DatabaseNavigation_Click(object s, RoutedEventArgs e) { ExpandProjectPanel(); DbObjectTab.IsSelected = true; UpdateNavigationSelection(); }
    private void UpdateNavigationSelection()
    {
        if (FilesNavigation == null) return;
        FilesNavigation.Tag = SidePanelContent.Visibility == Visibility.Visible && ExplorerPanel.Visibility == Visibility.Visible ? "Selected" : null;
        FunctionsNavigation.Tag = ProjectPanel.Visibility == Visibility.Visible && !DbObjectTab.IsSelected ? "Selected" : null;
        DatabaseNavigation.Tag = ProjectPanel.Visibility == Visibility.Visible && DbObjectTab.IsSelected ? "Selected" : null;
    }
    private void SetPanelCollapsed(bool project, bool collapsed)
    {
        if (collapsed && NavigationPane.Visibility == Visibility.Visible) savedNavigationWidth = NavigationColumn.Width;
        NavigationPane.Visibility = collapsed ? Visibility.Collapsed : Visibility.Visible;
        ProjectPanel.Visibility = !collapsed && project ? Visibility.Visible : Visibility.Collapsed;
        SidePanelContent.Visibility = !collapsed && !project ? Visibility.Visible : Visibility.Collapsed;
        ProjectRestore.Visibility = collapsed && project ? Visibility.Visible : Visibility.Collapsed;
        ExplorerRestore.Visibility = collapsed && !project ? Visibility.Visible : Visibility.Collapsed;
        NavigationColumn.MinWidth = collapsed ? 26 : 220;
        NavigationColumn.Width = collapsed ? new GridLength(26) : savedNavigationWidth;
        NavigationSplitterColumn.Width = new GridLength(collapsed ? 0 : 5);
        UpdateNavigationSelection();
    }
    private void SearchPanel_Click(object s, RoutedEventArgs e) { ShowSide(SearchPanel); SearchInput.Focus(); }
    private void SnippetPanel_Click(object s, RoutedEventArgs e) => ShowSide(SnippetPanel);
    private void Refresh_Click(object s, RoutedEventArgs e)
    {
        controllerNavigation?.Cancel(); controllerFileIndexes.Clear();
        while (ProjectFileTabs.Items.Count > 1) ProjectFileTabs.Items.RemoveAt(1);
        if (settings.Workspace is { } path) LoadWorkspace(path);
    }
    private async void Explorer_DoubleClick(object s, MouseButtonEventArgs e) { if (Explorer.SelectedItem is TreeViewItem { Tag: string path } && File.Exists(path)) { e.Handled = true; await Guard(() => OpenProjectFileAsync(path)); } }
    private async void Search_Click(object s, RoutedEventArgs e) => await Guard(SearchAsync);
    private void SearchCancel_Click(object s, RoutedEventArgs e) => searchCancellation?.Cancel();
    private async void SearchInput_KeyDown(object s, KeyEventArgs e) { if (e.Key == Key.Enter) await Guard(SearchAsync); }
    private async void SearchResult_DoubleClick(object s, MouseButtonEventArgs e) { if (SearchResults.SelectedItem is SearchHit hit) await Guard(() => OpenFileAsync(hit.Path, hit.Line, hit.Column)); }
    private void Documents_SelectionChanged(object s, SelectionChangedEventArgs e) { if (e.Source == Documents) { UpdateStatus(); if (Problems != null) ValidateActive(); } }
    private void Problem_DoubleClick(object s, MouseButtonEventArgs e) { if (Problems.SelectedItem is ProblemEntry p && tabs.Contains(p.Document)) { FocusDocument(p.Document, p.Diagnostic.Line, p.Diagnostic.Column); e.Handled = true; } }
    private void Outline_DoubleClick(object s, MouseButtonEventArgs e) { if (Outline.SelectedItem is TreeViewItem { Tag: int line } && Active is { } d) { d.ActiveEditor.ScrollTo(line, 1); d.ActiveEditor.TextArea.Caret.Line = Math.Clamp(line, 1, d.Buffer.LineCount); d.ActiveEditor.Focus(); } }
}













