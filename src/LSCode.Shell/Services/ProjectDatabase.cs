using System.Data;
using LSCode.Core;
using Microsoft.Data.SqlClient;

namespace LSCode.Shell.Services;

public static class ProjectDatabase
{
    public static async Task<IReadOnlyList<SqlTarget>> ConfiguredTargetsAsync(LegacyProject project)
    {
        var path = project.Field("WebConfigPath");
        IEnumerable<ProjectDiscovery.ConfiguredConnection> connections = !string.IsNullOrWhiteSpace(path)
            ? await Task.Run(() => ProjectDiscovery.ReadAllConnections(path))
            : [new("appConnectionString", project.Field("AppConnectionString"), ""), new("sysConnectionString", project.Field("SysConnectionString"), "")];
        return await ResolveTargetsAsync(connections, async (target, candidates) =>
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            var result = await ReadAsync(target,
                "SELECT @a AS name WHERE DB_ID(@a) IS NOT NULL AND HAS_DBACCESS(@a)=1 UNION ALL SELECT @app WHERE DB_ID(@app) IS NOT NULL AND HAS_DBACCESS(@app)=1",
                timeout.Token, new SqlParameter("@a", candidates[0]), new SqlParameter("@app", candidates[1]));
            return result.Rows.Cast<DataRow>().Select(r => (string)r[0]).ToList();
        });
    }

    internal static async Task<IReadOnlyList<SqlTarget>> ResolveTargetsAsync(IEnumerable<ProjectDiscovery.ConfiguredConnection> connections,
        Func<SqlTarget, IReadOnlyList<string>, Task<IReadOnlyList<string>>> probe)
    {
        var entries = connections.ToList();
        string Required(string name) => entries.FirstOrDefault(e => e.Name.Equals(name, StringComparison.OrdinalIgnoreCase))?.ConnectionString
            ?? throw new InvalidOperationException("Thiếu " + name + " trong connectionStrings.");
        var sys = Target(Required("sysConnectionString"));
        var identity = new SqlConnectionStringBuilder(sys.ConnectionString);
        var raw = Required("appConnectionString");
        var app = new SqlConnectionStringBuilder(raw);
        if (!new System.Data.Common.DbConnectionStringBuilder { ConnectionString = raw }.ContainsKey("Encrypt")) app.Encrypt = SqlConnectionEncryptOption.Optional;
        app.Authentication = identity.Authentication; app.IntegratedSecurity = identity.IntegratedSecurity;
        app.UserID = identity.UserID; app.Password = identity.Password;
        if (app.ApplicationName.Contains('%')) app.ApplicationName = "LS Code";
        if (app.InitialCatalog.Equals("%Database", StringComparison.OrdinalIgnoreCase))
        {
            var stem = System.Text.RegularExpressions.Regex.Replace(sys.Database, @"_(?:Sys|S)$", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (stem == sys.Database) throw new InvalidOperationException("Không xác định được App DB từ tên SYS: " + sys.Database);
            string[] candidates = [stem + "_A", stem + "_App"];
            app.InitialCatalog = "master";
            var available = await probe(Target(app.ConnectionString), candidates);
            app.InitialCatalog = candidates.FirstOrDefault(c => available.Contains(c, StringComparer.OrdinalIgnoreCase))
                ?? throw new InvalidOperationException("Không truy cập được App DB: " + string.Join(" hoặc ", candidates));
        }
        return [Target(app.ConnectionString), sys];
    }
    public static IReadOnlyList<SqlTarget> ConcreteTargets(IEnumerable<ProjectDiscovery.ConfiguredConnection> connections)
    {
        var entries = connections.ToList();
        var sys = entries.FirstOrDefault(e => e.Name.Equals("sysConnectionString", StringComparison.OrdinalIgnoreCase));
        var identity = sys == null ? null : new SqlConnectionStringBuilder(sys.ConnectionString);
        var targets = new List<SqlTarget>();
        foreach (var entry in entries)
        {
            if (entry.ProviderName.Length > 0 && entry.ProviderName is not ("System.Data.SqlClient" or "Microsoft.Data.SqlClient")) continue;
            var builder = new SqlConnectionStringBuilder(entry.ConnectionString);
            if (string.IsNullOrWhiteSpace(builder.InitialCatalog) || builder.InitialCatalog.Contains('%') || string.IsNullOrWhiteSpace(builder.DataSource) || builder.DataSource.Contains('%')) continue;
            var target = Target(entry.ConnectionString);
            builder = new SqlConnectionStringBuilder(target.ConnectionString);
            if (identity != null && IsAppDatabase(target.Database))
            {
                builder.Authentication = identity.Authentication;
                builder.IntegratedSecurity = identity.IntegratedSecurity;
                builder.UserID = identity.UserID;
                builder.Password = identity.Password;
            }
            if (builder.ApplicationName.Contains('%')) builder.ApplicationName = "LS Code";
            targets.Add(target with { ConnectionString = builder.ConnectionString });
        }
        return targets.DistinctBy(t => (t.Server.ToUpperInvariant(), t.Database.ToUpperInvariant())).ToList();
    }

    public static bool IsAppDatabase(string name) => name.EndsWith("_A", StringComparison.OrdinalIgnoreCase) || name.EndsWith("_App", StringComparison.OrdinalIgnoreCase);

    public static SqlTarget? DefaultAppTarget(IReadOnlyList<SqlTarget> available, string sysDatabase, string savedDatabase)
    {
        var apps = available.Where(t => IsAppDatabase(t.Database)).ToList();
        var saved = apps.FirstOrDefault(t => t.Database.Equals(savedDatabase, StringComparison.OrdinalIgnoreCase));
        if (saved != null) return saved;
        var inferred = ProjectConventions.FindAppDatabase(sysDatabase, apps.Select(t => t.Database));
        return apps.FirstOrDefault(t => t.Database.Equals(inferred, StringComparison.OrdinalIgnoreCase))
            ?? apps.OrderBy(t => t.Database.EndsWith("_A", StringComparison.OrdinalIgnoreCase) ? 0 : 1).ThenBy(t => t.Database, StringComparer.OrdinalIgnoreCase).FirstOrDefault();
    }

    public static SqlTarget Target(string connectionString)
    {
        var builder = new SqlConnectionStringBuilder(connectionString);
        // Match legacy System.Data.SqlClient defaults only when web.config omits encryption.
        if (!new System.Data.Common.DbConnectionStringBuilder { ConnectionString = connectionString }.ContainsKey("Encrypt")) builder.Encrypt = SqlConnectionEncryptOption.Optional;
        builder.ConnectTimeout = 12;
        if (string.IsNullOrWhiteSpace(builder.DataSource) || string.IsNullOrWhiteSpace(builder.InitialCatalog) || builder.InitialCatalog.Contains('%'))
            throw new InvalidOperationException("Kết nối sys phải có server và database cụ thể.");
        return new(builder.DataSource, builder.InitialCatalog, builder.ConnectionString);
    }
    public static Task<DataTable> ReadAsync(SqlTarget target, string sql, CancellationToken token, params SqlParameter[] parameters) => ReadCoreAsync(target, sql, token, 10000, parameters);
    public static Task<DataTable> ReadAllAsync(SqlTarget target, string sql, CancellationToken token, params SqlParameter[] parameters) => ReadCoreAsync(target, sql, token, null, parameters);
    private static async Task<DataTable> ReadCoreAsync(SqlTarget target, string sql, CancellationToken token, int? limit, SqlParameter[] parameters)
    {
        using var connection = new SqlConnection(target.ConnectionString);
        await connection.OpenAsync(token).ConfigureAwait(false);
        using var command = new SqlCommand(sql, connection) { CommandTimeout = 30 };
        command.Parameters.AddRange(parameters);
        using var reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false);
        var table = new DataTable();
        for (int i = 0; i < reader.FieldCount; i++)
        {
            var name = reader.GetName(i); if (string.IsNullOrEmpty(name)) name = "Column" + i;
            while (table.Columns.Contains(name)) name += "_";
            table.Columns.Add(name, reader.GetFieldType(i)).ExtendedProperties["OriginalName"] = reader.GetName(i);
        }
        while (await reader.ReadAsync(token).ConfigureAwait(false))
        {
            if (limit.HasValue && table.Rows.Count >= limit.Value) throw new InvalidOperationException("Metadata vượt giới hạn 10000 dòng.");
            var values = new object[reader.FieldCount]; reader.GetValues(values); table.Rows.Add(values);
        }
        return table;
    }
    public static async Task<List<CommandNode>> CommandsAsync(SqlTarget target, CancellationToken token)
    {
        var web = await ReadAsync(target, "SELECT wmenu_id,wmenu_id0,menu_id,bar,link,sysid FROM dbo.wcommand ORDER BY wmenu_id", token);
        var commands = await ReadAsync(target, "SELECT menu_id,menu_id0,bar,exe,sysid FROM dbo.command ORDER BY menu_id", token);
        string Value(DataRow row, string column) => Convert.ToString(row[column])?.Trim() ?? "";
        var lookup = commands.Rows.Cast<DataRow>().GroupBy(r => Value(r, "menu_id")).ToDictionary(g => g.Key, g => g.First());
        var entries = web.Rows.Cast<DataRow>().Select(r =>
        {
            var commandId = Value(r, "menu_id"); lookup.TryGetValue(commandId, out var command);
            var caption = Value(r, "bar"); var link = Value(r, "link");
            if (command != null) { if (caption.Length == 0) caption = Value(command, "bar"); if (link.Length == 0) link = Value(command, "exe"); }
            return new CommandEntry(Value(r, "wmenu_id"), Value(r, "wmenu_id0"), caption, link, commandId, Value(r, "sysid") + (command == null ? "" : " " + Value(command, "sysid")));
        }).ToList();
        var roots = CommandHierarchy.Build(entries);
        var used = entries.Select(e => e.CommandId).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var remaining = commands.Rows.Cast<DataRow>().Where(r => !used.Contains(Value(r, "menu_id")))
            .Select(r => new CommandEntry(Value(r, "menu_id"), Value(r, "menu_id0"), Value(r, "bar"), Value(r, "exe"), Value(r, "menu_id"), Value(r, "sysid")));
        var extra = CommandHierarchy.Build(remaining);
        if (extra.Count > 0) { var group = new CommandNode(new("command", "", "Command khác", "", "")); group.Children.AddRange(extra); roots.Add(group); }
        return roots;
    }
    public static string Quote(string identifier) => "[" + identifier.Replace("]", "]]", StringComparison.Ordinal) + "]";
}



