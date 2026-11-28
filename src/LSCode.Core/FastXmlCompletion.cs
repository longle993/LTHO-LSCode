using System.Text.RegularExpressions;

namespace LSCode.Core;

public sealed record FastXmlContext(string Language, int Start, int End, string Prefix,
    string? Attribute = null, bool InView = false, string Code = "", int CodeOffset = 0, string? ClosingTag = null,
    bool AttributeName = false, bool ExistingProperty = false, int StringStart = -1);

public sealed record FastCompletionEntry(string Keyword, string Body, string Description, string Object, bool Exact, bool Default, int Order);

public static class FastXmlCompletion
{
    private static readonly Regex Attributes = new("""(?<name>[\w:.-]+)\s*=\s*(?<q>["'])(?<value>(?:(?!\k<q>)[\s\S])*)(?<closed>\k<q>|$)""", RegexOptions.None, TimeSpan.FromMilliseconds(100));
    private static readonly Regex QualifiedMember = new(@"(?<object>[\w$]+(?:\.[\w$]+)*)\.[\w$]*$", RegexOptions.None, TimeSpan.FromMilliseconds(100));
    private static readonly Regex Checking = new("""(?:^|\s)(?:event|name)\s*=\s*["']Checking["']""", RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(100));
    private static readonly Regex Entity = new("""<!ENTITY\s+[%\s]*[\w:.-]+\s+(?<quote>["'])""", RegexOptions.None, TimeSpan.FromMilliseconds(100));
    private static readonly Regex FieldArguments = new("""(?<![\w$.])(?:f|w|parentForm|g|grid|z)\s*\.\s*[\w$]+\s*\((?:[^()"']|"(?:[^"\\\r\n]|\\.)*"|'(?:[^'\\\r\n]|\\.)*')*?(?<quote>['"])[\w%]*$""", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(50));
    private static readonly Regex Field = new("""<field\b[^<>]*?\b(?:name|field)\s*=\s*(?<q>["'])(?<value>[\w%:.-]+)\k<q>""", RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(100));
    private static readonly Regex OpaqueMarkup = new("<!--[\\s\\S]*?(?:-->|$)|<!\\[CDATA\\[[\\s\\S]*?(?:\\]\\]>|$)", RegexOptions.None, TimeSpan.FromMilliseconds(100));
    private sealed record Element(string Name, string Language, int Content);

    public static IReadOnlyList<FastCompletionEntry> ReadCatalog(string xml) => XmlTools.Parse(xml).Descendants("Item")
        .Where(node => node.Parent?.Name.LocalName is "Snippet" or "HintItem")
        .SelectMany(node => ((string?)node.Parent?.Attribute("object") ?? "").Split(';', StringSplitOptions.TrimEntries)
            .Select(owner => new FastCompletionEntry(
                ((string?)node.Attribute("keyword") ?? "").Trim(),
                node.Value.Trim().Replace("\\n", "\n").Replace("\\t", "\t")
                    .Replace("&lt;![CDATA[", "<![CDATA[").Replace("]]&gt;", "]]>"),
                (string?)node.Attribute("description") ?? "", owner, (string?)node.Parent?.Attribute("exact") != "0",
                (string?)node.Attribute("default") == "1", (int?)node.Attribute("order") ?? 0)))
        .Where(entry => entry.Keyword.Length > 0 && entry.Body.Length > 0)
        .DistinctBy(entry => (entry.Object, entry.Keyword)).ToArray();

    public static FastXmlContext? Context(string text, int offset)
    {
        if (offset < 0 || offset > text.Length || text.Length > 1_000_000) return null;
        var stack = new List<Element>();
        var cursor = 0;
        while (cursor < offset)
        {
            var open = text.IndexOf('<', cursor, offset - cursor);
            if (open < 0) break;
            var markup = text.AsSpan(open);
            if (markup.StartsWith("<!--") || markup.StartsWith("<?") || markup.StartsWith("<![CDATA["))
            {
                var cdata = markup.StartsWith("<![CDATA[");
                var terminator = cdata ? "]]>" : markup.StartsWith("<?") ? "?>" : "-->";
                var content = open + (cdata ? 9 : 2);
                var close = text.IndexOf(terminator, content, StringComparison.Ordinal);
                if (close < 0 || close >= offset)
                    return cdata && offset >= content
                        ? CodeContext(text, offset, content, close < 0 ? text.Length : close, stack.LastOrDefault()?.Language is "javascript" or "css" ? stack[^1].Language : "sql")
                        : null;
                cursor = close + terminator.Length;
                continue;
            }
            if (markup.StartsWith("<!DOCTYPE") || markup.StartsWith("<!ENTITY"))
            {
                var end = MarkupEnd(text, open + 2, true);
                if (end < 0 || end >= offset)
                {
                    var declaration = Entity.Matches(text[..offset], open).LastOrDefault();
                    if (declaration is null) return null;
                    var content = declaration.Index + declaration.Length;
                    var close = text.IndexOf(declaration.Groups["quote"].Value, content, StringComparison.Ordinal);
                    return close < offset && close >= 0 ? null : CodeContext(text, offset, content, close < 0 ? text.Length : close, "sql");
                }
                cursor = end + 1;
                continue;
            }

            var closing = markup.StartsWith("</");
            var nameStart = open + (closing ? 2 : 1);
            var nameEnd = nameStart;
            while (nameEnd < text.Length && IsName(text[nameEnd])) nameEnd++;
            var name = text[nameStart..nameEnd];
            var active = stack.LastOrDefault();
            if (active is { Language: not "xml" } &&
                !(closing ? stack.Any(element => element.Name.Equals(name, StringComparison.OrdinalIgnoreCase)) : Language(name, "") is not "xml"))
            {
                cursor = open + 1;
                continue;
            }
            if (nameEnd == nameStart && nameStart < offset)
            {
                cursor = open + 1;
                continue;
            }

            var endTag = MarkupEnd(text, nameEnd, false);
            if (endTag < 0 || endTag >= offset)
            {
                if (offset <= nameEnd)
                    return new("xml", open, nameEnd < text.Length && text[nameEnd] == '>' ? nameEnd + 1 : nameEnd,
                        text[open..offset], ClosingTag: closing ? active?.Name : null);
                if (closing) return null;
                var attributes = text[nameEnd..offset];
                var value = Attributes.Matches(attributes).LastOrDefault();
                if (value is not null && value.Groups["closed"].Length == 0)
                {
                    var close = text.IndexOf(value.Groups["q"].Value, offset, StringComparison.Ordinal);
                    var start = nameEnd + value.Groups["value"].Index;
                    if (text.AsSpan(start, offset - start).Contains('\n')) return null;
                    var end = offset;
                    while (end < text.Length && end != close && text[end] is not ('\r' or '\n' or '<' or '>')) end++;
                    var attribute = value.Groups["name"].Value;
                    var inView = stack.Any(node => node.Name.Equals("view", StringComparison.OrdinalIgnoreCase));
                    if (inView && attribute.ToLowerInvariant() is "value" or "field" or "name")
                        (start, end) = Token(text, offset, start, end, character => IsName(character) || character == '%');
                    return new("xml", start, end, text[start..offset], attribute, inView);
                }
                var (startToken, endToken) = Token(text, offset, nameEnd, text.Length, IsName);
                if (attributes.AsSpan().TrimEnd().EndsWith("=") || text.AsSpan(endToken).TrimStart().StartsWith("=")) return null;
                return new("xml", startToken, endToken, text[startToken..offset], AttributeName: true);
            }
            if (closing)
            {
                var found = stack.FindLastIndex(element => element.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
                if (found >= 0) stack.RemoveRange(found, stack.Count - found);
            }
            else if (endTag > open && text[endTag - 1] != '/' && !markup.StartsWith("<!"))
                stack.Add(new(name, Language(name, text[nameEnd..endTag]), endTag + 1));
            cursor = endTag + 1;
        }

        if (stack.LastOrDefault() is { Language: not "xml" } code)
        {
            var close = text.IndexOf("</" + code.Name, offset, StringComparison.OrdinalIgnoreCase);
            return CodeContext(text, offset, code.Content, close < 0 ? text.Length : close, code.Language);
        }
        var (startWord, endWord) = Token(text, offset, cursor, text.Length, character => IsName(character) || character == '@');
        return new("xml", startWord, endWord, text[startWord..offset]);
    }

    private static int MarkupEnd(string text, int start, bool declaration)
    {
        var quote = '\0';
        var bracket = 0;
        for (var i = start; i < text.Length; i++)
        {
            var character = text[i];
            if (quote != '\0') { if (quote == character) quote = '\0'; }
            else if (character is '"' or '\'') quote = character;
            else if (declaration && character == '[') bracket++;
            else if (declaration && character == ']') bracket = Math.Max(0, bracket - 1);
            else if (character == '>' && bracket == 0) return i;
            else if (!declaration && character == '<') return -1;
        }
        return -1;
    }

    private static string Language(string name, string attributes) => name.ToLowerInvariant() switch
    {
        "script" or "clientscript" => "javascript",
        "command" when Checking.IsMatch(attributes) => "javascript",
        "command" or "commands" or "response" or "queries" or "query" or "sql" or "processing" => "sql",
        "css" or "style" => "css",
        _ => "xml",
    };

    private static bool IsName(char character) => char.IsLetterOrDigit(character) || character is '_' or '-' or '.' or ':' or '$';

    private static (int Start, int End) Token(string text, int offset, int lower, int upper, Func<char, bool> valid)
    {
        var start = offset;
        var end = offset;
        while (start > lower && valid(text[start - 1])) start--;
        while (end < upper && valid(text[end])) end++;
        return (start, end);
    }

    private static FastXmlContext? CodeContext(string text, int offset, int begin, int finish, string language)
    {
        var sql = language == "sql";
        var quote = '\0';
        var stringStart = -1;
        var blockComment = 0;
        var lineComment = false;
        for (var at = begin; at < offset; at++)
        {
            var current = text[at];
            var next = at + 1 < offset ? text[at + 1] : '\0';
            if (lineComment)
            {
                if (current == '\n') lineComment = false;
                continue;
            }
            if (quote != '\0')
            {
                if (!sql && current == '\\' || sql && current == quote && next == quote) at++;
                else if (current == quote) { quote = '\0'; stringStart = -1; }
                continue;
            }
            if (current == '/' && next == '*' && (blockComment == 0 || sql)) { blockComment++; at++; }
            else if (current == '*' && next == '/' && blockComment > 0) { blockComment--; at++; }
            else if (blockComment == 0)
            {
                if (sql && current == '-' && next == '-' || language == "javascript" && current == '/' && next == '/')
                    lineComment = true;
                else if (current is '"' or '\'' || language == "javascript" && current == '`')
                {
                    quote = current;
                    stringStart = at - begin;
                }
            }
        }
        if (lineComment || blockComment > 0 || sql && quote != '\0') return null;
        var (start, end) = Token(text, offset, begin, finish, character => language == "css"
            ? IsName(character) && character is not ('.' or ':')
            : char.IsLetterOrDigit(character) || character is '_' or '$' or '@' or '#');
        return new(language, start, end, text[start..offset], Code: text[begin..finish], CodeOffset: offset - begin,
            ExistingProperty: language == "css" && text.AsSpan(end, finish - end).TrimStart().StartsWith(":"), StringStart: stringStart);
    }

    public static IReadOnlyList<SqlSuggestion> Suggest(FastXmlContext context, IReadOnlyList<FastCompletionEntry> catalog)
    {
        if (context.StringStart >= 0) return [];
        var qualifier = context.Attribute is { } attribute ? attribute + "="
            : context.Language == "javascript" ? QualifiedMember.Match(context.Code[..context.CodeOffset]).Groups["object"].Value : "";
        var exactComparison = context.Language == "javascript" ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        var entries = catalog.Where(entry =>
            (entry.Exact ? entry.Object.Equals(qualifier, exactComparison) : entry.Object.StartsWith(qualifier, StringComparison.OrdinalIgnoreCase) && (entry.Object.Length == 0) == (qualifier.Length == 0)) &&
            (context.Language != "xml" || context.Attribute is not null || entry.Keyword.StartsWith('<') == context.Prefix.StartsWith('<')) &&
            entry.Keyword.StartsWith(context.Prefix, StringComparison.OrdinalIgnoreCase));
        if (context.ClosingTag is { } name && ("</" + name).StartsWith(context.Prefix, StringComparison.OrdinalIgnoreCase))
            return [new("</" + name, "</" + name + ">", "Đóng thẻ XML")];
        IEnumerable<FastCompletionEntry> attributes = context.AttributeName ? catalog.Where(entry => entry.Object.EndsWith('=') &&
            entry.Object.StartsWith(context.Prefix, StringComparison.OrdinalIgnoreCase))
            .GroupBy(entry => entry.Object, StringComparer.OrdinalIgnoreCase)
            .SelectMany(group => new[] { new FastCompletionEntry(group.Key[..^1], group.Key + "\"{{-}}\"", "Thuộc tính FAST", "", false, false, 0) }
                .Concat(group.Where(entry => entry.Default).Select(entry => entry with
                {
                    Keyword = group.Key + "\"" + entry.Keyword + "\"",
                    Body = group.Key + "\"" + entry.Body + "\"",
                }))) : [];
        return entries.Concat(attributes).DistinctBy(entry => entry.Keyword, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(entry => entry.Default).ThenBy(entry => entry.Order).ThenBy(entry => entry.Keyword, StringComparer.OrdinalIgnoreCase)
            .Select(entry => new SqlSuggestion(entry.Keyword, context.ExistingProperty ? entry.Keyword : entry.Body,
                entry.Description, !context.ExistingProperty && entry.Body.Contains("{{", StringComparison.Ordinal)))
            .Take(150).ToArray();
    }

    public static IReadOnlyList<SqlSuggestion> Fields(FastXmlContext context, string text, string? path, IReadOnlyDictionary<string, string>? buffers)
    {
        var script = context.Language == "javascript";
        var before = context.Code[..context.CodeOffset];
        if (script)
        {
            var tail = Math.Max(0, before.Length - 4000);
            var match = FieldArguments.Match(before[tail..]);
            if (context.StringStart < 0 || !match.Success || tail + match.Groups["quote"].Index != context.StringStart) return [];
        }
        else if (!context.InView || context.Attribute is null) return [];

        var fields = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            var document = XmlTools.ParseEditor(text, path, buffers);
            foreach (var field in document.Descendants().Where(node => node.Name.LocalName.Equals("field", StringComparison.OrdinalIgnoreCase)))
                if (field.Attribute("name") is { } attribute) fields.Add(attribute.Value);
        }
        catch (Exception exception) when (exception is System.Xml.XmlException or IOException or UnauthorizedAccessException) { }
        foreach (Match match in Field.Matches(OpaqueMarkup.Replace(text, ""))) fields.Add(match.Groups["value"].Value);
        if (script)
        {
            var documentOffset = context.Start + context.Prefix.Length;
            foreach (var field in XmlTools.ScriptFields(text, text[..documentOffset])) fields.Add(field.Text);
        }
        return fields.Select(XmlViewWidths.FieldName).Where(name => name.Length > 0 && name.StartsWith(context.Prefix, StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.OrdinalIgnoreCase).Take(100)
            .Select(name => new SqlSuggestion(name, name, "Trường trong tài liệu XML")).ToArray();
    }
}
