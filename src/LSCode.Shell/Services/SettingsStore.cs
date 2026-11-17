using System.IO;
using System.Text.Json;

namespace LSCode.Shell.Services;

public sealed class StudioSettings
{
    public string Theme { get; set; } = "Dark";
    public string? AccentColor { get; set; }
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
        Directory.CreateDirectory(Root);
        var temp = FilePath + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true }));
        File.Move(temp, FilePath, true);
    }
}
