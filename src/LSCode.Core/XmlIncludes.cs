using System.Text.RegularExpressions;

namespace LSCode.Core;

public sealed record IncludeLocation(string Path, int Line, int Column);

public static class XmlIncludes
{
    private static string WithoutComments(string text) => Regex.Replace(text, @"<!--[\s\S]*?(?:-->|$)", m => Regex.Replace(m.Value, @"[^\r\n]", " "));
    private static readonly Regex Declarations = new("""<!ENTITY\s+(?<parameter>%\s*)?(?<name>[\w.:-]+)\s+(?:(?:SYSTEM\s+|PUBLIC\s+["'][^"']*["']\s+)(?<q>["'])(?<path>.*?)\k<q>|(?<q>["'])(?<value>.*?)\k<q>)\s*>""", RegexOptions.Singleline);
    private static readonly Regex References = new(@"(?<kind>[&%])(?<name>[\w.:-]+);");
    private static readonly Regex Doctype = new("""<!DOCTYPE\s+[^\s>]+\s+(?:SYSTEM\s+|PUBLIC\s+["'][^"']*["']\s+)["'](?<path>[^"']+)["']""");

    public static IncludeLocation? Find(string file, string text, int offset, Func<string, string?>? read = null)
    {
        file = Path.GetFullPath(file);
        text = WithoutComments(text);
        read ??= p => File.Exists(p) ? File.ReadAllText(p) : null;
        var cache = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        string? Load(string path)
        {
            if (cache.TryGetValue(path, out var cached)) return cached;
            try { return cache[path] = read(path); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return null; }
        }
        string? Resolve(string owner, string name)
        {
            if (Uri.TryCreate(name, UriKind.Absolute, out var uri))
            {
                if (!uri.IsFile) return null;
                name = uri.LocalPath;
            }
            var dir = Path.GetDirectoryName(owner)!;
            foreach (var folder in new[] { dir, Path.Combine(dir, "Include", "XML"), Path.Combine(dir, "..", "Include", "XML"), Path.Combine(dir, "..", "..", "App_Data", "Controllers", "Include", "XML") })
                foreach (var ext in new[] { "", ".xml", ".txt", ".sql" })
                {
                    try
                    {
                        var path = Path.GetFullPath(Path.Combine(folder, name.Replace('/', Path.DirectorySeparatorChar) + ext));
                        if (Load(path) != null) return path;
                    }
                    catch (Exception ex) when (ex is ArgumentException or NotSupportedException) { }
                }
            return null;
        }
        IncludeLocation? Location(string owner, string content, Match declaration)
        {
            if (declaration.Groups["path"].Success)
                return Resolve(owner, declaration.Groups["path"].Value) is { } target ? new(target, 1, 1) : null;
            var before = content[..declaration.Index];
            return new(owner, before.Count(c => c == '\n') + 1, declaration.Index - before.LastIndexOf('\n'));
        }
        bool Contains(Match m) => offset >= m.Index && offset <= m.Index + m.Length;
        var direct = Declarations.Matches(text).Cast<Match>().FirstOrDefault(Contains);
        direct ??= Doctype.Matches(text).Cast<Match>().FirstOrDefault(Contains);
        var reference = References.Matches(text).Cast<Match>().FirstOrDefault(Contains);
        if (reference == null) return direct == null ? null : Location(file, text, direct);
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        IncludeLocation? Search(string owner, string content, int depth)
        {
            if (depth > 64 || !visited.Add(owner)) return null;
            // Preserve newlines and offsets for navigation while ignoring commented declarations.
            content = WithoutComments(content);
            var declarations = Declarations.Matches(content).Cast<Match>().ToArray();
            var match = declarations.FirstOrDefault(m => m.Groups["name"].Value == reference.Groups["name"].Value && m.Groups["parameter"].Success == (reference.Groups["kind"].Value == "%"));
            if (match != null) return Location(owner, content, match);
            foreach (var external in declarations.Where(m => m.Groups["path"].Success).Concat(Doctype.Matches(content).Cast<Match>()))
                if (Resolve(owner, external.Groups["path"].Value) is { } path && Load(path) is { } nested && Search(path, nested, depth + 1) is { } result) return result;
            return null;
        }
        return Search(file, text, 0);
    }
}
