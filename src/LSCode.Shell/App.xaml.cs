using System.Windows;

namespace LSCode.Shell;
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        if (e.Args.Contains("--smoke") || e.Args.Contains("--project-smoke") || e.Args.Contains("--compact-ui-smoke") || e.Args.Contains("--catalog-transfer-smoke"))
        {
            var dataPath = Environment.GetEnvironmentVariable("LSCODE_DATA_HOME");
            // Smoke writes fixture catalogs. Never reuse a populated user data directory.
            if (string.IsNullOrWhiteSpace(dataPath) ||
                (System.IO.Directory.Exists(dataPath) && System.IO.Directory.EnumerateFileSystemEntries(dataPath).Any()))
                Environment.SetEnvironmentVariable("LSCODE_DATA_HOME", System.IO.Path.Combine(System.IO.Path.GetTempPath(), "LSCode-smoke-" + Guid.NewGuid().ToString("N")));
        }
        DispatcherUnhandledException += (_, args) =>
        {
            Dialogs.Notify(MainWindow, args.Exception.Message, "LS Code", MessageBoxButton.OK, MessageBoxImage.Error);
            args.Handled = true;
        };
        var window = new MainWindow(); MainWindow = window;
        if (e.Args.Contains("--smoke")) window.Loaded += async (_, _) => await window.RunSmokeAsync();
        if (e.Args.Contains("--project-smoke")) window.Loaded += async (_, _) => await window.RunProjectSmokeAsync();
        if (e.Args.Contains("--compact-ui-smoke")) window.Loaded += async (_, _) => await window.RunCompactUiSmokeAsync();
        if (e.Args.Contains("--catalog-transfer-smoke")) window.Loaded += async (_, _) => await window.RunCatalogTransferSmokeAsync();
        window.Show();
    }
}

