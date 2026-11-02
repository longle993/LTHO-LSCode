using System.Data;
using System.Windows;
using System.Windows.Controls;
using System.Xml.Linq;
using LSCode.Core;
using LSCode.Shell.Services;
using Microsoft.Data.SqlClient;

namespace LSCode.Shell;
public partial class MainWindow
{
    private void AddSqlToolbar(EditorTab doc, bool explicitTarget = false)
    {
        doc.Project = projects.FirstOrDefault(p => p.Id == settings.ActiveProjectId);
        doc.CompletionLoader = async () => { await doc.TargetReady; return await SqlSchemaCompletion.LoadAsync(doc.SqlTarget ?? throw new InvalidOperationException("Chưa chọn database cho tab SQL."), doc.Buffer.Text); };
        doc.CompletionError += Log;
        var dock = new DockPanel(); var bar = new WrapPanel { Margin = new Thickness(8) };
        bool hasProject = !string.IsNullOrWhiteSpace(doc.Project?.Field("SysConnectionString"));
        var choose = new ComboBox { Width = 90, ItemsSource = new[] { "SYS", "App" }, SelectedIndex = hasProject ? 1 : -1, Visibility = hasProject ? Visibility.Visible : Visibility.Collapsed, ToolTip = "Chọn DB cho riêng tab SQL này" };
        var label = new TextBlock { Text = doc.SqlTarget == null || string.IsNullOrWhiteSpace(doc.SqlTarget.Database) ? "Chưa chọn kết nối" : doc.SqlTarget.Server + " / " + doc.SqlTarget.Database, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 8, 0), MaxWidth = 260, TextTrimming = TextTrimming.CharacterEllipsis, ToolTip = "Kết nối của riêng tab SQL này" };
        doc.TargetLabel = label;
        var changeApp = new Button { Content = "Chọn App DB…", ToolTip = "Đổi database App đã lưu cho dự án" };
        var refresh = new Button { Content = "↻ Gợi ý SQL", ToolTip = "Xóa cache metadata; Ctrl+Space để tải lại" };
        refresh.Click += (_, _) => { SqlSchemaCompletion.Clear(); foreach (var tab in tabs) tab.InvalidateCompletionCache(); Log("Đã xóa cache gợi ý SQL. Nhấn Ctrl+Space trong editor."); };
        bool updating = false; int selected = choose.SelectedIndex;
        async Task Switch(bool forceApp)
        {
            choose.IsEnabled = false; changeApp.IsEnabled = false;
            try
            {
                if (doc.Project is not { } project || string.IsNullOrWhiteSpace(project.Field("SysConnectionString"))) throw new InvalidOperationException("Chọn dự án đã đồng bộ để dùng SYS/App.");
                var target = forceApp || choose.SelectedIndex == 1 ? (forceApp || doc.Project?.Id != settings.ActiveProjectId ? await ResolveAppTargetAsync(doc, forceApp) : await DefaultAppTargetAsync()) : (await ProjectDatabase.ConfiguredTargetsAsync(project))[1];
                if (target == null) { updating = true; choose.SelectedIndex = selected; updating = false; return; }
                doc.SqlTarget = target; if (forceApp && doc.Project?.Id == settings.ActiveProjectId) { appSqlTarget = target; objectCancellation?.Cancel(); DbObjects.Items.Clear(); } label.Text = target.Server + " / " + target.Database;
                selected = forceApp ? 1 : choose.SelectedIndex; updating = true; choose.SelectedIndex = selected; updating = false; UpdateStatus();
            }
            catch (Exception ex) { updating = true; choose.SelectedIndex = selected; updating = false; Log(ex.Message); Dialogs.Notify(this, ex.Message, "SYS / App"); }
            finally { choose.IsEnabled = true; changeApp.IsEnabled = true; }
        }
        choose.SelectionChanged += async (_, _) => { if (!updating) { doc.TargetReady = Switch(false); await doc.TargetReady; } };
        changeApp.Click += async (_, _) => { doc.TargetReady = Switch(true); await doc.TargetReady; };
        var run = new Button { Content = "▷  Chạy · F5", Style = (Style)FindResource("PrimaryButton") };
        run.Click += async (_, _) => { Documents.SelectedItem = doc.Tab; await Guard(ExecuteAsync); };
        var cancel = new Button { Content = "Dừng" }; cancel.Click += (_, _) => sqlCancellation?.Cancel();
        var more = new Button { Content = "⋯", ToolTip = "Database, gợi ý và định dạng", Style = (Style)FindResource("IconButton") };
        more.Click += (_, _) => {
            var menu = new ContextMenu();
            var connection = new MenuItem { Header = "Kết nối…" }; connection.Click += (_, _) => { Documents.SelectedItem = doc.Tab; ConfigureConnection(); }; menu.Items.Add(connection);
            var app = new MenuItem { Header = "Chọn App DB…" }; app.Click += async (_, _) => { doc.TargetReady = Switch(true); await doc.TargetReady; }; menu.Items.Add(app);
            var hints = new MenuItem { Header = "Tải lại gợi ý SQL" }; hints.Click += (_, _) => refresh.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); menu.Items.Add(hints);
            var format = new MenuItem { Header = "Định dạng SQL" }; format.Click += async (_, _) => { Documents.SelectedItem = doc.Tab; await Guard(FormatAsync); }; menu.Items.Add(format);
            menu.PlacementTarget = more; menu.IsOpen = true;
        };
        bar.Children.Add(choose); bar.Children.Add(label); bar.Children.Add(run); bar.Children.Add(cancel); bar.Children.Add(more);
        doc.Tab.Content = null;
        DockPanel.SetDock(bar, Dock.Top); dock.Children.Add(bar); dock.Children.Add(doc.Layout); doc.Tab.Content = dock;
        if (hasProject && !explicitTarget) { doc.SqlTarget = null; label.Text = "Đang chọn App DB…"; doc.TargetReady = Switch(false); }
    }
    private async Task<SqlTarget?> ResolveAppTargetAsync(EditorTab doc, bool chooseAgain)
    {
        var project = projects.FirstOrDefault(p => p.Id == doc.Project!.Id) ?? doc.Project!;
        var available = await ProjectDatabase.ConfiguredTargetsAsync(project);
        if (available.Count == 0) throw new InvalidOperationException("connectionStrings không có database cụ thể; hãy cập nhật web.config.");
        var target = available[0];
        var database = target.Database;
        using (var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20))) await ProjectDatabase.ReadAsync(target, "SELECT DB_NAME() AS database_name", timeout.Token);
        if (project.Field("AppDatabase") != database)
        {
            var xml = XmlTools.Parse(project.RawXml).Root!; xml.SetElementValue("AppDatabase", database);
            var updated = project with { RawXml = xml.ToString(SaveOptions.DisableFormatting) };
            var merged = ProjectImporter.Merge(projects, [updated], true); ProjectStore.Save(merged.Projects); projects = merged.Projects; doc.Project = updated;
        }
        return target;
    }
}






