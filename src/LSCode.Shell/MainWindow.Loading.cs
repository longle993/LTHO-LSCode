using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace LSCode.Shell;
public partial class MainWindow
{
    private int busyCount;
    private IDisposable BeginBusy(string message)
    {
        busyCount++; LoadingMessage.Text = message; LoadingStrip.Visibility = Visibility.Visible;
        return new BusyScope(() => { if (--busyCount == 0) LoadingStrip.Visibility = Visibility.Hidden; });
    }
    private static IDisposable ShowLoadingOverlay(Grid host, string message)
    {
        var overlay = new Grid { Background = new SolidColorBrush(Color.FromArgb(175, 0, 0, 0)), Focusable = true };
        Panel.SetZIndex(overlay, 1000);
        var card = new Border { Padding = new Thickness(32), CornerRadius = new CornerRadius(14), MaxWidth = 440,
            HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        card.SetResourceReference(Border.BackgroundProperty, "Panel");
        var stack = new StackPanel();
        var logo = new TextBlock { Text = "LS", FontSize = 38, FontWeight = FontWeights.Bold, HorizontalAlignment = HorizontalAlignment.Center };
        logo.SetResourceReference(TextBlock.ForegroundProperty, "Accent");
        logo.BeginAnimation(OpacityProperty, new DoubleAnimation(.3, 1, TimeSpan.FromMilliseconds(650)) { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever });
        stack.Children.Add(logo);
        stack.Children.Add(new TextBlock { Text = message, Margin = new Thickness(0, 18, 0, 16), TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center });
        var progress = new ProgressBar { IsIndeterminate = true, Height = 4 };
        progress.SetResourceReference(Control.ForegroundProperty, "Accent"); stack.Children.Add(progress);
        card.Child = stack; overlay.Children.Add(card);
        var previousFocus = System.Windows.Input.Keyboard.FocusedElement;
        var underlying = host.Children.OfType<UIElement>().Select(element => (element, element.IsEnabled)).ToList();
        foreach (var (element, _) in underlying) element.IsEnabled = false;
        host.Children.Add(overlay); overlay.Focus();
        return new BusyScope(() => {
            logo.BeginAnimation(OpacityProperty, null); host.Children.Remove(overlay);
            foreach (var (element, enabled) in underlying) element.IsEnabled = enabled;
            if (previousFocus is UIElement { IsVisible: true, IsEnabled: true } focus) focus.Focus();
        });
    }
    private sealed class BusyScope(Action close) : IDisposable
    {
        private Action? onClose = close;
        public void Dispose() { onClose?.Invoke(); onClose = null; }
    }
}
