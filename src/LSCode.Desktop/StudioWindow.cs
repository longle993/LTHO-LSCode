using System.Collections.Concurrent;
using System.ComponentModel;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using LSCode.Application;
using LSCode.Contracts;
using LSCode.Infrastructure;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

namespace LSCode.Desktop;

public sealed class StudioWindow : Window
{
    private const string Origin = "https://lscode.local";
    private readonly WebView2 view = new();
    private readonly Grid splash;
    private Window? startupOverlay;
    private readonly DispatcherTimer startupTimeout = new() { Interval = TimeSpan.FromSeconds(45) };
    private readonly DeveloperTools tools = new();
    private readonly StudioCommands commands;
    private readonly ConcurrentDictionary<string, CancellationTokenSource> operations = new();
    private bool closeAllowed, shuttingDown, closing, bridgeReady;
    private readonly bool smoke;
    public StudioWindow(bool smoke)
    {
        this.smoke = smoke;
        Title = "LS Code · WebView"; Width = 1440; Height = 920; MinWidth = 480; MinHeight = 480;
        Icon = BitmapFrame.Create(new Uri("pack://application:,,,/Assets/logo.ico"));
        WindowStyle = WindowStyle.None;
        System.Windows.Shell.WindowChrome.SetWindowChrome(this, new System.Windows.Shell.WindowChrome { CaptionHeight = 0, ResizeBorderThickness = new Thickness(6), GlassFrameThickness = new Thickness(0), CornerRadius = new CornerRadius(0) });
        WindowWorkArea.Attach(this);
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        commands = new(new DocumentWorkspace(), tools, new DesktopDialogs(this));
        var splashBackground = new SolidColorBrush(Color.FromRgb(4, 36, 49));
        Background = splashBackground;
        var brand = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        var logo = new IconBitmapDecoder(new Uri("pack://application:,,,/Assets/logo.ico"), BitmapCreateOptions.None, BitmapCacheOption.OnLoad)
            .Frames.OrderByDescending(frame => frame.PixelWidth).First();
        var splashLogo = new Image { Source = logo, Width = 108, Height = 108 };
        RenderOptions.SetBitmapScalingMode(splashLogo, BitmapScalingMode.HighQuality);
        brand.Children.Add(splashLogo);
        brand.Children.Add(new TextBlock { Text = "Studio Code", Foreground = Brushes.White, FontSize = 36, FontWeight = FontWeights.SemiBold, HorizontalAlignment = HorizontalAlignment.Center });
        brand.Children.Add(new TextBlock { Text = "Đang khởi động…", Foreground = Brushes.White, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 10, 0, 12) });
        brand.Children.Add(new ProgressBar { Width = 220, Height = 4, IsIndeterminate = SystemParameters.ClientAreaAnimation, Value = 35, Foreground = new SolidColorBrush(Color.FromRgb(0, 172, 174)) });
        splash = new Grid { Background = splashBackground };
        var artwork = new Canvas { Width = 1280, Height = 720, IsHitTestVisible = false };
        artwork.Children.Add(new System.Windows.Shapes.Path
        {
            Data = Geometry.Parse("M0,0 H764 C620,149 245,275 0,310 Z"),
            Fill = new LinearGradientBrush(Color.FromRgb(6, 80, 105), Color.FromRgb(0, 172, 174), 15)
        });
        artwork.Children.Add(new System.Windows.Shapes.Path
        {
            Data = Geometry.Parse("M764,0 H1280 V565 C1000,470 960,273 764,0 Z"),
            Fill = new LinearGradientBrush(Color.FromRgb(113, 225, 223), Color.FromRgb(0, 172, 174), 65)
        });
        splash.Children.Add(new Viewbox { Child = artwork, Stretch = Stretch.Fill });
        splash.Children.Add(brand);
        Content = splash;
        startupTimeout.Tick += (_, _) => ShowStartupError("WebView2 chưa sẵn sàng sau 45 giây. Đóng và mở lại ứng dụng.");
        Loaded += async (_, _) =>
        {
            startupTimeout.Start();
            await Dispatcher.Yield(DispatcherPriority.Background);
            if (closing || shuttingDown) return;
            await InitializeAsync();
        };
        Closing += OnClosing;
    }
    private void ShowStartupOverlay()
    {
        // A regular WebView2 is an HWND and would paint above a WPF sibling.
        // Cover it with an owned window until the web UI has painted.
        Content = null;
        startupOverlay = new Window
        {
            Owner = this,
            ShowActivated = false,
            ShowInTaskbar = false,
            WindowStyle = WindowStyle.None,
            ResizeMode = ResizeMode.NoResize,
            Background = Background,
            Content = splash
        };
        PositionStartupOverlay();
        LocationChanged += PositionStartupOverlay;
        SizeChanged += PositionStartupOverlay;
        startupOverlay.Show();
        Content = view;
    }
    private void PositionStartupOverlay(object? sender = null, EventArgs? e = null)
    {
        if (startupOverlay is not { } overlay) return;
        overlay.Left = Left;
        overlay.Top = Top;
        overlay.Width = ActualWidth;
        overlay.Height = ActualHeight;
    }
    private void EndStartup()
    {
        startupTimeout.Stop();
        LocationChanged -= PositionStartupOverlay;
        SizeChanged -= PositionStartupOverlay;
        startupOverlay?.Close();
        startupOverlay = null;
    }
    private void ShowStartupError(string message)
    {
        EndStartup();
        closeAllowed = true;
        Content = new TextBox { Text = message, IsReadOnly = true, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(24) };
    }
    private async Task InitializeAsync()
    {
        try
        {
            ShowStartupOverlay();
            var assets = Path.Combine(AppContext.BaseDirectory, "wwwroot");
            if (!File.Exists(Path.Combine(assets, "index.html"))) throw new FileNotFoundException("Thiếu giao diện web. Chạy build-webview.ps1 trước khi mở ứng dụng.");
            var data = Environment.GetEnvironmentVariable("LSCODE_DATA_HOME") ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LSCode");
            var environment = await CoreWebView2Environment.CreateAsync(userDataFolder: Path.Combine(data, "WebView2"));
            await view.EnsureCoreWebView2Async(environment);
            var core = view.CoreWebView2;
            core.SetVirtualHostNameToFolderMapping("lscode.local", assets, CoreWebView2HostResourceAccessKind.DenyCors);
            core.Settings.AreDefaultContextMenusEnabled = false;
            core.Settings.AreDevToolsEnabled = System.Diagnostics.Debugger.IsAttached;
            core.Settings.IsStatusBarEnabled = false;
            core.Settings.IsNonClientRegionSupportEnabled = true;
            core.Settings.IsWebMessageEnabled = true;
            core.NavigationStarting += (_, e) => { if (!Trusted(e.Uri)) e.Cancel = true; };
            core.FrameNavigationStarting += (_, e) => e.Cancel = true;
            core.NewWindowRequested += (_, e) => e.Handled = true;
            core.PermissionRequested += (_, e) => e.State = CoreWebView2PermissionState.Deny;
            core.DownloadStarting += (_, e) => e.Cancel = true;
            core.WebMessageReceived += Receive;
            if(smoke)await core.AddScriptToExecuteOnDocumentCreatedAsync("window.__LSCODE_SMOKE__ = true;");
            core.NavigationCompleted += async (_, e) =>
            {
                if (!e.IsSuccess)
                {
                    if (startupOverlay != null) ShowStartupError("Không tải được giao diện Studio Code: " + e.WebErrorStatus);
                    return;
                }
                if (smoke && e.IsSuccess) await SmokeAsync(data);
            };
            core.Navigate(Origin + "/index.html");
        }
        catch (Exception ex)
        {
            if (closing || shuttingDown) return;
            if (smoke) { File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "webview-smoke-error.txt"), ex.ToString()); closeAllowed = true; Close(); }
            else ShowStartupError("Không khởi động được WebView2. Kiểm tra WebView2 Runtime và bản đóng gói.\n\n" + ex.Message);
        }
    }
    private static bool Trusted(string source) => Uri.TryCreate(source, UriKind.Absolute, out var uri) && uri.Scheme == "https" && uri.Host == "lscode.local" && uri.IsDefaultPort;
    private async void Receive(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        if (!Trusted(e.Source) || closing) return;
        Request? request = null;
        try
        {
            var raw = e.WebMessageAsJson;
            if (raw.Length > Protocol.MaximumMessageLength) throw new ArgumentException("Yêu cầu vượt giới hạn kích thước.");
            request = JsonSerializer.Deserialize<Request>(raw, Protocol.Json) ?? throw new ArgumentException("Yêu cầu rỗng.");
            if (request.ProtocolVersion != Protocol.Version) throw new ArgumentException("Phiên bản giao thức không tương thích.");
            if(request.Method=="studio.bootstrap")bridgeReady=true;
            if (string.IsNullOrWhiteSpace(request.RequestId) || request.RequestId.Length > 100) throw new ArgumentException("Request ID không hợp lệ.");
            if (request.Method == "window.ready")
            {
                EndStartup();
                Reply(new(1, request.RequestId, true)); return;
            }
            if (request.Method == "operation.cancel")
            {
                if (operations.TryGetValue(request.Payload.GetProperty("requestId").GetString() ?? "", out var running)) await running.CancelAsync();
                Reply(new(1, request.RequestId, true)); return;
            }
            if (request.Method == "window.minimize") { WindowState = WindowState.Minimized; Reply(new(1, request.RequestId, true)); return; }
            if (request.Method == "window.maximize") { WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized; Reply(new(1, request.RequestId, true)); return; }
            if (request.Method == "window.close") { closeAllowed = true; Close(); return; }
            if(request.Method is "clipboard.read" or "clipboard.write")
            {
                var text=request.Method=="clipboard.read"?(Clipboard.ContainsText()?Clipboard.GetText(TextDataFormat.UnicodeText):""):request.Payload.GetProperty("text").GetString()??"";
                if(System.Text.Encoding.UTF8.GetByteCount(text)>5*1024*1024)throw new ArgumentException("Clipboard vượt giới hạn 5 MB.");
                if(request.Method=="clipboard.write") {if(text.Length==0)Clipboard.Clear();else Clipboard.SetText(text,TextDataFormat.UnicodeText);}
                Reply(new(1,request.RequestId,request.Method=="clipboard.read"?text:true));return;
            }
            if (request.Method == "window.new")
            {
                var executable=Environment.ProcessPath ?? throw new InvalidOperationException("Không tìm thấy executable của ứng dụng.");
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(executable) { UseShellExecute = true });
                Reply(new(1, request.RequestId, true)); return;
            }
            using var cts = new CancellationTokenSource();
            if (!operations.TryAdd(request.RequestId, cts)) throw new ArgumentException("Request ID bị trùng.");
            try
            {
                // IO-bound services retain the UI context for native pickers, while CPU/UNC scans use Task.Run in their adapters.
                var result = await commands.ExecuteAsync(request.Method, request.Payload, cts.Token);
                Reply(new(1, request.RequestId, result));
            }
            finally { operations.TryRemove(request.RequestId, out _); }
        }
        catch (Exception ex)
        {
            var code = ex switch { OperationCanceledException => "cancelled", LSCode.Core.FileConflictException => "file_conflict", DocumentReadOnlyException => "file_readonly", NotSupportedException => "unsupported", _ => "failed" };
            var details = ex is LSCode.Shell.Services.SqlExecutionException sqlError ? sqlError.Errors : null;
            if (request != null) Reply(new(1, request.RequestId, null, new(code, ex.Message, details)));
        }
    }
    private void Reply(Response response)
    {
        if (!shuttingDown) view.CoreWebView2.PostWebMessageAsJson(JsonSerializer.Serialize(response, Protocol.Json));
    }
    private async void OnClosing(object? sender, CancelEventArgs e)
    {
        if (shuttingDown) return;
        e.Cancel = true;
        if(closing)return;
        if (!closeAllowed && bridgeReady && view.CoreWebView2 != null)
        {
            view.CoreWebView2.PostWebMessageAsJson("{\"type\":\"window.closing\"}"); return;
        }
        try
        {
            closing=true;
            foreach (var op in operations.Values) await op.CancelAsync();
            await tools.DisposeAsync();
            EndStartup();
            shuttingDown = true; view.Dispose();
            // Finish the cancelled Closing event before requesting the final close.
            await Dispatcher.InvokeAsync(Close, System.Windows.Threading.DispatcherPriority.Background);
        }
        catch (Exception ex) { closing=false;closeAllowed = false; MessageBox.Show(this, "Chưa đóng được Profiler: " + ex.Message, "LS Code"); }
    }
    private async Task SmokeAsync(string data)
    {
        Directory.CreateDirectory(data);
        try
        {
            string check = "false";
            for (var i = 0; i < 80; i++)
            {
                check = await view.ExecuteScriptAsync("Boolean(document.querySelector('[data-ready=\"true\"]'))");
                if (check == "true") break;
                await Task.Delay(250);
            }
            if (check != "true") throw new InvalidOperationException("React không hoàn thành bootstrap qua native bridge.");
            for (var i = 0; i < 20 && startupOverlay != null; i++) await Task.Delay(50);
            if (startupOverlay != null) throw new InvalidOperationException("Màn khởi động chưa nhường chỗ cho giao diện.");
            var result = await view.ExecuteScriptAsync("window.lscodeSmoke ? window.lscodeSmoke() : Promise.resolve({error:'Missing smoke function'})");
            // ExecuteScriptAsync does not await JS promises: the smoke function writes its result to DOM.
            for (var i = 0; i < 100; i++)
            {
                result = await view.ExecuteScriptAsync("document.documentElement.dataset.smoke || ''");
                if (result != "\"\"") break;
                await Task.Delay(250);
            }
            foreach (var layoutState in new[] { WindowState.Maximized, WindowState.Normal }) {
                WindowState = layoutState; await Task.Delay(250);
                if (!WindowWorkArea.FitsMonitor(this)) throw new InvalidOperationException($"Cửa sổ vượt vùng làm việc ở trạng thái {layoutState}.");
                var fits = await view.ExecuteScriptAsync("(()=>{const f=document.querySelector('.statusbar').getBoundingClientRect(),w=document.querySelector('.workbench').getBoundingClientRect();return f.bottom<=innerHeight+1&&f.height>=27&&w.bottom<=f.top+1;})()");
                if(fits!="true")throw new InvalidOperationException("Workbench tràn đáy ở trạng thái "+layoutState);
            }
            await using (var png = File.Create(Path.Combine(data, "webview.png"))) await view.CoreWebView2.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png, png);
            File.WriteAllText(Path.Combine(data, "webview-smoke.json"), result);
        }
        catch (Exception ex) { File.WriteAllText(Path.Combine(data, "webview-smoke.json"), JsonSerializer.Serialize(new { error = ex.ToString() })); }
        closeAllowed = true; Close();
    }
}
