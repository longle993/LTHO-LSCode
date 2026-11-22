using System.Text.Json;
using LSCode.Domain;

namespace LSCode.Application;

public interface IDocumentWorkspace
{
    Task<DocumentInfo> OpenAsync(string path, CancellationToken cancellationToken);
    Task<DocumentInfo> SaveAsync(string id, string? path, string text, long version, CancellationToken cancellationToken, bool overwriteReadOnly = false);
    Task<bool> ChangedAsync(string id, CancellationToken cancellationToken);
    void Release(string id);
    DocumentInfo Restore(DocumentInfo document);
    Task<Entry[]> ListAsync(string path, CancellationToken cancellationToken);
    Task<string[]> FilesAsync(string path, CancellationToken cancellationToken);
    Task<object> SearchAsync(string path, string query, bool regex, bool matchCase, Dictionary<string, string> buffers, CancellationToken cancellationToken, bool firstPerFile = false);
    Task<object> RecoverAsync(CancellationToken cancellationToken);
    Task StoreRecoveryAsync(JsonElement documents, CancellationToken cancellationToken);
    Task<string> RenameAsync(string path, string name, CancellationToken cancellationToken);
}
