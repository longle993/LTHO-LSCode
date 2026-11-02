using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using LSCode.Core;
using LSCode.Shell.Services;
using Microsoft.Data.SqlClient;

namespace LSCode.Shell;
public partial class MainWindow
{
    private sealed record ProjectRow(LegacyProject Project, string Name, string Sys, string App, string Path, string LastAccess);
    private static string AppDatabaseName(LegacyProject p)
    {
        if (p.Field("AppDatabase").Length > 0) return p.Field("AppDatabase");
        try { var db = new SqlConnectionStringBuilder(p.Field("AppConnectionString")).InitialCatalog; return db.Contains('%') || db.Length == 0 ? "Chưa chọn" : db; }
        catch { return "Chưa chọn"; }
    }
    private void ShowProjectPicker(bool smoke = false)
    {
        var dock = new DockPanel { Margin = new Thickness(24) };
        var top = new StackPanel(); DockPanel.SetDock(top, Dock.Top); dock.Children.Add(top);
        top.Children.Add(new TextBlock { Text = "Dự án của bạn", FontSize = 24, FontWeight = FontWeights.SemiBold, Margin = new Thickness(4, 0, 4, 16) });
        top.Children.Add(new TextBlock { Text = "Tìm dự án / Sys DB / App DB / đường dẫn", Margin = new Thickness(4) });
        var search = new TextBox { ToolTip = "Tìm dự án / Sys DB / App DB / đường dẫn" }; top.Children.Add(search);
        var buttons = new WrapPanel { HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 16, 0, 0) }; DockPanel.SetDock(buttons, Dock.Bottom); dock.Children.Add(buttons);
        var connect = new Button { Content = "Kết nối", IsDefault = true, MinHeight = 40, Style = (Style)FindResource("PrimaryButton") };
        var sync = new Button { Content = "+ Đồng bộ dự án" }; var refresh = new Button { Content = "Làm mới" }; var delete = new Button { Content = "Xóa khỏi danh sách" }; var close = new Button { Content = "Đóng", IsCancel = true };
        foreach (var button in new[] { sync, refresh, delete, close, connect }) buttons.Children.Add(button);
        var recent = new WrapPanel { Margin = new Thickness(3, 8, 3, 8) }; top.Children.Add(recent);
        var status = new TextBlock { Text = "Chọn dự án rồi Enter hoặc nhấp đúp để kết nối. Menu luôn dùng DB SYS.", Margin = new Thickness(5), TextWrapping = TextWrapping.Wrap }; DockPanel.SetDock(status, Dock.Bottom); dock.Children.Add(status);
        var grid = new DataGrid { AutoGenerateColumns = false, IsReadOnly = true, SelectionMode = DataGridSelectionMode.Single, CanUserAddRows = false };
        foreach (var (header, path, width) in new[] { ("Dự án", "Name", 210d), ("Sys Data", "Sys", 180d), ("App Data", "App", 180d), ("Program Path", "Path", 320d), ("Truy cập gần nhất", "LastAccess", 150d) })
            grid.Columns.Add(ResultGrids.TextColumn(header, path, width));
        dock.Children.Add(grid);
        var layers = new Grid(); layers.Children.Add(dock);
        var window = Dialogs.Create(this, "LS Code — Chọn dự án để kết nối", layers, 1150, 650);
        using var lifetime = new CancellationTokenSource(); window.Closed += (_, _) => lifetime.Cancel();
        LegacyProject? chosen = null;
        void Reload()
        {
            var term = search.Text.Trim();
            var rows = projects.OrderByDescending(p => settings.ProjectLastAccess.GetValueOrDefault(p.Id)).ThenBy(p => p.Name)
                .Select(p => new ProjectRow(p, p.Name, p.Database, AppDatabaseName(p), p.SourcePath, settings.ProjectLastAccess.TryGetValue(p.Id, out var date) ? date.ToLocalTime().ToString("dd/MM/yyyy HH:mm") : ""))
                .Where(r => new[] { r.Name, r.Sys, r.App, r.Path }.Any(v => v.Contains(term, StringComparison.OrdinalIgnoreCase))).ToList();
            grid.ItemsSource = rows; if (rows.Count > 0) grid.SelectedIndex = 0;
            recent.Children.Clear();
            foreach (var row in rows.Where(r => r.LastAccess.Length > 0).Take(8))
            {
                var button = new Button { Content = row.Name, ToolTip = row.Sys }; button.Click += async (_, _) => await Connect(row); recent.Children.Add(button);
            }
            if (projects.Count == 0) status.Text = "Chưa có dự án đã lưu. Chọn + Đồng bộ dự án để bắt đầu.";
        }
        bool busy = false;
        async Task Connect(ProjectRow row)
        {
            if (busy) return; busy = true; connect.IsEnabled = sync.IsEnabled = delete.IsEnabled = false; status.Text = "Đang kết nối " + row.Name + "…";
            using var loading = ShowLoadingOverlay(layers, "Đang kết nối " + row.Name + "…");
            try
            {
                if (!string.IsNullOrWhiteSpace(row.Project.Field("SysConnectionString"))) await ProjectDatabase.ReadAsync(ProjectDatabase.Target(row.Project.Field("SysConnectionString")), "SELECT DB_NAME() AS database_name", lifetime.Token);
                lifetime.Token.ThrowIfCancellationRequested(); chosen = row.Project; window.DialogResult = true;
            }
            catch (OperationCanceledException) { }
            catch (Exception ex) { status.Text = "Không kết nối được: " + ex.Message; }
            finally { busy = false; connect.IsEnabled = sync.IsEnabled = delete.IsEnabled = true; }
        }
        connect.Click += async (_, _) => { if (grid.SelectedItem is ProjectRow row) await Connect(row); };
        grid.MouseDoubleClick += async (_, _) => { if (grid.SelectedItem is ProjectRow row) await Connect(row); };
        search.TextChanged += (_, _) => Reload();
        sync.Click += async (_, _) => { sync.IsEnabled = false; try { await Guard(SyncProjectAsync); Reload(); } finally { sync.IsEnabled = true; } };
        refresh.Click += (_, _) => { try { projects = ProjectStore.Load(); Reload(); } catch (Exception ex) { status.Text = ex.Message; } };
        delete.Click += (_, _) =>
        {
            if (grid.SelectedItem is not ProjectRow row || !projectCatalogAvailable) return;
            if (Dialogs.Notify(window, "Xóa " + row.Name + " khỏi catalog? File và database được giữ nguyên.", "Projects", MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;
            try { var remaining = projects.Where(p => p.Id != row.Project.Id).ToList(); ProjectStore.Save(remaining); projects = remaining; settings.ProjectLastAccess.Remove(row.Project.Id); SettingsStore.Save(settings); Reload(); }
            catch (Exception ex) { status.Text = ex.Message; }
        };
        Exception? renderError = null;
        Reload(); window.Loaded += (_, _) =>
        {
            search.Focus();
            if (!smoke) return;
            try
            {
                if (grid.Items.Count == 0) throw new InvalidOperationException("Saved project list empty");
                window.UpdateLayout();
                var bitmap = new System.Windows.Media.Imaging.RenderTargetBitmap((int)window.ActualWidth, (int)window.ActualHeight, 96, 96, System.Windows.Media.PixelFormats.Pbgra32); bitmap.Render(window);
                var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder(); encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));
                using var stream = System.IO.File.Create(System.IO.Path.Combine(SettingsStore.Root, "project-picker.png")); encoder.Save(stream);
            }
            catch (Exception ex) { renderError = ex; }
            finally { window.DialogResult = false; }
        };
        window.ShowDialog(); if (renderError != null) throw renderError;
        if (chosen != null) ActivateProject(chosen);
    }
}


