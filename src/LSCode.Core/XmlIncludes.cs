using System.Text.RegularExpressions;
using System.Net;
using System.Text;
using System.Xml;

namespace LSCode.Core;

public sealed record IncludeLocation(
    string Path,
    int Line,
    int Column,
    string Name = "",
    int Start = 0,
    int Length = 0);

public static class XmlIncludes
{
    private static string WithoutComments(string text) => Regex.Replace(text, @"<!--[\s\S]*?(?:-->|$)", m => Regex.Replace(m.Value, @"[^\r\n]", " "));
    private static readonly Regex Declarations = new("""<!ENTITY\s+(?<parameter>%\s*)?(?<name>[\w.:-]+)\s+(?:(?:SYSTEM\s+|PUBLIC\s+["'][^"']*["']\s+)(?<q>["'])(?<path>.*?)\k<q>|(?<q>["'])(?<value>.*?)\k<q>)\s*>""", RegexOptions.Singleline);
    private static readonly Regex References = new(@"(?<kind>[&%])(?<name>[\w.:-]+);");
    private static readonly Regex Doctype = new("""<!DOCTYPE\s+[^\s>]+\s+(?:SYSTEM\s+|PUBLIC\s+["'][^"']*["']\s+)["'](?<path>[^"']+)["']""");

    private static string? Resolve(string owner, string name, Func<string, bool> exists)
    {
        if (Uri.TryCreate(name, UriKind.Absolute, out var uri))
        {
            if (!uri.IsFile) return null;
            name = uri.LocalPath;
        }
        var directory = Path.GetDirectoryName(owner)!;
        var folders = new List<string> { directory };
        for (var current = directory; current is not null && folders.Count <= 8; current = Path.GetDirectoryName(current))
        {
            folders.Add(Path.Combine(current, "Include", "XML"));
            folders.Add(Path.Combine(current, "Include"));
        }
        folders.Add(Path.Combine(directory, "..", "..", "App_Data", "Controllers", "Include", "XML"));
        foreach (var folder in folders)
            foreach (var extension in new[] { "", ".ent", ".dtd", ".xml", ".txt", ".sql" })
            {
                try
                {
                    var path = Path.GetFullPath(Path.Combine(folder, name.Replace('/', Path.DirectorySeparatorChar) + extension));
                    if (exists(path)) return path;
                }
                catch (Exception ex) when (ex is ArgumentException or NotSupportedException) { }
            }
        return null;
    }

    internal sealed class EditorResolver(string documentPath, IReadOnlyDictionary<string, string>? buffers, string? allowedRoot = null) : XmlResolver
    {
        private readonly string root = allowedRoot is null ? IncludeRoot(documentPath) : Path.GetFullPath(allowedRoot);
        public override ICredentials? Credentials { set { } }

        private static string IncludeRoot(string path)
        {
            var directory = new DirectoryInfo(Path.GetDirectoryName(Path.GetFullPath(path))!);
            for (var current = directory; current is not null; current = current.Parent)
                if (current.Name.Equals("Controllers", StringComparison.OrdinalIgnoreCase) ||
                    current.Name.Equals("Controller", StringComparison.OrdinalIgnoreCase))
                {
                    var parent = current.Parent;
                    return (parent?.Name.Equals("App_Data", StringComparison.OrdinalIgnoreCase) == true ||
                        parent?.Name.Equals("AppData", StringComparison.OrdinalIgnoreCase) == true
                        ? parent.Parent : parent)?.FullName ?? current.FullName;
                }
            return directory.FullName;
        }

        private bool IsInside(string path)
        {
            var relative = Path.GetRelativePath(root, path);
            return !Path.IsPathRooted(relative) && relative != ".." &&
                !relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal);
        }

        public override Uri ResolveUri(Uri? baseUri, string? relativeUri)
        {
            var resolved = base.ResolveUri(baseUri, relativeUri);
            if (baseUri?.IsFile != true || relativeUri is null || !resolved.IsFile)
                return resolved;
            return Resolve(baseUri.LocalPath, relativeUri,
                path => IsInside(path) && (buffers?.ContainsKey(path) == true || File.Exists(path))) is { } local
                ? new Uri(local) : resolved;
        }

        public override object GetEntity(Uri absoluteUri, string? role, Type? ofObjectToReturn)
        {
            if (!absoluteUri.IsFile)
                throw new XmlException("Chỉ hỗ trợ include XML từ file.");
            var path = absoluteUri.LocalPath;
            if (!IsInside(path)) throw new XmlException("Include XML nằm ngoài thư mục dự án.");
            if (buffers?.TryGetValue(path, out var text) == true)
                return new MemoryStream(Encoding.UTF8.GetBytes(text), writable: false);
            return File.OpenRead(path);
        }
    }

    public static IncludeLocation? Find(
        string file,
        string text,
        int offset,
        Func<string, string?>? read = null,
        string? contextFile = null,
        string? contextText = null)
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
        string? ResolveInclude(string owner, string name) => Resolve(owner, name, path => Load(path) is not null);
        IncludeLocation? Location(string owner, string content, Match declaration, string? referenceName = null)
        {
            if (declaration.Groups["path"].Success)
            {
                if (ResolveInclude(owner, declaration.Groups["path"].Value) is not { } target || Load(target) is not { } targetText)
                    return null;
                var declaredName = declaration.Groups["name"].Value;
                var externalName = !string.IsNullOrWhiteSpace(referenceName)
                    ? referenceName
                    : !string.IsNullOrWhiteSpace(declaredName)
                        ? declaredName
                        : Path.GetFileNameWithoutExtension(target);
                return new(target, 1, 1, externalName, 0, targetText.Length);
            }
            var value = declaration.Groups["value"];
            var start = value.Success ? value.Index : declaration.Index;
            var length = value.Success ? value.Length : declaration.Length;
            var before = content[..start];
            var name = referenceName ?? declaration.Groups["name"].Value;
            return new(owner, before.Count(c => c == '\n') + 1, start - before.LastIndexOf('\n'), name, start, length);
        }
        bool Contains(Match m) => offset >= m.Index && offset <= m.Index + m.Length;
        var direct = Declarations.Matches(text).Cast<Match>().FirstOrDefault(Contains);
        direct ??= Doctype.Matches(text).Cast<Match>().FirstOrDefault(Contains);
        var reference = References.Matches(text).Cast<Match>().FirstOrDefault(Contains);
        if (reference == null) return direct == null ? null : Location(file, text, direct);
        IncludeLocation? SearchFrom(string owner, string content)
        {
            var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            IncludeLocation? Search(string currentOwner, string currentContent, int depth)
            {
                if (depth > 64 || !visited.Add(currentOwner)) return null;
                // Preserve newlines and offsets for navigation while ignoring commented declarations.
                currentContent = WithoutComments(currentContent);
                var declarations = Declarations.Matches(currentContent).Cast<Match>().ToArray();
                var match = declarations.FirstOrDefault(m => m.Groups["name"].Value == reference.Groups["name"].Value && m.Groups["parameter"].Success == (reference.Groups["kind"].Value == "%"));
                if (match != null) return Location(currentOwner, currentContent, match, reference.Groups["name"].Value);
                foreach (var external in declarations.Where(m => m.Groups["path"].Success).Concat(Doctype.Matches(currentContent).Cast<Match>()))
                    if (ResolveInclude(currentOwner, external.Groups["path"].Value) is { } path && Load(path) is { } nested && Search(path, nested, depth + 1) is { } result) return result;
                return null;
            }
            return Search(owner, content, 0);
        }
        var found = SearchFrom(file, text);
        if (found != null || string.IsNullOrWhiteSpace(contextFile) || contextText is null)
            return found;
        var root = Path.GetFullPath(contextFile);
        return root.Equals(file, StringComparison.OrdinalIgnoreCase)
            ? null
            : SearchFrom(root, contextText);
    }
}
