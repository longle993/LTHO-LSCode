using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace LSCode.Shell;
public partial class MainWindow
{
    private TreeViewItem FileNode(string path)
    {
        var node = new TreeViewItem { Header = CreateTreeHeader(Path.GetFileName(path), false), Tag = path, ToolTip = path };
        node.PreviewMouseRightButtonDown += (_, _) => { node.IsSelected = true; node.Focus(); };
        node.PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.C && Keyboard.Modifiers == ModifierKeys.Control)
            {
                e.Handled = true;
                try { Clipboard.SetText(Path.GetFileNameWithoutExtension(path)); } catch (Exception ex) { Log(ex.Message); }
            }
        };
        var menu = new ContextMenu(); node.ContextMenu = menu;
        void Action(string label, Func<Task> action)
        {
            var item = new MenuItem { Header = label };
            item.Click += async (_, _) => await Guard(action); menu.Items.Add(item);
        }
        Action("Mở thư mục chứa file", () => { Process.Start(new ProcessStartInfo("explorer.exe", "/select,\"" + path + "\"") { UseShellExecute = true }); return Task.CompletedTask; });
        Action("Xóa file…", async () =>
        {
            if (Dialogs.Notify(this, "Xóa file?\n" + path, "Xóa file", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
            var doc = tabs.FirstOrDefault(t => string.Equals(t.Path, path, StringComparison.OrdinalIgnoreCase));
            if (doc != null && !await CloseDocumentAsync(doc)) return;
            Microsoft.VisualBasic.FileIO.FileSystem.DeleteFile(path, Microsoft.VisualBasic.FileIO.UIOption.OnlyErrorDialogs, Microsoft.VisualBasic.FileIO.RecycleOption.SendToRecycleBin, Microsoft.VisualBasic.FileIO.UICancelOption.ThrowException);
            RefreshFileNodes(path, null); controllerFileIndexes.Clear();
        });
        Action("Clone file…", async () =>
        {
            var name = Dialogs.Text(this, "Tên file clone (bao gồm phần mở rộng)", Path.GetFileNameWithoutExtension(path) + "_copy" + Path.GetExtension(path));
            if (name == null) return;
            name = name.Trim();
            if (name.Length == 0 || name is "." or ".." || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || name.EndsWith('.')) throw new ArgumentException("Tên file không hợp lệ.");
            var clone = Path.Combine(Path.GetDirectoryName(path)!, name);
            File.Copy(path, clone, false);
            RefreshFileNodes(path, clone); controllerFileIndexes.Clear();
            await OpenFileAsync(clone);
        });
        Action("Sao chép đường dẫn", () => { Clipboard.SetText(path); return Task.CompletedTask; });
        Action("Sao chép tên file (không phần mở rộng) · Ctrl+C", () => { Clipboard.SetText(Path.GetFileNameWithoutExtension(path)); return Task.CompletedTask; });
        return node;
    }
    private void RefreshFileNodes(string path, string? clone)
    {
        void Visit(DependencyObject parent)
        {
            if (parent is ItemsControl items)
                foreach (var child in items.Items.OfType<TreeViewItem>().ToList())
                    if (child.Tag is string p && string.Equals(p, path, StringComparison.OrdinalIgnoreCase))
                    {
                        if (clone == null) items.Items.Remove(child);
                        else if (!items.Items.OfType<TreeViewItem>().Any(n => Equals(n.Tag, clone))) items.Items.Insert(items.Items.IndexOf(child) + 1, FileNode(clone));
                    }
            foreach (var child in LogicalTreeHelper.GetChildren(parent).OfType<DependencyObject>().ToList()) Visit(child);
        }
        Visit(Explorer);
        foreach (var tab in ProjectFileTabs.Items.OfType<TabItem>()) if (tab.Content is DependencyObject content && content != Explorer) Visit(content);
    }
}
