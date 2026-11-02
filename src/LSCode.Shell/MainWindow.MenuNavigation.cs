using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using LSCode.Core;

namespace LSCode.Shell;
public partial class MainWindow
{
    private GridLength lastBottomHeight = new(210);
    private void ToggleBottomPanel()
    {
        bool show = BottomPanel.Visibility != Visibility.Visible;
        if (!show && BottomRow.Height.GridUnitType == GridUnitType.Pixel && BottomRow.Height.Value > 80) lastBottomHeight = BottomRow.Height;
        BottomPanel.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
        BottomSplitter.Visibility = Visibility.Visible;
        BottomRow.MinHeight = show ? 120 : 36;
        BottomRow.Height = show ? lastBottomHeight : new GridLength(36);
        BottomSplitterRow.Height = new GridLength(6);
        BottomToggleIcon.Text = show ? "▼" : "▲";
        BottomToggle.ToolTip = show ? "Ẩn kết quả" : "Hiện kết quả";
    }
    private string lastMenuSearch = "";
    private int menuMatchIndex = -1;
    private readonly List<(TreeViewItem Item, List<TreeViewItem> Parents, CommandEntry Entry)> menuItems = [];
    private void CommandSearch_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return; e.Handled = true;
        NavigateMenuMatch();
    }
    private void NavigateMenuMatch()
    {
        var term = CommandSearch.Text.Trim(); if (term.Length == 0) return;
        var matches = menuItems.Where(n => new[] { n.Entry.Caption, n.Entry.SysId, n.Entry.Id, n.Entry.CommandId }.Any(v => v.Contains(term, StringComparison.OrdinalIgnoreCase))).ToList();
        if (matches.Count == 0) { CommandStatus.Text = "Không tìm thấy: " + term; return; }
        menuMatchIndex = term.Equals(lastMenuSearch, StringComparison.OrdinalIgnoreCase) ? (menuMatchIndex + 1) % matches.Count : 0; lastMenuSearch = term;
        var match = matches[menuMatchIndex]; foreach (var parent in match.Parents) parent.IsExpanded = true;
        match.Item.IsSelected = true; CommandTree.UpdateLayout(); match.Item.BringIntoView();
        CommandStatus.Text = $"Kết quả {menuMatchIndex + 1}/{matches.Count} • {match.Entry.Id}";
    }
    private readonly Dictionary<string, List<string>> controllerFileIndexes = new(StringComparer.OrdinalIgnoreCase);
    private CancellationTokenSource? controllerNavigation;
    private async Task OpenControllerFilesAsync(string root, CommandEntry entry)
    {
        controllerNavigation?.Cancel(); using var current = new CancellationTokenSource(TimeSpan.FromSeconds(45)); controllerNavigation = current;
        try
        {
            var file = await Task.Run(() => ProjectConventions.ResolveMenuFile(root, entry.Link), current.Token).WaitAsync(current.Token);
            if (file == null) { CommandStatus.Text = "Không tìm thấy file: " + entry.Link; Log(CommandStatus.Text); return; }

            CommandStatus.Text = "Đang tìm file controller…";
            var controllers = ProjectConventions.Controllers(await File.ReadAllTextAsync(file, current.Token));
            if (controllers.Count == 0) controllers = new[] { Path.GetFileNameWithoutExtension(file) };
            if (!controllerFileIndexes.TryGetValue(root, out var files))
            {
                files = await Task.Run(() =>
                {
                    var found = new List<string>(); var pending = new Stack<string>(); pending.Push(Path.Combine(root, "App_Data"));
                    while (pending.Count > 0)
                    {
                        current.Token.ThrowIfCancellationRequested(); var folder = pending.Pop(); if (!Directory.Exists(folder)) continue;
                        found.AddRange(Directory.EnumerateFiles(folder));
                        foreach (var sub in Directory.EnumerateDirectories(folder)) if ((File.GetAttributes(sub) & FileAttributes.ReparsePoint) == 0) pending.Push(sub);
                        if (found.Count > 100000) throw new IOException("Project vượt 100000 file; cần thu hẹp thư mục.");
                    }
                    return found;
                }, current.Token).WaitAsync(current.Token);
                controllerFileIndexes[root] = files;
            }
            current.Token.ThrowIfCancellationRequested();
            foreach (var controller in controllers)
            {
                var key = root + "|" + controller;
                var existing = ProjectFileTabs.Items.OfType<TabItem>().FirstOrDefault(t => Equals(t.Tag, key));
                if (existing != null) { ProjectFileTabs.SelectedItem = existing; ShowSide(ExplorerPanel); continue; }
                var tree = new TreeView(); var tab = new TabItem { Tag = key, ToolTip = file };
                var header = new DockPanel { LastChildFill = true };
                var close = new Button { Content = "×", Padding = new Thickness(4, 0, 4, 0), ToolTip = "Đóng" }; close.Click += (_, _) => ProjectFileTabs.Items.Remove(tab); DockPanel.SetDock(close, Dock.Right); header.Children.Add(close); header.Children.Add(new TextBlock { Text = controller, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis, ToolTip = controller }); tab.Header = header;
                tab.Content = ScrollableTree(tree);
                tree.MouseDoubleClick += async (_, e) => { if (tree.SelectedItem is TreeViewItem { Tag: string path } && File.Exists(path)) { e.Handled = true; await Guard(() => OpenProjectFileAsync(path)); } };
                var related = await Task.Run(() =>
                {
                    var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { controller }; var queue = new Queue<string>(); queue.Enqueue(controller);
                    while (queue.Count > 0)
                    {
                        current.Token.ThrowIfCancellationRequested(); var name = queue.Dequeue();
                        foreach (var xml in files.Where(p => Path.GetExtension(p).Equals(".xml", StringComparison.OrdinalIgnoreCase) && Path.GetFileNameWithoutExtension(p).Equals(name, StringComparison.OrdinalIgnoreCase)))
                        {
                            if (new FileInfo(xml).Length > 4_000_000) continue;
                            foreach (var child in ProjectConventions.GridControllers(File.ReadAllText(xml))) if (names.Count < 64 && names.Add(child)) queue.Enqueue(child);
                        }
                    }
                    return files.Where(p => names.Any(n => Path.GetFileNameWithoutExtension(p).StartsWith(n, StringComparison.OrdinalIgnoreCase))).Append(file).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                }, current.Token).WaitAsync(current.Token);
                current.Token.ThrowIfCancellationRequested();
                foreach (var path in related.OrderBy(p => Path.GetRelativePath(root, p), StringComparer.OrdinalIgnoreCase)) AddControllerNode(tree, root, path);
                tab.ToolTip = file + "\n" + related.Count + " file";
                ProjectFileTabs.Items.Add(tab); ProjectFileTabs.SelectedItem = tab;
                CommandStatus.Text = controller + " · " + related.Count + " file";
            }
            ShowSide(ExplorerPanel);
        }
        catch (OperationCanceledException) { }
        finally { if (controllerNavigation == current) controllerNavigation = null; }
    }
    private void AddControllerNode(ItemsControl parent, string root, string path)
    {
        var parts = Path.GetRelativePath(root, path).Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var current = parent;
        for (var i = 0; i < parts.Length; i++)
        {
            var name = parts[i]; var leaf = i == parts.Length - 1;
            var folderPath = Path.Combine(root, Path.Combine(parts.Take(i + 1).ToArray()));
            var match = current.Items.OfType<TreeViewItem>().FirstOrDefault(item => string.Equals(Convert.ToString(item.Tag), leaf ? path : folderPath, StringComparison.OrdinalIgnoreCase));
            if (match == null)
            {
                match = leaf ? FileNode(path) : new TreeViewItem { Header = CreateTreeHeader(name, true), IsExpanded = true };
                if (leaf) { match.Tag = path; match.ToolTip = path; current.Items.Add(match); }
                else
                {
                    match.Tag = folderPath;
                    match.ToolTip = string.Join(Path.DirectorySeparatorChar, parts.Take(i + 1));
                    current.Items.Add(match);
                }
            }
            current = match;
        }
    }
    private static UIElement ScrollableTree(TreeView tree)
    {
        tree.ItemsPanel = new ItemsPanelTemplate(new FrameworkElementFactory(typeof(StackPanel)));
        var itemStyle = new Style(typeof(TreeViewItem), (Style)Application.Current.FindResource(typeof(TreeViewItem)));
        itemStyle.Setters.Add(new Setter(ItemsControl.ItemsPanelProperty, new ItemsPanelTemplate(new FrameworkElementFactory(typeof(StackPanel)))));
        itemStyle.Setters.Add(new Setter(Control.HorizontalContentAlignmentProperty, HorizontalAlignment.Left));
        tree.ItemContainerStyle = itemStyle;
        tree.Template = new ControlTemplate(typeof(TreeView)) { VisualTree = new FrameworkElementFactory(typeof(ItemsPresenter)) };
        tree.HorizontalAlignment = HorizontalAlignment.Left;
        tree.SetResourceReference(Control.BackgroundProperty, "Panel");
        tree.SetResourceReference(Control.ForegroundProperty, "Text");
        var scroll = new ScrollViewer { Content = tree, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        scroll.SetResourceReference(Control.BackgroundProperty, "Panel");
        return scroll;
    }
}




