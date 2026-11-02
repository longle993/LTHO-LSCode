using System.Windows;
using System.Windows.Controls;
using LSCode.Shell.Services;
namespace LSCode.Shell;
public partial class MainWindow
{
    private ProfilerWindow? profilerWindow;
    private TabItem? profilerTab;
    private async Task OpenProfilerAsync()
    {
        if (profilerTab != null) { Documents.SelectedItem = profilerTab; Welcome.Visibility = Visibility.Collapsed; return; }
        var project = projects.FirstOrDefault(p => p.Id == settings.ActiveProjectId);
        SqlTarget target;
        if (project != null && !string.IsNullOrWhiteSpace(project.Field("SysConnectionString")))
        {
            var context = new EditorTab("profiler.sql", "") { Project = project };
            try { target = await ResolveAppTargetAsync(context, false) ?? throw new InvalidOperationException("Chưa chọn database cho Profiler."); }
            finally { context.Dispose(); }
        }
        else target = await DefaultAppTargetAsync();
        if (closing || profilerWindow != null) return;
        AttachProfiler(ProfilerStorage.Connection(target, ProfilerStorage.DefaultCredentials));
    }
    private void AttachProfiler(SqlTarget target, bool preview = false)
    {
        profilerWindow = new ProfilerWindow(this, target, preview);
        profilerTab = new TabItem { Content = profilerWindow.Embed(), ToolTip = target.Server + " / " + target.Database };
        var header = new StackPanel { Orientation = Orientation.Horizontal };
        header.Children.Add(new TextBlock { Text = "Profiler · " + target.Database, VerticalAlignment = VerticalAlignment.Center });
        var close = new Button { Content = "×", Padding = new Thickness(5, 0, 5, 0), Margin = new Thickness(12, 0, 0, 0) };
        close.Click += async (_, _) => await Guard(CloseProfilerTabAsync);
        header.Children.Add(close); profilerTab.Header = header;
        Documents.Items.Add(profilerTab); Documents.SelectedItem = profilerTab; Welcome.Visibility = Visibility.Collapsed;
    }
    private async Task CloseProfilerTabAsync()
    {
        var current = profilerWindow; if (current == null) return;
        await current.StopAsync();
        if (current.HasPendingSession) throw new InvalidOperationException("Chưa dọn được phiên Profiler. Kiểm tra kết nối và nhấn Dừng để thử lại.");
        if (profilerWindow != current) return;
        if (profilerTab != null) { profilerTab.Content = null; Documents.Items.Remove(profilerTab); }
        profilerTab = null; profilerWindow = null; current.CloseStopped();
        Welcome.Visibility = Documents.Items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }
}
