using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using LSCode.Core;
using Microsoft.Win32;

namespace LSCode.Shell;

internal static class CompareTools
{
    private static DataGrid Grid(params (string Header, string Property, double Width)[] columns)
    {
        var grid = new DataGrid { AutoGenerateColumns = false, IsReadOnly = true, CanUserAddRows = false, EnableRowVirtualization = true, EnableColumnVirtualization = true };
        foreach (var c in columns) grid.Columns.Add(new DataGridTextColumn { Header = c.Header, Binding = new Binding(c.Property), Width = c.Width });
        return grid;
    }

    public static Window Text(Window owner, string left = "", string right = "")
    {
        var dock = new DockPanel { Margin = new Thickness(20) };
        var bar = new WrapPanel(); DockPanel.SetDock(bar, Dock.Top); dock.Children.Add(bar);
        var compare = new Button { Content = "So sánh", Style = (Style)Application.Current.FindResource("PrimaryButton") }; var onlyChanges = new CheckBox { Content = "Chỉ khác biệt", Margin = new Thickness(8), VerticalAlignment = VerticalAlignment.Center };
        var mode = new ComboBox { ItemsSource = new[] { "Unified", "Split" }, SelectedIndex = 0, Width = 105, Margin = new Thickness(5) };
        var context = new CheckBox { Content = "Thu gọn dòng không đổi", IsChecked = true, Margin = new Thickness(8), VerticalAlignment = VerticalAlignment.Center };
        var edit = new CheckBox { Content = "Hiện ô nhập", IsChecked = true, Margin = new Thickness(8), VerticalAlignment = VerticalAlignment.Center };
        var previous = new Button { Content = "↑ Thay đổi" }; var next = new Button { Content = "↓ Thay đổi" };
        bar.Children.Add(compare); bar.Children.Add(mode); bar.Children.Add(context); bar.Children.Add(onlyChanges); bar.Children.Add(previous); bar.Children.Add(next); bar.Children.Add(edit);
        var status = new TextBlock { Text = "Dán văn bản hoặc mở file ở hai bên. So sánh theo dòng, bỏ qua CRLF/LF.", TextWrapping = TextWrapping.Wrap };
        DockPanel.SetDock(status, Dock.Bottom); dock.Children.Add(status);
        var body = new System.Windows.Controls.Grid(); body.RowDefinitions.Add(new() { Height = new GridLength(1, GridUnitType.Star) }); body.RowDefinitions.Add(new() { Height = new GridLength(5) }); body.RowDefinitions.Add(new() { Height = new GridLength(1, GridUnitType.Star) }); dock.Children.Add(body);
        var inputs = new System.Windows.Controls.Grid(); inputs.ColumnDefinitions.Add(new()); inputs.ColumnDefinitions.Add(new()); body.Children.Add(inputs);
        var result = Grid();
        result.FontFamily = new FontFamily("Cascadia Mono, Consolas"); result.FontSize = 13;
        result.GridLinesVisibility = DataGridGridLinesVisibility.None; result.HeadersVisibility = DataGridHeadersVisibility.Column;
        result.SelectionUnit = DataGridSelectionUnit.FullRow;
        var style = new Style(typeof(DataGridRow));
        foreach (var (kind, color) in new[] { ("+", Color.FromArgb(55, 50, 190, 100)), ("−", Color.FromArgb(55, 240, 90, 90)) })
        {
            var trigger = new DataTrigger { Binding = new Binding("Kind"), Value = kind };
            trigger.Setters.Add(new Setter(Control.BackgroundProperty, new SolidColorBrush(color))); style.Triggers.Add(trigger);
        }
        result.RowStyle = style;
        System.Windows.Controls.Grid.SetRow(result, 2); body.Children.Add(result);
        var split = new GridSplitter { Height = 5, HorizontalAlignment = HorizontalAlignment.Stretch }; System.Windows.Controls.Grid.SetRow(split, 1); body.Children.Add(split);
        void ShowInputs()
        {
            inputs.Visibility = edit.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
            split.Visibility = inputs.Visibility;
            body.RowDefinitions[0].Height = edit.IsChecked == true ? new GridLength(1, GridUnitType.Star) : new GridLength(0);
            body.RowDefinitions[1].Height = new GridLength(edit.IsChecked == true ? 5 : 0);
            body.RowDefinitions[2].Height = new GridLength(2, GridUnitType.Star);
        }
        edit.Checked += (_, _) => ShowInputs(); edit.Unchecked += (_, _) => ShowInputs(); ShowInputs();
        var window = Dialogs.Create(owner, "Compare Text", dock, 1200, 780);
        bool closed = false; window.Closed += (_, _) => closed = true;
        TextBox Input(int column, string text)
        {
            var panel = new DockPanel { Margin = new Thickness(4) }; System.Windows.Controls.Grid.SetColumn(panel, column); inputs.Children.Add(panel);
            var open = new Button { Content = column == 0 ? "Mở file bên trái…" : "Mở file bên phải…" }; DockPanel.SetDock(open, Dock.Top); panel.Children.Add(open);
            var box = new TextBox { Text = text, AcceptsReturn = true, AcceptsTab = true, FontFamily = new FontFamily("Consolas"), HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            panel.Children.Add(box);
            box.TextChanged += (_, _) => { result.ItemsSource = null; status.Text = "Nội dung đã thay đổi — nhấn So sánh."; };
            open.Click += async (_, _) =>
            {
                var picker = new OpenFileDialog(); if (picker.ShowDialog(window) != true) return;
                open.IsEnabled = false;
                try { var file = await DocumentStore.OpenAsync(picker.FileName); if (!closed) { box.Text = file.Text; open.ToolTip = picker.FileName; } }
                catch (Exception ex) { if (!closed) status.Text = ex.Message; }
                finally { open.IsEnabled = true; }
            };
            return box;
        }
        var a = Input(0, left); var b = Input(1, right);
        IReadOnlyList<DiffRow> rows = [];
        void ResizeColumns()
        {
            var columns = result.Columns.OfType<DataGridTemplateColumn>().ToList();
            if (columns.Count == 0) return;
            var gutter = mode.SelectedIndex == 1 ? 130 : 195;
            var width = Math.Max(200, (result.ActualWidth - gutter - 22) / columns.Count);
            foreach (var column in columns) column.Width = width;
        }
        result.SizeChanged += (_, _) => ResizeColumns();
        void Render()
        {
            var sideBySide = mode.SelectedIndex == 1;
            result.RowStyle = sideBySide ? null : style;
            result.Columns.Clear();
            void Number(string title, string property) => result.Columns.Add(new DataGridTextColumn { Header = title, Binding = new Binding(property), Width = 65 });
            void TextColumn(string title, string property, string other, string kind)
            {
                var factory = new FrameworkElementFactory(typeof(DiffTextBlock));
                factory.SetBinding(DiffTextBlock.ValueProperty, new Binding(property));
                factory.SetBinding(DiffTextBlock.OtherProperty, new Binding(other));
                factory.SetValue(FrameworkElement.MarginProperty, new Thickness(5, 1, 5, 1));
                factory.SetValue(TextBlock.TextWrappingProperty, TextWrapping.Wrap);
                var cell = new Style(typeof(DataGridCell));
                foreach (var (change, color) in new[] { ("+", Color.FromArgb(55, 50, 190, 100)), ("−", Color.FromArgb(55, 240, 90, 90)), ("~", kind == "−" ? Color.FromArgb(55, 240, 90, 90) : Color.FromArgb(55, 50, 190, 100)) })
                {
                    if (sideBySide && change != "~" && change != kind) continue;
                    var trigger = new DataTrigger { Binding = new Binding("Kind"), Value = change };
                    trigger.Setters.Add(new Setter(Control.BackgroundProperty, new SolidColorBrush(color))); cell.Triggers.Add(trigger);
                }
                result.Columns.Add(new DataGridTemplateColumn { Header = title, CellTemplate = new DataTemplate { VisualTree = factory }, ClipboardContentBinding = new Binding(property), CellStyle = cell, Width = new DataGridLength(1, DataGridLengthUnitType.Star), MinWidth = 200 });
            }
            Number("Cũ", "LeftLine");
            if (sideBySide)
            {
                TextColumn("Bản cũ", "Left", "Right", "−"); Number("Mới", "RightLine"); TextColumn("Bản mới", "Right", "Left", "+");
            }
            else
            {
                Number("Mới", "RightLine"); Number("+/−", "Kind"); TextColumn("Nội dung", "Content", "Content", "+");
            }
            var view = DiffPresentation.Create(rows, sideBySide, context.IsChecked == true);
            result.ItemsSource = onlyChanges.IsChecked == true ? view.Where(r => r.Kind is not ("=" or "…")).ToList() : view;
            ResizeColumns();
        }
        mode.SelectionChanged += (_, _) => { if (result.ItemsSource != null) Render(); };
        context.Checked += (_, _) => { if (result.ItemsSource != null) Render(); }; context.Unchecked += (_, _) => { if (result.ItemsSource != null) Render(); };
        void Move(int direction)
        {
            var changes = result.Items.Cast<DiffViewRow>().Select((r, i) => (r, i)).Where(x => x.r.Kind is "+" or "−" or "~").Select(x => x.i).ToList();
            if (changes.Count == 0) return;
            var index = direction > 0 ? changes.FirstOrDefault(i => i > result.SelectedIndex, changes[0]) : changes.LastOrDefault(i => i < result.SelectedIndex, changes[^1]);
            result.SelectedIndex = index; result.ScrollIntoView(result.SelectedItem);
        }
        previous.Click += (_, _) => Move(-1); next.Click += (_, _) => Move(1);
        onlyChanges.Checked += (_, _) => { if (result.ItemsSource != null) Render(); }; onlyChanges.Unchecked += (_, _) => { if (result.ItemsSource != null) Render(); };
        compare.Click += async (_, _) =>
        {
            if (!compare.IsEnabled) return;
            var first = a.Text; var second = b.Text; compare.IsEnabled = false; status.Text = "Đang so sánh…";
            try
            {
                var diff = await Task.Run(() => TextDiff.Compare(first, second));
                if (closed) return;
                if (a.Text != first || b.Text != second) { status.Text = "Nội dung đã thay đổi — nhấn So sánh lại."; return; }
                rows = diff; Render(); edit.IsChecked = false; status.Text = $"{rows.Count(r => r.Kind == "+")} dòng thêm · {rows.Count(r => r.Kind == "−")} dòng xóa · {rows.Count(r => r.Kind == "=")} dòng giống";
            }
            catch (Exception ex) { if (!closed) status.Text = ex.Message; }
            finally { compare.IsEnabled = true; }
        };
        if (left.Length > 0 && right.Length > 0) window.Loaded += (_, _) => compare.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        return window;
    }

    public static Window Folder(Window owner)
    {
        var dock = new DockPanel { Margin = new Thickness(12) };
        var controls = new StackPanel(); DockPanel.SetDock(controls, Dock.Top); dock.Children.Add(controls);
        var status = new TextBlock { Text = "So sánh đệ quy theo nội dung file, gồm file ẩn và thư mục rỗng. Không đi theo liên kết. Nhấp đúp một cặp file để so sánh text.", TextWrapping = TextWrapping.Wrap }; DockPanel.SetDock(status, Dock.Bottom); dock.Children.Add(status);
        var grid = Grid(("Đường dẫn tương đối", "RelativePath", 440), ("Trạng thái", "Status", 160), ("Chi tiết", "Detail", 420)); dock.Children.Add(grid);
        var window = Dialogs.Create(owner, "Compare Folder", dock, 1150, 730);
        TextBox PathInput(string label)
        {
            var row = new DockPanel(); controls.Children.Add(row); var browse = new Button { Content = label + "…" }; DockPanel.SetDock(browse, Dock.Left); row.Children.Add(browse);
            var input = new TextBox { Margin = new Thickness(4) }; row.Children.Add(input);
            browse.Click += (_, _) => { var picker = new OpenFolderDialog(); if (picker.ShowDialog(window) == true) input.Text = picker.FolderName; };
            input.TextChanged += (_, _) => { grid.ItemsSource = null; status.Text = "Đường dẫn đã đổi — nhấn So sánh."; };
            return input;
        }
        var a = PathInput("Thư mục trái"); var b = PathInput("Thư mục phải");
        var bar = new WrapPanel(); controls.Children.Add(bar); var compare = new Button { Content = "So sánh" }; var cancel = new Button { Content = "Dừng", IsEnabled = false }; var changes = new CheckBox { Content = "Chỉ khác biệt", Margin = new Thickness(8) };
        bar.Children.Add(compare); bar.Children.Add(cancel); bar.Children.Add(changes);
        CancellationTokenSource? operation = null; bool closed = false;
        window.Closed += (_, _) => { closed = true; operation?.Cancel(); };
        cancel.Click += (_, _) => operation?.Cancel();
        IReadOnlyList<FolderDiffRow> rows = [];
        void Render() => grid.ItemsSource = changes.IsChecked == true ? rows.Where(r => r.Status is not ("Giống nhau" or "Thư mục")).ToList() : rows;
        changes.Checked += (_, _) => { if (grid.ItemsSource != null) Render(); }; changes.Unchecked += (_, _) => { if (grid.ItemsSource != null) Render(); };
        compare.Click += async (_, _) =>
        {
            using var cts = new CancellationTokenSource(); operation = cts;
            var leftPath = a.Text.Trim(); var rightPath = b.Text.Trim(); controls.IsEnabled = true; compare.IsEnabled = false; cancel.IsEnabled = true; grid.ItemsSource = null; status.Text = "Đang so sánh…";
            try
            {
                var found = await Task.Run(() => FolderDiff.CompareAsync(leftPath, rightPath, cts.Token), cts.Token);
                if (closed) return;
                if (leftPath != a.Text.Trim() || rightPath != b.Text.Trim()) { status.Text = "Đường dẫn đã đổi — nhấn So sánh lại."; return; }
                rows = found; Render(); status.Text = $"{rows.Count} mục · {rows.Count(r => r.Status == "Khác nội dung")} file khác · {rows.Count(r => r.Status == "Lỗi")} lỗi · {rows.Count(r => r.Status == "Bỏ qua liên kết")} liên kết bỏ qua";
            }
            catch (OperationCanceledException) { if (!closed) status.Text = "Đã dừng so sánh."; }
            catch (Exception ex) { if (!closed) status.Text = ex.Message; }
            finally { operation = null; compare.IsEnabled = true; cancel.IsEnabled = false; }
        };
        grid.MouseDoubleClick += async (_, _) =>
        {
            if (grid.SelectedItem is not FolderDiffRow row || row.Status is not ("Giống nhau" or "Khác nội dung")) return;
            try
            {
                var leftFile = await DocumentStore.OpenAsync(row.LeftPath!); var rightFile = await DocumentStore.OpenAsync(row.RightPath!);
                if (!closed) Text(window, leftFile.Text, rightFile.Text).Show();
            }
            catch (Exception ex) { if (!closed) status.Text = "Không mở được dưới dạng text: " + ex.Message; }
        };
        return window;
    }
}
