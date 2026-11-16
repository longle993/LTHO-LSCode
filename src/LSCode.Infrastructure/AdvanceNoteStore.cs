using System.Security.Cryptography;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using LSCode.Shell.Services;

namespace LSCode.Infrastructure;

public sealed record AdvanceNoteItem(string Id, string Title, string Status, string Details,
    string MenuId, string MenuCaption, string SysId, string Link, string[] SourceFiles, DateTimeOffset UpdatedAt,
    Dictionary<string, AdvanceNoteSql>? Sql = null);

public sealed record AdvanceNoteSql(string Objects, string Data, string TopScript);

public sealed record AdvanceNoteDocument(string ProjectId, AdvanceNoteItem[] Items, string FilePath);

public static class AdvanceNoteStore
{
    private static readonly SemaphoreSlim Gate = new(1, 1);

    public static async Task<AdvanceNoteDocument> LoadAsync(string projectId, CancellationToken cancellationToken)
    {
        projectId = Required(projectId, "Dự án");
        await Gate.WaitAsync(cancellationToken);
        try
        {
            var path = PathFor(projectId);
            if (!File.Exists(path)) return new(projectId, [], path);
            await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 16 * 1024, true);
            using var reader = XmlReader.Create(stream, new XmlReaderSettings
            {
                Async = true,
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null,
                MaxCharactersInDocument = 16 * 1024 * 1024,
            });
            var document = await XDocument.LoadAsync(reader, LoadOptions.None, cancellationToken);
            var root = document.Root ?? throw new InvalidDataException("File Advance Note không có phần tử gốc.");
            if (root.Name.LocalName != "advanceNotes") throw new InvalidDataException("File Advance Note không đúng định dạng.");
            if (!string.Equals((string?)root.Attribute("projectId"), projectId, StringComparison.Ordinal))
                throw new InvalidDataException("File Advance Note không thuộc dự án đang mở.");
            return new(projectId, root.Elements("request").Select(ReadItem)
                .OrderByDescending(item => item.UpdatedAt).ToArray(), path);
        }
        catch (XmlException exception)
        {
            throw new InvalidDataException("Không đọc được XML Advance Note. " + exception.Message, exception);
        }
        finally { Gate.Release(); }
    }

    public static async Task<AdvanceNoteDocument> SaveAsync(string projectId,
        IReadOnlyCollection<AdvanceNoteItem> items, CancellationToken cancellationToken)
    {
        projectId = Required(projectId, "Dự án");
        ValidateItems(items);
        await Gate.WaitAsync(cancellationToken);
        try
        {
            Directory.CreateDirectory(SettingsStore.Root);
            var path = PathFor(projectId);
            var temporaryPath = path + ".tmp";
            var ordered = items.OrderByDescending(item => item.UpdatedAt).ToArray();
            var document = new XDocument(new XElement("advanceNotes",
                new XAttribute("version", "1"), new XAttribute("projectId", projectId), ordered.Select(WriteItem)));
            try
            {
                {
                    await using var stream = new FileStream(temporaryPath, FileMode.Create, FileAccess.Write, FileShare.None, 16 * 1024, true);
                    await using var writer = XmlWriter.Create(stream, new XmlWriterSettings
                    {
                        Async = true, Encoding = new UTF8Encoding(true), Indent = true, NewLineChars = "\r\n",
                    });
                    document.Save(writer);
                    await writer.FlushAsync();
                    await stream.FlushAsync(cancellationToken);
                }
                File.Move(temporaryPath, path, true);
            }
            finally { if (File.Exists(temporaryPath)) File.Delete(temporaryPath); }
            return new(projectId, ordered, path);
        }
        finally { Gate.Release(); }
    }

    private static AdvanceNoteItem ReadItem(XElement element)
    {
        var updatedText = (string?)element.Attribute("updatedAt");
        return new(Required((string?)element.Attribute("id"), "ID yêu cầu"),
            element.Element("title")?.Value ?? "", (string?)element.Attribute("status") ?? "Mới",
            element.Element("details")?.Value ?? "", (string?)element.Attribute("menuId") ?? "",
            (string?)element.Attribute("menuCaption") ?? "", (string?)element.Attribute("sysId") ?? "",
            (string?)element.Attribute("link") ?? "",
            element.Element("sources")?.Elements("file").Select(file => (string?)file.Attribute("path") ?? "")
                .Where(path => path.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToArray() ?? [],
            DateTimeOffset.TryParse(updatedText, out var updatedAt) ? updatedAt : DateTimeOffset.MinValue,
            element.Elements("database").ToDictionary(db => (string?)db.Attribute("name") ?? "App",
                db => new AdvanceNoteSql(db.Element("objects")?.Value ?? "", db.Element("data")?.Value ?? "",
                    db.Element("topScript")?.Value ?? "")));
    }

    private static XElement WriteItem(AdvanceNoteItem item) => new("request",
        new XAttribute("id", item.Id), new XAttribute("status", item.Status),
        new XAttribute("updatedAt", item.UpdatedAt.ToUniversalTime().ToString("O")),
        Attribute("menuId", item.MenuId), Attribute("menuCaption", item.MenuCaption),
        Attribute("sysId", item.SysId), Attribute("link", item.Link),
        new XElement("title", item.Title), new XElement("details", item.Details),
        item.Sql?.Select(pair => new XElement("database", new XAttribute("name", pair.Key),
            new XElement("objects", pair.Value.Objects), new XElement("data", pair.Value.Data),
            new XElement("topScript", pair.Value.TopScript))),
        new XElement("sources", item.SourceFiles.Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(path => new XElement("file", new XAttribute("path", path)))));

    private static XAttribute? Attribute(string name, string value) => value.Length == 0 ? null : new(name, value);

    private static void ValidateItems(IReadOnlyCollection<AdvanceNoteItem> items)
    {
        if (items.Count > 10_000) throw new InvalidDataException("Advance Note vượt quá 10.000 yêu cầu.");
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in items)
        {
            Required(item.Id, "ID yêu cầu");
            Required(item.Title, "Tiêu đề yêu cầu");
            Required(item.Status, "Trạng thái yêu cầu");
            if (!ids.Add(item.Id)) throw new InvalidDataException("ID yêu cầu bị trùng: " + item.Id);
        }
    }

    private static string Required(string? value, string label) =>
        string.IsNullOrWhiteSpace(value) ? throw new InvalidDataException(label + " không được để trống.") : value.Trim();

    private static string PathFor(string projectId)
    {
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(projectId)))[..20];
        return Path.Combine(SettingsStore.Root, "advance-notes-" + hash + ".xml");
    }
}
