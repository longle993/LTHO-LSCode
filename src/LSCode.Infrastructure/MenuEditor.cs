using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using LSCode.Core;
using LSCode.Shell.Services;
using Microsoft.Data.SqlClient;

namespace LSCode.Infrastructure;

internal static class MenuEditor
{
    private static readonly string[] PreferredFields =
    [
        "wmenu_id", "wmenu_id0", "bar", "bar2", "menu_id", "link", "parameter", "icon_url",
        "status", "icon", "sysid", "type", "syscode", "msys", "target", "xtype", "edition", "expi_icon"
    ];

    internal sealed record Field(string Name, string Type, int MaxLength, bool Nullable, bool ReadOnly, bool HasDefault);
    internal sealed record Resource(string Kind, string Status, string[] Paths);

    public static async Task<object> ReadAsync(SqlTarget target, string wmenuId, CancellationToken token)
    {
        await using var connection = new SqlConnection(target.ConnectionString);
        await connection.OpenAsync(token).ConfigureAwait(false);
        var fields = await FieldsAsync(connection, null, token).ConfigureAwait(false);
        var values = await RowAsync(connection, null, fields, wmenuId, token).ConfigureAwait(false)
            ?? throw new KeyNotFoundException("Menu không còn tồn tại trong dbo.wcommand.");
        return new { mode = "edit", fields, values, version = Fingerprint(values), canSave = true };
    }

    public static async Task<object> NewAsync(SqlTarget target, string selectedId, CancellationToken token)
    {
        await using var connection = new SqlConnection(target.ConnectionString);
        await connection.OpenAsync(token).ConfigureAwait(false);
        var fields = await FieldsAsync(connection, null, token).ConfigureAwait(false);
        var candidate = await GenerateIdAsync(connection, null, selectedId, token).ConfigureAwait(false);
        var values = fields.ToDictionary(field => field.Name, field => DefaultValue(field), StringComparer.OrdinalIgnoreCase);
        values["wmenu_id"] = candidate.Id;
        if (values.ContainsKey("wmenu_id0")) values["wmenu_id0"] = candidate.Parent;
        if (values.ContainsKey("menu_id"))
            values["menu_id"] = await GenerateCommandIdAsync(connection, token).ConfigureAwait(false);
        if (values.ContainsKey("status")) values["status"] = "1";
        return new { mode = "new", fields, values, version = "", canSave = true };
    }

    public static async Task<object> SaveAsync(SqlTarget target, string mode, string originalId, string version,
        IReadOnlyDictionary<string, string?> submitted, CancellationToken token)
    {
        await using var connection = new SqlConnection(target.ConnectionString);
        await connection.OpenAsync(token).ConfigureAwait(false);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(IsolationLevel.Serializable, token).ConfigureAwait(false);
        string savedId;
        try
        {
            var fields = await FieldsAsync(connection, transaction, token).ConfigureAwait(false);
            var editable = fields.Where(field => !field.ReadOnly).ToDictionary(field => field.Name, StringComparer.OrdinalIgnoreCase);
            var values = submitted.Where(pair => editable.ContainsKey(pair.Key))
                .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);
            foreach (var required in new[] { "wmenu_id", "bar" })
                if (!values.TryGetValue(required, out var value) || string.IsNullOrWhiteSpace(value))
                    throw new ArgumentException($"{required} không được để trống.");

            savedId = values["wmenu_id"]!.Trim();
            if (savedId.Length > 128) throw new ArgumentException("wmenu_id vượt quá 128 ký tự.");
            if (values.TryGetValue("wmenu_id0", out var parent) && savedId.Equals(parent?.Trim(), StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Menu không thể là cha của chính nó.");

            if (mode.Equals("new", StringComparison.OrdinalIgnoreCase))
            {
                if (await ExistsAsync(connection, transaction, savedId, values.GetValueOrDefault("menu_id"), token).ConfigureAwait(false))
                    throw new InvalidOperationException("Mã menu đã tồn tại trong wcommand hoặc command. Nhấn Thêm mới để lấy mã khác.");
                var columns = values.Keys.Where(name => !(values[name] is null && editable[name].HasDefault)).ToArray();
                var sql = $"INSERT dbo.wcommand ({string.Join(',', columns.Select(Quote))}) VALUES ({string.Join(',', columns.Select((_, i) => "@p" + i))})";
                await ExecuteAsync(connection, transaction, sql, columns.Select((name, i) => Parameter("@p" + i, editable[name], values[name])).ToArray(), token).ConfigureAwait(false);
            }
            else
            {
                var current = await RowAsync(connection, transaction, fields, originalId, token, lockRow: true).ConfigureAwait(false)
                    ?? throw new KeyNotFoundException("Menu đã bị xóa bởi phiên khác.");
                if (!CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(Fingerprint(current)), Encoding.UTF8.GetBytes(version)))
                    throw new InvalidOperationException("Menu đã được thay đổi bởi phiên khác. Tải lại trước khi lưu.");
                var columns = values.Keys.Where(name => !name.Equals("wmenu_id", StringComparison.OrdinalIgnoreCase)).ToArray();
                var sql = $"UPDATE dbo.wcommand SET {string.Join(',', columns.Select((name, i) => Quote(name) + "=@p" + i))} WHERE RTRIM(wmenu_id)=@original";
                var parameters = columns.Select((name, i) => Parameter("@p" + i, editable[name], values[name])).ToList();
                parameters.Add(new SqlParameter("@original", originalId));
                await ExecuteAsync(connection, transaction, sql, parameters.ToArray(), token).ConfigureAwait(false);
            }
            await transaction.CommitAsync(token).ConfigureAwait(false);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
            throw;
        }
        return await ReadAsync(target, savedId, token).ConfigureAwait(false);
    }

    public static async Task<object> ResourcesAsync(string root, string link, string sysId, CancellationToken token)
    {
        return await Task.Run(async () =>
        {
            var main = ProjectConventions.ResolveMenuFile(root, link);
            var names = main != null && Path.GetExtension(main).Equals(".aspx", StringComparison.OrdinalIgnoreCase)
                ? ProjectConventions.Controllers(await File.ReadAllTextAsync(main, token).ConfigureAwait(false))
                : Array.Empty<string>();
            if (!Directory.Exists(Path.Combine(root, "Main")))
            {
                var metadataController = ProjectConventions.ControllerFromPageMetadata(root, sysId, link);
                if (!string.IsNullOrWhiteSpace(metadataController)) names = [metadataController];
                else if (names.Count == 0 && !string.IsNullOrWhiteSpace(sysId)) names = [sysId];
            }
            var roots = new[] { Path.Combine(root, "App_Data"), Path.Combine(root, "AppData") }.Where(Directory.Exists).ToArray();
            var files = (roots.Length == 0 ? [root] : roots).SelectMany(folder => WorkspaceFileIndex.Files(folder, token, false)).Take(100001).ToArray();
            if (files.Length > 100000) throw new IOException("Project vượt 100000 file; cần thu hẹp thư mục.");
            var fallback = Path.GetFileNameWithoutExtension(main ?? Uri.UnescapeDataString(link.Split('?', '#')[0]));
            var controllers = (names.Count > 0 ? names : [fallback]).Where(name => !string.IsNullOrWhiteSpace(name)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            var related = files.Where(path => controllers.Contains(Path.GetFileNameWithoutExtension(path), StringComparer.OrdinalIgnoreCase)).ToArray();
            var resources = new[] { "Dir", "Filter", "Grid" }.Select(kind =>
            {
                var paths = related.Where(path => HasFolder(path, kind)).Select(path => Path.GetRelativePath(root, path)).ToArray();
                return new Resource(kind, paths.Length == 0 ? "missing" : paths.Length == 1 ? "found" : "multiple", paths);
            }).Append(new Resource("Main", main == null ? "missing" : "found", main == null ? [] : [Path.GetRelativePath(root, main)])).ToArray();
            var dirFiles = related.Where(path => HasFolder(path, "Dir") && Path.GetExtension(path).ToLowerInvariant() is ".xml" or ".f").ToArray();
            var tables = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var path in dirFiles)
            {
                var text = await File.ReadAllTextAsync(path, token).ConfigureAwait(false);
                foreach (Match match in Regex.Matches(text, """\b(?:table|tableName|baseTable)\s*=\s*['"](?<v>[\w.$]+)['"]|<table\b[^>]*>\s*(?<v>[\w.$]+)\s*</table>""", RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(300)))
                    if (match.Groups["v"].Success) tables.Add(match.Groups["v"].Value);
            }
            return new { controllers, resources, tables = tables.ToArray() };
        }, token).ConfigureAwait(false);
    }

    private static bool HasFolder(string path, string folder) => path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
        .Any(part => part.Equals(folder, StringComparison.OrdinalIgnoreCase));

    private static async Task<Field[]> FieldsAsync(SqlConnection connection, SqlTransaction? transaction, CancellationToken token)
    {
        const string sql = "SELECT c.name,t.name,c.max_length,c.is_nullable,c.is_identity,c.is_computed,CASE WHEN c.default_object_id=0 THEN 0 ELSE 1 END FROM sys.columns c JOIN sys.types t ON c.user_type_id=t.user_type_id WHERE c.object_id=OBJECT_ID('dbo.wcommand') ORDER BY c.column_id";
        await using var command = new SqlCommand(sql, connection, transaction);
        await using var reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false);
        var all = new List<Field>();
        while (await reader.ReadAsync(token).ConfigureAwait(false))
            all.Add(new(reader.GetString(0), reader.GetString(1), reader.GetInt16(2), reader.GetBoolean(3), reader.GetBoolean(4) || reader.GetBoolean(5), reader.GetInt32(6) == 1));
        if (all.Count == 0) throw new InvalidOperationException("Không tìm thấy dbo.wcommand.");
        var byName = all.ToDictionary(field => field.Name, StringComparer.OrdinalIgnoreCase);
        var ordered = PreferredFields.Where(byName.ContainsKey).Select(name => byName[name]).ToList();
        ordered.AddRange(all.Where(field => !PreferredFields.Contains(field.Name, StringComparer.OrdinalIgnoreCase)));
        return ordered.ToArray();
    }

    private static async Task<Dictionary<string, string?>?> RowAsync(SqlConnection connection, SqlTransaction? transaction, Field[] fields,
        string id, CancellationToken token, bool lockRow = false)
    {
        var hint = lockRow ? " WITH (UPDLOCK,HOLDLOCK)" : "";
        var sql = $"SELECT {string.Join(',', fields.Select(field => Quote(field.Name)))} FROM dbo.wcommand{hint} WHERE RTRIM(wmenu_id)=@id";
        await using var command = new SqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("@id", id);
        await using var reader = await command.ExecuteReaderAsync(CommandBehavior.SingleRow, token).ConfigureAwait(false);
        if (!await reader.ReadAsync(token).ConfigureAwait(false)) return null;
        return fields.Select((field, index) => (field.Name, Value: reader.IsDBNull(index) ? null : StringValue(reader.GetValue(index))))
            .ToDictionary(item => item.Name, item => item.Value, StringComparer.OrdinalIgnoreCase);
    }

    private static async Task<(string Id, string Parent)> GenerateIdAsync(SqlConnection connection, SqlTransaction? transaction, string selectedId, CancellationToken token)
    {
        var parts = selectedId.Split('.', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        var prefix = parts.Length >= 2 ? string.Join('.', parts.Take(2)) : parts.FirstOrDefault() ?? "AA";
        var parent = parts.Length >= 3 ? prefix + ".00" : selectedId;
        var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var hasMenuId = await HasColumnAsync(connection, transaction, "dbo.wcommand", "menu_id", token).ConfigureAwait(false);
        var usedSql = hasMenuId
            ? "SELECT RTRIM(wmenu_id) FROM dbo.wcommand UNION SELECT RTRIM(menu_id) FROM dbo.wcommand WHERE menu_id IS NOT NULL"
            : "SELECT RTRIM(wmenu_id) FROM dbo.wcommand";
        await using (var command = new SqlCommand(usedSql, connection, transaction))
        await using (var reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false))
            while (await reader.ReadAsync(token).ConfigureAwait(false)) used.Add(reader.GetString(0));
        if (await HasColumnAsync(connection, transaction, "dbo.command", "menu_id", token).ConfigureAwait(false))
        {
            await using var command = new SqlCommand("SELECT RTRIM(menu_id) FROM dbo.command WHERE menu_id IS NOT NULL", connection, transaction);
            await using var reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false);
            while (await reader.ReadAsync(token).ConfigureAwait(false)) used.Add(reader.GetString(0));
        }
        for (var number = 1; number <= 99; number++)
        {
            var candidate = prefix + "." + number.ToString("00");
            if (!used.Contains(candidate)) return (candidate, parent);
        }
        throw new InvalidOperationException("Không còn mã trống trong nhánh " + prefix + ".xx.");
    }

    private static async Task<string> GenerateCommandIdAsync(SqlConnection connection, CancellationToken token)
    {
        var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var table in new[] { "dbo.wcommand", "dbo.command" })
        {
            if (!await HasColumnAsync(connection, null, table, "menu_id", token).ConfigureAwait(false)) continue;
            await using var command = new SqlCommand($"SELECT RTRIM(menu_id) FROM {table} WHERE menu_id LIKE 'AA.__.__'", connection);
            await using var reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false);
            while (await reader.ReadAsync(token).ConfigureAwait(false)) used.Add(reader.GetString(0));
        }
        return NextCommandId(used);
    }

    internal static string NextCommandId(IReadOnlySet<string> used)
    {
        for (var number = 0; number < 10000; number++)
        {
            var candidate = $"AA.{number / 100:00}.{number % 100:00}";
            if (!used.Contains(candidate)) return candidate;
        }
        throw new InvalidOperationException("Không còn mã command trống trong AA.00.00–AA.99.99.");
    }

    private static async Task<bool> ExistsAsync(SqlConnection connection, SqlTransaction transaction, string treeId, string? commandId, CancellationToken token)
    {
        var hasMenuId = await HasColumnAsync(connection, transaction, "dbo.wcommand", "menu_id", token).ConfigureAwait(false);
        var sql = hasMenuId
            ? "SELECT CASE WHEN EXISTS(SELECT 1 FROM dbo.wcommand WITH (UPDLOCK,HOLDLOCK) WHERE RTRIM(wmenu_id)=@tree OR RTRIM(menu_id)=@command) THEN 1 ELSE 0 END"
            : "SELECT CASE WHEN EXISTS(SELECT 1 FROM dbo.wcommand WITH (UPDLOCK,HOLDLOCK) WHERE RTRIM(wmenu_id)=@tree) THEN 1 ELSE 0 END";
        await using var command = new SqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("@tree", treeId);
        command.Parameters.AddWithValue("@command", commandId ?? treeId);
        var exists = Convert.ToInt32(await command.ExecuteScalarAsync(token).ConfigureAwait(false)) == 1;
        if (exists || !await HasColumnAsync(connection, transaction, "dbo.command", "menu_id", token).ConfigureAwait(false)) return exists;
        await using var other = new SqlCommand("SELECT CASE WHEN EXISTS(SELECT 1 FROM dbo.command WITH (UPDLOCK,HOLDLOCK) WHERE RTRIM(menu_id)=@id) THEN 1 ELSE 0 END", connection, transaction);
        other.Parameters.AddWithValue("@id", commandId ?? treeId);
        return Convert.ToInt32(await other.ExecuteScalarAsync(token).ConfigureAwait(false)) == 1;
    }

    private static async Task<bool> HasColumnAsync(SqlConnection connection, SqlTransaction? transaction, string table, string column, CancellationToken token)
    {
        await using var command = new SqlCommand("SELECT CASE WHEN COL_LENGTH(@table,@column) IS NULL THEN 0 ELSE 1 END", connection, transaction);
        command.Parameters.AddWithValue("@table", table); command.Parameters.AddWithValue("@column", column);
        return Convert.ToInt32(await command.ExecuteScalarAsync(token).ConfigureAwait(false)) == 1;
    }

    private static SqlParameter Parameter(string name, Field field, string? value)
    {
        if (value is null && !field.Nullable) throw new ArgumentException(field.Name + " không cho phép NULL.");
        if (field.MaxLength > 0 && field.Type is "varchar" or "char" && value != null && Encoding.Default.GetByteCount(value) > field.MaxLength)
            throw new ArgumentException(field.Name + " vượt độ dài " + field.MaxLength + ".");
        if (field.MaxLength > 0 && field.Type is "nvarchar" or "nchar" && value != null && value.Length > field.MaxLength / 2)
            throw new ArgumentException(field.Name + " vượt độ dài " + field.MaxLength / 2 + ".");
        return new SqlParameter(name, value is null ? DBNull.Value : value);
    }

    private static string? DefaultValue(Field field)
    {
        if (field.Nullable || field.HasDefault) return null;
        return field.Type is "tinyint" or "smallint" or "int" or "bigint" or "decimal" or "numeric" or "float" or "real" or "money" or "smallmoney" or "bit"
            ? "0" : "";
    }

    private static string StringValue(object value) => value switch
    {
        byte[] bytes => Convert.ToBase64String(bytes),
        _ => Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture)?.TrimEnd() ?? ""
    };

    private static async Task ExecuteAsync(SqlConnection connection, SqlTransaction transaction, string sql, SqlParameter[] parameters, CancellationToken token)
    {
        await using var command = new SqlCommand(sql, connection, transaction); command.Parameters.AddRange(parameters);
        if (await command.ExecuteNonQueryAsync(token).ConfigureAwait(false) != 1) throw new DBConcurrencyException("Không cập nhật được menu.");
    }

    private static string Fingerprint(IReadOnlyDictionary<string, string?> values)
    {
        var json = JsonSerializer.Serialize(values.OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json)));
    }
    private static string Quote(string name) => "[" + name.Replace("]", "]]", StringComparison.Ordinal) + "]";
}
