using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Data.SqlClient;

namespace LSCode.Shell.Services;
public sealed record ProfilerCredentials(bool UseProjectIdentity, string User, string Password);
public static class ProfilerStorage
{
    public static ProfilerCredentials DefaultCredentials => new(false, "profile", "fsd");
    private static string CredentialPath(string server) => Path.Combine(SettingsStore.Root, "profiler-" + Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(server.ToUpperInvariant())))[..16] + ".dat");
    public static ProfilerCredentials? LoadCredentials(string server)
    {
        var path = CredentialPath(server); if (!File.Exists(path)) return null;
        return JsonSerializer.Deserialize<ProfilerCredentials>(ProtectedData.Unprotect(File.ReadAllBytes(path), null, DataProtectionScope.CurrentUser));
    }
    public static void SaveCredentials(string server, ProfilerCredentials value)
    {
        Directory.CreateDirectory(SettingsStore.Root); var path = CredentialPath(server);
        var data = ProtectedData.Protect(JsonSerializer.SerializeToUtf8Bytes(value), null, DataProtectionScope.CurrentUser);
        File.WriteAllBytes(path + ".tmp", data); File.Move(path + ".tmp", path, true);
    }
    public static SqlTarget Connection(SqlTarget project, ProfilerCredentials credentials)
    {
        var builder = new SqlConnectionStringBuilder(project.ConnectionString) { ApplicationName = "LSCode.Profiler" };
        if (!credentials.UseProjectIdentity) { builder.Authentication = SqlAuthenticationMethod.NotSpecified; builder.IntegratedSecurity = false; builder.UserID = credentials.User; builder.Password = credentials.Password; }
        return new(project.Server, project.Database, builder.ConnectionString);
    }
    public sealed record TraceFile(int Version, string Server, string Database, ProfilerTemplate Template, ProfilerEvent[] Events);
    public static void SaveTrace(string path, TraceFile trace)
    {
        ProfilerTemplate.Validate(trace.Template);
        File.WriteAllText(path + ".tmp", JsonSerializer.Serialize(trace, new JsonSerializerOptions { WriteIndented = true })); File.Move(path + ".tmp", path, true);
    }
    public static TraceFile LoadTrace(string path)
    {
        if (new FileInfo(path).Length > 64 * 1024 * 1024) throw new InvalidDataException("Trace vượt 64 MB.");
        var trace = JsonSerializer.Deserialize<TraceFile>(File.ReadAllText(path)) ?? throw new InvalidDataException("Trace trống.");
        if (trace.Version != 1 || trace.Events == null || trace.Events.Length > 5000) throw new InvalidDataException("Định dạng trace không hợp lệ (v1, tối đa 5000 dòng).");
        ProfilerTemplate.Validate(trace.Template); return trace;
    }
}
