using System.IO;
using System.Text.Json;

namespace LSCode.Shell.Services;

public sealed class StudioSettings
{
    public string Theme { get; set; } = "Dark";
    public string? AccentColor { get; set; }
    public Dictionary<string, Dictionary<string, Dictionary<string, string>>>? ButtonColors { get; set; }
    public int ExplorerFontSize { get; set; } = 12;
    public int EditorFontSize { get; set; } = 14;
    public int ExplorerWidth { get; set; } = 264;
    public List<string>? ActivityCommands { get; set; }
    public Dictionary<string, string[]> Shortcuts { get; set; } = [];
    public string? Workspace { get; set; }
    public string? ActiveProjectId { get; set; }
    public Dictionary<string, DateTimeOffset> ProjectLastAccess { get; set; } = [];
    public List<string> RecentFiles { get; set; } = [];
    public string Server { get; set; } = ".";
    public string Database { get; set; } = "master";
    public bool IntegratedSecurity { get; set; } = true;
    public bool TrustCertificate { get; set; }
    public string SqlUser { get; set; } = "";
    public string CrystalPath { get; set; } = "";
    public string Profiler18Path { get; set; } = "";
    public string StandardCatalogUser { get; set; } = "";
    public string SourceCollectionVersion { get; set; } = "";
}
public static class SettingsStore
{
    public static string Root => Environment.GetEnvironmentVariable("LSCODE_DATA_HOME") ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LSCode");
    public static string FilePath => Path.Combine(Root, "settings.json");
    public static StudioSettings Load()
    {
        try { return File.Exists(FilePath) ? JsonSerializer.Deserialize<StudioSettings>(File.ReadAllText(FilePath)) ?? new() : new(); }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException) { return new(); }
    }
    public static void Save(StudioSettings settings)
    {
        if (settings.ButtonColors is { } themes)
            foreach (var (theme, roles) in themes)
            {
                if (theme is not ("Light" or "Dark" or "High Contrast") || roles is null)
                    throw new ArgumentException("Theme màu nút không hợp lệ.");
                foreach (var (role, colors) in roles)
                {
                    if (role is not ("primary" or "secondary" or "ghost" or "destructive" or "icon") || colors is null)
                        throw new ArgumentException("Loại nút không hợp lệ.");
                    foreach (var (part, color) in colors)
                        if (part is not ("background" or "foreground" or "border") ||
                            color is not { Length: 7 } || color[0] != '#' || !color.Skip(1).All(Uri.IsHexDigit))
                            throw new ArgumentException("Màu nút phải có dạng #RRGGBB.");
                }
            }
        Directory.CreateDirectory(Root);
        var temp = FilePath + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true }));
        File.Move(temp, FilePath, true);
    }
}
