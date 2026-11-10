using System.Collections.Concurrent;
using System.ComponentModel;
using System.IO;
using System.Text.Json;
using System.Windows;
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
    private readonly DeveloperTools tools = new();
    private readonly StudioCommands commands;
    private readonly ConcurrentDictionary<string, CancellationTokenSource> operations = new();
    private bool closeAllowed, shuttingDown, closing, bridgeReady;
    private readonly bool smoke;
    public StudioWindow(bool smoke)
    {
        this.smoke = smoke;
        Title = "LS Code · WebView"; Width = 1440; Height = 920; MinWidth = 480; MinHeight = 480;
        WindowStyle = WindowStyle.None;
        System.Windows.Shell.WindowChrome.SetWindowChrome(this, new System.Windows.Shell.WindowChrome { CaptionHeight = 0, ResizeBorderThickness = new Thickness(6), GlassFrameThickness = new Thickness(0), CornerRadius = new CornerRadius(0) });
        WindowWorkArea.Attach(this);
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        commands = new(new DocumentWorkspace(), tools, new DesktopDialogs(this));
        Content = view;
        Loaded += async (_, _) => await InitializeAsync();
        Closing += OnClosing;
    }
    private async Task InitializeAsync()
    {
        try
        {
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
                if (smoke && e.IsSuccess) await SmokeAsync(data);
            };
            core.Navigate(Origin + "/index.html");
        }
        catch (Exception ex)
        {
            if (smoke) { File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "webview-smoke-error.txt"), ex.ToString()); closeAllowed = true; Close(); }
            else Content = new System.Windows.Controls.TextBox { Text = "Không khởi động được WebView2. Kiểm tra WebView2 Runtime và bản đóng gói.\n\n" + ex.Message, IsReadOnly = true, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(24) };
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
                if (layoutState == WindowState.Maximized && !WindowWorkArea.FitsMonitor(this)) throw new InvalidOperationException("Cửa sổ vượt work area khi phóng to.");
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
