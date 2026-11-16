using System.Text.Json;
using System.Text.RegularExpressions;

namespace DynamicTemplateStudio.Core.Services;

public static class PathExpression
{
    private static readonly Regex Token = new(@"\G(?:\.([A-Za-z_][A-Za-z0-9_]*)|\['((?:\\.|[^'\\])*)'\]|\[(\d+|\*)\])", RegexOptions.Compiled);
    public static List<string> Parse(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || path[0] != '$') throw new FormatException("Path must start with $. Supported: .name, ['name'], [index], [*].");
        List<string> parts = [];
        for (int i = 1; i < path.Length;)
        {
            var m = Token.Match(path, i);
            if (!m.Success) throw new FormatException($"Unsupported path expression at character {i + 1}.");
            parts.Add(m.Groups[1].Success ? "." + m.Groups[1].Value : m.Groups[2].Success ? "." + Regex.Unescape(m.Groups[2].Value) : m.Groups[3].Value);
            i += m.Length;
        }
        return parts;
    }
    public static List<JsonElement> Select(JsonElement root, string path)
    {
        List<JsonElement> nodes = [root];
        foreach (var part in Parse(path))
        {
            List<JsonElement> next = [];
            foreach (var node in nodes)
            {
                if (part.StartsWith('.')) { if (node.ValueKind == JsonValueKind.Object && node.TryGetProperty(part[1..], out var v)) next.Add(v); }
                else if (node.ValueKind == JsonValueKind.Array)
                {
                    if (part == "*") next.AddRange(node.EnumerateArray());
                    else if (int.TryParse(part, out int index) && index < node.GetArrayLength()) next.Add(node[index]);
                }
            }
            nodes = next;
        }
        return nodes;
    }
    public static string PropertyPath(string property) => Regex.IsMatch(property, "^[A-Za-z_][A-Za-z0-9_]*$") ? "$." + property : "$['" + property.Replace("\\", "\\\\").Replace("'", "\\'") + "']";
}
