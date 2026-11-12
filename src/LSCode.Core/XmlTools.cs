using System.Xml;
using System.Xml.Linq;
using System.Text.RegularExpressions;

namespace LSCode.Core;

public sealed record XmlDiagnostic(string Message, int Line, int Column);
public sealed record XmlEditorDiagnostics(XmlDiagnostic? Diagnostic, IReadOnlyList<XmlDiagnostic> Warnings);
public static class XmlTools
{
    internal static XDocument ParseEditor(string text, string? path = null, IReadOnlyDictionary<string, string>? buffers = null)
    {
        var hasPath = !string.IsNullOrEmpty(path) && Path.IsPathFullyQualified(path);
        using var reader = XmlReader.Create(new StringReader(text.TrimStart('\uFEFF')), new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Parse,
            XmlResolver = hasPath ? new XmlIncludes.EditorResolver(path!, buffers) : null,
            MaxCharactersFromEntities = 1_000_000,
        }, hasPath ? new Uri(Path.GetFullPath(path!)).AbsoluteUri : "");
        return XDocument.Load(reader, LoadOptions.SetLineInfo);
    }

    public static IReadOnlyList<SqlSuggestion> ScriptFields(string text, string beforeCaret)
    {
        if (text.Length > 1_000_000) return [];
        var suffix = beforeCaret.Length > 4000 ? beforeCaret[^4000..] : beforeCaret;
        var argument = Regex.Match(suffix,
            """(?<![\w$.])[fg]\s*\.\s*[\w$]+\s*\((?:[^()"']|"(?:[^"\\\r\n]|\\.)*"|'(?:[^'\\\r\n]|\\.)*')*?['"](?<prefix>[\w%]*)$""",
            RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(50));
        if (!argument.Success) return [];
        XDocument document;
        try { document = ParseEditor(text); }
        catch (XmlException) { return []; }
        var prefix = argument.Groups["prefix"].Value;
        return document.Descendants().Where(node => node.Name.LocalName.Equals("views", StringComparison.OrdinalIgnoreCase))
            .SelectMany(node => node.Descendants().SelectMany(item =>
            {
                var explicitField = (string?)item.Attribute("field") ??
                    (item.Name.LocalName.Equals("field", StringComparison.OrdinalIgnoreCase) ? (string?)item.Attribute("name") : null);
                if (explicitField is not null) return new[] { explicitField };
                var value = (string?)item.Attribute("value") ?? "";
                var mask = XmlViewWidths.LayoutMask.Match(value);
                return mask.Success ? XmlViewWidths.LayoutFields.Matches(value[mask.Length..])
                    .Where(XmlViewWidths.IsField).Select(match => match.Groups["name"].Value) : [];
            }))
            .Select(XmlViewWidths.FieldName).Where(name => name.Length > 0 && name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.OrdinalIgnoreCase).Take(100)
            .Select(name => new SqlSuggestion(name, name, "Field trong <views>")).ToArray();
    }

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
    public static XmlEditorDiagnostics InspectEditor(string text, string? path = null, IReadOnlyDictionary<string, string>? buffers = null)
    {
        if (text.Length > 1_000_000) return new(null, []);
        var hasBom = text.StartsWith('\uFEFF');
        XDocument document;
        try
        {
            document = ParseEditor(text, path, buffers);
        }
        catch (XmlException ex)
        {
            return new(new(ex.Message, Math.Max(1, ex.LineNumber), Math.Max(1, ex.LinePosition) +
                (hasBom && ex.LineNumber == 1 ? 1 : 0)), []);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return new(new($"Không đọc được include XML: {ex.Message}", 1, 1), []);
        }

        var warnings = new List<XmlDiagnostic>();
        void Warn(XObject node, string message)
        {
            var position = (IXmlLineInfo)node;
            if (position.HasLineInfo()) warnings.Add(new(message, position.LineNumber,
                position.LinePosition + (hasBom && position.LineNumber == 1 ? 1 : 0)));
        }
        static bool Named(XElement node, string name) =>
            node.Name.LocalName.Equals(name, StringComparison.OrdinalIgnoreCase);
        static XAttribute? Attribute(XElement node, string name) =>
            node.Attributes().FirstOrDefault(attribute =>
                attribute.Name.LocalName.Equals(name, StringComparison.OrdinalIgnoreCase));
        var comparer = StringComparer.OrdinalIgnoreCase;
        foreach (var owner in document.Descendants().Where(node => node.Elements().Any(child => Named(child, "views"))))
        {
            var fields = owner.Elements().Where(node => Named(node, "fields"))
                .SelectMany(node => node.Elements().Where(field => Named(field, "field")))
                .Select(node => Attribute(node, "name")).OfType<XAttribute>().ToArray();
            var visible = new HashSet<string>(comparer);
            var viewIds = new HashSet<string>(comparer);
            foreach (var view in owner.Elements().Where(node => Named(node, "views"))
                .SelectMany(node => node.Elements().Where(view => Named(view, "view"))))
            {
                var id = Attribute(view, "id");
                if (id is not null && !string.IsNullOrWhiteSpace(id.Value) && !viewIds.Add(id.Value.Trim()))
                    Warn(id, $"View '{id.Value}' bị trùng id.");
                var used = new HashSet<string>(comparer);
                void CheckReference(string name, XAttribute location)
                {
                    name = XmlViewWidths.FieldName(name);
                    if (name.Length == 0) return;
                    visible.Add(name);
                    if (!used.Add(name)) Warn(location, $"Field '{name}' xuất hiện nhiều lần trong cùng một view.");
                }
                foreach (var node in view.Descendants())
                {
                    var field = Named(node, "field") ? Attribute(node, "name") : Attribute(node, "field");
                    if (field is not null) CheckReference(field.Value, field);
                    if (!Named(node, "item") || Attribute(node, "value") is not { } value) continue;
                    var row = XmlViewWidths.LayoutMask.Match(value.Value);
                    if (!row.Success) continue;
                    var references = XmlViewWidths.LayoutFields.Matches(value.Value[row.Length..]);
                    var fieldCount = references.Count(XmlViewWidths.IsField);
                    var textCount = references.Count - fieldCount;
                    var fieldSlots = Math.Max(0, row.Groups["slots"].Value.Count(slot => slot == '1') - textCount);
                    if ((fieldCount > 0 || textCount == 0) && fieldSlots != fieldCount)
                        Warn(value, $"Layout có {fieldSlots} ô dành cho field và {fieldCount} field.");
                    foreach (Match reference in references)
                        if (XmlViewWidths.IsField(reference)) CheckReference(reference.Groups["name"].Value, value);
                }
            }
            var declared = new HashSet<string>(comparer);
            foreach (var field in fields)
            {
                var name = XmlViewWidths.FieldName(field.Value);
                if (name.Length == 0) continue;
                if (!declared.Add(name)) Warn(field, $"Field '{name}' bị trùng trong <fields>.");
                else if (!visible.Contains(name)) Warn(field, $"Field '{name}' đã khai báo nhưng chưa có trong <views>.");
            }
        }
        return new(null, warnings);
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
