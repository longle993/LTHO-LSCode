using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using LSCode.Core;

namespace LSCode.Shell;

public partial class MainWindow
{
    private Window? includePreview;
    private Task GoToIncludeAsync() => ShowIncludeAsync(false);
    private Task PreviewIncludeAsync() => ShowIncludeAsync(true);
    private async Task ShowIncludeAsync(bool preview)
    {
        if (Active is not { } doc || doc.Language is not (".xml" or ".ent" or ".dtd" or ".config" or ".f" or ".xaml")) return;
        if (doc.Path == null) { Log("Lưu file trước khi tìm include tương đối."); return; }
        var buffers = tabs.Where(t => t.Path != null).ToDictionary(t => t.Path!, t => t.Buffer.Text, StringComparer.OrdinalIgnoreCase);
        var text = doc.Buffer.Text;
        var offset = doc.ActiveEditor.SelectionLength > 0 ? doc.ActiveEditor.SelectionStart : doc.ActiveEditor.CaretOffset;
        var location = await Task.Run(() => XmlIncludes.Find(doc.Path, text, offset,
            path => buffers.TryGetValue(path, out var buffer) ? buffer : File.Exists(path) ? DocumentStore.OpenAsync(path).GetAwaiter().GetResult().Text : null));
        if (location == null) { Log("Không tìm thấy khai báo include tại vị trí đang chọn."); return; }
        if (preview)
        {
            var content = buffers.TryGetValue(location.Path, out var buffer) ? buffer : (await DocumentStore.OpenAsync(location.Path)).Text;
            includePreview?.Close();
            var panel = new DockPanel { Margin = new Thickness(8) };
            var header = new TextBlock { Text = location.Path, TextTrimming = TextTrimming.CharacterEllipsis, ToolTip = location.Path, Margin = new Thickness(4) };
            DockPanel.SetDock(header, Dock.Top); panel.Children.Add(header);
            var open = new Button { Content = "Mở file include · Ctrl+F12", HorizontalAlignment = HorizontalAlignment.Right };
            DockPanel.SetDock(open, Dock.Bottom); panel.Children.Add(open);
            var document = new EditorTab(Path.GetFileName(location.Path), content) { Path = location.Path };
            document.Primary.IsReadOnly = true;
            document.Tab.Content = null;
            panel.Children.Add(document.Layout);
            var window = Dialogs.Create(this, "Xem nhanh include", panel, 760, 430);
            window.ShowInTaskbar = false; includePreview = window;
            async Task Open() { window.Close(); await OpenFileAsync(location.Path, location.Line, location.Column); }
            open.Click += async (_, _) => await Guard(Open);
            window.PreviewKeyDown += async (_, e) =>
            {
                if (e.Key == Key.Escape) { e.Handled = true; window.Close(); }
                else if (e.Key == Key.F12 && Keyboard.Modifiers == ModifierKeys.Control) { e.Handled = true; await Guard(Open); }
            };
            window.Closed += (_, _) => { document.Dispose(); if (includePreview == window) includePreview = null; };
            window.Loaded += (_, _) => { document.Primary.ScrollTo(location.Line, location.Column); document.Primary.Focus(); };
            window.Show();
            return;
        }
        includePreview?.Close();
        await OpenFileAsync(location.Path, location.Line, location.Column);
        Log($"Include: {location.Path} · dòng {location.Line}");
    }
}
