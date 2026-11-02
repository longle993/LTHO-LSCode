using System.Security.Cryptography;

namespace LSCode.Core;

public sealed record FolderDiffRow(string RelativePath, string Status, string? LeftPath, string? RightPath, string Detail = "");

public static class FolderDiff
{
    private sealed record Entry(string Path, bool Directory, bool Link);
    public static async Task<IReadOnlyList<FolderDiffRow>> CompareAsync(string left, string right, CancellationToken token)
    {
        var errors = new List<FolderDiffRow>();
        Dictionary<string, Entry> Scan(string root, bool isLeft)
        {
            root = Path.GetFullPath(root);
            if (!Directory.Exists(root)) throw new DirectoryNotFoundException(root);
            if ((File.GetAttributes(root) & FileAttributes.ReparsePoint) != 0) throw new IOException("Chọn thư mục gốc thực, không chọn liên kết: " + root);
            var entries = new Dictionary<string, Entry>(StringComparer.OrdinalIgnoreCase);
            void Visit(string folder)
            {
                token.ThrowIfCancellationRequested();
                try
                {
                    foreach (var path in Directory.EnumerateFileSystemEntries(folder))
                    {
                        token.ThrowIfCancellationRequested();
                        var relative = Path.GetRelativePath(root, path);
                        try
                        {
                            var attr = File.GetAttributes(path);
                            var entry = new Entry(path, (attr & FileAttributes.Directory) != 0, (attr & FileAttributes.ReparsePoint) != 0);
                            entries.Add(relative, entry);
                            if (entry.Directory && !entry.Link) Visit(path);
                        }
                        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                        { errors.Add(new(relative, "Lỗi", isLeft ? path : null, isLeft ? null : path, ex.Message)); }
                    }
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                { errors.Add(new(Path.GetRelativePath(root, folder), "Lỗi", isLeft ? folder : null, isLeft ? null : folder, ex.Message)); }
            }
            Visit(root); return entries;
        }
        var a = Scan(left, true); var b = Scan(right, false);
        var rows = new List<FolderDiffRow>();
        foreach (var name in a.Keys.Union(b.Keys, StringComparer.OrdinalIgnoreCase).Order(StringComparer.OrdinalIgnoreCase))
        {
            token.ThrowIfCancellationRequested();
            a.TryGetValue(name, out var x); b.TryGetValue(name, out var y);
            string status;
            try
            {
                if (x?.Link == true || y?.Link == true) status = "Bỏ qua liên kết";
                else if (x == null) status = "Chỉ bên phải";
                else if (y == null) status = "Chỉ bên trái";
                else if (x.Directory != y.Directory) status = "Khác loại";
                else if (x.Directory) status = "Thư mục";
                else
                {
                    await using var first = new FileStream(x.Path, FileMode.Open, FileAccess.Read, FileShare.Read, 65536, FileOptions.Asynchronous | FileOptions.SequentialScan);
                    await using var second = new FileStream(y.Path, FileMode.Open, FileAccess.Read, FileShare.Read, 65536, FileOptions.Asynchronous | FileOptions.SequentialScan);
                    status = "Khác nội dung";
                    if (first.Length == second.Length)
                    {
                        var firstHash = await SHA256.HashDataAsync(first, token);
                        var secondHash = await SHA256.HashDataAsync(second, token);
                        if (firstHash.SequenceEqual(secondHash)) status = "Giống nhau";
                    }
                }
                rows.Add(new(name, status, x?.Path, y?.Path));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            { rows.Add(new(name, "Lỗi", x?.Path, y?.Path, ex.Message)); }
        }
        rows.AddRange(errors);
        return rows;
    }
}
