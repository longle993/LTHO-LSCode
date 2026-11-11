using System.Text.Json;
using LSCode.Domain;

namespace LSCode.Application;

public interface IDocumentWorkspace
{
    Task<DocumentInfo> OpenAsync(string path, CancellationToken ct);
    Task<DocumentInfo> SaveAsync(string id, string? path, string text, long version, CancellationToken ct, bool overwriteReadOnly = false);
    Task<bool> ChangedAsync(string id, CancellationToken ct);
    void Release(string id);
    DocumentInfo Restore(DocumentInfo document);
    Task<Entry[]> ListAsync(string path, CancellationToken ct);
    Task<string[]> FilesAsync(string path, CancellationToken ct);
    Task<object> SearchAsync(string path, string query, bool regex, bool matchCase, Dictionary<string, string> buffers, CancellationToken ct);
    Task<object> RecoverAsync(CancellationToken ct);
    Task StoreRecoveryAsync(JsonElement documents, CancellationToken ct);
    Task<string> RenameAsync(string path, string name, CancellationToken ct);
}
public interface IDeveloperTools
{
    Task<object?> ExecuteAsync(string method, JsonElement payload, CancellationToken ct);
}
public interface IDesktopDialogs
{
    string? OpenFile();
    string[] OpenFiles();
    string? OpenFolder();
    string? SaveFile(string? name);
}

// All transport adapters share the same application command boundary.
public sealed class StudioCommands(IDocumentWorkspace documents, IDeveloperTools tools, IDesktopDialogs dialogs)
{
    public async Task<object?> ExecuteAsync(string method, JsonElement p, CancellationToken ct)
    {
        string S(string key, string fallback = "") => p.TryGetProperty(key, out var x) && x.ValueKind == JsonValueKind.String ? x.GetString()! : fallback;
        bool B(string key) => p.TryGetProperty(key, out var x) && x.ValueKind == JsonValueKind.True;
        switch (method)
        {
            case "document.open":
                var open = S("path");
                if (open.Length == 0) open = dialogs.OpenFile() ?? "";
                return open.Length == 0 ? null : await documents.OpenAsync(open, ct);
            case "document.openMany":
                var opened = new List<DocumentInfo>();
                foreach (var file in dialogs.OpenFiles()) opened.Add(await documents.OpenAsync(file, ct));
                return opened;
            case "file.pick": return dialogs.OpenFile();
            case "document.save":
                var path = S("path");
                if (B("saveAs") || path.Length == 0) path = dialogs.SaveFile(S("name", path));
                return string.IsNullOrEmpty(path) ? null : await documents.SaveAsync(S("id"), path, S("text"), p.GetProperty("version").GetInt64(), ct, B("overwriteReadOnly"));
            case "text.export":
                var exportPath = dialogs.SaveFile(S("name", "export.txt"));
                if (exportPath == null) return null;
                var exportId = Guid.NewGuid().ToString("N");
                try { await documents.SaveAsync(exportId, exportPath, S("text"), 1, ct); return exportPath; }
                finally { documents.Release(exportId); }
            case "document.changed": return await documents.ChangedAsync(S("id"), ct);
            case "document.close": documents.Release(S("id")); return true;
            case "document.restore": return documents.Restore(p.Deserialize<DocumentInfo>(new JsonSerializerOptions(JsonSerializerDefaults.Web)) ?? throw new ArgumentException("Recovery không hợp lệ."));
            case "workspace.folder": return dialogs.OpenFolder();
            case "workspace.rename": return await Task.Run(() => documents.RenameAsync(S("path"), S("name"), ct), ct);
            case "workspace.list": return await documents.ListAsync(S("path"), ct);
            case "workspace.files": return await documents.FilesAsync(S("path"), ct);
            case "workspace.search":
                var buffers = p.TryGetProperty("buffers", out var b) ? b.Deserialize<Dictionary<string, string>>() ?? [] : [];
                return await documents.SearchAsync(S("path"), S("query"), B("regex"), B("matchCase"), buffers, ct);
            case "recovery.load": return await documents.RecoverAsync(ct);
            case "recovery.save": await documents.StoreRecoveryAsync(p.GetProperty("documents"), ct); return true;
            default: return await Task.Run(() => tools.ExecuteAsync(method, p, ct), ct);
        }
    }
}
