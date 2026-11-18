using System.Text;
using System.Text.RegularExpressions;

namespace LSCode.Core;

public sealed record CompletionSnippet(string Key, string Body, string Description, string Object = "", bool Exact = true);
public static class CompletionSnippets
{
    public static IReadOnlyList<(int Offset, int Length)> Placeholders(string text) => Regex.Matches(text,
        @"\{\{[^{}]*\}\}", RegexOptions.None, TimeSpan.FromMilliseconds(100))
        .Select(m => (m.Index, m.Length)).ToList();
    public static IReadOnlyList<CompletionSnippet> Read(string xml) => XmlTools.Parse(xml).Descendants("Item")
        .Where(x => x.Parent?.Name.LocalName is "Snippet" or "HintItem")
        .Select(x => new CompletionSnippet(((string?)x.Attribute("keyword") ?? "").Trim(), x.Value.Trim(),
            (string?)x.Attribute("description") ?? "", (string?)x.Parent?.Attribute("object") ?? "",
            (string?)x.Parent?.Attribute("exact") != "0"))
        .Where(x => x.Key.Length > 0).DistinctBy(x => (x.Object, x.Key)).ToList();

    // Scan markup, ignoring comments, attributes and CDATA outside a script element.
    public static bool IsInScript(string text, int offset)
    {
        var before = text[..Math.Clamp(offset, 0, text.Length)];
        var inside = false;
        foreach (Match m in Regex.Matches(before, "<!--[\\s\\S]*?(?:-->|$)|<!\\[CDATA\\[[\\s\\S]*?(?:\\]\\]>|$)|<(?<close>/)?(?<name>[\\w:.-]+)\\b(?:\"[^\"]*\"|'[^']*'|[^'\">])*>", RegexOptions.None, TimeSpan.FromMilliseconds(200)))
        {
            if (!m.Groups["name"].Value.Equals("script", StringComparison.OrdinalIgnoreCase)) continue;
            inside = !m.Groups["close"].Success && !m.Value.EndsWith("/>");
        }
        return inside;
    }

    public static IEnumerable<SqlSuggestion> Get(IEnumerable<CompletionSnippet> snippets, string beforeCaret)
    {
        var qualifier = Regex.Match(beforeCaret, @"(?<object>[\w$]+(?:\.[\w$]+)*)\.[\w$]*$", RegexOptions.None, TimeSpan.FromMilliseconds(100)).Groups["object"].Value;
        return snippets.Where(s => !s.Key.StartsWith("ss", StringComparison.OrdinalIgnoreCase))
            .Where(s => s.Object.Length == 0 ? qualifier.Length == 0 :
            string.Equals(s.Object, qualifier, s.Exact ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase))
            .Select(s => new SqlSuggestion(s.Key, s.Body, s.Description.Length > 0 ? s.Description : "Snippet " + s.Object, true));
    }

    public static (string Text, int Offset, int Length) Expand(string body, bool preservePlaceholders = false)
    {
        if (preservePlaceholders)
        {
            var first = Placeholders(body).FirstOrDefault((body.Length, 0));
            return (body, first.Item1, first.Item2);
        }
        var output = new StringBuilder(); int end = 0, caret = -1, length = 0;
        foreach (Match m in Regex.Matches(body, @"\{\{(?<id>[-|]|\d+)(?:-(?<label>[^}]*))?\}\}", RegexOptions.None, TimeSpan.FromMilliseconds(100)))
        {
            output.Append(body, end, m.Index - end);
            if (caret < 0) { caret = output.Length; length = m.Groups["label"].Value.Length; }
            output.Append(m.Groups["label"].Value); end = m.Index + m.Length;
        }
        output.Append(body, end, body.Length - end);
        return (output.ToString(), caret < 0 ? output.Length : caret, length);
    }
}
