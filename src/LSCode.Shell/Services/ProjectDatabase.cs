using System.Data;
using LSCode.Core;
using Microsoft.Data.SqlClient;

namespace LSCode.Shell.Services;

public static class ProjectDatabase
{
    public static async Task<IReadOnlyList<SqlTarget>> ConfiguredTargetsAsync(LegacyProject project)
    {
        var connections = await ReadProjectConnectionsAsync(project);
        return await ResolveTargetsAsync(connections, async (target, candidates) =>
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            var result = await ReadAsync(target,
                "SELECT @a AS name WHERE DB_ID(@a) IS NOT NULL AND HAS_DBACCESS(@a)=1 UNION ALL SELECT @app WHERE DB_ID(@app) IS NOT NULL AND HAS_DBACCESS(@app)=1",
                timeout.Token, new SqlParameter("@a", candidates[0]), new SqlParameter("@app", candidates[1]));
            return result.Rows.Cast<DataRow>().Select(r => (string)r[0]).ToList();
        });
    }

    private static async Task<IReadOnlyList<ProjectDiscovery.ConfiguredConnection>> ReadProjectConnectionsAsync(LegacyProject project)
    {
        var path = project.Field("WebConfigPath");
        var app = project.Field("AppConnectionString");
        var sys = project.Field("SysConnectionString");
        if (!string.IsNullOrWhiteSpace(path))
        {
            try
            {
                return await Task.Run(() => ProjectDiscovery.ReadAllConnections(path));
            }
            catch (Exception ex) when ((ex is IOException or UnauthorizedAccessException) &&
                !string.IsNullOrWhiteSpace(app) && !string.IsNullOrWhiteSpace(sys))
            {
                // A synced profile can still connect to SQL when its source share is unavailable.
            }
        }

        return [new("appConnectionString", app, ""), new("sysConnectionString", sys, "")];
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
    public static async Task<List<CommandNode>> CommandsAsync(SqlTarget target, CancellationToken token, string? appDatabase = null)
    {
        appDatabase ??= System.Text.RegularExpressions.Regex.Replace(target.Database, @"_(?:Sys|S)$", "_A", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        if (appDatabase == target.Database) throw new InvalidOperationException("App database must be configured for menu loading.");
        var columns = await ReadAsync(target, "SELECT name FROM sys.columns WHERE object_id=OBJECT_ID('dbo.wcommand')", token);
        var names = columns.Rows.Cast<DataRow>().Select(r => Convert.ToString(r[0])!).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var required in new[] { "wmenu_id", "wmenu_id0", "menu_id", "bar" })
            if (!names.Contains(required)) throw new InvalidOperationException("wcommand missing required column: " + required);
        string Column(string name) => names.Contains(name) ? "a." + Quote(name) : "CAST('' AS nvarchar(4000))";
        var projection = "a.wmenu_id AS menu_id,a.wmenu_id0 AS menu_id0,RTRIM(a.bar) AS bar," + Column("link") + " AS link,CASE WHEN " + Column("icon") + "='' THEN " + Column("icon_url") + " ELSE '' END AS icon_url," + Column("icon") + " AS icon," + Column("status") + " AS status,RTRIM(" + Column("target") + ") AS target";
        var app = Quote(appDatabase);
        var metadata = await ReadAsync(target, "SELECT a.wmenu_id,a.menu_id," + Column("sysid") + " AS sysid FROM dbo.wcommand a", token);
        var details = metadata.Rows.Cast<DataRow>().GroupBy(r => Convert.ToString(r[0])?.Trim() ?? "")
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
        var sql = $"""
            SET NOCOUNT ON;
            SELECT {projection} INTO #t FROM dbo.wcommand a JOIN dbo.userinfo b ON b.id='2'
            WHERE PATINDEX('%'+RTRIM(a.menu_id)+'%',b.r_access)<>0 OR b.admin=1;
            DELETE #t WHERE status='9';
            DELETE a FROM #t a WHERE menu_id0='' AND NOT EXISTS(SELECT 1 FROM #t WHERE menu_id0=a.menu_id AND bar<>'-');
            SELECT {projection} INTO #z FROM dbo.wcommand a JOIN {app}..denyunitright b ON PATINDEX('%'+RTRIM(a.wmenu_id)+'%',b.r_deny)>0;
            DELETE a FROM #t a JOIN #z b ON a.menu_id=b.menu_id;
            INSERT INTO #t SELECT DISTINCT b.* FROM #t a JOIN #z b ON a.menu_id0=b.menu_id WHERE a.bar<>'-' AND b.menu_id NOT IN(SELECT menu_id FROM #t);
            INSERT INTO #t SELECT DISTINCT b.* FROM #t a JOIN #z b ON a.menu_id0=b.menu_id WHERE a.bar<>'-' AND b.menu_id NOT IN(SELECT menu_id FROM #t);
            DROP TABLE #z;
            EXEC {app}..FastBusiness$App$Startup;
            DELETE #t WHERE menu_id0<>'' AND menu_id0 NOT IN(SELECT menu_id FROM #t);
            SELECT * FROM #t ORDER BY menu_id;
            """;
        using var connection = new SqlConnection(target.ConnectionString);
        await connection.OpenAsync(token).ConfigureAwait(false);
        using var command = new SqlCommand(sql, connection) { CommandTimeout = 30 };
        using var reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false);
        var entries = new List<CommandEntry>();
        // Startup can return intermediate result sets. The final menu is the last set.
        do
        {
            var fields = Enumerable.Range(0, reader.FieldCount).Select(reader.GetName).ToHashSet(StringComparer.OrdinalIgnoreCase);
            if (!fields.Contains("menu_id") || !fields.Contains("menu_id0") || !fields.Contains("bar")) continue;
            entries.Clear();
            string Value(string name) => fields.Contains(name) ? Convert.ToString(reader[name])?.Trim() ?? "" : "";
            while (await reader.ReadAsync(token).ConfigureAwait(false))
            {
                if (entries.Count >= 10000) throw new InvalidOperationException("Menu exceeds 10000 rows.");
                var id = Value("menu_id"); details.TryGetValue(id, out var detail);
                entries.Add(new(id, Value("menu_id0"), Value("bar"), Value("link"),
                    detail == null ? id : Convert.ToString(detail[1])?.Trim() ?? "",
                    detail == null ? "" : Convert.ToString(detail[2])?.Trim() ?? ""));
            }
        } while (await reader.NextResultAsync(token).ConfigureAwait(false));
        return CommandHierarchy.Build(entries);
    }
    public static string Quote(string identifier) => "[" + identifier.Replace("]", "]]", StringComparison.Ordinal) + "]";
}



