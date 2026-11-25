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
        CancellationToken ct, IReadOnlyDictionary<string, string>? openBuffers = null, bool firstPerFile = false,
        IProgress<IReadOnlyList<SearchHit>>? progress = null)
    {
        if (string.IsNullOrEmpty(query)) return [];
        var pattern = new Regex(regex ? query : Regex.Escape(query), matchCase ? RegexOptions.None : RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(200));
        return await Task.Run(async () =>
        {
            var hits = new List<SearchHit>();
            var published = 0;
            var clock = System.Diagnostics.Stopwatch.StartNew();
            // Bound concurrent reads and retain traversal order in the results.
            foreach (var batch in files.Chunk(firstPerFile ? 8 : 1))
            {
                ct.ThrowIfCancellationRequested();
                var found = (await Task.WhenAll(batch.Select(ScanAsync))).SelectMany(matches => matches)
                    .Take(2000 - hits.Count).ToArray();
                hits.AddRange(found);
                if (progress is not null && hits.Count > published &&
                    (published == 0 || clock.ElapsedMilliseconds >= 80))
                {
                    progress.Report(hits.GetRange(published, hits.Count - published));
                    published = hits.Count;
                    clock.Restart();
                }
                if (hits.Count == 2000) return hits;
            }
            return hits;

            async Task<List<SearchHit>> ScanAsync(string file)
            {
                ct.ThrowIfCancellationRequested();
                var matches = new List<SearchHit>();
                string content;
                try { content = openBuffers != null && openBuffers.TryGetValue(file, out var buffer) ? buffer : await DocumentStore.ReadTextAsync(file, ct); }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Text.DecoderFallbackException) { return matches; }
                using var reader = new StringReader(content);
                var number = 0;
                while (reader.ReadLine() is { } line)
                {
                    ct.ThrowIfCancellationRequested(); number++;
                    var match = pattern.Match(line);
                    if (!match.Success) continue;
                    var start = line.Length > 220 ? Math.Max(0, match.Index - 50) : 0;
                    matches.Add(new(file, number, match.Index + 1, line.Substring(start, Math.Min(220, line.Length - start))));
                    if (firstPerFile || matches.Count == 2000) break;
                }
                return matches;
            }
        }, ct);
    }
}
