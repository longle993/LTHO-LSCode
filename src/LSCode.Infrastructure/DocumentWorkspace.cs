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
        var snapshot = await DocumentStore.OpenAsync(path, ct);
        var id = Guid.NewGuid().ToString("N");
        var state = new State(snapshot, 1); snapshots[id] = state;
        return Info(id, state);
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
        if (!snapshots.TryGetValue(id, out var state) || state.Snapshot.Path.Length == 0) return false;
        try { return (await DocumentStore.OpenAsync(state.Snapshot.Path, ct)).Hash != state.Snapshot.Hash; }
        catch (FileNotFoundException) { return true; }
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
    public async Task<object> SearchAsync(string path, string query, bool regex, bool matchCase, Dictionary<string, string> buffers, CancellationToken ct)
    {
        var open=new Dictionary<string,string>(buffers,StringComparer.OrdinalIgnoreCase);
        var hits=await WorkspaceSearch.FindAsync(path,query,regex,matchCase,ct,open);
        if(query.Length==0||hits.Count>=2000)return hits;
        await Task.Run(async()=>{
            var pattern=new System.Text.RegularExpressions.Regex(regex?query:System.Text.RegularExpressions.Regex.Escape(query),matchCase?System.Text.RegularExpressions.RegexOptions.None:System.Text.RegularExpressions.RegexOptions.IgnoreCase,TimeSpan.FromMilliseconds(200));
            foreach(var file in WorkspaceFileIndex.Files(path,ct).Where(f=>WorkspaceFileIndex.ExtraTextExtensions.Contains(Path.GetExtension(f))))
            {
                ct.ThrowIfCancellationRequested();string text;
                try{text=open.TryGetValue(file,out var buffer)?buffer:(await DocumentStore.OpenAsync(file,ct)).Text;}catch(Exception ex)when(ex is IOException or UnauthorizedAccessException or System.Text.DecoderFallbackException){continue;}
                using var reader=new StringReader(text);var line=0;
                while(reader.ReadLine() is {} content){ct.ThrowIfCancellationRequested();line++;var match=pattern.Match(content);if(match.Success)hits.Add(new(file,line,match.Index+1,content.Length>220?content[..220]:content));if(hits.Count>=2000)return;}
            }
        },ct);
        return hits;
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
