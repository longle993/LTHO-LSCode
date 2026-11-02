using System.Xml.Linq;
using System.Xml;
using System.Text;

namespace LSCode.Core;

public sealed record Snippet(string Key, string Category, string Body, string Description = "", int Position = 0, string Param = "")
{
    public override string ToString() => $"{Category}  /  {Key}";
}
public static class SnippetImporter
{
    public static List<Snippet> Read(string text) => XmlTools.Parse(text).Descendants("snippet").Select(x => new Snippet(
        (string?)x.Element("key") ?? "Untitled", (string?)x.Element("type") ?? "User", (string?)x.Element("code") ?? "",
        (string?)x.Element("description") ?? "", int.TryParse((string?)x.Element("position"), out var n) ? n : 0, (string?)x.Element("param") ?? "")).ToList();
    public static string Write(IEnumerable<Snippet> snippets)
    {
        var document = new XDocument(new XElement("NewDataSet", snippets.Select(s =>
        new XElement("snippet", new XElement("type", s.Category), new XElement("key", s.Key), new XElement("position", s.Position),
            new XElement("param", s.Param), new XElement("description", s.Description), new XElement("code", s.Body)))));
        var builder = new StringBuilder();
        using (var writer = XmlWriter.Create(builder, new XmlWriterSettings { Indent = true, OmitXmlDeclaration = true, NewLineHandling = NewLineHandling.Entitize })) document.Save(writer);
        return builder.ToString();
    }
}
