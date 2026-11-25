using System.Data;
using System.Globalization;
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
    internal sealed record ResourceFile(string Path, string[] Tables);
    internal sealed record Resource(string Kind, string Status, string[] Paths, ResourceFile[] Files);
    internal sealed record ResourceResult(string[] Controllers, Resource[] Resources, string[] Tables);

    public static async Task<object> ReadAsync(SqlTarget target, string wmenuId, CancellationToken token)
    {
        await using var connection = new SqlConnection(target.ConnectionString);
        await connection.OpenAsync(token).ConfigureAwait(false);
        var fields = await FieldsAsync(connection, null, token).ConfigureAwait(false);
        var values = await RowAsync(connection, null, fields, wmenuId, token).ConfigureAwait(false)
            ?? throw new KeyNotFoundException("Menu không còn tồn tại trong dbo.wcommand.");
        return new { mode = "edit", fields, values, version = Fingerprint(values), canSave = true };
    }

    internal sealed record NewMenu(string Mode, Field[] Fields, Dictionary<string, string?> Values, string Version, bool CanSave);
    public static async Task<NewMenu> NewAsync(SqlTarget target, string selectedId, CancellationToken token)
    {
        await using var connection = new SqlConnection(target.ConnectionString);
        await connection.OpenAsync(token).ConfigureAwait(false);
        var fields = await FieldsAsync(connection, null, token).ConfigureAwait(false);
        var candidate = await GenerateIdAsync(connection, null, selectedId, token).ConfigureAwait(false);
        var values = fields.ToDictionary(field => field.Name,
            field => field.ReadOnly || field.HasDefault ? null : DefaultValue(field), StringComparer.OrdinalIgnoreCase);
        values["wmenu_id"] = candidate.Id;
        if (values.ContainsKey("wmenu_id0")) values["wmenu_id0"] = candidate.Parent;
        if (values.ContainsKey("menu_id"))
            values["menu_id"] = await GenerateCommandIdAsync(connection, token).ConfigureAwait(false);
        if (values.ContainsKey("status")) values["status"] = "1";
        return new("new", fields, values, "", true);
    }

    public static async Task<string> InsertScriptAsync(SqlTarget target, string wmenuId, CancellationToken token, bool includeChildren = false)
    {
        await using var connection = new SqlConnection(target.ConnectionString);
        await connection.OpenAsync(token).ConfigureAwait(false);
        if (!includeChildren) return await BuildScriptAsync(connection, wmenuId, new(StringComparer.OrdinalIgnoreCase), token).ConfigureAwait(false);

        var parents = await ReadTreeAsync(connection, null, token).ConfigureAwait(false);
        var children = parents.ToLookup(row => row.Value, row => row.Key, StringComparer.OrdinalIgnoreCase);
        var pending = new Queue<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { wmenuId };
        var commands = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var script = new StringBuilder();
        pending.Enqueue(wmenuId);
        while (pending.TryDequeue(out var id))
        {
            token.ThrowIfCancellationRequested();
            script.AppendLine(await BuildScriptAsync(connection, id, commands, token).ConfigureAwait(false));
            foreach (var child in children[id])
                if (seen.Add(child)) pending.Enqueue(child);
        }
        return script.ToString();
    }

    private static async Task<Dictionary<string, string>> ReadTreeAsync(SqlConnection connection, SqlTransaction? transaction, CancellationToken token)
    {
        var parents = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        await using var command = new SqlCommand("SELECT wmenu_id, wmenu_id0 FROM dbo.wcommand ORDER BY wmenu_id", connection, transaction) { CommandTimeout = 0 };
        await using var reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false);
        while (await reader.ReadAsync(token).ConfigureAwait(false))
            if (!reader.IsDBNull(0)) parents.TryAdd(reader.GetString(0).Trim(), reader.IsDBNull(1) ? "" : reader.GetString(1).Trim());
        return parents;
    }

    private static async Task<string> BuildScriptAsync(SqlConnection connection, string id, HashSet<string> commands, CancellationToken token)
    {
        var menu = await InsertRowScriptAsync(connection, "dbo.wcommand", "wmenu_id", id, token).ConfigureAwait(false);
        if (menu.Script is null) throw new KeyNotFoundException("Không tìm thấy menu trong dbo.wcommand.");

        var result = new StringBuilder();
        result.AppendLine("-- dbo.wcommand").AppendLine(menu.Script).AppendLine("GO").AppendLine();
        var commandId = menu.Values.TryGetValue("menu_id", out var value) ? Convert.ToString(value, CultureInfo.InvariantCulture)?.Trim() : null;
        if (string.IsNullOrWhiteSpace(commandId))
            result.AppendLine("-- Menu không khai báo menu_id nên không có bản ghi dbo.command.");
        else if (commands.Add(commandId))
        {
            var command = await InsertRowScriptAsync(connection, "dbo.command", "menu_id", commandId, token).ConfigureAwait(false);
            result.AppendLine("-- dbo.command");
            result.AppendLine(command.Script ?? $"-- Không tìm thấy menu_id {QuoteLiteral(commandId)} trong dbo.command.");
            if (command.Script is not null) result.AppendLine("GO");
        }
        return result.ToString();
    }

    public static async Task<object> SaveAsync(SqlTarget target, string mode, string originalId, string version,
        IReadOnlyDictionary<string, string?> submitted, CancellationToken token,
        Func<SqlConnection, SqlTransaction, CancellationToken, Task>? beforeCommit = null)
    {
        await using var connection = new SqlConnection(target.ConnectionString);
        await connection.OpenAsync(token).ConfigureAwait(false);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(IsolationLevel.Serializable, token).ConfigureAwait(false);
        object saved;
        try
        {
            var fields = await FieldsAsync(connection, transaction, token).ConfigureAwait(false);
            var editable = fields.Where(field => !field.ReadOnly).ToDictionary(field => field.Name, StringComparer.OrdinalIgnoreCase);
            var values = submitted.Where(pair => editable.ContainsKey(pair.Key))
                .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);
            foreach (var required in new[] { "wmenu_id", "bar" })
                if (!values.TryGetValue(required, out var value) || string.IsNullOrWhiteSpace(value))
                    throw new ArgumentException($"{required} không được để trống.");

            var savedId = values["wmenu_id"]!.Trim();
            if (savedId.Length > 128) throw new ArgumentException("wmenu_id vượt quá 128 ký tự.");
            if (values.TryGetValue("wmenu_id0", out var parent) && savedId.Equals(parent?.Trim(), StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Menu không thể là cha của chính nó.");

            if (mode.Equals("new", StringComparison.OrdinalIgnoreCase))
            {
                if (await ExistsAsync(connection, transaction, savedId, values.GetValueOrDefault("menu_id"), token).ConfigureAwait(false))
                    throw new InvalidOperationException("Mã menu đã tồn tại trong wcommand hoặc command. Nhấn Thêm mới để lấy mã khác.");
                foreach (var field in editable.Values)
                {
                    if (values.GetValueOrDefault(field.Name) is not null) continue;
                    if (field.HasDefault) values.Remove(field.Name);
                    else values[field.Name] = DefaultValue(field);
                }
                var columns = values.Keys.ToArray();
                var sql = $"INSERT dbo.wcommand ({string.Join(',', columns.Select(Quote))}) VALUES ({string.Join(',', columns.Select((_, i) => "@p" + i))})";
                await ExecuteAsync(connection, transaction, sql, columns.Select((name, i) => Parameter("@p" + i, editable[name], values[name])).ToArray(), token).ConfigureAwait(false);
            }
            else
            {
                var current = await RowAsync(connection, transaction, fields, originalId, token, lockRow: true).ConfigureAwait(false)
                    ?? throw new KeyNotFoundException("Menu đã bị xóa bởi phiên khác.");
                if (!CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(Fingerprint(current)), Encoding.UTF8.GetBytes(version)))
                    throw new InvalidOperationException("Menu đã được thay đổi bởi phiên khác. Tải lại trước khi lưu.");
                var renamed = !savedId.Equals(originalId.Trim(), StringComparison.Ordinal);
                var oldParent = current.GetValueOrDefault("wmenu_id0")?.Trim() ?? "";
                var newParent = values.TryGetValue("wmenu_id0", out var submittedParent) ? submittedParent?.Trim() ?? "" : oldParent;
                if (renamed || !newParent.Equals(oldParent, StringComparison.OrdinalIgnoreCase))
                {
                    var parents = await ReadTreeAsync(connection, transaction, token).ConfigureAwait(false);
                    if (renamed && !savedId.Equals(originalId.Trim(), StringComparison.OrdinalIgnoreCase) && parents.ContainsKey(savedId))
                        throw new InvalidOperationException("Mã cây đã tồn tại.");
                    var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { savedId, originalId.Trim() };
                    for (var ancestor = newParent; ancestor.Length > 0;)
                    {
                        if (!seen.Add(ancestor)) throw new ArgumentException("Mã cha tạo vòng lặp trong cây menu.");
                        if (!parents.TryGetValue(ancestor, out var next)) break;
                        ancestor = next;
                    }
                }
                values["wmenu_id"] = savedId;
                var columns = values.Keys.Where(name => renamed || !name.Equals("wmenu_id", StringComparison.OrdinalIgnoreCase)).ToArray();
                var sql = $"UPDATE dbo.wcommand SET {string.Join(',', columns.Select((name, i) => Quote(name) + "=@p" + i))} WHERE wmenu_id=@original";
                var parameters = columns.Select((name, i) => Parameter("@p" + i, editable[name], values[name])).ToList();
                parameters.Add(new SqlParameter("@original", originalId));
                await ExecuteAsync(connection, transaction, sql, parameters.ToArray(), token).ConfigureAwait(false);
                if (renamed)
                    await ExecuteAsync(connection, transaction, "UPDATE dbo.wcommand SET wmenu_id0=@new WHERE wmenu_id0=@old",
                        [Parameter("@new", editable["wmenu_id"], savedId), new SqlParameter("@old", originalId)], token).ConfigureAwait(false);
            }
            var savedValues = await RowAsync(connection, transaction, fields, savedId, token).ConfigureAwait(false)
                ?? throw new DBConcurrencyException("Không đọc được menu vừa lưu.");
            if (mode.Equals("new", StringComparison.OrdinalIgnoreCase))
            {
                var nullColumns = savedValues.Where(pair => pair.Value is null).Select(pair => pair.Key).ToArray();
                if (nullColumns.Length > 0)
                    throw new InvalidOperationException("Menu mới còn giá trị NULL ở cột: " + string.Join(", ", nullColumns));
            }
            saved = new { mode = "edit", fields, values = savedValues, version = Fingerprint(savedValues), canSave = true };
            if (beforeCommit is not null) await beforeCommit(connection, transaction, token).ConfigureAwait(false);
            await transaction.CommitAsync(beforeCommit is null ? token : CancellationToken.None).ConfigureAwait(false);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
            throw;
        }
        return saved;
    }

    public static async Task<ResourceResult> ResourcesAsync(string root, string link, string sysId, CancellationToken token)
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
            var fallback = Path.GetFileNameWithoutExtension(main ?? Uri.UnescapeDataString(link.Split('?', '#')[0]));
            var controllers = (names.Count > 0 ? names : [fallback]).Where(name => !string.IsNullOrWhiteSpace(name))
                .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            var roots = new[] { Path.Combine(root, "App_Data"), Path.Combine(root, "AppData") }.Where(Directory.Exists).ToArray();
            var kinds = new[] { "Dir", "Filter", "Grid", "Report", "Lookup", "Upload" };
            var files = controllers.Length == 0 ? [] : (roots.Length == 0 ? [root] : roots)
                .SelectMany(folder => WorkspaceFileIndex.Files(folder, token, false))
                .Where(path => Path.GetExtension(path).ToLowerInvariant() is ".xml" or ".f")
                .Select(path => Path.GetRelativePath(root, path))
                .Where(path => kinds.Any(kind => HasFolder(path, kind))).Take(100001).ToArray();
            if (files.Length > 100000) throw new IOException("Project vượt 100000 file controller.");
            var byName = files.ToLookup(Path.GetFileNameWithoutExtension, StringComparer.OrdinalIgnoreCase);
            var visited = new HashSet<string>(controllers, StringComparer.OrdinalIgnoreCase);
            var pending = new Queue<string>(controllers);
            var related = new List<string>();
            var tables = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var tablesByPath = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
            var timeout = TimeSpan.FromMilliseconds(300);
            while (pending.TryDequeue(out var controller))
            {
                foreach (var path in byName[controller])
                {
                    related.Add(path);
                    var isDir = HasFolder(path, "Dir");
                    if (!isDir && !HasFolder(path, "Grid")) continue;
                    var content = await File.ReadAllTextAsync(Path.Combine(root, path), token).ConfigureAwait(false);
                    content = Regex.Replace(content, """<!--[\s\S]*?-->""", "", RegexOptions.None, timeout);
                    foreach (var child in ProjectConventions.GridControllers(content))
                        if (visited.Add(child)) pending.Enqueue(child);
                    if (!isDir) continue;
                    var fileTables = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    foreach (Match match in Regex.Matches(content,
                        """(?:\s(?:table|tableName|baseTable|masterTable|detailTable)\s*=\s*['"]|<(?:table|tableName|baseTable|masterTable|detailTable)\b[^>]*>\s*)(?<table>(?:[\w$]+|\[[\w$ ]+\])(?:\.(?:[\w$]+|\[[\w$ ]+\]))?)(?=\s*(?:['"]|</))""",
                        RegexOptions.IgnoreCase, timeout))
                        fileTables.Add(match.Groups["table"].Value);
                    tables.UnionWith(fileTables);
                    tablesByPath[path] = fileTables.Order(StringComparer.OrdinalIgnoreCase).ToArray();
                }
            }
            var resources = kinds.Select(kind =>
            {
                var paths = related.Where(path => HasFolder(path, kind)).Order(StringComparer.OrdinalIgnoreCase).ToArray();
                return new Resource(kind, paths.Length == 0 ? "missing" : paths.Length == 1 ? "found" : "multiple", paths,
                    paths.Select(path => new ResourceFile(path, tablesByPath.GetValueOrDefault(path, []))).ToArray());
            }).Append(new Resource("Main", main == null ? "missing" : "found", main == null ? [] : [Path.GetRelativePath(root, main)],
                main == null ? [] : [new ResourceFile(Path.GetRelativePath(root, main), [])])).ToArray();
            return new ResourceResult(controllers, resources, tables.ToArray());
        }, token).ConfigureAwait(false);
    }

    private static bool HasFolder(string path, string folder) => path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
        .Any(part => part.Equals(folder, StringComparison.OrdinalIgnoreCase));

    private static async Task<Field[]> FieldsAsync(SqlConnection connection, SqlTransaction? transaction, CancellationToken token)
    {
        const string sql = "SELECT c.name,t.name,c.max_length,c.is_nullable,c.is_identity,c.is_computed,CASE WHEN c.default_object_id=0 THEN 0 ELSE 1 END FROM sys.columns c JOIN sys.types t ON c.user_type_id=t.user_type_id WHERE c.object_id=OBJECT_ID('dbo.wcommand') ORDER BY c.column_id";
        await using var command = new SqlCommand(sql, connection, transaction) { CommandTimeout = 0 };
        await using var reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false);
        var all = new List<Field>();
        while (await reader.ReadAsync(token).ConfigureAwait(false))
            all.Add(new(reader.GetString(0), reader.GetString(1), reader.GetInt16(2), reader.GetBoolean(3),
                reader.GetBoolean(4) || reader.GetBoolean(5) || reader.GetString(1) is "timestamp" or "rowversion", reader.GetInt32(6) == 1));
        if (all.Count == 0) throw new InvalidOperationException("Không tìm thấy dbo.wcommand.");
        var byName = all.ToDictionary(field => field.Name, StringComparer.OrdinalIgnoreCase);
        var ordered = PreferredFields.Where(byName.ContainsKey).Select(name => byName[name]).ToList();
        ordered.AddRange(all.Where(field => !PreferredFields.Contains(field.Name, StringComparer.OrdinalIgnoreCase)));
        return ordered.ToArray();
    }

    private static async Task<(string? Script, Dictionary<string, object?> Values)> InsertRowScriptAsync(
        SqlConnection connection, string table, string keyColumn, string keyValue, CancellationToken token)
    {
        const string metadataSql = "SELECT c.name,t.name FROM sys.columns c JOIN sys.types t ON c.user_type_id=t.user_type_id WHERE c.object_id=OBJECT_ID(@table) AND c.is_identity=0 AND c.is_computed=0 AND t.name NOT IN ('timestamp','rowversion') ORDER BY c.column_id";
        var columns = new List<(string Name, string Type)>();
        await using (var metadata = new SqlCommand(metadataSql, connection) { CommandTimeout = 0 })
        {
            metadata.Parameters.AddWithValue("@table", table);
            await using var reader = await metadata.ExecuteReaderAsync(token).ConfigureAwait(false);
            while (await reader.ReadAsync(token).ConfigureAwait(false)) columns.Add((reader.GetString(0), reader.GetString(1)));
        }
        if (columns.Count == 0) return (null, new(StringComparer.OrdinalIgnoreCase));

        var sql = $"SELECT {string.Join(',', columns.Select(column => Quote(column.Name)))} FROM {table} WHERE RTRIM({Quote(keyColumn)})=@id";
        await using var command = new SqlCommand(sql, connection) { CommandTimeout = 0 };
        command.Parameters.AddWithValue("@id", keyValue);
        await using var row = await command.ExecuteReaderAsync(CommandBehavior.SingleRow, token).ConfigureAwait(false);
        if (!await row.ReadAsync(token).ConfigureAwait(false)) return (null, new(StringComparer.OrdinalIgnoreCase));
        var values = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < columns.Count; index++) values[columns[index].Name] = row.IsDBNull(index) ? null : row.GetValue(index);
        var insert = $"INSERT INTO {table} ({string.Join(", ", columns.Select(column => Quote(column.Name)))})\r\nVALUES ({string.Join(", ", columns.Select(column => SqlLiteral(values[column.Name], column.Type)))})";
        return (insert, values);
    }

    private static string SqlLiteral(object? value, string type)
    {
        if (value is null or DBNull) return "NULL";
        if (value is byte[] bytes) return "0x" + Convert.ToHexString(bytes);
        if (value is bool boolean) return boolean ? "1" : "0";
        if (value is DateTime dateTime) return QuoteLiteral(dateTime.ToString("yyyy-MM-ddTHH:mm:ss.fffffff", CultureInfo.InvariantCulture));
        if (value is DateTimeOffset offset) return QuoteLiteral(offset.ToString("O", CultureInfo.InvariantCulture));
        if (value is byte or sbyte or short or ushort or int or uint or long or ulong or float or double or decimal)
            return Convert.ToString(value, CultureInfo.InvariantCulture) ?? "NULL";
        var prefix = type is "nvarchar" or "nchar" or "ntext" ? "N" : "";
        return prefix + QuoteLiteral(Convert.ToString(value, CultureInfo.InvariantCulture) ?? "");
    }

    private static string QuoteLiteral(string value) => "'" + value.Replace("'", "''", StringComparison.Ordinal) + "'";

    private static async Task<Dictionary<string, string?>?> RowAsync(SqlConnection connection, SqlTransaction? transaction, Field[] fields,
        string id, CancellationToken token, bool lockRow = false)
    {
        var hint = lockRow ? " WITH (UPDLOCK,HOLDLOCK)" : "";
        var sql = $"SELECT {string.Join(',', fields.Select(field => Quote(field.Name)))} FROM dbo.wcommand{hint} WHERE wmenu_id=@id";
        await using var command = new SqlCommand(sql, connection, transaction) { CommandTimeout = 0 };
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
        await using (var command = new SqlCommand(usedSql, connection, transaction) { CommandTimeout = 0 })
        await using (var reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false))
            while (await reader.ReadAsync(token).ConfigureAwait(false)) used.Add(reader.GetString(0));
        if (await HasColumnAsync(connection, transaction, "dbo.command", "menu_id", token).ConfigureAwait(false))
        {
            await using var command = new SqlCommand("SELECT RTRIM(menu_id) FROM dbo.command WHERE menu_id IS NOT NULL", connection, transaction) { CommandTimeout = 0 };
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
            await using var command = new SqlCommand($"SELECT RTRIM(menu_id) FROM {table} WHERE menu_id LIKE 'AA.__.__'", connection) { CommandTimeout = 0 };
            await using var reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false);
            while (await reader.ReadAsync(token).ConfigureAwait(false)) used.Add(reader.GetString(0));
        }
        return NextCommandId(used);
    }

    internal static string NextCommandId(IReadOnlySet<string> used)
    {
        for (var number = 101; number < 10000; number++)
        {
            var candidate = $"AA.{number / 100:00}.{number % 100:00}";
            if (!used.Contains(candidate)) return candidate;
        }
        throw new InvalidOperationException("Không còn mã command trống trong AA.01.01–AA.99.99.");
    }

    private static async Task<bool> ExistsAsync(SqlConnection connection, SqlTransaction transaction, string treeId, string? commandId, CancellationToken token)
    {
        var hasMenuId = await HasColumnAsync(connection, transaction, "dbo.wcommand", "menu_id", token).ConfigureAwait(false);
        var sql = hasMenuId
            ? "SELECT CASE WHEN EXISTS(SELECT 1 FROM dbo.wcommand WITH (UPDLOCK,HOLDLOCK) WHERE RTRIM(wmenu_id)=@tree OR RTRIM(menu_id)=@command) THEN 1 ELSE 0 END"
            : "SELECT CASE WHEN EXISTS(SELECT 1 FROM dbo.wcommand WITH (UPDLOCK,HOLDLOCK) WHERE RTRIM(wmenu_id)=@tree) THEN 1 ELSE 0 END";
        await using var command = new SqlCommand(sql, connection, transaction) { CommandTimeout = 0 };
        command.Parameters.AddWithValue("@tree", treeId);
        command.Parameters.AddWithValue("@command", commandId ?? treeId);
        var exists = Convert.ToInt32(await command.ExecuteScalarAsync(token).ConfigureAwait(false)) == 1;
        if (exists || !await HasColumnAsync(connection, transaction, "dbo.command", "menu_id", token).ConfigureAwait(false)) return exists;
        await using var other = new SqlCommand("SELECT CASE WHEN EXISTS(SELECT 1 FROM dbo.command WITH (UPDLOCK,HOLDLOCK) WHERE RTRIM(menu_id)=@id) THEN 1 ELSE 0 END", connection, transaction) { CommandTimeout = 0 };
        other.Parameters.AddWithValue("@id", commandId ?? treeId);
        return Convert.ToInt32(await other.ExecuteScalarAsync(token).ConfigureAwait(false)) == 1;
    }

    private static async Task<bool> HasColumnAsync(SqlConnection connection, SqlTransaction? transaction, string table, string column, CancellationToken token)
    {
        await using var command = new SqlCommand("SELECT CASE WHEN COL_LENGTH(@table,@column) IS NULL THEN 0 ELSE 1 END", connection, transaction) { CommandTimeout = 0 };
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

    private static string DefaultValue(Field field)
    {
        return field.Type is "tinyint" or "smallint" or "int" or "bigint" or "decimal" or "numeric" or "float" or "real" or "money" or "smallmoney" or "bit"
            ? "0" : "";
    }

    private static string StringValue(object value) => value switch
    {
        bool flag => flag ? "1" : "0",
        byte[] bytes => Convert.ToBase64String(bytes),
        _ => Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture)?.TrimEnd() ?? ""
    };

    private static async Task ExecuteAsync(SqlConnection connection, SqlTransaction transaction, string sql, SqlParameter[] parameters, CancellationToken token)
    {
        await using var command = new SqlCommand(sql, connection, transaction) { CommandTimeout = 0 }; command.Parameters.AddRange(parameters);
        if (await command.ExecuteNonQueryAsync(token).ConfigureAwait(false) != 1) throw new DBConcurrencyException("Không cập nhật được menu.");
    }

    private static string Fingerprint(IReadOnlyDictionary<string, string?> values)
    {
        var json = JsonSerializer.Serialize(values.OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json)));
    }
    private static string Quote(string name) => "[" + name.Replace("]", "]]", StringComparison.Ordinal) + "]";
}
