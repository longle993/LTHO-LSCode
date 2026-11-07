using System.Windows;
using System.Windows.Controls;
using LSCode.Core;

namespace LSCode.Shell;

public static class Dialogs
{
    public static MessageBoxResult Notify(Window? owner, string message, string title = "LS Code", MessageBoxButton buttons = MessageBoxButton.OK, MessageBoxImage image = MessageBoxImage.Information, string okLabel = "Đồng ý")
    {
        var result = buttons == MessageBoxButton.YesNo ? MessageBoxResult.No : buttons == MessageBoxButton.OK ? MessageBoxResult.OK : MessageBoxResult.Cancel;
        var window = new Window { Title = title, Width = 540, SizeToContent = SizeToContent.Height, MaxHeight = 650, ResizeMode = ResizeMode.NoResize, WindowStyle = WindowStyle.None, ShowInTaskbar = false, WindowStartupLocation = owner == null ? WindowStartupLocation.CenterScreen : WindowStartupLocation.CenterOwner };
        if (owner?.IsVisible == true) window.Owner = owner;
        var border = new Border { BorderThickness = new Thickness(1), Padding = new Thickness(24), CornerRadius = new CornerRadius(8) };
        border.SetResourceReference(Border.BackgroundProperty, "Surface"); border.SetResourceReference(Border.BorderBrushProperty, "Border");
        var panel = new StackPanel(); border.Child = panel; window.Content = border;
        var heading = new TextBlock { Text = title, FontSize = 19, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 16) };
        heading.MouseLeftButtonDown += (_, e) => { if (e.ButtonState == System.Windows.Input.MouseButtonState.Pressed) window.DragMove(); };
        panel.Children.Add(heading);
        panel.Children.Add(new ScrollViewer { MaxHeight = 440, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap } });
        var actions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 20, 0, 0) }; panel.Children.Add(actions);
        void Add(string label, MessageBoxResult value, bool isDefault = false, bool isCancel = false)
        {
            var button = new Button { Content = label, MinWidth = 85, MinHeight = 40, IsDefault = isDefault, IsCancel = isCancel };
            if (value == MessageBoxResult.OK) button.SetResourceReference(FrameworkElement.StyleProperty, "PrimaryButton");
            button.Click += (_, _) => { result = value; window.Close(); }; actions.Children.Add(button);
        }
        if (buttons is MessageBoxButton.YesNo or MessageBoxButton.YesNoCancel) { Add("Có", MessageBoxResult.Yes); Add("Không", MessageBoxResult.No, buttons == MessageBoxButton.YesNo, buttons == MessageBoxButton.YesNo); }
        else Add(okLabel, MessageBoxResult.OK, true, buttons == MessageBoxButton.OK);
        if (buttons is MessageBoxButton.OKCancel or MessageBoxButton.YesNoCancel) Add("Hủy", MessageBoxResult.Cancel, buttons == MessageBoxButton.YesNoCancel, true);
        window.ShowDialog(); return result;
    }
    public static Window Create(Window owner, string title, UIElement content, double width = 600, double height = 430)
    {
        var heading = new TextBlock { Text = title, FontSize = 22, FontWeight = FontWeights.SemiBold, Margin = new Thickness(4, 4, 4, 20), TextWrapping = TextWrapping.Wrap };
        if (content is DockPanel dock) { DockPanel.SetDock(heading, Dock.Top); dock.Children.Insert(0, heading); }
        else if (content is StackPanel stack) stack.Children.Insert(0, heading);
        return new Window {
            Owner = owner, Title = title, Content = content,
            Width = Math.Min(width, SystemParameters.WorkArea.Width - 32), Height = Math.Min(height + 48, SystemParameters.WorkArea.Height - 32),
            MinWidth = Math.Min(width, 480), MinHeight = Math.Min(height, 260), UseLayoutRounding = true,
            WindowStartupLocation = WindowStartupLocation.CenterOwner, ShowInTaskbar = false
        };
    }
    public static Window ProjectWindow(Window owner, string title, UIElement content, double width, double height)
    {
        var window = new Window { Owner = owner, Title = title, Width = width, Height = height, WindowStyle = WindowStyle.None,
            ResizeMode = ResizeMode.NoResize, ShowInTaskbar = false, WindowStartupLocation = WindowStartupLocation.CenterOwner };
        var frame = new Border { BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(8) };
        frame.SetResourceReference(Border.BackgroundProperty, "Surface"); frame.SetResourceReference(Border.BorderBrushProperty, "Border");
        var dock = new DockPanel(); frame.Child = dock; window.Content = frame;
        var header = new DockPanel { Height = 48, Background = System.Windows.Media.Brushes.Transparent };
        var close = new Button { Content = "×", ToolTip = "Đóng", Width = 32, Height = 32, IsCancel = true, Style = (Style)Application.Current.FindResource("IconButton") };
        close.Click += (_, _) => window.Close(); DockPanel.SetDock(close, Dock.Right); header.Children.Add(close);
        header.Children.Add(new TextBlock { Text = title, FontSize = 16, FontWeight = FontWeights.SemiBold, Margin = new Thickness(20, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center });
        header.MouseLeftButtonDown += (_, e) => { if (e.ButtonState == System.Windows.Input.MouseButtonState.Pressed) window.DragMove(); };
        DockPanel.SetDock(header, Dock.Top); dock.Children.Add(header); dock.Children.Add(content); return window;
    }
    public static string? ProjectName(Window owner)
    {
        var panel = new DockPanel { Margin = new Thickness(20, 8, 20, 16) };
        var actions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 12, 0, 0) };
        var cancel = new Button { Content = "Hủy", IsCancel = true };
        var ok = new Button { Content = "Đồng bộ", IsDefault = true, IsEnabled = false, Style = (Style)Application.Current.FindResource("PrimaryButton") };
        actions.Children.Add(cancel); actions.Children.Add(ok); DockPanel.SetDock(actions, Dock.Bottom); panel.Children.Add(actions);
        var row = new DockPanel(); var input = new TextBox { AcceptsReturn = false, TextWrapping = TextWrapping.NoWrap };
        var label = new Label { Content = "Tên dự án", Target = input, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 12, 0) };
        DockPanel.SetDock(label, Dock.Left); row.Children.Add(label); row.Children.Add(input); panel.Children.Add(row);
        var window = ProjectWindow(owner, "Đồng bộ dự án", panel, 500, 190);
        input.TextChanged += (_, _) => ok.IsEnabled = !string.IsNullOrWhiteSpace(input.Text);
        ok.Click += (_, _) => window.DialogResult = true; window.Loaded += (_, _) => input.Focus();
        return window.ShowDialog() == true ? input.Text.Trim() : null;
    }
    public static string? Text(Window owner, string title, string initial = "", bool multiline = false)
    {
        var panel = new DockPanel { Margin = new Thickness(18) };
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        var ok = new Button { Style = (Style)Application.Current.FindResource("PrimaryButton"), Content = "OK", IsDefault = !multiline, MinWidth = 85 };
        var cancel = new Button { Content = "Hủy", IsCancel = true, MinWidth = 85 };
        buttons.Children.Add(ok); buttons.Children.Add(cancel); DockPanel.SetDock(buttons, Dock.Bottom); panel.Children.Add(buttons);
        var input = new TextBox { Text = initial, AcceptsReturn = multiline, AcceptsTab = multiline, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, TextWrapping = TextWrapping.Wrap };
        panel.Children.Add(input);
        var window = Create(owner, title, panel, 680, multiline ? 510 : 190);
        ok.Click += (_, _) => window.DialogResult = true;
        window.Loaded += (_, _) => { input.Focus(); if (!multiline) input.SelectAll(); };
        return window.ShowDialog() == true ? input.Text : null;
    }
    public static IdeCommand? Palette(Window owner, IEnumerable<IdeCommand> commands, string title = "Command Palette")
    {
        var panel = new DockPanel { Margin = new Thickness(14) };
        var search = new TextBox(); DockPanel.SetDock(search, Dock.Top); panel.Children.Add(search);
        var list = new ListBox(); panel.Children.Add(list);
        var all = commands.ToList(); list.ItemsSource = all; list.SelectedIndex = 0;
        var window = Create(owner, title, panel, 700, 500);
        void Choose() { if (list.SelectedItem != null) window.DialogResult = true; }
        search.TextChanged += (_, _) => { list.ItemsSource = all.Where(c => c.Name.Contains(search.Text, StringComparison.OrdinalIgnoreCase) || c.Id.Contains(search.Text, StringComparison.OrdinalIgnoreCase)).ToList(); list.SelectedIndex = 0; };
        search.KeyDown += (_, e) =>
        {
            if (e.Key == System.Windows.Input.Key.Enter) { Choose(); e.Handled = true; }
            if (e.Key == System.Windows.Input.Key.Down) { list.Focus(); e.Handled = true; }
            if (e.Key == System.Windows.Input.Key.Escape) window.Close();
        };
        list.MouseDoubleClick += (_, _) => Choose();
        list.KeyDown += (_, e) => { if (e.Key == System.Windows.Input.Key.Enter) Choose(); };
        window.Loaded += (_, _) => search.Focus();
        return window.ShowDialog() == true ? list.SelectedItem as IdeCommand : null;
    }
}
