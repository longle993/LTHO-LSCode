using System.Text.Json;
using LSCode.Shell.Services;

namespace LSCode.Infrastructure;

internal static class SourceVersionCatalog
{
    internal sealed record Entry(string Group, string Version, string Root);
    private static readonly string[] Groups = ["FAW", "FBO-FBI", "HRM"];
    private static string GroupPath(string group) => Path.Combine(SettingsStore.Root, $"source-versions-{group}.json");

    internal static Entry[] Load() => Groups.SelectMany(group => ReadGroup(GroupPath(group)))
        .OrderBy(entry => entry.Version, StringComparer.OrdinalIgnoreCase)
        .ThenBy(entry => entry.Root, StringComparer.OrdinalIgnoreCase).ToArray();

    private static Entry[] ReadGroup(string path) => File.Exists(path)
        ? JsonSerializer.Deserialize<Entry[]>(File.ReadAllText(path)) ?? []
        : [];

    internal static Entry[] Discover(string group, CancellationToken ct)
    {
        const string collection = @"\\172.168.5.14\SourceCollection";
        string[] paths = group switch
        {
            "FAW" or "FBO-FBI" => Directory.EnumerateDirectories(Path.Combine(collection, group), "*",
                new EnumerationOptions { AttributesToSkip = FileAttributes.ReparsePoint, IgnoreInaccessible = false })
                .Select(path => { ct.ThrowIfCancellationRequested(); return path; }).ToArray(),
            "HRM" => [@"\\172.168.5.14\CustomerPro\HRM\DEMO24\SP24", @"\\172.168.5.14\CustomerPro\HRM\HRM227\SP227"],
            _ => throw new ArgumentException("Nhóm phiên bản không hợp lệ.")
        };
        ct.ThrowIfCancellationRequested();
        return paths.Select(path => new Entry(group, Path.GetFileName(Path.TrimEndingDirectorySeparator(path)), path)).ToArray();
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
