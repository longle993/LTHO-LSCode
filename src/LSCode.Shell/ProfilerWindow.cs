using System.IO;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Shell;
using LSCode.Shell.Services;

namespace LSCode.Shell;
public sealed partial class ProfilerWindow : Window
{
    private readonly SqlTarget target;
    public bool HasPendingSession => pendingCleanup != null;
    private readonly ObservableCollection<ProfilerEvent> events = [];
    private readonly TextBlock status = new() { Margin = new Thickness(8), TextWrapping = TextWrapping.Wrap };
    private readonly Button start = IconButton("\uE768", "Chạy"), stop = IconButton("\uE71A", "Dừng");
    private readonly TextBlock filterLabel = new() { Margin = new Thickness(10, 2, 10, 8) };
    private readonly DataGrid grid;
    private readonly Grid body = new();
    private readonly GridSplitter splitter = new();
    private readonly Button layoutToggle = IconButton("\uE8A0", "Chuyển sang bố cục trái / phải");
    private bool sideBySide;
    private static Button IconButton(string glyph, string label)
    {
        var button = new Button { Content = glyph, FontFamily = new System.Windows.Media.FontFamily("Segoe MDL2 Assets"), FontSize = 16, Width = 34, Height = 32, Padding = new Thickness(0), ToolTip = label };
        System.Windows.Automation.AutomationProperties.SetName(button, label);
        return button;
    }
    private void ApplyLayout()
    {
        body.RowDefinitions.Clear(); body.ColumnDefinitions.Clear();
        foreach (UIElement child in body.Children) { Grid.SetRow(child, 0); Grid.SetColumn(child, 0); }
        if (sideBySide)
        {
            body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star) });
            body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(5) });
            body.ColumnDefinitions.Add(new ColumnDefinition());
            Grid.SetColumn(splitter, 1); Grid.SetColumn(detail, 2);
        }
        else
        {
            body.RowDefinitions.Add(new RowDefinition { Height = new GridLength(2, GridUnitType.Star) });
            body.RowDefinitions.Add(new RowDefinition { Height = new GridLength(5) });
            body.RowDefinitions.Add(new RowDefinition());
            Grid.SetRow(splitter, 1); Grid.SetRow(detail, 2);
        }
        splitter.Width = sideBySide ? 5 : double.NaN; splitter.Height = sideBySide ? double.NaN : 5;
        splitter.HorizontalAlignment = HorizontalAlignment.Stretch; splitter.VerticalAlignment = VerticalAlignment.Stretch;
        splitter.ResizeDirection = sideBySide ? GridResizeDirection.Columns : GridResizeDirection.Rows;
        splitter.ResizeBehavior = GridResizeBehavior.PreviousAndNext;
        layoutToggle.Content = sideBySide ? "\uE8A1" : "\uE8A0";
        var label = sideBySide ? "Chuyển sang bố cục trên / dưới" : "Chuyển sang bố cục trái / phải";
        layoutToggle.ToolTip = label; System.Windows.Automation.AutomationProperties.SetName(layoutToggle, label);
    }
    private readonly EditorTab detailDocument = new("trace-detail.sql", "");
    private ICSharpCode.AvalonEdit.TextEditor detail => detailDocument.Primary;
    private readonly ProfilerTemplate template;
    private CancellationTokenSource? cancellation;
    private Task? running;
    private SqlTraceSession? pendingCleanup;
    private bool allowClose, closingWindow;
    private readonly bool previewMode;
    internal UIElement Embed()
    {
        var frame = (Border)Content;
        var dock = (DockPanel)frame.Child;
        dock.Children.RemoveAt(0); // Standalone window chrome is replaced by the document tab header.
        Content = null;
        var started = false;
        frame.Loaded += (_, _) => { if (!started) { started = true; if (!previewMode) Start(); } };
        return frame;
    }
    private long lastSequence = -1, received;
    public ProfilerWindow(Window owner, SqlTarget target, bool preview = false)
    {
        previewMode = preview;
        stop.IsEnabled = false;
        this.target = target; template = ProfilerTemplate.Default(target.Database);
        SetResourceReference(BackgroundProperty, "Surface"); SetResourceReference(ForegroundProperty, "Text");
        FontFamily = new System.Windows.Media.FontFamily("Segoe UI"); FontSize = 14; UseLayoutRounding = true;
        Owner = owner; Title = "LS Code — Profiler"; Width = Math.Min(1250, SystemParameters.WorkArea.Width - 24); Height = Math.Min(780, SystemParameters.WorkArea.Height - 24);
        MinWidth = 650; MinHeight = 400; WindowStartupLocation = WindowStartupLocation.CenterOwner; WindowStyle = WindowStyle.None;
        WindowChrome.SetWindowChrome(this, new WindowChrome { CaptionHeight = 40, ResizeBorderThickness = new Thickness(5), GlassFrameThickness = new Thickness(0), UseAeroCaptionButtons = false });
        var dock = new DockPanel();
        var frame = new Border { BorderThickness = new Thickness(1), Child = dock };
        frame.SetResourceReference(Border.BorderBrushProperty, "Border");
        frame.SetResourceReference(Border.BackgroundProperty, "Surface");
        Content = frame;
        var title = new DockPanel { Height = 40 }; title.SetResourceReference(BackgroundProperty, "Panel"); DockPanel.SetDock(title, Dock.Top); dock.Children.Add(title);
        var close = new Button { Content = "×", Width = 34 }; close.Click += (_, _) => Close(); DockPanel.SetDock(close, Dock.Right); WindowChrome.SetIsHitTestVisibleInChrome(close, true); title.Children.Add(close);
        var minimize = new Button { Content = "─", Width = 34 }; minimize.Click += (_, _) => WindowState = WindowState.Minimized; DockPanel.SetDock(minimize, Dock.Right); WindowChrome.SetIsHitTestVisibleInChrome(minimize, true); title.Children.Add(minimize);
        title.Children.Add(new TextBlock { Text = "LS  ·  PROFILER  ·  " + target.Server + " / " + template.DatabaseFilter, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(12,0,0,0), TextTrimming = TextTrimming.CharacterEllipsis });
        var toolbar = new WrapPanel { Margin = new Thickness(6) }; DockPanel.SetDock(toolbar, Dock.Top); dock.Children.Add(toolbar);
        var clear = IconButton("\uE74D", "Xóa danh sách"); clear.Click += (_, _) => { events.Clear(); detail.Text = ""; received = 0; };
        foreach (var button in new[] { start, stop, clear, layoutToggle }) toolbar.Children.Add(button);
        filterLabel.Text = "SQL:BatchCompleted · Database bắt đầu bằng: " + template.DatabaseFilter;
        DockPanel.SetDock(filterLabel, Dock.Top); dock.Children.Add(filterLabel);
        DockPanel.SetDock(status, Dock.Bottom); dock.Children.Add(status);
        dock.Children.Add(body);
        grid = new DataGrid { AutoGenerateColumns = false, IsReadOnly = true, CanUserAddRows = false, CanUserResizeRows = false, RowHeight = 32, ColumnHeaderHeight = 40, EnableRowVirtualization = true, EnableColumnVirtualization = true, ItemsSource = events, SelectionMode = DataGridSelectionMode.Single };
        var cellTextStyle = new Style(typeof(TextBlock));
        cellTextStyle.Setters.Add(new Setter(TextBlock.TextWrappingProperty, TextWrapping.NoWrap));
        cellTextStyle.Setters.Add(new Setter(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis));
        cellTextStyle.Setters.Add(new Setter(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Top));
        foreach (var (header, property, width) in new[] { ("Thời gian", "Time", 160d), ("Event", "Event", 170d), ("TextData", "Text", 390d), ("Database", "Database", 140d), ("Login", "Login", 130d), ("SPID", "Session", 65d), ("Duration ms", "DurationMs", 100d), ("CPU ms", "CpuMs", 90d), ("Reads", "Reads", 85d), ("Writes", "Writes", 85d), ("Application", "Application", 160d), ("Host", "Host", 140d) })
            grid.Columns.Add(new DataGridTextColumn { Header = header, Binding = new Binding(property) { StringFormat = property == "Time" ? "HH:mm:ss.fff" : null }, ElementStyle = cellTextStyle, Width = width });
        detailDocument.Layout.Children.Remove(detail); detail.IsReadOnly = true;
        grid.SelectionChanged += (_, _) => detail.Text = (grid.SelectedItem as ProfilerEvent)?.Text ?? ""; body.Children.Add(grid);
        body.Children.Add(splitter); body.Children.Add(detail); ApplyLayout();
        start.Click += (_, _) => Start(); stop.Click += async (_, _) => await StopAsync(); layoutToggle.Click += (_, _) => { sideBySide = !sideBySide; ApplyLayout(); };
        Closing += (_, e) => { if (allowClose) return; e.Cancel = true; if (closingWindow) return; closingWindow = true; _ = Dispatcher.BeginInvoke(new Action(async () => { await StopAsync(); if (pendingCleanup != null) { closingWindow = false; return; } allowClose = true; Close(); })); };
        Closed += (_, _) => detailDocument.Dispose();
        Loaded += (_, _) => { if (!preview) Start(); };
        if (preview) { events.Add(new(1, DateTime.Now, "SQL:BatchCompleted", target.Database, "developer", "WORKSTATION", "ERP", "72", 12.5, 32, 0, 3, "exec dbo.GetVoucher @ma_bp = N'01';")); grid.SelectedIndex = 0; status.Text = "Dữ liệu minh họa kiểm tra giao diện • chưa kết nối server"; }
    }
    private void Start()
    {
        if (running is { IsCompleted: false } || closingWindow) return;
        events.Clear(); detail.Text = ""; 
        cancellation?.Dispose(); cancellation = new CancellationTokenSource(); lastSequence = -1; received = 0;
        start.IsEnabled = false; stop.IsEnabled = true;
        running = RunAsync(cancellation.Token);
    }
    internal void SmokeLayoutToggle()
    {
        var selected = grid.SelectedItem; var sql = detail.Text;
        layoutToggle.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); UpdateLayout();
        if (body.ColumnDefinitions.Count != 3 || body.RowDefinitions.Count != 0 || Grid.GetColumn(detail) != 2 || splitter.ResizeDirection != GridResizeDirection.Columns || detail.ActualWidth <= 0) throw new Exception("Profiler side-by-side layout");
        if (grid.SelectedItem != selected || detail.Text != sql) throw new Exception("Profiler layout lost selection or SQL");
        layoutToggle.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); UpdateLayout();
        if (body.RowDefinitions.Count != 3 || body.ColumnDefinitions.Count != 0 || Grid.GetRow(detail) != 2 || splitter.ResizeDirection != GridResizeDirection.Rows) throw new Exception("Profiler stacked layout");
    }
    private async Task RunAsync(CancellationToken token)
    {
        SqlTraceSession? session = null;
        try
        {
            if (pendingCleanup != null) { await pendingCleanup.DropAsync(); pendingCleanup = null; }
            session = new SqlTraceSession(target, template); pendingCleanup = session;
            status.Text = "Đang kết nối Profiler…"; await session.StartAsync(token);
            while (true)
            {
                token.ThrowIfCancellationRequested();
                var batch = await session.ReadAsync(token);
                foreach (var item in batch.Events.Where(e => e.Sequence > lastSequence)) { events.Add(item); received++; lastSequence = Math.Max(lastSequence, item.Sequence); }
                while (events.Count > 5000) events.RemoveAt(0);
                FocusLastEvent();
                status.Text = $"Đang chạy • {received:N0} sự kiện • giữ tối đa 5.000 dòng • phiên {session.Name}" + (batch.Truncated || batch.Dropped > 0 ? " • Bộ đệm quá tải, có thể thiếu sự kiện" : "");
                await Task.Delay(1000, token);
            }
        }
        catch (OperationCanceledException) { status.Text = "Đã dừng • " + received + " sự kiện"; }
        catch (Exception ex) { status.Text = "Profiler: " + ex.Message; }
        finally
        {
            if (pendingCleanup != null)
            {
                try { await pendingCleanup.DropAsync(); pendingCleanup = null; }
                catch (Exception ex) { status.Text += " • Không dọn được phiên " + (pendingCleanup?.Name ?? "unknown") + ": " + ex.Message; }
            }
            start.IsEnabled = !closingWindow; stop.IsEnabled = false;
        }
    }
    private void FocusLastEvent()
    {
        if (grid.Items.Count == 0) return;
        var last = grid.Items[grid.Items.Count - 1];
        grid.SelectedItem = last;
        grid.CurrentCell = new DataGridCellInfo(last, grid.Columns[0]);
        grid.ScrollIntoView(last);
    }
    public async Task StopAsync()
    {
        cancellation?.Cancel();
        if (running != null) await running;
        if (pendingCleanup != null)
        {
            try { await pendingCleanup.DropAsync(); pendingCleanup = null; }
            catch (Exception ex) { status.Text = "Không dọn được phiên " + (pendingCleanup?.Name ?? "unknown") + ": " + ex.Message; }
        }
    }
    internal void CloseStopped()
    {
        if (pendingCleanup != null || running is { IsCompleted: false }) throw new InvalidOperationException("Profiler vẫn đang chạy.");
        allowClose = true;
        Close();
    }
}
