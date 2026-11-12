using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace LSCode.Core;

public sealed record GridPreviewColumn(string Name, string Label, int Width);
public sealed record DirPreviewDetail(string Controller, GridPreviewColumn[] Columns, string? Error);
public sealed record DirPreviewCell(string Name, string Label, bool IsLabel, int Column, int Span,
    string? Description = null, DirPreviewDetail? Detail = null);
public sealed record DirPreviewCategory(string Label, int[] Widths, IReadOnlyList<DirPreviewCell[]> Rows);
public sealed record DirPreviewLayout(int[] Widths, IReadOnlyList<DirPreviewCell[]> Rows,
    IReadOnlyList<DirPreviewCategory> Categories);
public sealed record ControllerPreviewView(string Name, IReadOnlyList<GridPreviewColumn> Columns, DirPreviewLayout? Layout = null);
public sealed record XmlControllerPreview(string Kind, string Title, string Subtitle, IReadOnlyList<string?> Toolbar,
    IReadOnlyList<ControllerPreviewView> Views)
{
    private static readonly StringComparer Names = StringComparer.OrdinalIgnoreCase;
    private static readonly Regex BareField = new(@"^[\p{L}_][\p{L}\p{N}_.$]*(?:%l)?$", RegexOptions.IgnoreCase);

    public static XmlControllerPreview Read(string text, string? path = null, IReadOnlyDictionary<string, string>? buffers = null)
        => Read(text, path, buffers, true);

    private static XmlControllerPreview Read(string text, string? path, IReadOnlyDictionary<string, string>? buffers, bool includeDetails)
    {
        if (text.Length > 1_000_000)
            throw new ArgumentException("XML vượt quá 1 triệu ký tự.");
        var root = XmlTools.ParseEditor(text, path, buffers).Root;
        var grid = root?.DescendantsAndSelf().FirstOrDefault(node => Is(node, "grid") || Is(node, "dir"))
            ?? throw new ArgumentException("File này chưa có thẻ <grid> hoặc <dir>.");
        var isDir = Is(grid, "dir");
        var details = new Dictionary<string, DirPreviewDetail>(Names);
        DirPreviewDetail Detail(string controller)
        {
            if (!details.TryGetValue(controller, out var detail))
                details[controller] = detail = ReadDetail(controller, path, buffers);
            return detail;
        }
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
                isDir ? DirLayout(view, fields, includeDetails ? Detail : null) : null))
            .Where(view => view.Columns.Count > 0 || view.Layout?.Rows.Count > 0 || view.Layout?.Categories.Count > 0).ToArray();
        var toolbar = Children(grid, "toolbar").SelectMany(section => section.Elements())
            .Where(item => !Hidden(item))
            .Select(item => Is(item, "separator") || Attribute(item, "id") == "-" ? null :
                First(Caption(Child(item, "title")), Caption(Child(item, "text")), Caption(Child(item, "label")),
                    Caption(item), Attribute(item, "label"), Attribute(item, "text"), Attribute(item, "id"),
                    Attribute(item, "command"), Attribute(item, "name"), Attribute(item, "value")))
            .Where(label => label != "").ToArray();
        return new(isDir ? "dir" : "grid", Caption(Child(grid, "title")), Caption(Child(grid, "subtitle")), toolbar, views);
    }

    private static DirPreviewDetail ReadDetail(string controller, string? path, IReadOnlyDictionary<string, string>? buffers)
    {
        if (path is not null && Path.IsPathFullyQualified(path) && BareField.IsMatch(controller) && !controller.Contains('%'))
        {
            var directory = new DirectoryInfo(Path.GetDirectoryName(path)!);
            var parent = directory;
            while (parent is not null && !Names.Equals(parent.Name, "Dir")) parent = parent.Parent;
            if (parent?.Parent is { } controllers)
                foreach (var extension in new[] { ".f", ".xml" })
                {
                    var file = Path.Combine(controllers.FullName, "Grid", controller + extension);
                    var buffered = buffers?.TryGetValue(file, out var buffer) == true ? buffer : null;
                    if (buffered is null && !File.Exists(file)) continue;
                    try
                    {
                        var preview = Read(buffered ?? File.ReadAllText(file), file, buffers, false);
                        if (preview.Kind != "grid") continue;
                        var grid = preview.Views.FirstOrDefault(view => Names.Equals(view.Name, "Grid")) ?? preview.Views.FirstOrDefault();
                        return new(controller, grid?.Columns.ToArray() ?? [], null);
                    }
                    catch (Exception error) when (error is IOException or UnauthorizedAccessException or System.Xml.XmlException or ArgumentException)
                    {
                        return new(controller, [], $"Không đọc được Grid: {error.Message}");
                    }
                }
        }
        return new(controller, [], $"Không tìm thấy Grid cho {controller}.");
    }

    private static int[] ReadWidths(string value)
    {
        var parts = value.Split(',');
        return parts.All(part => int.TryParse(part, out var pixels) && pixels >= 0) ? parts.Select(int.Parse).ToArray() : [];
    }

    private static DirPreviewLayout DirLayout(XElement view, ILookup<string, XElement> fields,
        Func<string, DirPreviewDetail>? detailFor)
    {
        var widths = Array.Empty<int>();
        var categories = Children(view, "categories").SelectMany(section => Children(section, "category"))
            .Where(category => Attribute(category, "index").Length > 0).ToArray();
        var categoryTracks = categories.ToLookup(category => Attribute(category, "index"), Names);
        var rows = new Dictionary<string, List<DirPreviewCell[]>>(Names) { [""] = [] };
        var maskLengths = new Dictionary<string, int>(Names);
        foreach (var item in Children(view, "item").Where(item => !Hidden(item)))
        {
            var value = Attribute(item, "value");
            var mask = XmlViewWidths.LayoutMask.Match(value);
            if (!mask.Success)
            {
                if (widths.Length == 0) widths = ReadWidths(value);
                continue;
            }
            var slots = mask.Groups["slots"].Value;
            var references = XmlViewWidths.LayoutFields.Matches(value[mask.Length..]);
            var cells = XmlViewWidths.Spans(slots).Zip(references, (span, reference) => (span, reference))
                .Select(entry =>
                {
                    var name = XmlViewWidths.FieldName(entry.reference.Groups["name"].Value);
                    var field = fields[name].FirstOrDefault();
                    if (field is null || Hidden(field)) return null;
                    var items = Children(field, "items").FirstOrDefault(node => Names.Equals(Attribute(node, "style"), "Grid"));
                    var controller = items is null ? "" : Attribute(items, "controller");
                    var detail = XmlViewWidths.IsField(entry.reference) && controller.Length > 0 ? detailFor?.Invoke(controller) : null;
                    var category = categoryTracks[Attribute(field, "categoryIndex")].FirstOrDefault();
                    var categoryWidthCount = category is null ? 0 : ReadWidths(Attribute(category, "columns")).Length;
                    var span = detail is not null && items is not null && int.TryParse(Attribute(items, "row"), out var row) && row > 0
                        ? Math.Min(row, Math.Max(1, Math.Max(categoryWidthCount, slots.Length) - entry.span.Start)) : entry.span.Length;
                    return new DirPreviewCell(name, First(Caption(Child(field, "label")), FieldCaption(field, name)),
                        entry.reference.Groups["label"].Success, entry.span.Start + 1, span,
                        entry.reference.Groups["description"].Success ? Caption(Child(field, "footer")) : null, detail);
                }).OfType<DirPreviewCell>().ToArray();
            var category = cells.Select(cell => fields[cell.Name].FirstOrDefault())
                .OfType<XElement>().Select(field => Attribute(field, "categoryIndex")).FirstOrDefault(categoryTracks.Contains) ?? "";
            if (!rows.TryGetValue(category, out var group)) rows[category] = group = [];
            group.Add(cells);
            maskLengths[category] = Math.Max(maskLengths.GetValueOrDefault(category), slots.Length);
        }
        int[] Tracks(int[] configured, string group) => Enumerable.Range(0, Math.Max(maskLengths.GetValueOrDefault(group),
            Math.Max(configured.Length, rows[group].SelectMany(row => row).Select(cell => cell.Column + cell.Span - 1).DefaultIfEmpty().Max())))
            .Select(column => column < configured.Length ? configured[column] : 120).ToArray();
        return new(Tracks(widths, ""), rows[""], categories.Select(category =>
        {
            var index = Attribute(category, "index");
            if (!rows.TryGetValue(index, out var group)) return null;
            var categoryWidths = ReadWidths(Attribute(category, "columns"));
            return new DirPreviewCategory(First(Caption(Child(category, "header")), index),
                Tracks(categoryWidths.Length > 0 ? categoryWidths : widths, index), group);
        }).OfType<DirPreviewCategory>().ToArray());
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
                if (reference.Success && XmlViewWidths.IsField(reference))
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
