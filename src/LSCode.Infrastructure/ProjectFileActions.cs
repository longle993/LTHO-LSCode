using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using LSCode.Core;
using LSCode.Shell.Services;

namespace LSCode.Infrastructure;

internal static class ProjectFileActions
{
    private static string Root(LegacyProject project)
    {
        if (!Path.IsPathFullyQualified(project.SourcePath))
            throw new InvalidOperationException("Dự án chưa có đường dẫn thư mục đầy đủ.");
        return Path.GetFullPath(project.SourcePath);
    }

    private static bool IsInside(string root, string path) =>
        Path.GetRelativePath(root, path) is var relative &&
        relative != "." && relative != ".." &&
        !relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal) &&
        !Path.IsPathRooted(relative);

    private static string HistoryPath(LegacyProject project) => Path.Combine(SettingsStore.Root,
        "file-history", Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(project.Id))) + ".json");

    internal static string[] ReadHistory(LegacyProject project)
    {
        var historyPath = HistoryPath(project);
        if (!File.Exists(historyPath)) return [];
        var root = Root(project);
        var entries = JsonSerializer.Deserialize<string[]>(File.ReadAllText(historyPath)) ?? [];
        return entries.Where(path => !Path.IsPathRooted(path))
            .Select(path => Path.GetFullPath(path, root))
            .Where(path => IsInside(root, path)).ToArray();
    }

    internal static void RecordHistory(LegacyProject project, string path)
    {
        var root = Root(project);
        if (!Path.IsPathFullyQualified(path) || !IsInside(root, path))
            throw new ArgumentException("File nằm ngoài thư mục dự án.");
        WriteHistory(project, ReadHistory(project).Prepend(Path.GetFullPath(path)));
    }

    internal static void RemoveStructureHistory(LegacyProject project, string folder)
    {
        var files = ReadHistory(project);
        var remaining = files.Where(file => !IsInside(folder, file)).ToArray();
        if (files.Length != remaining.Length) WriteHistory(project, remaining);
    }

    internal static void RemoveHistory(LegacyProject project, string path) =>
        RemoveHistory(project, [path]);

    internal static void RemoveHistory(LegacyProject project, IEnumerable<string> paths)
    {
        var removed = paths.ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (removed.Count == 0) return;
        var files = ReadHistory(project);
        var remaining = files.Where(file => !removed.Contains(file)).ToArray();
        if (remaining.Length != files.Length) WriteHistory(project, remaining);
    }

    private static void WriteHistory(LegacyProject project, IEnumerable<string> files)
    {
        var path = HistoryPath(project);
        var entries = files.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        if (entries.Length == 0)
        {
            if (File.Exists(path)) File.Delete(path);
            return;
        }
        var root = Root(project);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(temporary, JsonSerializer.Serialize(entries
                .Select(file => Path.GetRelativePath(root, file))));
            File.Move(temporary, path, true);
        }
        finally { File.Delete(temporary); }
    }

    internal static async Task<string> RefreshConfigAsync(LegacyProject project, CancellationToken ct)
    {
        var root = Root(project);
        var path = Path.Combine(root, "web.config");
        if (!IsInside(root, path) || !Path.GetFileName(path).Equals("web.config", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("web.config phải nằm trong thư mục dự án.");
        EnsureOrdinaryPath(root, path);
        await using var file = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.Read, 4096, true);
        var buffer = new byte[8192];
        int read;
        while ((read = await file.ReadAsync(buffer, ct)) > 0)
        {
            file.Position -= read;
            await file.WriteAsync(buffer.AsMemory(0, read), ct);
        }
        await file.FlushAsync(ct);
        return path;
    }

    internal static string StructureFolder(LegacyProject project)
    {
        var root = Root(project);
        var requested = Path.Combine(root, "Controller", "Structure", "App");
        var alternatives = from prefix in new[] { "", "App_Data", "AppData" }
                           from controller in new[] { "Controller", "Controllers" }
                           select Path.Combine(root, prefix, controller, "Structure", "App");
        var folders = Directory.Exists(requested) ? [requested] : alternatives.Where(Directory.Exists).ToArray();
        if (folders.Length != 1) throw new InvalidOperationException(folders.Length == 0
            ? "Không tìm thấy thư mục Controller/Structure/App."
            : "Có nhiều thư mục Structure/App trong dự án. Cần xác định một thư mục duy nhất.");
        EnsureOrdinaryPath(root, folders[0]);
        return folders[0];
    }

    private static void EnsureOrdinaryPath(string root, string path)
    {
        for (var current = path; IsInside(root, current); current = Path.GetDirectoryName(current)!)
            if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                throw new IOException("Đường dẫn thao tác đi qua liên kết file hoặc thư mục.");
    }

    internal static IEnumerable<string> StructureFiles(string folder) =>
        Directory.EnumerateFiles(folder, "*", new EnumerationOptions
        {
            RecurseSubdirectories = true, IgnoreInaccessible = false, AttributesToSkip = FileAttributes.ReparsePoint,
        });

    internal static int ClearStructure(string folder, CancellationToken ct)
    {
        var files = StructureFiles(folder).ToArray();
        var deleted = 0;
        foreach (var file in files)
        {
            ct.ThrowIfCancellationRequested();
            try { File.Delete(file); deleted++; }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                throw new IOException($"Đã xóa {deleted}/{files.Length} file. Không thể xóa {file}: {error.Message}", error);
            }
        }
        return deleted;
    }
}
