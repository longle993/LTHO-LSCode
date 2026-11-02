using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using LSCode.Shell.Services;

namespace LSCode.Shell;

public partial class MainWindow
{
    private Task CompareTextAsync()
    {
        CompareTools.Text(this, Active is { } doc ? (doc.ActiveEditor.SelectionLength > 0 ? doc.ActiveEditor.SelectedText : doc.Buffer.Text) : "").Show();
        return Task.CompletedTask;
    }
    private Task CompareFolderAsync() { CompareTools.Folder(this).Show(); return Task.CompletedTask; }
    private Task BeautifySqlAsync()
    {
        var doc = Active;
        var original = doc?.Buffer.Text;
        var offset = doc?.ActiveEditor.SelectionStart ?? 0;
        var length = doc?.ActiveEditor.SelectionLength ?? 0;
        if (length == 0) { offset = 0; length = original?.Length ?? 0; }
        var dock = new DockPanel { Margin = new Thickness(20) };
        var bar = new WrapPanel(); DockPanel.SetDock(bar, Dock.Top); dock.Children.Add(bar);
        var format = new Button { Content = "Beautify SQL", Style = (Style)FindResource("PrimaryButton") }; var copy = new Button { Content = "Sao chép kết quả", IsEnabled = false }; var apply = new Button { Content = "Áp dụng vào tab gốc", IsEnabled = false, Visibility = doc == null ? Visibility.Collapsed : Visibility.Visible };
        bar.Children.Add(format); bar.Children.Add(copy); bar.Children.Add(apply);
        var status = new TextBlock { Text = "Dán SQL vào ô trái. Từ khóa viết hoa, thụt lề 4 khoảng trắng. Chỉ định dạng, không chạy SQL.", TextWrapping = TextWrapping.Wrap }; DockPanel.SetDock(status, Dock.Bottom); dock.Children.Add(status);
        var body = new Grid(); body.ColumnDefinitions.Add(new()); body.ColumnDefinitions.Add(new() { Width = new GridLength(5) }); body.ColumnDefinitions.Add(new()); dock.Children.Add(body);
        TextBox Editor(bool readOnly) => new() { AcceptsReturn = true, AcceptsTab = true, IsReadOnly = readOnly, FontFamily = new FontFamily("Consolas"), HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        var input = Editor(false); input.Text = original?.Substring(offset, length) ?? ""; var output = Editor(true); body.Children.Add(input); Grid.SetColumn(output, 2); body.Children.Add(output);
        var splitter = new GridSplitter { Width = 5, HorizontalAlignment = HorizontalAlignment.Stretch }; Grid.SetColumn(splitter, 1); body.Children.Add(splitter);
        var window = Dialogs.Create(this, "Beautify SQL", dock, 1150, 730); bool closed = false; window.Closed += (_, _) => closed = true;
        input.TextChanged += (_, _) => { output.Clear(); copy.IsEnabled = apply.IsEnabled = false; };
        format.Click += async (_, _) =>
        {
            var sql = input.Text; format.IsEnabled = false; copy.IsEnabled = apply.IsEnabled = false; output.Clear(); status.Text = "Đang định dạng…";
            try
            {
                var formatted = await Task.Run(() => SqlLanguageService.Format(sql));
                if (closed) return;
                if (input.Text != sql) { status.Text = "SQL đã thay đổi — nhấn Beautify SQL lại."; return; }
                output.Text = formatted; copy.IsEnabled = apply.IsEnabled = true; status.Text = "Đã định dạng. Áp dụng vào tab gốc có thể hoàn tác bằng Ctrl+Z.";
            }
            catch (Exception ex) { if (!closed) status.Text = ex.Message; }
            finally { format.IsEnabled = true; }
        };
        copy.Click += (_, _) => { try { Clipboard.SetText(output.Text); status.Text = "Đã sao chép kết quả."; } catch (Exception ex) { status.Text = ex.Message; } };
        apply.Click += (_, _) =>
        {
            if (doc == null || !tabs.Contains(doc) || doc.Buffer.Text != original) { status.Text = "Tab gốc đã thay đổi hoặc đóng. Hãy sao chép kết quả hoặc mở lại công cụ."; return; }
            doc.Buffer.Replace(offset, length, output.Text); window.Close();
        };
        window.Show(); return Task.CompletedTask;
    }
    private async void CompareText_Click(object sender, RoutedEventArgs e) => await Guard(CompareTextAsync);
    private async void CompareFolder_Click(object sender, RoutedEventArgs e) => await Guard(CompareFolderAsync);
    private async void BeautifySql_Click(object sender, RoutedEventArgs e) => await Guard(BeautifySqlAsync);

    private async Task CheckCompareToolsAsync(List<string> report)
    {
        static IEnumerable<T> Find<T>(DependencyObject root) where T : DependencyObject
        {
            if (root is T value) yield return value;
            foreach (var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>())
                foreach (var found in Find<T>(child)) yield return found;
        }
        static Button FindButton(Window window, string label) => Find<Button>(window).Single(b => Equals(b.Content, label));
        static async Task Wait(Button button)
        {
            for (var i = 0; !button.IsEnabled && i < 100; i++) await Task.Delay(50);
            if (!button.IsEnabled) throw new Exception("Tool operation timed out");
        }
        var text = CompareTools.Text(this, "same\nold", "same\nnew");
        try
        {
            text.Show(); var run = FindButton(text, "So sánh"); run.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); await Wait(run);
            var rows = Find<DataGrid>(text).Single().Items.Cast<LSCode.Core.DiffViewRow>().ToList();
            if (rows.Count(r => r.Kind != "=") != 2) throw new Exception("Compare Text rows");
            Find<ComboBox>(text).Single().SelectedIndex = 1;
            var paired = Find<DataGrid>(text).Single().Items.Cast<LSCode.Core.DiffViewRow>().ToList();
            if (paired.Count != 2 || paired[1].Kind != "~") throw new Exception("Split diff replacement alignment");
            FindButton(text, "↓ Thay đổi").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            if (Find<DataGrid>(text).Single().SelectedIndex != 1) throw new Exception("Diff change navigation");
            text.UpdateLayout();
            var bitmap = new System.Windows.Media.Imaging.RenderTargetBitmap((int)text.ActualWidth, (int)text.ActualHeight, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(text); var png = new System.Windows.Media.Imaging.PngBitmapEncoder(); png.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));
            using (var stream = System.IO.File.Create(System.IO.Path.Combine(SettingsStore.Root, "smoke-diff.png"))) png.Save(stream);
        }
        finally { text.Close(); }
        var includeFile = System.IO.Path.Combine(SettingsStore.Root, "preview-part.xml");
        await System.IO.File.WriteAllTextAsync(includeFile, "<part>Include preview</part>");
        var includeDoc = AddDocument("preview-root.xml", "<!DOCTYPE root [<!ENTITY Part SYSTEM 'preview-part.xml'>]><root>&Part;</root>");
        includeDoc.Path = System.IO.Path.Combine(SettingsStore.Root, "preview-root.xml");
        includeDoc.Primary.CaretOffset = includeDoc.Buffer.Text.IndexOf("&Part;") + 2;
        var tabCount = tabs.Count;
        try
        {
            await PreviewIncludeAsync();
            if (includePreview == null || !includePreview.IsVisible || tabs.Count != tabCount) throw new Exception("F12 must only open preview");
            var previewEditor = Find<ICSharpCode.AvalonEdit.TextEditor>(includePreview).Single();
            if (!previewEditor.IsReadOnly || !previewEditor.Text.Contains("Include preview")) throw new Exception("Include preview content");
            includePreview.UpdateLayout();
            var image = new System.Windows.Media.Imaging.RenderTargetBitmap((int)includePreview.ActualWidth, (int)includePreview.ActualHeight, 96, 96, PixelFormats.Pbgra32);
            image.Render(includePreview); var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder(); encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(image));
            using (var stream = System.IO.File.Create(System.IO.Path.Combine(SettingsStore.Root, "smoke-include.png"))) encoder.Save(stream);
            includePreview.Close();
            await GoToIncludeAsync();
            if (Active?.Path != includeFile || tabs.Count != tabCount + 1) throw new Exception("Ctrl+F12 must open include tab");
            var savedNodes = commandNodes.ToList();
            RenderCommands([new LSCode.Core.CommandNode(new("separator", "", "-", "", "")), new LSCode.Core.CommandNode(new("normal", "", "Normal", "", ""))]);
            if (CommandTree.Items[0] is not TreeViewItem { Header: Border, Focusable: false } || menuItems.Count != 1) throw new Exception("Menu separator must not be searchable");
            RenderCommands(savedNodes);
            report.Add("PASS F12 read-only preview, Ctrl+F12 opens include tab, menu bar separator");
        }
        finally
        {
            includePreview?.Close();
            foreach (var opened in tabs.Where(t => t == includeDoc || t.Path == includeFile).ToList()) { tabs.Remove(opened); Documents.Items.Remove(opened.Tab); opened.Dispose(); }
        }
        var folder = CompareTools.Folder(this);
        try { folder.Show(); folder.UpdateLayout(); if (Find<TextBox>(folder).Count() != 2 || Find<DataGrid>(folder).Count() != 1) throw new Exception("Compare Folder controls"); }
        finally { folder.Close(); }
        var doc = AddDocument("beautify-fixture.txt", "prefix\nselect 1 as value;\nsuffix");
        doc.ActiveEditor.Select(7, "select 1 as value;".Length);
        Window? beauty = null;
        try
        {
            await BeautifySqlAsync(); beauty = OwnedWindows.Cast<Window>().Single(w => w.Title == "Beautify SQL");
            var run = FindButton(beauty, "Beautify SQL"); run.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); await Wait(run);
            var apply = FindButton(beauty, "Áp dụng vào tab gốc"); if (!apply.IsEnabled) throw new Exception("Beautify failed");
            apply.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            if (!doc.Buffer.Text.StartsWith("prefix\nSELECT") || !doc.Buffer.Text.EndsWith("\nsuffix")) throw new Exception("Beautify selection range");
            doc.Buffer.UndoStack.Undo(); if (doc.Buffer.Text != "prefix\nselect 1 as value;\nsuffix") throw new Exception("Beautify undo");
            bool rejected = false; try { SqlLanguageService.Format("select from ;"); } catch (InvalidOperationException) { rejected = true; }
            if (!rejected) throw new Exception("Invalid SQL not rejected");
        }
        finally { beauty?.Close(); tabs.Remove(doc); Documents.Items.Remove(doc.Tab); doc.Dispose(); }
        report.Add("PASS Compare Text diff UI, Folder controls, Beautify selection/undo and invalid SQL rejection");
    }
}
