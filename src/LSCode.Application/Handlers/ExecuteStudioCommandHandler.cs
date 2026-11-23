using System.Text.Json;
using LSCode.Application.Commands;
using LSCode.Domain;
using MediatR;

namespace LSCode.Application.Handlers;

internal sealed class ExecuteStudioCommandHandler(
    IDocumentWorkspace documents,
    IDeveloperTools tools,
    IDesktopDialogs dialogs) : IRequestHandler<ExecuteStudioCommand, object?>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<object?> Handle(ExecuteStudioCommand request, CancellationToken cancellationToken)
    {
        var method = request.Method;
        var payload = request.Payload;
        string Text(string key, string fallback = "") =>
            payload.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.String
                ? value.GetString()!
                : fallback;
        bool Flag(string key) => payload.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.True;

        switch (method)
        {
            case "document.open":
                var open = Text("path");
                if (open.Length == 0) open = dialogs.OpenFile() ?? "";
                return open.Length == 0 ? null : await documents.OpenAsync(open, cancellationToken);
            case "document.openMany":
                var opened = new List<DocumentInfo>();
                foreach (var file in dialogs.OpenFiles())
                    opened.Add(await documents.OpenAsync(file, cancellationToken));
                return opened;
            case "file.pick":
                return dialogs.OpenFile();
            case "project.import.choose":
                return dialogs.OpenFile("LSCode Project Catalog (*.xml)|*.xml|Tất cả file|*.*");
            case "project.export.choose":
                return dialogs.SaveFile(Text("name", "projects.xml"), "LSCode Project Catalog (*.xml)|*.xml");
            case "document.save":
                var path = Text("path");
                if (Flag("saveAs") || path.Length == 0) path = dialogs.SaveFile(Text("name", path));
                return string.IsNullOrEmpty(path)
                    ? null
                    : await documents.SaveAsync(Text("id"), path, Text("text"), payload.GetProperty("version").GetInt64(), cancellationToken, Flag("overwriteReadOnly"));
            case "requestTracking.exportTemplate":
                var templateData = Text("base64");
                if (templateData.Length > 256_000) throw new ArgumentException("Tệp mẫu vượt giới hạn kích thước.");
                var templateBytes = Convert.FromBase64String(templateData);
                var templatePath = dialogs.SaveFile("mau-nhan-vien-qlyc.xlsx", "Excel (*.xlsx)|*.xlsx");
                if (templatePath is null) return false;
                await File.WriteAllBytesAsync(templatePath, templateBytes, cancellationToken);
                return true;
            case "text.export":
                var exportPath = dialogs.SaveFile(Text("name", "export.txt"));
                if (exportPath == null) return null;
                var exportId = Guid.NewGuid().ToString("N");
                try
                {
                    await documents.SaveAsync(exportId, exportPath, Text("text"), 1, cancellationToken);
                    return exportPath;
                }
                finally
                {
                    documents.Release(exportId);
                }
            case "document.changed":
                return await documents.ChangedAsync(Text("id"), cancellationToken);
            case "document.close":
                documents.Release(Text("id"));
                return true;
            case "document.restore":
                return documents.Restore(payload.Deserialize<DocumentInfo>(Json)
                    ?? throw new ArgumentException("Recovery không hợp lệ."));
            case "workspace.folder":
                return dialogs.OpenFolder();
            case "workspace.rename":
                return await Task.Run(
                    () => documents.RenameAsync(Text("path"), Text("name"), cancellationToken),
                    cancellationToken);
            case "workspace.list":
                return await documents.ListAsync(Text("path"), cancellationToken);
            case "workspace.files":
                return await documents.FilesAsync(Text("path"), cancellationToken);
            case "workspace.search":
                var buffers = payload.TryGetProperty("buffers", out var bufferElement)
                    ? bufferElement.Deserialize<Dictionary<string, string>>(Json) ?? []
                    : [];
                return await documents.SearchAsync(Text("path"), Text("query"), Flag("regex"), Flag("matchCase"), buffers,
                    cancellationToken, Flag("firstPerFile"));
            case "recovery.load":
                return await documents.RecoverAsync(cancellationToken);
            case "recovery.save":
                await documents.StoreRecoveryAsync(payload.GetProperty("documents"), cancellationToken);
                return true;
            default:
                return await Task.Run(
                    () => tools.ExecuteAsync(method, payload, cancellationToken),
                    cancellationToken);
        }
    }
}
