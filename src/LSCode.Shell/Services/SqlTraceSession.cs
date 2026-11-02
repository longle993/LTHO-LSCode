using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using Microsoft.Data.SqlClient;
namespace LSCode.Shell.Services;

// A separate x86 Framework process hosts the SSMS 18 TraceServer COM dependencies.
public sealed class SqlTraceSession(SqlTarget target, ProfilerTemplate template)
{
    public string Name => "SSMS 18 / " + Path.GetFileName(template.TraceTemplatePath);
    private Process? process;
    private Task? pump;
    private readonly ConcurrentQueue<ProfilerEvent> queue = new();
    private readonly TaskCompletionSource ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private Exception? error;
    private long dropped;
    private bool stopRequested;
    private bool cleanupConfirmed;
    public static string DefaultProfilerPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Microsoft SQL Server Management Studio 18", "Common7", "PROFILER.EXE");
    public static string ResolveLibraryFolder(string profilerPath) => Path.Combine(Path.GetDirectoryName(Path.GetFullPath(profilerPath.Trim().Trim('"')))!, "IDE");
    public static string LibraryFolder => ResolveLibraryFolder(string.IsNullOrWhiteSpace(SettingsStore.Load().Profiler18Path) ? DefaultProfilerPath : SettingsStore.Load().Profiler18Path);
    public static string ProfilerPath() => string.IsNullOrWhiteSpace(SettingsStore.Load().Profiler18Path) ? DefaultProfilerPath : SettingsStore.Load().Profiler18Path;
    public static void ValidateProfilerPath(string profilerPath)
    {
        profilerPath = profilerPath.Trim().Trim('"');
        if (!File.Exists(profilerPath) || !Path.GetFileName(profilerPath).Equals("PROFILER.EXE", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Chọn PROFILER.EXE của phiên bản SSMS cần dùng trong Tools → External tools settings.");
        var library = ResolveLibraryFolder(profilerPath);
        foreach (var file in new[] { Path.Combine(library, "Microsoft.SqlServer.ConnectionInfoExtended.dll"), Path.Combine(library, "Microsoft.SqlServer.ConnectionInfo.dll"), Path.Combine(Path.GetDirectoryName(library)!, "PFCLNT.DLL") })
            if (!File.Exists(file)) throw new InvalidOperationException("Thiếu thư viện: " + file + ". Chọn lại PROFILER.EXE trong Tools → External tools settings.");
    }
    public static string StandardTemplate => Path.Combine(Path.GetDirectoryName(LibraryFolder)!, "Profiler", "Templates", "Microsoft SQL Server", "120", "Standard.tdf");
    internal static string PrepareHost(string sourceHost, string libraryFolder, string cacheRoot)
    {
        var definitions = Path.Combine(Path.GetDirectoryName(libraryFolder)!, "Profiler", "TraceDefinitions");
        if (!Directory.Exists(definitions)) throw new IOException("Thiếu thư mục TraceDefinitions của SSMS: " + definitions);
        var files = Directory.GetFiles(definitions, "*", SearchOption.AllDirectories).Order(StringComparer.OrdinalIgnoreCase).ToArray();
        if (!files.Any(f => Path.GetFileName(f).StartsWith("Microsoft SQL Server TraceDefinition ", StringComparison.OrdinalIgnoreCase)))
            throw new IOException("TraceDefinitions không có định nghĩa SQL Server: " + definitions);
        static string Hash(string file) { using var stream = File.OpenRead(file); return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(stream)); }
        var manifest = Hash(sourceHost) + libraryFolder + string.Join("", files.Select(f => Path.GetRelativePath(definitions, f) + Hash(f)));
        var key = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(manifest)))[..24];
        var runtime = Path.Combine(cacheRoot, "trace-runtime", key);
        void Copy(string source, string destination)
        {
            if (File.Exists(destination) && Hash(source) == Hash(destination)) return;
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            var temporary = destination + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try { File.Copy(source, temporary); File.Move(temporary, destination, true); }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }
        var host = Path.Combine(runtime, "LSCode.TraceHost.exe"); Copy(sourceHost, host);
        foreach (var file in files) Copy(file, Path.Combine(runtime, "Profiler", "TraceDefinitions", Path.GetRelativePath(definitions, file)));
        return host;
    }
    public async Task StartAsync(CancellationToken token)
    {
        if (string.IsNullOrWhiteSpace(template.TraceTemplatePath) || !File.Exists(template.TraceTemplatePath))
            throw new InvalidOperationException("Không tìm thấy template SQL Profiler (.tdf). Chọn file trong Template… hoặc khôi phục Template/SQL2014.tdf đi kèm ứng dụng.");
        var configuredPath = ProfilerPath();
        ValidateProfilerPath(configuredPath);
        var libraryFolder = ResolveLibraryFolder(configuredPath);
        // PFCLNT resolves TraceDefinitions relative to the helper executable, not its DLL or working directory.
        var host = await Task.Run(() => PrepareHost(Path.Combine(AppContext.BaseDirectory,"LSCode.TraceHost.exe"), libraryFolder, SettingsStore.Root), token);
        token.ThrowIfCancellationRequested();
        var info = new ProcessStartInfo(host) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true, StandardInputEncoding = new UTF8Encoding(false), StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8 };
        var request = BuildRequest(target, template);
        process = Process.Start(info) ?? throw new InvalidOperationException("Không khởi động được SQL Trace reader.");
        await process.StandardInput.WriteLineAsync(request); await process.StandardInput.FlushAsync();
        pump = PumpAsync(process);
        await ready.Task.WaitAsync(TimeSpan.FromSeconds(30), token);
    }
    internal static string BuildRequest(SqlTarget target, ProfilerTemplate template)
    {
        var b = new SqlConnectionStringBuilder(target.ConnectionString);
        if (b.Encrypt == SqlConnectionEncryptOption.Strict)
            throw new InvalidOperationException("SQL Trace không hỗ trợ Encrypt=Strict. Hãy dùng Encrypt=True hoặc False.");
        // Pass values, not a Microsoft.Data.SqlClient string whose keywords differ from Framework.
        return JsonSerializer.Serialize(new Dictionary<string,string> {
            ["ProfilerVersion"]="18",
            ["Server"]=b.DataSource, ["IntegratedSecurity"]=b.IntegratedSecurity.ToString(),
            ["User"]=b.UserID, ["Password"]=b.Password,
            ["TrustServerCertificate"]=b.TrustServerCertificate.ToString(),
            ["Encrypt"]=(b.Encrypt == SqlConnectionEncryptOption.Mandatory).ToString(),
            ["LibraryFolder"]=ResolveLibraryFolder(ProfilerPath()), ["TemplatePath"]=template.TraceTemplatePath ?? "",
            ["Database"]=template.DatabaseFilter ?? LSCode.Core.ProfilerDatabaseFilter.Prefix(target.Database)
        });
    }
    private async Task PumpAsync(Process child)
    {
        var stderr = child.StandardError.ReadToEndAsync();
        try
        {
            while (await child.StandardOutput.ReadLineAsync() is { } line)
            {
                using var json = JsonDocument.Parse(line); var kind=json.RootElement.GetProperty("Kind").GetString();
                if (kind=="ready") ready.TrySetResult();
                else if (kind=="stopped") cleanupConfirmed = true;
                else if (kind=="error") { error=new InvalidOperationException(json.RootElement.GetProperty("Message").GetString()); ready.TrySetException(error); }
                else if (kind=="event")
                {
                    queue.Enqueue(JsonSerializer.Deserialize<ProfilerEvent>(line)!);
                    while(queue.Count>5000 && queue.TryDequeue(out _)) Interlocked.Increment(ref dropped);
                }
            }
            await child.WaitForExitAsync(); var diagnostic=await stderr;
            if(child.ExitCode!=0 && error==null) error=new InvalidOperationException("SQL Trace reader kết thúc lỗi ("+child.ExitCode+"). "+diagnostic);
            if(!stopRequested && error==null) error=new InvalidOperationException("SQL Trace đã kết thúc trên server.");
        }
        catch(Exception ex) { error=ex; }
        finally { if(!ready.Task.IsCompleted) ready.TrySetException(error ?? new IOException("SQL Trace reader đã đóng.")); }
    }
    public Task<ProfilerBatch> ReadAsync(CancellationToken token)
    {

        token.ThrowIfCancellationRequested(); if(error!=null) throw error;
        var rows=new List<ProfilerEvent>(); while(rows.Count<5000 && queue.TryDequeue(out var row)) rows.Add(row);
        return Task.FromResult(new ProfilerBatch(rows,false,Interlocked.Read(ref dropped)));
    }
    public async Task DropAsync()
    {

        if(process==null) return;
        stopRequested=true;
        if(!process.HasExited)
        {
            try { await process.StandardInput.WriteLineAsync("stop"); await process.StandardInput.FlushAsync(); } catch(IOException) { }
            await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(15));
        }
        if(pump!=null) await pump;
        // Reader exit code 2 explicitly means cleanup could not be confirmed.
        if(process.ExitCode==2 || (ready.Task.IsCompletedSuccessfully && (!cleanupConfirmed || process.ExitCode!=0))) throw error ?? new IOException("Không xác nhận được việc đóng SQL Trace.");
        process.Dispose(); process=null;
    }
}
