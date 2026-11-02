using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Collections.Specialized;

namespace LSCode.Shell;
public partial class MainWindow
{
    private void MinimizeWindow_Click(object sender, RoutedEventArgs e) => SystemCommands.MinimizeWindow(this);
    private void MaximizeWindow_Click(object sender, RoutedEventArgs e)
    {
        if (WindowState == WindowState.Maximized) SystemCommands.RestoreWindow(this); else SystemCommands.MaximizeWindow(this);
    }
    private void CloseWindow_Click(object sender, RoutedEventArgs e) => Close();
    private readonly List<TabItem> controllerHistory = [];
    private static string ControllerTitle(TabItem tab) => tab.Header is Panel p ? p.Children.OfType<TextBlock>().FirstOrDefault()?.Text ?? "" : Convert.ToString(tab.Header) ?? "";
    private void InitializeControllerHistory()
    {
        ((INotifyCollectionChanged)ProjectFileTabs.Items).CollectionChanged += (_, _) => RefreshControllerHistory();
        StateChanged += (_, _) => { MaximizeWindowButton.Content = WindowState == WindowState.Maximized ? "❐" : "□"; MaximizeWindowButton.ToolTip = WindowState == WindowState.Maximized ? "Khôi phục" : "Phóng to"; };
        RefreshControllerHistory();
    }
    private void RefreshControllerHistory()
    {
        if (RecentControllers == null) return;
        controllerHistory.RemoveAll(t => !ProjectFileTabs.Items.Contains(t));
        if (ProjectFileTabs.SelectedItem is TabItem active) { controllerHistory.Remove(active); controllerHistory.Insert(0, active); }
        foreach (var tab in ProjectFileTabs.Items.OfType<TabItem>()) if (!controllerHistory.Contains(tab)) controllerHistory.Add(tab);
        RecentControllers.Children.Clear();
        foreach (var tab in controllerHistory.Where(t => t != ProjectFileTabs.SelectedItem).Take(2))
        {
            var button = new Button { Content = new TextBlock { Text = ControllerTitle(tab), TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center }, ToolTip = ControllerTitle(tab), Padding = new Thickness(5, 6, 5, 6), MinWidth = 0, MinHeight = 32 };
            button.Click += (_, _) => ProjectFileTabs.SelectedItem = tab; RecentControllers.Children.Add(button);
        }
        RecentControllers.Visibility = RecentControllers.Children.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
    }
    private void AllControllers_Click(object sender, RoutedEventArgs e)
    {
        var menu = new ContextMenu();
        foreach (var tab in controllerHistory)
        {
            var item = new MenuItem { Header = new TextBlock { Text = (tab == ProjectFileTabs.SelectedItem ? "✓ " : "") + ControllerTitle(tab) }, ToolTip = tab.ToolTip };
            item.Click += (_, _) => ProjectFileTabs.SelectedItem = tab; menu.Items.Add(item);
        }
        menu.PlacementTarget = (Button)sender; menu.IsOpen = true;
    }
    internal static string DistinctFileLabel(string path, IEnumerable<string> otherPaths)
    {
        var parts = path.Replace('\\', '/').Split('/'); var others = otherPaths.Select(p => p.Replace('\\', '/')).Where(p => !p.Equals(path.Replace('\\', '/'), StringComparison.OrdinalIgnoreCase)).ToList();
        if (!others.Any(p => Path.GetFileName(p).Equals(parts[^1], StringComparison.OrdinalIgnoreCase))) return parts[^1];
        for (var count = 2; count <= parts.Length; count++)
        {
            var suffix = string.Join("/", parts.TakeLast(count));
            if (!others.Any(p => p.Equals(suffix, StringComparison.OrdinalIgnoreCase) || p.EndsWith("/" + suffix, StringComparison.OrdinalIgnoreCase))) return suffix;
        }
        return path;
    }
    private async Task OpenProjectFileAsync(string path)
    {
        await OpenFileAsync(path);
        var doc = tabs.FirstOrDefault(t => string.Equals(t.Path, Path.GetFullPath(path), StringComparison.OrdinalIgnoreCase));
        if (doc != null) doc.ShowParentFolder = true;
        RefreshDocumentHeaders();
    }
    private static string DocumentLabel(EditorTab doc, IEnumerable<string> paths)
    {
        var label = DistinctFileLabel(doc.Path!, paths);
        if (doc.ShowParentFolder && label == doc.Name) label = new DirectoryInfo(Path.GetDirectoryName(doc.Path!)!).Name + "/" + label;
        return label;
    }
    private void RefreshDocumentHeaders()
    {
        var paths = tabs.Where(t => t.Path != null).Select(t => t.Path!).ToList();
        foreach (var doc in tabs)
        {
            if (doc.Tab.Header is StackPanel header && header.Children.OfType<TextBlock>().FirstOrDefault() is { } label)
                label.Text = (doc.Dirty ? "● " : "") + (doc.Path == null ? doc.Name : DocumentLabel(doc, paths));
            doc.Tab.ToolTip = doc.Path ?? "Chưa lưu";
        }
    }
}

