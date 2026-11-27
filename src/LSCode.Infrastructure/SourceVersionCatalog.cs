using System.Text.Json;
using LSCode.Shell.Services;

namespace LSCode.Infrastructure;

internal static class SourceVersionCatalog
{
    internal sealed record Entry(string Group, string Version, string Root);
    private static readonly string[] Groups = ["FAP", "FastFE", "FAW", "FBFE", "FBO-FBI", "FE_TT78", "HDDV", "QLTB", "HRM"];
    private static string GroupPath(string group) => Path.Combine(SettingsStore.Root, $"source-versions-{group}.json");
    private static bool IsSelectableVersion(string version) =>
        !version.TrimStart().StartsWith("ZZZ", StringComparison.OrdinalIgnoreCase);

    internal static Entry[] Load() => Groups.SelectMany(group => ReadGroup(GroupPath(group)))
        .Where(entry => IsSelectableVersion(entry.Version))
        .OrderBy(entry => entry.Version, StringComparer.OrdinalIgnoreCase)
        .ThenBy(entry => entry.Root, StringComparer.OrdinalIgnoreCase).ToArray();

    private static Entry[] ReadGroup(string path) => File.Exists(path)
        ? JsonSerializer.Deserialize<Entry[]>(File.ReadAllText(path)) ?? []
        : [];

    internal static Entry[] Discover(string group, CancellationToken ct)
    {
        const string collection = @"\\172.168.5.14\SourceCollection";
        if (!Groups.Contains(group, StringComparer.OrdinalIgnoreCase))
            throw new ArgumentException("Nhóm phiên bản không hợp lệ.");
        string[] paths = group.Equals("HRM", StringComparison.OrdinalIgnoreCase)
            ? [@"\\172.168.5.14\CustomerPro\HRM\DEMO24\SP24", @"\\172.168.5.14\CustomerPro\HRM\HRM227\SP227"]
            : Directory.EnumerateDirectories(Path.Combine(collection, group), "*",
                new EnumerationOptions { AttributesToSkip = FileAttributes.ReparsePoint, IgnoreInaccessible = false })
                .Select(path => { ct.ThrowIfCancellationRequested(); return path; }).ToArray();
        ct.ThrowIfCancellationRequested();
        return paths.Select(path => new Entry(group, Path.GetFileName(Path.TrimEndingDirectorySeparator(path)), path))
            .Where(entry => IsSelectableVersion(entry.Version)).ToArray();
    }

    internal static Entry[] SaveGroup(string group, Entry[] entries, CancellationToken ct)
    {
        var path = GroupPath(group);
        Directory.CreateDirectory(SettingsStore.Root);
        var temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(temp, JsonSerializer.Serialize(entries));
            ct.ThrowIfCancellationRequested();
            File.Move(temp, path, overwrite: true);
        }
        finally
        {
            File.Delete(temp);
        }
        return Load();
    }
}
