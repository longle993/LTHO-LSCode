using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using LSCode.Core;
using Microsoft.Data.SqlClient;

namespace LSCode.Infrastructure;

public sealed record StandardProjectRow(string Code, string Title, string Server, string SysDatabase,
    string WebProgramPath, string WebSourcePath, string AppProgramPath, string AppSourcePath, string UpdatePath,
    IReadOnlyDictionary<string, string>? Fields = null);
public sealed record StandardProjectPreview(string Code, string Title, string SourcePath, string Status, string? Note);
public sealed record StandardProjectSummary(int Added, int Updated, int Unchanged, int Skipped,
    string Fingerprint, StandardProjectPreview[] Projects);
public sealed record StandardProjectPlan(StandardProjectSummary Summary, List<LegacyProject> Projects);

public static class StandardProjectSync
{
    private static readonly Regex EmbeddedConnection = new(
        "string\\s+cVal\\s*=\\s*\"(?<encoded>[A-Za-z0-9+/=]+)\"",
        RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(300));

    public static string ReadFsdConnection(string sourceFile)
    {
        if (!File.Exists(sourceFile)) throw new FileNotFoundException("Không tìm thấy clsDatabase.cs của FSD Studio.", sourceFile);
        if (new FileInfo(sourceFile).Length > 2_000_000) throw new InvalidDataException("File nguồn FSD Studio quá lớn.");
        var match = EmbeddedConnection.Match(File.ReadAllText(sourceFile));
        if (!match.Success) throw new InvalidDataException("Không tìm thấy kết nối danh mục chuẩn trong clsDatabase.cs.");
        var encoded = match.Groups["encoded"].Value;
        var padded = encoded.PadRight((encoded.Length + 3) / 4 * 4, '=');
        string decoded;
        try { decoded = Encoding.UTF32.GetString(Convert.FromBase64String(padded)); }
        catch (FormatException ex) { throw new InvalidDataException("Kết nối FSD Studio không đúng định dạng.", ex); }
        var connection = new SqlConnectionStringBuilder(decoded);
        if (string.IsNullOrWhiteSpace(connection.DataSource) || string.IsNullOrWhiteSpace(connection.InitialCatalog))
            throw new InvalidDataException("Kết nối FSD Studio thiếu server hoặc database.");
        connection.ConnectTimeout = 15;
        connection.ApplicationName = "LS Code Standard Project Sync";
        if (!decoded.Contains("Encrypt=", StringComparison.OrdinalIgnoreCase))
            connection.Encrypt = SqlConnectionEncryptOption.Optional;
        return connection.ConnectionString;
    }

    public static async Task<StandardProjectRow[]> ReadAsync(string sourceFile, string currentUser, CancellationToken token)
    {
        currentUser = currentUser.Trim();
        if (currentUser.Length == 0) throw new ArgumentException("Nhập CurrUser của FSD Studio.");
        await using var connection = new SqlConnection(ReadFsdConnection(sourceFile));
        await connection.OpenAsync(token).ConfigureAwait(false);
        await using var command = new SqlCommand("dbo.ns_getListDept", connection)
        {
            CommandType = CommandType.StoredProcedure,
            CommandTimeout = 60
        };
        command.Parameters.Add("@Name", SqlDbType.VarChar, 128).Value = currentUser;
        await using var reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false);
        var columns = Enumerable.Range(0, reader.FieldCount).Select(reader.GetName).ToArray();
        var rows = new List<StandardProjectRow>();
        while (await reader.ReadAsync(token).ConfigureAwait(false))
        {
            if (rows.Count >= 10_000) throw new InvalidDataException("Danh mục chuẩn vượt 10.000 dự án.");
            var fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (var ordinal = 0; ordinal < columns.Length; ordinal++)
                fields[columns[ordinal]] = reader.IsDBNull(ordinal) ? "" : Convert.ToString(reader.GetValue(ordinal))?.Trim() ?? "";
            string Read(params string[] names)
            {
                foreach (var name in names) if (fields.TryGetValue(name, out var value)) return value;
                throw new InvalidDataException($"ns_getListDept thiếu cột {string.Join("/", names)}.");
            }
            rows.Add(new(Read("ma_da"), Read("ten_da"), Read("server", "ten_server"), Read("db_sys"),
                Read("dir_pro_web"), Read("dir_src_web"), Read("dir_pro_app"), Read("dir_src_app"), Read("dir_update"), fields));
        }
        return rows.ToArray();
    }

    public static StandardProjectPlan Plan(IEnumerable<LegacyProject> saved, IEnumerable<StandardProjectRow> source,
        CancellationToken token = default)
    {
        var projects = saved.ToList();
        var rows = source.ToArray();
        var previews = new List<StandardProjectPreview>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var byCode = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < projects.Count; i++)
        {
            var savedCode = projects[i].Field("StandardProjectCode");
            if (!string.IsNullOrWhiteSpace(savedCode)) byCode.TryAdd(savedCode, i);
            byCode.TryAdd(projects[i].Name, i);
        }
        foreach (var row in rows)
        {
            token.ThrowIfCancellationRequested();
            var code = row.Code.Trim();
            if (code.Length == 0 || !seen.Add(code))
            {
                previews.Add(new(code, row.Title, "", "skipped", "Thiếu hoặc trùng mã dự án trong danh mục chuẩn."));
                continue;
            }
            var sourcePath = new[] { row.WebSourcePath, row.WebProgramPath, row.AppSourcePath, row.AppProgramPath }
                .Select(path => path.Trim().TrimEnd('\\', '/'))
                .FirstOrDefault(Path.IsPathFullyQualified);
            if (sourcePath is null || string.IsNullOrWhiteSpace(row.Server) || string.IsNullOrWhiteSpace(row.SysDatabase))
            {
                previews.Add(new(code, row.Title, sourcePath ?? "", "skipped", "Thiếu đường dẫn nguồn, server hoặc SYS database."));
                continue;
            }
            var index = byCode.TryGetValue(code, out var foundIndex) ? foundIndex : -1;
            var previous = index >= 0 ? projects[index] : null;
            var candidate = previous ?? ProjectImporter.Create(code, row.Server.Trim(), row.SysDatabase.Trim(), sourcePath);
            var xml = XmlTools.Parse(candidate.RawXml).Root!;
            if (row.Fields is not null)
                foreach (var (name, value) in row.Fields)
                    Set(xml, name.Equals("xpass", StringComparison.OrdinalIgnoreCase) ? "FsdEncryptedPassword" : "Fsd_" + name, value);
            Set(xml, "StandardProjectCode", code);
            Set(xml, "StandardProjectTitle", row.Title.Trim());
            Set(xml, "servername", row.Server.Trim());
            Set(xml, "df_Database", row.SysDatabase.Trim());
            Set(xml, "SourcePath", sourcePath);
            Set(xml, "WinProgramPath", row.AppProgramPath.Trim());
            Set(xml, "WinSourcePath", row.AppSourcePath.Trim());
            Set(xml, "WebProgramPath", row.WebProgramPath.Trim());
            Set(xml, "WebSourcePath", row.WebSourcePath.Trim());
            Set(xml, "WebHost1", Value(row, "web_host1"));
            Set(xml, "WebHost2", Value(row, "web_host2"));
            Set(xml, "UpdatePath", row.UpdatePath.Trim());
            Set(xml, "User", Value(row, "xuser"));
            Set(xml, "Authentication", "sql");
            Set(xml, "Notes", Value(row, "ghi_chu"));
            Set(xml, "IsActive", Value(row, "status") is "0" or "False" or "false" ? "0" : "1");
            Set(xml, "HomeUpdateCode", Value(row, "ma_da_tmp", "ma_da_update"));
            Set(xml, "ProgramPath", First(Value(row, "web_host1"), Value(row, "web_host2"), row.WebProgramPath, row.AppProgramPath));
            // A catalog can contain thousands of UNC roots. Probing every share here can
            // stall the entire sync for minutes; project.open reads its config on demand.
            Set(xml, "WebConfigPath", Path.Combine(First(row.WebProgramPath, sourcePath), "web.config"));
            var updated = candidate with { RawXml = xml.ToString(SaveOptions.DisableFormatting) };
            var status = previous is null ? "added" : previous.RawXml == updated.RawXml ? "unchanged" : "updated";
            if (index >= 0) projects[index] = updated;
            else { byCode[code] = projects.Count; projects.Add(updated); }
            previews.Add(new(code, row.Title, sourcePath, status, null));
        }
        var fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join('\n',
            rows.Select(row => row.Fields is null
                ? string.Join('\u001f', row.Code, row.Title, row.Server, row.SysDatabase, row.WebProgramPath, row.WebSourcePath,
                    row.AppProgramPath, row.AppSourcePath, row.UpdatePath)
                : string.Join('\u001f', row.Fields.OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
                    .Select(pair => pair.Key + "=" + pair.Value)))))));
        return new(new(previews.Count(item => item.Status == "added"), previews.Count(item => item.Status == "updated"),
            previews.Count(item => item.Status == "unchanged"), previews.Count(item => item.Status == "skipped"),
            fingerprint, previews.ToArray()), projects);
    }

    private static void Set(XElement xml, string name, string value)
    {
        var field = xml.Elements().FirstOrDefault(element => element.Name.LocalName.Equals(name, StringComparison.OrdinalIgnoreCase));
        if (field is null) xml.Add(new XElement(name, value));
        else field.Value = value;
    }

    private static string Value(StandardProjectRow row, params string[] names)
    {
        if (row.Fields is null) return "";
        foreach (var name in names)
            if (row.Fields.TryGetValue(name, out var value) && !string.IsNullOrWhiteSpace(value)) return value.Trim();
        return "";
    }

    private static string First(params string[] values) => values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim() ?? "";
}
