using System.Security.Cryptography;
using System.Text;
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
    public static ProjectImportResult Merge(IEnumerable<LegacyProject> existing, IEnumerable<LegacyProject> incoming, bool overwrite)
    {
        var result = existing.ToList();
        var added = 0; var updated = 0; var skipped = 0;
        foreach (var item in incoming)
        {
            var index = result.FindIndex(saved => saved.Id.Equals(item.Id, StringComparison.OrdinalIgnoreCase) ||
                ProjectCode(saved).Equals(ProjectCode(item), StringComparison.OrdinalIgnoreCase));
            if (index < 0) { result.Add(item); added++; }
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
    public static bool IsSensitive(string name) => new[] { "password", "pwd", "secret", "token", "connectionstring", "apikey" }.Any(s => name.Contains(s, StringComparison.OrdinalIgnoreCase));
    private static string ProjectCode(LegacyProject project) => project.Field("StandardProjectCode").Trim() is { Length: > 0 } code ? code : project.Name.Trim();
    public static LegacyProject Create(string name, string server, string database, string sourcePath) => FromElement(new XElement("project",
        new XElement("FolderName", name), new XElement("servername", server), new XElement("df_Database", database), new XElement("SourcePath", sourcePath),
        new XElement("User", ""), new XElement("Password", ""), new XElement("AppData", ""), new XElement("WorkingPath", ""), new XElement("ProgramPath", "")));
}
