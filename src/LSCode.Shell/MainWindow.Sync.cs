using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Xml.Linq;
using LSCode.Core;
using LSCode.Shell.Services;

namespace LSCode.Shell;

public partial class MainWindow
{
    private SqlTarget? projectSqlTarget;
    private SqlTarget? configuredSqlTarget;
    private List<CommandNode> commandNodes = [];
    private CancellationTokenSource? syncCancellation, commandCancellation;
    private SqlTarget CurrentSqlTarget() => configuredSqlTarget ?? appSqlTarget ?? new(settings.Server, settings.Database, SqlRunner.ConnectionString(settings, sqlPassword));

    private async Task SyncProjectAsync()
    {
        if (!projectCatalogAvailable) throw new IOException("Catalog hiện không đọc được; cần khôi phục trước khi đồng bộ.");
        if (syncCancellation != null) { Log("Đang đồng bộ dự án. Có thể nhấn Dừng trong panel Chức năng."); return; }
        var name = Dialogs.ProjectName(this);
        if (string.IsNullOrWhiteSpace(name)) return;
        using var cancellation = new CancellationTokenSource(); syncCancellation = cancellation;
        try
        {
            CommandStatus.Text = "Đang tìm " + name.Trim() + "…";
            var found = await Task.Run(() => ProjectDiscovery.Find(@"\\172.168.5.14\CustomerPro", name, cancellation.Token), cancellation.Token).WaitAsync(cancellation.Token);
            if (found.Count == 0) throw new IOException("Không tìm thấy web.config trong một cấp ứng dụng của dự án " + name.Trim());
            DiscoveredProject? selected = found.Count == 1 ? found[0] : null;
            if (selected == null)
            {
                var choice = Dialogs.Palette(this, found.Select(p => new IdeCommand(p.Folder, p.ToString(), "", () => { selected = p; return Task.CompletedTask; })), "Chọn ứng dụng cần đồng bộ");
                if (choice == null) return; await choice.Execute();
            }
            var connections = await Task.Run(() => ProjectDiscovery.ReadConnections(selected!.ConfigPath), cancellation.Token).WaitAsync(cancellation.Token);
            var target = ProjectDatabase.Target(connections.Sys);
            CommandStatus.Text = "Đang đọc cây chức năng từ " + target.Database + "…";
            // Fetch before committing: failed connections leave the current catalog/workspace intact.
            var resolved = await ProjectDatabase.ResolveTargetsAsync([new("appConnectionString", connections.App, ""), new("sysConnectionString", connections.Sys, "")], async (probe, candidates) =>
            {
                var data = await ProjectDatabase.ReadAsync(probe, "SELECT name FROM sys.databases WHERE name IN (@a,@b) AND HAS_DBACCESS(name)=1", cancellation.Token, new Microsoft.Data.SqlClient.SqlParameter("@a", candidates[0]), new Microsoft.Data.SqlClient.SqlParameter("@b", candidates[1]));
                return data.Rows.Cast<System.Data.DataRow>().Select(r => Convert.ToString(r[0])!).ToList();
            });
            var nodes = await ProjectDatabase.CommandsAsync(target, cancellation.Token, resolved[0].Database);
            cancellation.Token.ThrowIfCancellationRequested();
            CommitSynchronizedProject(selected!, connections, target, nodes);
        }
        finally { syncCancellation = null; if (CommandStatus.Text.StartsWith("Đang")) CommandStatus.Text = "Đồng bộ đã kết thúc. Xem Output nếu có lỗi."; }
    }
    private void CommitSynchronizedProject(DiscoveredProject selected, (string App, string Sys) connections, SqlTarget target, List<CommandNode> nodes)
    {
            var profile = ProjectImporter.Create(selected.Name + " / " + Path.GetFileName(selected.Folder), target.Server, target.Database, selected.Folder);
            var xml = XmlTools.Parse(profile.RawXml).Root!;
            var existing = projects.FirstOrDefault(p => p.Id == profile.Id);
            if (existing != null && existing.Field("AppConnectionString") == connections.App && existing.Field("AppDatabase").Length > 0) xml.Add(new XElement("AppDatabase", existing.Field("AppDatabase")));
            xml.Add(new XElement("SysConnectionString", connections.Sys), new XElement("AppConnectionString", connections.App), new XElement("WebConfigPath", selected.ConfigPath));
            profile = profile with { RawXml = xml.ToString(SaveOptions.DisableFormatting) };
            var merged = ProjectImporter.Merge(projects, [profile], true);
            ProjectStore.Save(merged.Projects); projects = merged.Projects;
            ActivateProject(profile, false); RenderCommands(nodes);
            Log("Đã đồng bộ " + profile.Name + ". Kết nối được lưu trong catalog DPAPI; web.config và DB không bị sửa.");
    }
    private async Task RefreshCommandsAsync()
    {
        commandCancellation?.Cancel();
        using var current = new CancellationTokenSource(); commandCancellation = current;
        CommandTree.Items.Clear(); menuItems.Clear();
        commandNodes = [];
        try
        {
            if (projectSqlTarget == null) { CommandStatus.Text = "Đồng bộ dự án để tải cây chức năng."; return; }
            CommandStatus.Text = "Đang tải chức năng…";
            var nodes = await ProjectDatabase.CommandsAsync(projectSqlTarget, current.Token, (await DefaultAppTargetAsync()).Database);
            current.Token.ThrowIfCancellationRequested(); RenderCommands(nodes);
        }
        catch (OperationCanceledException) { if (commandCancellation == current) CommandStatus.Text = "Đã hủy tải chức năng."; }
        catch (Exception ex) { if (commandCancellation == current) { CommandStatus.Text = "Không tải được cây chức năng. Nhấn Tải lại."; Log("Cây chức năng: " + ex.Message); } }
        finally { if (commandCancellation == current) commandCancellation = null; }
    }
    private void RenderCommands(IEnumerable<CommandNode> nodes)
    {
        commandNodes = nodes.ToList(); ApplyCommandFilter();
    }
    private void ApplyCommandFilter()
    {
        var nodes = commandNodes;
        CommandTree.Items.Clear(); menuItems.Clear(); lastMenuSearch = ""; menuMatchIndex = -1;
        TreeViewItem Build(CommandNode node, List<TreeViewItem> parents)
        {
            if (node.Entry.Caption.Trim() == "-")
            {
                var line = new Border { Height = 1, MinWidth = 160, Margin = new Thickness(0, 6, 0, 6) };
                line.SetResourceReference(Border.BackgroundProperty, "Border");
                var separator = new TreeViewItem { Header = line, Focusable = false, IsHitTestVisible = false, HorizontalContentAlignment = HorizontalAlignment.Stretch };
                foreach (var child in node.Children) separator.Items.Add(Build(child, parents));
                return separator;
            }
            var header = new TextBlock { Text = node.Entry.Caption + " (" + node.Entry.Id + ")", TextTrimming = TextTrimming.None };
            if (parents.Count == 0)
            {
                header.FontWeight = FontWeights.Bold;
                header.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(82, 169, 255));
            }
            var item = new TreeViewItem { Header = header, Tag = node.Entry, ToolTip = "SysId: " + node.Entry.SysId + "\nCommand: " + node.Entry.CommandId + "\n" + node.Entry.Link };
            menuItems.Add((item, parents, node.Entry));
            foreach (var child in node.Children) item.Items.Add(Build(child, [..parents, item]));
            return item;
        }
        foreach (var node in nodes) CommandTree.Items.Add(Build(node, []));
        CommandStatus.Text = $"{CommandTree.Items.Count} nhóm • wcommand / command";
    }
    private async void CommandTree_DoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (e.ClickCount != 2) return;
        var source = e.OriginalSource as DependencyObject;
        while (source != null && source is not TreeViewItem)
        {
            // The arrow already toggles on a single click; leave its native behavior intact.
            if (source is System.Windows.Controls.Primitives.ToggleButton) return;
            source = source is System.Windows.Media.Visual or System.Windows.Media.Media3D.Visual3D ? System.Windows.Media.VisualTreeHelper.GetParent(source) : LogicalTreeHelper.GetParent(source);
        }
        if (source is not TreeViewItem clicked) return;
        clicked.IsSelected = true;
        if (clicked.HasItems)
        {
            e.Handled = true;
            clicked.IsExpanded = !clicked.IsExpanded;
            return;
        }
        if (clicked.Tag is not CommandEntry entry || string.IsNullOrEmpty(settings.Workspace)) return;
        e.Handled = true;
        if (string.IsNullOrWhiteSpace(entry.Link)) return;
        await Guard(() => OpenControllerFilesAsync(settings.Workspace!, entry));
    }
    private async Task OpenTableBrowserAsync()
    {
        var project = Active is { Language: ".sql" } sql ? sql.Project : projects.FirstOrDefault(p => p.Id == settings.ActiveProjectId);
        var targets = project == null ? null : await ProjectDatabase.ConfiguredTargetsAsync(project);
        if (Active is { Language: ".sql" } doc) { await doc.TargetReady; new TableBrowser(this, doc.SqlTarget ?? throw new InvalidOperationException("Chưa chọn DB SQL."), targets).Show(); }
        else new TableBrowser(this, await DefaultAppTargetAsync(), targets).Show();
    }
    public async Task RunProjectSmokeAsync()
    {
        var report = new List<string>(); TableBrowser? browser = null;
        try
        {
            Directory.CreateDirectory(SettingsStore.Root);
            var path = Environment.GetEnvironmentVariable("LSCODE_TEST_WEB_CONFIG") ?? throw new InvalidOperationException("Missing test web.config path");
            var folder = Path.GetDirectoryName(path)!;
            var name = Directory.GetParent(folder)!.Name;
            using var discoveryTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            var found = await Task.Run(() => ProjectDiscovery.Find(@"\\172.168.5.14\CustomerPro", name, discoveryTimeout.Token)).WaitAsync(discoveryTimeout.Token);
            var discovered = found.FirstOrDefault(p => string.Equals(p.ConfigPath, path, StringComparison.OrdinalIgnoreCase)) ?? throw new InvalidOperationException("Application not found through project discovery");
            report.Add("PASS live CustomerPro project/application discovery");
            var connections = await Task.Run(() => ProjectDiscovery.ReadConnections(path));
            var target = ProjectDatabase.Target(connections.Sys);
            report.Add("PASS read named connections from real web.config (credentials omitted)");
            var nodes = await ProjectDatabase.CommandsAsync(target, CancellationToken.None);
            if (nodes.Count == 0) throw new InvalidOperationException("No command roots");
            CommitSynchronizedProject(discovered, connections, target, nodes);
            if (ProjectStore.Load().Count != 1 || Explorer.Items.Count == 0) throw new InvalidOperationException("Catalog or project files missing");
            report.Add("PASS live sys DB wcommand/command hierarchy + encrypted catalog + project file explorer");
            var sql = AddDocument("project-query.sql", "SELECT DB_NAME() AS database_name;");
            await sql.TargetReady;
            if (sql.SqlTarget == null || sql.SqlTarget.Database == target.Database) throw new InvalidOperationException("New query did not default to App");
            report.Add("PASS new SQL tab defaults to inferred App database");
            await ExecuteAsync(); if (Results.Items.Count == 0) throw new Exception("Direct SQL execution produced no result");
            report.Add("PASS direct SELECT execution without confirmation");
            sql.SqlTarget = target;
            var symbols = await SqlSchemaCompletion.LoadAsync(target);
            if (!symbols.Any(s => s.Table.Equals("freecolumns", StringComparison.OrdinalIgnoreCase) && s.Column == "controller")) throw new InvalidOperationException("Live schema suggestions missing");
            report.Add("PASS SQL metadata suggestions for tables and columns");
            await sql.SmokeCompletionAsync(); report.Add("PASS SQL completion popup and prefix replacement");
            ShowProjectPicker(true); report.Add("PASS saved project picker / recent list render");
            var appName = target.Database.EndsWith("_S", StringComparison.OrdinalIgnoreCase) ? target.Database[..^2] + "_A" : "";
            if (appName.Length > 0)
            {
                var xml = XmlTools.Parse(sql.Project!.RawXml).Root!; xml.SetElementValue("AppDatabase", appName);
                var fixture = sql.Project with { RawXml = xml.ToString(SaveOptions.DisableFormatting) };
                projects = ProjectImporter.Merge(projects, [fixture], true).Projects; sql.Project = fixture;
                var app = await ResolveAppTargetAsync(sql, false);
                if (app?.Database != appName || projectSqlTarget != target) throw new InvalidOperationException("App target/menu SYS isolation failed");
                report.Add("PASS live App target resolution while menu stays on SYS");
            }
            TableBrowser.BuildQuery("dbo", "freecolumns", "*", "controller LIKE 'GLDetail'", "");
            foreach (var invalid in new[] { "1=1; DELETE FROM dbo.freecolumns", "1=1 UNION SELECT * FROM dbo.freecolumns" })
            {
                try { TableBrowser.BuildQuery("dbo", "freecolumns", "*", invalid, ""); throw new Exception("Unsafe query accepted"); }
                catch (InvalidOperationException) { }
            }
            report.Add("PASS table browser single SELECT validation");
            browser = new TableBrowser(this, target); browser.Show();
            await browser.SmokeAsync(Path.Combine(SettingsStore.Root, "table-browser.png"));
            report.Add("PASS live table schema + bounded data query + WPF render");
            browser.Close(); browser = null;
            await OpenControllerFilesAsync(folder, new CommandEntry("test", "", "GLTran", "glctpk1.aspx", ""));
            if (!ProjectFileTabs.Items.OfType<TabItem>().Any(t => Convert.ToString(t.Tag)?.EndsWith("|GLTran") == true)) throw new InvalidOperationException("Controller file tab missing");
            report.Add("PASS ASPX ReportExtender controller file tab");
            ToggleBottomPanel(); if (BottomPanel.Visibility != Visibility.Collapsed || BottomRow.Height.Value > 40) throw new Exception("Panel collapse failed"); ToggleBottomPanel(); if (BottomPanel.Visibility != Visibility.Visible) throw new Exception("Panel restore failed");
            report.Add("PASS bottom results panel collapse / restore");
            var descendant = menuItems.First(n => n.Parents.Count > 0); CommandSearch.Text = descendant.Entry.Id; NavigateMenuMatch();
            if (!descendant.Item.IsSelected || descendant.Parents.Any(p => !p.IsExpanded)) throw new Exception("Menu search navigation failed");
            report.Add("PASS Enter menu navigation expands ancestors and selects result");
            UpdateLayout();
            var bitmap = new System.Windows.Media.Imaging.RenderTargetBitmap((int)ActualWidth, (int)ActualHeight, 96, 96, System.Windows.Media.PixelFormats.Pbgra32); bitmap.Render(this);
            var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder(); encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));
            using (var stream = File.Create(Path.Combine(SettingsStore.Root, "project-layout.png"))) encoder.Save(stream);
            report.Add("PASS three-pane project layout render");
        }
        catch (Exception ex) { report.Add("FAIL " + ex.GetType().Name + ": " + ex.Message); }
        finally { browser?.Close(); }
        await File.WriteAllLinesAsync(Path.Combine(SettingsStore.Root, "project-smoke-result.txt"), report);
        closing = true; Close();
    }
    private async void SyncProject_Click(object sender, RoutedEventArgs e) => await Guard(SyncProjectAsync);
    private async void ReloadCommands_Click(object sender, RoutedEventArgs e) => await RefreshCommandsAsync();
    private void CancelSync_Click(object sender, RoutedEventArgs e) { syncCancellation?.Cancel(); commandCancellation?.Cancel(); }
    private async void TableBrowser_Click(object sender, RoutedEventArgs e) => await Guard(OpenTableBrowserAsync);
}


