using System.Collections.Concurrent;
using System.Text.Json;
using System.Text;
using LSCode.Application;
using LSCode.Core;
using LSCode.Domain;
using LSCode.Shell.Services;

namespace LSCode.Infrastructure;
public sealed class DocumentReadOnlyException(string path) : IOException("File đang có thuộc tính chỉ đọc: " + path);

public sealed class DocumentWorkspace : IDocumentWorkspace
{
    private sealed record State(FileSnapshot Snapshot, long Version);
    private readonly ConcurrentDictionary<string, State> snapshots = new();
    private readonly SemaphoreSlim writes = new(1);
    private static string Recovery => Path.Combine(SettingsStore.Root, "webview-recovery.json");

    private static DocumentInfo Info(string id, State state) => new(id, state.Snapshot.Path, Path.GetFileName(state.Snapshot.Path), state.Snapshot.Text,
        state.Snapshot.Encoding.WebName, state.Snapshot.Bom, state.Snapshot.Newline, state.Version);
    public async Task<DocumentInfo> OpenAsync(string path, CancellationToken ct)
    {
        await writes.WaitAsync(ct);
        try
        {
            var snapshot = await DocumentStore.OpenAsync(path, ct);
            var id = Guid.NewGuid().ToString("N");
            var state = new State(snapshot, 1); snapshots[id] = state;
            return Info(id, state);
        }
        finally { writes.Release(); }
    }
    public async Task<DocumentInfo> SaveAsync(string id, string? path, string text, long version, CancellationToken ct, bool overwriteReadOnly = false)
    {
        if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("Chưa chọn đường dẫn lưu.");
        await writes.WaitAsync(ct);
        try
        {
            snapshots.TryGetValue(id, out var previous);
            if (previous != null && version < previous.Version) throw new InvalidOperationException("Nội dung lưu đã cũ. Hãy thử lại từ tab hiện tại.");
            var fullPath = Path.GetFullPath(path);
            if (!string.Equals(previous?.Snapshot.Path, fullPath, StringComparison.OrdinalIgnoreCase) && snapshots.Any(pair => pair.Key != id && string.Equals(pair.Value.Snapshot.Path, fullPath, StringComparison.OrdinalIgnoreCase)))
                throw new IOException("File này đang mở trong tab khác.");
            // Save As carries the original encoding/BOM, while DocumentStore only checks hash for the same path.
            if (File.Exists(path) && (File.GetAttributes(path) & FileAttributes.ReadOnly) != 0 && !overwriteReadOnly) throw new DocumentReadOnlyException(path);
            var saved = await DocumentStore.SaveAsync(path, text, previous?.Snapshot, ct, overwriteReadOnly);
            var next = new State(saved, version + 1); snapshots[id] = next;
            return Info(id, next);
        }
        finally { writes.Release(); }
    }
    public async Task<bool> ChangedAsync(string id, CancellationToken ct)
    {
        await writes.WaitAsync(ct);
        try
        {
            if (!snapshots.TryGetValue(id, out var state) || state.Snapshot.Path.Length == 0) return false;
            try { return (await DocumentStore.OpenAsync(state.Snapshot.Path, ct)).Hash != state.Snapshot.Hash; }
            catch (FileNotFoundException) { return true; }
        }
        finally { writes.Release(); }
    }
    public async Task<string> RenameAsync(string path, string name, CancellationToken ct)
    {
        if (!Path.IsPathFullyQualified(path) || string.IsNullOrWhiteSpace(name) ||
            name != name.Trim() || name.EndsWith('.') || name is "." or ".." ||
            name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            throw new ArgumentException("Nhập một tên file hoặc thư mục hợp lệ, không gồm đường dẫn.");
        var source = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
        var parent = Path.GetDirectoryName(source) ?? throw new ArgumentException("Không thể đổi tên thư mục gốc.");
        var destination = Path.Combine(parent, name);
        await writes.WaitAsync(ct);
        try
        {
            ct.ThrowIfCancellationRequested();
            var directory = Directory.Exists(source);
            if (!directory && !File.Exists(source)) throw new FileNotFoundException("Không tìm thấy mục cần đổi tên.", source);
            if (source.Equals(destination, StringComparison.Ordinal)) return destination;
            var samePath = source.Equals(destination, StringComparison.OrdinalIgnoreCase);
            if (!samePath && (File.Exists(destination) || Directory.Exists(destination) ||
                snapshots.Values.Any(state => state.Snapshot.Path.Equals(destination, StringComparison.OrdinalIgnoreCase))))
                throw new IOException("Tên này đã tồn tại. Hãy chọn tên khác.");
            void Move(string from, string to)
            {
                if (directory) Directory.Move(from, to);
                else File.Move(from, to);
            }
            if (samePath)
            {
                var intermediate = Path.Combine(parent, ".lscode-rename-" + Guid.NewGuid().ToString("N"));
                Move(source, intermediate);
                try { Move(intermediate, destination); }
                catch
                {
                    Move(intermediate, source);
                    throw;
                }
            }
            else Move(source, destination);
            // Preserve the snapshot hash, encoding and version so dirty tabs can save at the new path.
            foreach (var (id, previous) in snapshots.ToArray())
            {
                var oldPath = previous.Snapshot.Path;
                if (oldPath.Equals(source, StringComparison.OrdinalIgnoreCase) ||
                    directory && oldPath.StartsWith(source + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                {
                    var next = previous with { Snapshot = previous.Snapshot with { Path = destination + oldPath[source.Length..] } };
                    snapshots.TryUpdate(id, next, previous);
                }
            }
            return destination;
        }
        finally { writes.Release(); }
    }
    public void Release(string id) => snapshots.TryRemove(id, out _);
    public DocumentInfo Restore(DocumentInfo document)
    {
        Encoding encoding = document.Encoding switch { "utf-16" => new UnicodeEncoding(false, document.Bom, true), "utf-16BE" => new UnicodeEncoding(true, document.Bom, true),
            "utf-32" => new UTF32Encoding(false, document.Bom, true), "utf-32BE" => new UTF32Encoding(true, document.Bom, true), _ => new UTF8Encoding(document.Bom, true) };
        var id = Guid.NewGuid().ToString("N");
        snapshots[id] = new(new FileSnapshot("", document.Text, encoding, document.Bom, ""), 1);
        return document with { Id = id, Path = null, Version = 1 };
    }
    public Task<Entry[]> ListAsync(string path, CancellationToken ct) => Task.Run(() => new DirectoryInfo(path).EnumerateFileSystemInfos()
        .Where(entry => { ct.ThrowIfCancellationRequested(); return (entry.Attributes & FileAttributes.ReparsePoint) == 0 && !WorkspaceSearch.IgnoredDirectories.Contains(entry.Name); })
        .Select(entry => new Entry(entry.FullName, entry.Name, entry is DirectoryInfo)).OrderByDescending(e => e.Directory).ThenBy(e => e.Name, StringComparer.OrdinalIgnoreCase).ToArray(), ct);
    public Task<string[]> FilesAsync(string path, CancellationToken ct) => Task.Run(() => WorkspaceFileIndex.Files(path, ct).Take(100000).ToArray(), ct);
    public async Task<object> SearchAsync(string path, string query, bool regex, bool matchCase,
        Dictionary<string, string> buffers, CancellationToken ct, bool firstPerFile = false, string fileName = "", IProgress<object>? progress = null)
    {
        var files = WorkspaceFileIndex.Files(path, ct, textOnly: query.Length > 0);
        if (fileName.Length > 0)
            files = files.Where(file => Path.GetFileName(file).Contains(fileName, StringComparison.OrdinalIgnoreCase));
        if (query.Length == 0)
        {
            if (fileName.Length == 0) return new List<SearchHit>();
            return await Task.Run(() =>
            {
                var results = new List<SearchHit>();
                var published = 0;
                var clock = System.Diagnostics.Stopwatch.StartNew();
                foreach (var file in files.Take(2000))
                {
                    results.Add(new SearchHit(file, 0, 0, ""));
                    if (progress is null || (published != 0 && clock.ElapsedMilliseconds < 80)) continue;
                    progress.Report(results.GetRange(published, results.Count - published));
                    published = results.Count;
                    clock.Restart();
                }
                return results;
            }, ct);
        }
        var open = new Dictionary<string, string>(buffers, StringComparer.OrdinalIgnoreCase);
        return await WorkspaceSearch.FindAsync(files, query, regex, matchCase, ct, open, firstPerFile, progress);
    }
    public async Task<object> RecoverAsync(CancellationToken ct) => File.Exists(Recovery) ? JsonDocument.Parse(await File.ReadAllTextAsync(Recovery, ct)).RootElement.Clone() : Array.Empty<object>();
    public async Task StoreRecoveryAsync(JsonElement documents, CancellationToken ct)
    {
        await writes.WaitAsync(ct);
        try
        {
            Directory.CreateDirectory(SettingsStore.Root);
            await File.WriteAllTextAsync(Recovery + ".tmp", documents.GetRawText(), ct);
            File.Move(Recovery + ".tmp", Recovery, true);
        }
        finally { writes.Release(); }
    }
}
