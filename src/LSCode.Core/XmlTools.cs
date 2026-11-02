using System.Xml;
using System.Xml.Linq;

namespace LSCode.Core;

public sealed record XmlDiagnostic(string Message, int Line, int Column);
public static class XmlTools
{
    public static XDocument Parse(string text)
    {
        using var reader = XmlReader.Create(new StringReader(text), new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null });
        return XDocument.Load(reader, LoadOptions.SetLineInfo | LoadOptions.PreserveWhitespace);
    }
    public static XmlDiagnostic? Validate(string text)
    {
        try { Parse(text); return null; }
        catch (XmlException ex) { return new(ex.Message, ex.LineNumber, ex.LinePosition); }
    }
    public static string Format(string text)
    {
        XDocument document;
        try { document = Parse(text); }
        catch (XmlException)
        {
            return FormatLegacyText(text);
        }
        // Preserve mixed content and CDATA; remove indentation only in element-only content.
        foreach (var node in document.Descendants().Where(e => e.HasElements && !e.Nodes().OfType<XText>().Any(t => !string.IsNullOrWhiteSpace(t.Value))).ToList())
            if (!node.AncestorsAndSelf().Any(e => (string?)e.Attribute(XNamespace.Xml + "space") == "preserve"))
                foreach (var whitespace in node.Nodes().OfType<XText>().Where(t => t is not XCData && string.IsNullOrWhiteSpace(t.Value)).ToList()) whitespace.Remove();
        return (document.Declaration is null ? "" : document.Declaration + Environment.NewLine) + document.ToString();
    }
    private static string FormatLegacyText(string text)
    {
        var lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        var depth = 0;
        var output = new List<string>(lines.Length);
        foreach (var raw in lines)
        {
            var line = raw.Trim();
            if (line.Length == 0) { output.Add(""); continue; }
            if (line.StartsWith("</", StringComparison.Ordinal) || line.StartsWith("%", StringComparison.Ordinal)) depth = Math.Max(0, depth - 1);
            output.Add(new string(' ', depth * 4) + line);
            if (line.StartsWith("<", StringComparison.Ordinal) && !line.StartsWith("</", StringComparison.Ordinal) &&
                !line.StartsWith("<!", StringComparison.Ordinal) && !line.StartsWith("<?", StringComparison.Ordinal) &&
                !line.Contains("</", StringComparison.Ordinal) && !line.EndsWith("/>", StringComparison.Ordinal)) depth++;
        }
        return string.Join(Environment.NewLine, output);
    }
}
