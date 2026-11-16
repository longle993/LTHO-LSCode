using System.Diagnostics;
using System.Text;
using LSCode.Shell.Services;

namespace LSCode.Infrastructure;

internal static class SqlDebugLauncher
{
    internal static async Task<string?> FindAsync(CancellationToken token)
    {
        var locator = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
            "Microsoft Visual Studio", "Installer", "vswhere.exe");
        if (!File.Exists(locator)) return null;
        var start = new ProcessStartInfo(locator) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true };
        foreach (var argument in new[] { "-latest", "-products", "Microsoft.VisualStudio.Product.Community",
            "Microsoft.VisualStudio.Product.Professional", "Microsoft.VisualStudio.Product.Enterprise",
            "-requires", "Microsoft.VisualStudio.Component.SQL.SSDT", "-property", "productPath" })
            start.ArgumentList.Add(argument);
        using var process = Process.Start(start) ?? throw new IOException("Không thể kiểm tra Visual Studio.");
        var output = await process.StandardOutput.ReadToEndAsync(token).ConfigureAwait(false);
        await process.WaitForExitAsync(token).ConfigureAwait(false);
        var path = output.Trim();
        return process.ExitCode == 0 && File.Exists(path) ? path : null;
    }

    internal static string Script(string server, string database, string sql)
    {
        if (string.IsNullOrWhiteSpace(sql)) throw new ArgumentException("Nhập SQL cần debug.");
        // Connection credentials never enter the script or process arguments.
        var serverLabel = server.Replace('\r', ' ').Replace('\n', ' ');
        return $"-- LSCode: kết nối tới {serverLabel} trong SQL Server Object Explorer.\r\n" +
            "-- Chọn Execute with Debugger / Alt+F5; F9 breakpoint, F10 step over, F11 step into.\r\n" +
            "-- Biến của stored procedure xem trong Watch/Locals của Visual Studio.\r\n" +
            $"USE [{database.Replace("]", "]]", StringComparison.Ordinal)}];\r\nGO\r\n" + sql;
    }

    internal static async Task<object> OpenAsync(SqlTarget target, string sql, CancellationToken token)
    {
        var executable = await FindAsync(token).ConfigureAwait(false)
            ?? throw new InvalidOperationException("Chưa tìm thấy Visual Studio có SQL Server Data Tools. Cài thành phần SSDT bằng Visual Studio Installer rồi thử lại.");
        var folder = Path.Combine(SettingsStore.Root, "SqlDebug");
        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, $"Debug-{DateTime.UtcNow:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}.sql");
        await File.WriteAllTextAsync(path, Script(target.Server, target.Database, sql), new UTF8Encoding(true), token).ConfigureAwait(false);
        var start = new ProcessStartInfo(executable) { UseShellExecute = true };
        start.ArgumentList.Add("/Edit");
        start.ArgumentList.Add(path);
        token.ThrowIfCancellationRequested();
        using var process = Process.Start(start) ?? throw new IOException("Không thể mở Visual Studio.");
        return new { path, server = target.Server, database = target.Database };
    }
}
