namespace LSCode.Infrastructure;

internal static class SourceCollection
{
    internal sealed record Match(string Root, string RelativePath, bool Checked);
    internal sealed record SearchResult(List<Match> Files, bool Truncated, List<string> Errors);
    internal sealed record CopyResult(int Copied, int Skipped, List<string> Errors);
    internal sealed record ContentResult(List<int> Indices, int Skipped);
    private static readonly HashSet<string> ErpTextExtensions = new(StringComparer.OrdinalIgnoreCase)
        { ".f", ".ent", ".aspx", ".ascx" };

    internal static async Task<ContentResult> FindContentAsync(Match[] files, string query, CancellationToken ct)
    {
        var hits = new List<int>();
        var skipped = 0;
        if (string.IsNullOrWhiteSpace(query)) return new(hits, skipped);
        for (var index = 0; index < files.Length; index++)
        {
            ct.ThrowIfCancellationRequested();
            var file = files[index];
            try
            {
                var path = ChildPath(file.Root, file.RelativePath);
                var extension = Path.GetExtension(path);
                if (!LSCode.Core.WorkspaceSearch.TextExtensions.Contains(extension) &&
                    !ErpTextExtensions.Contains(extension)) continue;
                CheckLinks(path);
                if (new FileInfo(path).Length > 4 * 1024 * 1024)
                {
                    skipped++;
                    continue;
                }
                var snapshot = await LSCode.Core.DocumentStore.OpenAsync(path, ct);
                if (snapshot.Text.Contains(query, StringComparison.OrdinalIgnoreCase)) hits.Add(index);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
            {
                skipped++;
            }
        }
        return new(hits, skipped);
    }

    internal static SearchResult Find(string[] roots, string[] samples, CancellationToken ct)
    {
        var comparison = StringComparer.OrdinalIgnoreCase;
        var names = samples.ToLookup(Stem, ParentName, comparison);
        var files = new List<Match>();
        var errors = new List<string>();
        var seen = new HashSet<string>(comparison);
        var options = new EnumerationOptions
        {
            RecurseSubdirectories = true,
            IgnoreInaccessible = false,
            AttributesToSkip = FileAttributes.ReparsePoint
        };
        foreach (var root in roots.Select(FullPath).Distinct(comparison))
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                CheckLinks(root);
                foreach (var file in Directory.EnumerateFiles(root, "*", options))
                {
                    ct.ThrowIfCancellationRequested();
                    var name = Stem(file);
                    if (!names.Contains(name) || !seen.Add(file)) continue;
                    if (files.Count == 500) return Result(true);
                    files.Add(new(root, Path.GetRelativePath(root, file), names[name].Contains(ParentName(file), comparison)));
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                errors.Add($"{root} — {ex.Message}");
            }
        }
        return Result(false);

        SearchResult Result(bool truncated) => new(files.OrderByDescending(file => file.Checked)
            .ThenBy(file => file.RelativePath, comparison).ThenBy(file => file.Root, comparison).ToList(), truncated, errors);
    }

    internal static async Task<CopyResult> CopyAsync(string destination, Match[] files, CancellationToken ct)
    {
        var root = FullPath(destination);
        if (!Directory.Exists(root)) throw new DirectoryNotFoundException("Không tìm thấy thư mục đích.");
        var copied = 0;
        var skipped = 0;
        var errors = new List<string>();
        foreach (var file in files)
        {
            ct.ThrowIfCancellationRequested();
            string? temp = null;
            try
            {
                var source = ChildPath(file.Root, file.RelativePath);
                var target = ChildPath(root, file.RelativePath);
                if (File.Exists(target) || Directory.Exists(target))
                {
                    skipped++;
                    continue;
                }
                CheckLinks(source);
                CheckLinks(target);
                var parent = Path.GetDirectoryName(target)!;
                Directory.CreateDirectory(parent);
                CheckLinks(parent);
                await using (var input = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, true))
                {
                    var pending = Path.Combine(parent, $".lscode-{Guid.NewGuid():N}.tmp");
                    await using var output = new FileStream(pending, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, true);
                    temp = pending;
                    await input.CopyToAsync(output, ct);
                }
                ct.ThrowIfCancellationRequested();
                try { File.Move(temp, target, overwrite: false); }
                catch (IOException) when (File.Exists(target) || Directory.Exists(target))
                {
                    skipped++;
                    continue;
                }
                temp = null;
                copied++;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
            {
                errors.Add($"{file.RelativePath} — {ex.Message}");
            }
            finally
            {
                if (temp is not null)
                {
                    try { File.Delete(temp); }
                    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                    {
                        errors.Add($"{temp} — {ex.Message}");
                    }
                }
            }
        }
        return new(copied, skipped, errors);
    }

    private static string Stem(string path) => Path.GetFileName(path).Split('.')[0];
    private static string ParentName(string path) => Path.GetFileName(Path.GetDirectoryName(path)) ?? "";

    private static string FullPath(string path) => Path.IsPathFullyQualified(path)
        ? Path.TrimEndingDirectorySeparator(Path.GetFullPath(path))
        : throw new ArgumentException("Chọn đường dẫn thư mục đầy đủ.");

    private static string ChildPath(string root, string relativePath)
    {
        root = FullPath(root);
        if (string.IsNullOrWhiteSpace(relativePath) || Path.IsPathRooted(relativePath) || relativePath.Contains(':'))
            throw new ArgumentException("Đường dẫn file không hợp lệ.");
        var path = Path.GetFullPath(relativePath, root);
        var prefix = Path.EndsInDirectorySeparator(root) ? root : root + Path.DirectorySeparatorChar;
        if (path.Equals(root, StringComparison.OrdinalIgnoreCase) || !path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Đường dẫn file nằm ngoài thư mục đã chọn.");
        return path;
    }

    private static void CheckLinks(string path)
    {
        var parent = Path.GetDirectoryName(path);
        if (parent is null) return;
        CheckLinks(parent);
        try
        {
            if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
                throw new IOException("Không sao chép qua liên kết file hoặc thư mục.");
        }
        catch (FileNotFoundException) { }
        catch (DirectoryNotFoundException) { }
    }
}
