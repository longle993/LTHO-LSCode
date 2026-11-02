using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using LSCode.Core;

namespace LSCode.Shell.Services;

public static class ProjectStore
{
    private static string FilePath => Path.Combine(SettingsStore.Root, "projects.dat");
    private static readonly byte[] Entropy = "LSCode.ProjectCatalog.v1"u8.ToArray();
    public static List<LegacyProject> Load()
    {
        if (!File.Exists(FilePath)) return [];
        var data = ProtectedData.Unprotect(File.ReadAllBytes(FilePath), Entropy, DataProtectionScope.CurrentUser);
        return JsonSerializer.Deserialize<List<LegacyProject>>(data) ?? [];
    }
    public static void Save(IEnumerable<LegacyProject> projects)
    {
        Directory.CreateDirectory(SettingsStore.Root);
        var data = JsonSerializer.SerializeToUtf8Bytes(projects);
        var encrypted = ProtectedData.Protect(data, Entropy, DataProtectionScope.CurrentUser);
        var temporary = FilePath + ".tmp"; File.WriteAllBytes(temporary, encrypted);
        if (File.Exists(FilePath)) File.Replace(temporary, FilePath, FilePath + ".bak", true);
        else File.Move(temporary, FilePath);
    }
}
