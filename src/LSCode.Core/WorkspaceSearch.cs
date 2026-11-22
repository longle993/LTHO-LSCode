using System.Text.RegularExpressions;

namespace LSCode.Core;

public sealed record SearchHit(string Path, int Line, int Column, string Preview)
{
    public override string ToString() => $"{System.IO.Path.GetFileName(Path)}:{Line}  {Preview.Trim()}";
}
public static class WorkspaceSearch
{
    public static readonly HashSet<string> IgnoredDirectories = new(StringComparer.OrdinalIgnoreCase) { ".git", ".vs", "bin", "obj", "node_modules", "artifacts" };
    public static readonly HashSet<string> TextExtensions = new(StringComparer.OrdinalIgnoreCase) { ".cs", ".sql", ".xml", ".xaml", ".json", ".js", ".ts", ".css", ".html", ".txt", ".md", ".config", ".csproj", ".props", ".sln", ".slnx", ".csv", ".yaml", ".yml" };
    public static IEnumerable<string> Files(string root, CancellationToken ct = default)
    {
        var pending = new Stack<string>(); pending.Push(root);
        while (pending.TryPop(out var folder))
        {
            ct.ThrowIfCancellationRequested();
            string[] files, dirs;
            try { files = Directory.GetFiles(folder); dirs = Directory.GetDirectories(folder); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { continue; }
            foreach (var file in files)
                if ((File.GetAttributes(file) & FileAttributes.ReparsePoint) == 0 && TextExtensions.Contains(Path.GetExtension(file))) yield return file;
            foreach (var dir in dirs)
                if (!IgnoredDirectories.Contains(Path.GetFileName(dir)) && (File.GetAttributes(dir) & FileAttributes.ReparsePoint) == 0) pending.Push(dir);
        }
    }
    public static Task<List<SearchHit>> FindAsync(string root, string query, bool regex, bool matchCase, CancellationToken ct, IReadOnlyDictionary<string, string>? openBuffers = null) =>
        FindAsync(Files(root, ct), query, regex, matchCase, ct, openBuffers);

    public static async Task<List<SearchHit>> FindAsync(IEnumerable<string> files, string query, bool regex, bool matchCase,
        CancellationToken ct, IReadOnlyDictionary<string, string>? openBuffers = null, bool firstPerFile = false)
    {
        if (string.IsNullOrEmpty(query)) return [];
        var pattern = new Regex(regex ? query : Regex.Escape(query), matchCase ? RegexOptions.None : RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(200));
        return await Task.Run(async () =>
        {
            var hits = new List<SearchHit>();
            foreach (var file in files)
            {
                ct.ThrowIfCancellationRequested();
                string content;
                try { content = openBuffers != null && openBuffers.TryGetValue(file, out var buffer) ? buffer : await DocumentStore.ReadTextAsync(file, ct); }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Text.DecoderFallbackException) { continue; }
                using var reader = new StringReader(content);
                var number = 0;
                while (reader.ReadLine() is { } line)
                {
                    ct.ThrowIfCancellationRequested(); number++;
                    var match = pattern.Match(line);
                    if (!match.Success) continue;
                    var start = line.Length > 220 ? Math.Max(0, match.Index - 50) : 0;
                    hits.Add(new(file, number, match.Index + 1, line.Substring(start, Math.Min(220, line.Length - start))));
                    if (hits.Count >= 2000) return hits;
                    if (firstPerFile) break;
                }
            }
            return hits;
        }, ct);
    }
}
