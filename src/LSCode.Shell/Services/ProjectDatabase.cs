using System.Data;
using System.IO;
using System.Collections.Concurrent;
using LSCode.Core;
using Microsoft.Data.SqlClient;

namespace LSCode.Shell.Services;

public static class ProjectDatabase
{
    private sealed record CommandCacheEntry(DateTimeOffset ExpiresAt, List<CommandNode> Nodes);
    private static readonly ConcurrentDictionary<string, CommandCacheEntry> CommandCache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> CommandLocks = new(StringComparer.OrdinalIgnoreCase);
    private static readonly TimeSpan CommandCacheLifetime = TimeSpan.FromMinutes(5);
    private const int CommandCacheLimit = 8;

    public static async Task<IReadOnlyList<SqlTarget>> ConfiguredTargetsAsync(LegacyProject project)
    {
        var connections = await ReadProjectConnectionsAsync(project);
        return await ResolveTargetsAsync(connections, async (target, candidates) =>
        {
            var result = await ReadAsync(target,
                "SELECT @a AS name WHERE DB_ID(@a) IS NOT NULL AND HAS_DBACCESS(@a)=1 UNION ALL SELECT @app WHERE DB_ID(@app) IS NOT NULL AND HAS_DBACCESS(@app)=1",
                CancellationToken.None, new SqlParameter("@a", candidates[0]), new SqlParameter("@app", candidates[1]));
            return result.Rows.Cast<DataRow>().Select(r => (string)r[0]).ToList();
        });
    }

    private static async Task<IReadOnlyList<ProjectDiscovery.ConfiguredConnection>> ReadProjectConnectionsAsync(LegacyProject project)
    {
        var source = project.SourcePath.Trim();
        var path = string.IsNullOrWhiteSpace(source)
            ? project.Field("WebConfigPath")
            : Path.Combine(source, "web.config");
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
        using var command = new SqlCommand(sql, connection) { CommandTimeout = 0 };
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
    public static async Task<IReadOnlyList<EmployeeFunctionTab>> EmployeeFunctionTabsAsync(SqlTarget target, CancellationToken token)
    {
        const string sql = """
            SELECT LTRIM(RTRIM(sysid)),
                   COALESCE(NULLIF(LTRIM(RTRIM(bar)), N''), NULLIF(LTRIM(RTRIM(bar2)), N''), LTRIM(RTRIM(sysid)))
            FROM dbo.hrtabinfo
            WHERE NULLIF(LTRIM(RTRIM(sysid)), N'') IS NOT NULL
            ORDER BY id, sysid
            """;
        var table = await ReadAsync(target, sql, token).ConfigureAwait(false);
        return table.Rows.Cast<DataRow>()
            .Select(row => new EmployeeFunctionTab((string)row[0], (string)row[1]))
            .ToArray();
    }

    public sealed record EmployeeFunctionTab(string SysId, string Caption);

    public static async Task<List<CommandNode>> CommandsAsync(SqlTarget target, CancellationToken token, string? appDatabase = null,
        string kind = "web", bool refresh = false, string context = "")
    {
        kind = kind.Equals("mobile", StringComparison.OrdinalIgnoreCase) ? "mobile" : "web";
        appDatabase ??= System.Text.RegularExpressions.Regex.Replace(target.Database, @"_(?:Sys|S)$", "_A", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        if (kind == "web" && (string.IsNullOrWhiteSpace(appDatabase) || appDatabase.Equals(target.Database, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("App database must be configured for menu loading.");
        var cacheKey = CommandCacheKey(target, appDatabase, kind, context);
        if (!refresh && TryGetCommands(cacheKey, out var cached)) return cached;

        var gate = CommandLocks.GetOrAdd(cacheKey, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(token).ConfigureAwait(false);
        try
        {
            if (!refresh && TryGetCommands(cacheKey, out cached)) return cached;
            var loaded = kind == "mobile"
                ? await MobileCommandsAsync(target, token).ConfigureAwait(false)
                : await WebCommandsAsync(target, appDatabase!, token).ConfigureAwait(false);
            CommandCache[cacheKey] = new(DateTimeOffset.UtcNow.Add(CommandCacheLifetime), CloneCommands(loaded));
            TrimCommandCache();
            return loaded;
        }
        finally
        {
            gate.Release();
        }
    }

    public static void InvalidateCommands(SqlTarget target)
    {
        var prefix = target.Server.Trim().ToUpperInvariant() + "|" + target.Database.Trim().ToUpperInvariant() + "|";
        foreach (var key in CommandCache.Keys.Where(key => key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
            CommandCache.TryRemove(key, out _);
    }

    private static async Task<List<CommandNode>> WebCommandsAsync(SqlTarget target, string appDatabase, CancellationToken token)
    {
        using var connection = new SqlConnection(target.ConnectionString);
        await connection.OpenAsync(token).ConfigureAwait(false);
        using var columnsCommand = new SqlCommand("SELECT name FROM sys.columns WHERE object_id=OBJECT_ID('dbo.wcommand')", connection) { CommandTimeout = 0 };
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        using (var columnsReader = await columnsCommand.ExecuteReaderAsync(token).ConfigureAwait(false))
            while (await columnsReader.ReadAsync(token).ConfigureAwait(false)) names.Add(columnsReader.GetString(0));
        foreach (var required in new[] { "wmenu_id", "wmenu_id0", "bar" })
            if (!names.Contains(required)) throw new InvalidOperationException("wcommand missing required column: " + required);
        string Column(string name) => names.Contains(name) ? "a." + Quote(name) : "CAST('' AS nvarchar(4000))";
        var commandId = names.Contains("menu_id") ? Column("menu_id") : "a.wmenu_id";
        var projection = "a.wmenu_id AS menu_id,a.wmenu_id0 AS menu_id0,RTRIM(a.bar) AS bar," + Column("link") + " AS link,CASE WHEN " + Column("icon") + "='' THEN " + Column("icon_url") + " ELSE '' END AS icon_url," + Column("icon") + " AS icon," + Column("status") + " AS status,RTRIM(" + Column("target") + ") AS target," + commandId + " AS command_id," + Column("sysid") + " AS sysid";
        var app = Quote(appDatabase);
        var sql = $"""
            SET NOCOUNT ON;
            SELECT {projection} INTO #t FROM dbo.wcommand a;
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
        using var command = new SqlCommand(sql, connection) { CommandTimeout = 0 };
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
                var id = Value("menu_id");
                entries.Add(new(id, Value("menu_id0"), Value("bar"), Value("link"), Value("command_id"), Value("sysid")));
            }
        } while (await reader.NextResultAsync(token).ConfigureAwait(false));
        return CommandHierarchy.Build(entries);
    }

    private static async Task<List<CommandNode>> MobileCommandsAsync(SqlTarget target, CancellationToken token)
    {
        const string sql = """
            IF OBJECT_ID('dbo.dvcommand') IS NULL
            BEGIN
                RAISERROR(N'Không tìm thấy bảng dbo.dvcommand trong SYS.', 16, 1);
                RETURN;
            END;
            SELECT * FROM dbo.dvcommand ORDER BY wmenu_id;
            """;
        using var connection = new SqlConnection(target.ConnectionString);
        await connection.OpenAsync(token).ConfigureAwait(false);
        using var command = new SqlCommand(sql, connection) { CommandTimeout = 0 };
        using var reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false);
        var fields = Enumerable.Range(0, reader.FieldCount).Select(reader.GetName).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var required in new[] { "wmenu_id", "wmenu_id0", "bar" })
            if (!fields.Contains(required)) throw new InvalidOperationException("dvcommand missing required column: " + required);
        string Value(string name) => fields.Contains(name) ? Convert.ToString(reader[name])?.Trim() ?? "" : "";
        var entries = new List<CommandEntry>();
        while (await reader.ReadAsync(token).ConfigureAwait(false))
        {
            if (entries.Count >= 10000) throw new InvalidOperationException("Mobile menu exceeds 10000 rows.");
            var id = Value("wmenu_id");
            var caption = Value("bar");
            if (caption.Length == 0) caption = Value("bar2");
            entries.Add(new(id, Value("wmenu_id0"), caption, "", id, Value("sysid")));
        }
        return CommandHierarchy.BuildAll(entries);
    }

    private static string CommandCacheKey(SqlTarget target, string appDatabase, string kind, string context)
    {
        var builder = new SqlConnectionStringBuilder(target.ConnectionString);
        var identity = builder.IntegratedSecurity ? "INTEGRATED" : builder.UserID.Trim().ToUpperInvariant();
        return string.Join("|", target.Server.Trim().ToUpperInvariant(), target.Database.Trim().ToUpperInvariant(),
            appDatabase.Trim().ToUpperInvariant(), kind.ToUpperInvariant(), identity, context.Trim().ToUpperInvariant());
    }

    private static void TrimCommandCache()
    {
        var now = DateTimeOffset.UtcNow;
        foreach (var expired in CommandCache.Where(entry => entry.Value.ExpiresAt <= now).Select(entry => entry.Key))
            CommandCache.TryRemove(expired, out _);
        foreach (var excess in CommandCache.OrderBy(entry => entry.Value.ExpiresAt).Take(Math.Max(0, CommandCache.Count - CommandCacheLimit)).Select(entry => entry.Key))
            CommandCache.TryRemove(excess, out _);
    }

    private static bool TryGetCommands(string key, out List<CommandNode> nodes)
    {
        if (CommandCache.TryGetValue(key, out var entry) && entry.ExpiresAt > DateTimeOffset.UtcNow)
        {
            nodes = CloneCommands(entry.Nodes);
            return true;
        }
        CommandCache.TryRemove(key, out _);
        nodes = [];
        return false;
    }

    private static List<CommandNode> CloneCommands(IEnumerable<CommandNode> nodes) => nodes.Select(CloneCommand).ToList();
    private static CommandNode CloneCommand(CommandNode source)
    {
        var clone = new CommandNode(source.Entry);
        clone.Children.AddRange(source.Children.Select(CloneCommand));
        return clone;
    }
    public static string Quote(string identifier) => "[" + identifier.Replace("]", "]]", StringComparison.Ordinal) + "]";
}



