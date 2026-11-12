using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace LSCode.Core;

public sealed record GridPreviewColumn(string Name, string Label, int Width);
public sealed record DirPreviewCell(string Name, string Label, bool IsLabel, int Column, int Span);
public sealed record DirPreviewLayout(int[] Widths, IReadOnlyList<DirPreviewCell[]> Rows);
public sealed record ControllerPreviewView(string Name, IReadOnlyList<GridPreviewColumn> Columns, DirPreviewLayout? Layout = null);
public sealed record XmlControllerPreview(string Kind, string Title, string Subtitle, IReadOnlyList<string?> Toolbar,
    IReadOnlyList<ControllerPreviewView> Views)
{
    private static readonly StringComparer Names = StringComparer.OrdinalIgnoreCase;
    private static readonly Regex BareField = new(@"^[\p{L}_][\p{L}\p{N}_.$]*(?:%l)?$", RegexOptions.IgnoreCase);

    public static XmlControllerPreview Read(string text, string? path = null, IReadOnlyDictionary<string, string>? buffers = null)
    {
        if (text.Length > 1_000_000)
            throw new ArgumentException("XML vượt quá 1 triệu ký tự.");
        var root = XmlTools.ParseEditor(text, path, buffers).Root;
        var grid = root?.DescendantsAndSelf().FirstOrDefault(node => Is(node, "grid") || Is(node, "dir"))
            ?? throw new ArgumentException("File này chưa có thẻ <grid> hoặc <dir>.");
        var isDir = Is(grid, "dir");
        var fields = Children(grid, "fields").SelectMany(section => Children(section, "field"))
            .Where(field => Attribute(field, "name").Length > 0)
            .ToLookup(field => XmlViewWidths.FieldName(Attribute(field, "name")), Names);
        var views = Children(grid, "views").SelectMany(section => Children(section, "view"))
            .Where(view => !Hidden(view))
            .Select((view, index) => new ControllerPreviewView(
                First(Attribute(view, "id"), Attribute(view, "name"), $"View {index + 1}"),
                isDir ? [] : ColumnNames(view)
                    .Select(XmlViewWidths.FieldName)
                    .Distinct(Names)
                    .Select(name => (Name: name, Field: fields[name].FirstOrDefault()))
                    .Where(entry => entry.Field is not null && !Hidden(entry.Field))
                    .Select(entry =>
                    {
                        var field = entry.Field!;
                        var width = int.TryParse(Attribute(field, "width"), out var pixels) && pixels > 0 ? pixels : 120;
                        return new GridPreviewColumn(entry.Name, FieldCaption(field, entry.Name), width);
                    }).ToArray(),
                isDir ? DirLayout(view, fields) : null))
            .Where(view => view.Columns.Count > 0 || view.Layout?.Rows.Count > 0).ToArray();
        var toolbar = Children(grid, "toolbar").SelectMany(section => section.Elements())
            .Where(item => !Hidden(item))
            .Select(item => Is(item, "separator") || Attribute(item, "id") == "-" ? null :
                First(Caption(Child(item, "title")), Caption(Child(item, "text")), Caption(Child(item, "label")),
                    Caption(item), Attribute(item, "label"), Attribute(item, "text"), Attribute(item, "id"),
                    Attribute(item, "command"), Attribute(item, "name"), Attribute(item, "value")))
            .Where(label => label != "").ToArray();
        return new(isDir ? "dir" : "grid", Caption(Child(grid, "title")), Caption(Child(grid, "subtitle")), toolbar, views);
    }

    private static DirPreviewLayout DirLayout(XElement view, ILookup<string, XElement> fields)
    {
        var widths = Array.Empty<int>();
        var rows = new List<DirPreviewCell[]>();
        var columnCount = 0;
        foreach (var item in Children(view, "item").Where(item => !Hidden(item)))
        {
            var value = Attribute(item, "value");
            var mask = XmlViewWidths.LayoutMask.Match(value);
            if (!mask.Success)
            {
                var parts = value.Split(',');
                if (widths.Length == 0 && parts.All(part => int.TryParse(part, out var pixels) && pixels >= 0))
                    widths = parts.Select(int.Parse).ToArray();
                continue;
            }
            var slots = mask.Groups["slots"].Value;
            columnCount = Math.Max(columnCount, slots.Length);
            var references = XmlViewWidths.LayoutFields.Matches(value[mask.Length..]);
            var cells = XmlViewWidths.Spans(slots).Zip(references, (span, reference) => (span, reference))
                .Select(entry =>
                {
                    var name = XmlViewWidths.FieldName(entry.reference.Groups["name"].Value);
                    var field = fields[name].FirstOrDefault();
                    if (field is null || Hidden(field)) return null;
                    return new DirPreviewCell(name, First(Caption(Child(field, "label")), FieldCaption(field, name)),
                        entry.reference.Groups["label"].Success, entry.span.Start + 1, entry.span.Length);
                }).OfType<DirPreviewCell>().ToArray();
            rows.Add(cells);
        }
        var tracks = Enumerable.Range(0, Math.Max(columnCount, widths.Length))
            .Select(column => column < widths.Length ? widths[column] : 120).ToArray();
        return new(tracks, rows);
    }

    private static IEnumerable<string> ColumnNames(XElement view)
    {
        foreach (var item in view.Descendants().Where(item => Is(item, "item") || Is(item, "field") || Is(item, "column")))
        {
            if (item.Ancestors().TakeWhile(parent => parent != view).Any(Hidden) || Hidden(item)) continue;
            var name = First(Attribute(item, "field"), Attribute(item, "name"));
            if (name.Length > 0)
            {
                yield return name;
                continue;
            }
            var value = First(Attribute(item, "value"), item.HasElements ? "" : item.Value);
            var mask = XmlViewWidths.LayoutMask.Match(value);
            if (mask.Success) value = value[mask.Length..];
            foreach (var part in value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
            {
                var reference = XmlViewWidths.LayoutFields.Match(part);
                if (reference.Success && !reference.Groups["label"].Success)
                    yield return reference.Groups["name"].Value;
                else if (!reference.Success && BareField.IsMatch(part)) yield return part;
            }
        }
    }

    private static bool Is(XElement node, string name) => Names.Equals(node.Name.LocalName, name);
    private static IEnumerable<XElement> Children(XElement node, string name) => node.Elements().Where(child => Is(child, name));
    private static XElement? Child(XElement node, string name) => Children(node, name).FirstOrDefault();
    private static string Attribute(XElement node, string name) =>
        node.Attributes().FirstOrDefault(attribute => Names.Equals(attribute.Name.LocalName, name))?.Value.Trim() ?? "";
    private static bool Hidden(XElement node) => Names.Equals(Attribute(node, "hidden"), "true");
    private static string FieldCaption(XElement field, string name) =>
        First(Caption(Child(field, "header")), Caption(Child(field, "label")), Caption(Child(field, "title")),
            Attribute(field, "label"), Caption(field), name);
    private static string Caption(XElement? node) => node is null ? "" :
        First(Attribute(node, "v"), Attribute(node, "e"), node.HasElements ? "" : node.Value.Trim());
    private static string First(params string[] values) => values.FirstOrDefault(value => value.Length > 0) ?? "";
}
