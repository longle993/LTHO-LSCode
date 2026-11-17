using System.Security.Cryptography;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace LSCode.Core;

public sealed record LegacyProject(string Id, string Name, string RawXml)
{
    public string Field(string key) => XmlTools.Parse(RawXml).Root?.Elements().FirstOrDefault(e => e.Name.LocalName.Equals(key, StringComparison.OrdinalIgnoreCase))?.Value ?? "";
    public string Server => Field("servername");
    public string Database => Field("df_Database");
    public string User => Field("User");
    public string SourcePath => Field("SourcePath");
    public override string ToString() => $"{Name} — {Server} / {Database}";
}
public sealed record ProjectImportResult(List<LegacyProject> Projects, int Added, int Updated, int Skipped);
public sealed record ProjectImportPreview(int Total, int Added, int Updated, int Skipped, string FileName);
public static class ProjectImporter
{
    public static List<LegacyProject> Read(string xml)
    {
        var document = XmlTools.Parse(xml);
        return document.Descendants().Where(e => e.Name.LocalName == "project").Select(FromElement).ToList();
    }
    private static LegacyProject FromElement(XElement element)
    {
        string Value(string name) => element.Elements().FirstOrDefault(e => e.Name.LocalName.Equals(name, StringComparison.OrdinalIgnoreCase))?.Value ?? "";
        var name = Value("FolderName").Trim();
        if (name.Length == 0) name = Value("AppData").Trim();
        if (name.Length == 0) name = Value("df_Database").Trim();
        if (name.Length == 0) name = "Untitled project";
        // Conservative identity; same display name on a different server is a distinct project.
        var identity = string.Join("\u001f", Value("servername").Trim().ToUpperInvariant(), Value("User").Trim().ToUpperInvariant(), name.ToUpperInvariant());
        var id = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(identity)))[..24];
        return new(id, name, element.ToString(SaveOptions.DisableFormatting));
    }

    public static List<LegacyProject> ReadFile(string path, CancellationToken cancellationToken = default)
    {
        path = ValidateXmlPath(path, mustExist: true);
        var settings = new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            IgnoreComments = true,
            IgnoreProcessingInstructions = true,
            MaxCharactersInDocument = 256L * 1024 * 1024
        };
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read,
            64 * 1024, FileOptions.SequentialScan);
        using var reader = XmlReader.Create(stream, settings);
        var projects = new List<LegacyProject>();
        while (reader.Read())
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (reader.NodeType != XmlNodeType.Element ||
                !reader.LocalName.Equals("project", StringComparison.OrdinalIgnoreCase)) continue;
            if (projects.Count >= 100_000)
                throw new InvalidDataException("Catalog vượt giới hạn 100.000 dự án.");
            projects.Add(FromElement((XElement)XNode.ReadFrom(reader)));
        }
        return projects;
    }

    public static ProjectImportPreview PreviewFile(IEnumerable<LegacyProject> existing, string path,
        bool overwrite, CancellationToken cancellationToken = default)
    {
        var incoming = ReadFile(path, cancellationToken);
        var merged = Merge(existing, incoming, overwrite);
        return new(incoming.Count, merged.Added, merged.Updated, merged.Skipped, Path.GetFileName(path));
    }
    public static ProjectImportResult Merge(IEnumerable<LegacyProject> existing, IEnumerable<LegacyProject> incoming, bool overwrite)
    {
        var result = existing.ToList();
        var added = 0; var updated = 0; var skipped = 0;
        var byId = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var byCode = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < result.Count; index++)
        {
            byId.TryAdd(result[index].Id, index);
            var code = ProjectCode(result[index]);
            if (code.Length > 0) byCode.TryAdd(code, index);
        }
        foreach (var item in incoming)
        {
            var code = ProjectCode(item);
            var index = byId.TryGetValue(item.Id, out var idIndex) ? idIndex :
                code.Length > 0 && byCode.TryGetValue(code, out var codeIndex) ? codeIndex : -1;
            if (index < 0)
            {
                index = result.Count;
                result.Add(item);
                byId[item.Id] = index;
                if (code.Length > 0) byCode[code] = index;
                added++;
            }
            else if (overwrite && result[index].RawXml != item.RawXml) { result[index] = item with { Id = result[index].Id }; updated++; }
            else skipped++;
        }
        return new(result.OrderBy(p => ProjectCode(p), StringComparer.OrdinalIgnoreCase).ToList(), added, updated, skipped);
    }
    public static string Export(IEnumerable<LegacyProject> projects, bool includeSensitive = false)
    {
        var root = new XElement("NewDataSet");
        foreach (var project in projects)
        {
            var element = new XElement(XmlTools.Parse(project.RawXml).Root!);
            if (!includeSensitive)
            {
                foreach (var field in element.Descendants().Where(e => IsSensitive(e.Name.LocalName)).ToList()) field.Value = "";
                foreach (var attribute in element.DescendantsAndSelf().Attributes().Where(a => IsSensitive(a.Name.LocalName)).ToList()) attribute.Value = "";
            }
            root.Add(element);
        }
        return new XDocument(root).ToString();
    }

    public static int ExportFile(IEnumerable<LegacyProject> projects, string path,
        bool includeSensitive = false, CancellationToken cancellationToken = default)
    {
        path = ValidateXmlPath(path, mustExist: false);
        var folder = Path.GetDirectoryName(path)!;
        Directory.CreateDirectory(folder);
        var temp = Path.Combine(folder, $".{Path.GetFileName(path)}.{Guid.NewGuid():N}.tmp");
        var count = 0;
        try
        {
            var settings = new XmlWriterSettings
            {
                Encoding = new UTF8Encoding(false),
                Indent = true,
                CloseOutput = true
            };
            using (var stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                64 * 1024, FileOptions.SequentialScan))
            using (var writer = XmlWriter.Create(stream, settings))
            {
                writer.WriteStartDocument();
                writer.WriteStartElement("NewDataSet");
                foreach (var project in projects)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var element = new XElement(XmlTools.Parse(project.RawXml).Root!);
                    if (!includeSensitive) Sanitize(element);
                    element.WriteTo(writer);
                    count++;
                }
                writer.WriteEndElement();
                writer.WriteEndDocument();
            }
            File.Move(temp, path, true);
            return count;
        }
        catch
        {
            try { File.Delete(temp); } catch (IOException) { }
            throw;
        }
    }

    private static void Sanitize(XElement element)
    {
        foreach (var field in element.Descendants().Where(e => IsSensitive(e.Name.LocalName)).ToList()) field.Value = "";
        foreach (var attribute in element.DescendantsAndSelf().Attributes().Where(a => IsSensitive(a.Name.LocalName)).ToList()) attribute.Value = "";
    }

    private static string ValidateXmlPath(string path, bool mustExist)
    {
        if (string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path))
            throw new ArgumentException("Đường dẫn catalog XML không hợp lệ.");
        path = Path.GetFullPath(path);
        if (!Path.GetExtension(path).Equals(".xml", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Catalog dự án phải là file XML.");
        if (mustExist && !File.Exists(path)) throw new FileNotFoundException("Không tìm thấy catalog XML.", path);
        return path;
    }
    public static bool IsSensitive(string name) => new[] { "password", "pwd", "secret", "token", "connectionstring", "apikey" }.Any(s => name.Contains(s, StringComparison.OrdinalIgnoreCase));
    private static string ProjectCode(LegacyProject project) => project.Field("StandardProjectCode").Trim() is { Length: > 0 } code ? code : project.Name.Trim();
    public static LegacyProject Create(string name, string server, string database, string sourcePath) => FromElement(new XElement("project",
        new XElement("FolderName", name), new XElement("servername", server), new XElement("df_Database", database), new XElement("SourcePath", sourcePath),
        new XElement("User", ""), new XElement("Password", ""), new XElement("AppData", ""), new XElement("WorkingPath", ""), new XElement("ProgramPath", "")));
}
