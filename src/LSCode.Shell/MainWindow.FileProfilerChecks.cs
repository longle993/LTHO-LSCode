using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using LSCode.Shell.Services;

namespace LSCode.Shell;
public partial class MainWindow
{
    private async Task CheckFileActionsAndEmbeddedProfilerAsync(List<string> report)
    {
        var path = Path.Combine(SettingsStore.Root, "file-actions.test.xml");
        await File.WriteAllTextAsync(path, "<fixture/>");
        var root = new TreeViewItem { Header = "File actions fixture" }; Explorer.Items.Add(root);
        var node = FileNode(path); root.Items.Add(node);
        if (node.ContextMenu?.Items.Count != 5) throw new Exception("File context actions missing");
        var clone = Path.Combine(SettingsStore.Root, "file-actions-copy.xml");
        File.Copy(path, clone, false); RefreshFileNodes(path, clone);
        if (root.Items.Count != 2 || root.Items[1] is not TreeViewItem { ContextMenu.Items.Count: 5 }) throw new Exception("Cloned file node is not actionable");
        RefreshFileNodes(path, null);
        if (root.Items.Count != 1 || !Equals(((TreeViewItem)root.Items[0]).Tag, clone)) throw new Exception("Deleted file remains in tree");
        Explorer.Items.Remove(root);

        var count = Documents.Items.Count;
        AttachProfiler(new SqlTarget("fixture-server", "DEMO_App", ""), preview: true);
        try
        {
            UpdateLayout();
            if (profilerWindow?.IsVisible != false || profilerTab?.Content is not Border || Documents.SelectedItem != profilerTab || Documents.Items.Count != count + 1) throw new Exception("Profiler is not embedded in document tab");
            var instance = profilerWindow;
            if (tabs.FirstOrDefault() is { } editor) Documents.SelectedItem = editor.Tab;
            await OpenProfilerAsync(); UpdateLayout();
            if (profilerWindow != instance || Documents.SelectedItem != profilerTab || Documents.Items.Count != count + 1) throw new Exception("Profiler activation created a duplicate session");
            var bitmap = new RenderTargetBitmap((int)ActualWidth, (int)ActualHeight, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(this); var png = new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(bitmap));
            using var output = File.Create(Path.Combine(SettingsStore.Root, "smoke-profiler-tab.png")); png.Save(output);
        }
        finally { await CloseProfilerTabAsync(); }
        if (Documents.Items.Count != count || profilerWindow != null) throw new Exception("Profiler tab close cleanup");
        report.Add("PASS five file actions, clone/delete tree refresh, embedded Profiler reuses tab and closes cleanly");
    }
}
