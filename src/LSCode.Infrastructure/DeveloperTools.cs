using System.Collections.Concurrent;
using System.Data;
using System.Diagnostics;
using System.Text.Json;
using System.Xml;
using System.Xml.Linq;
using System.Xml.XPath;
using LSCode.Application;
using LSCode.Core;
using LSCode.Domain;
using LSCode.Shell.Services;
using Microsoft.Data.SqlClient;

namespace LSCode.Infrastructure;

public sealed class DeveloperTools : IDeveloperTools, IAsyncDisposable
{
    private readonly ConcurrentDictionary<string, SqlTarget> targets = new();
    private readonly HttpClient http = new() { Timeout = TimeSpan.FromSeconds(25) };
    private TranslationClient? translator;
    private SqlTraceSession? trace;
    private ProfilerSession? extendedTrace;
    private long lastExtendedSequence;
    private readonly SemaphoreSlim traceGate = new(1);
    private readonly SemaphoreSlim persistenceGate = new(1);
    private readonly CatalogWorkspace catalog = new();
    public DeveloperTools() => http.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "Mozilla/5.0");
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private SqlTarget Target(JsonElement p) => targets.TryGetValue(p.GetProperty("targetId").GetString() ?? "", out var t) ? t : throw new InvalidOperationException("Chọn kết nối cho tab hiện tại.");
    private TargetInfo Register(SqlTarget t) { var id = Guid.NewGuid().ToString("N"); targets[id] = t; return new(id, t.Server, t.Database); }
    private static ProjectInfo Project(LegacyProject p) => new(p.Id, p.Name, p.Server, p.Database, p.SourcePath);
    private static ResultSet Table(DataTable t) => new(t.Columns.Cast<DataColumn>().Select(c => c.ColumnName).ToArray(),
        t.Rows.Cast<DataRow>().Select(r => r.ItemArray.Select(x => x is null or DBNull ? null : Convert.ToString(x, System.Globalization.CultureInfo.InvariantCulture)).ToArray()).ToArray());
    public async Task<object?> ExecuteAsync(string method, JsonElement p, CancellationToken ct)
    {
        if (method.StartsWith("catalog.", StringComparison.Ordinal)) return await catalog.ExecuteAsync(method, p, Target(p), ct);
        string S(string key, string fallback = "") => p.TryGetProperty(key, out var x) && x.ValueKind == JsonValueKind.String ? x.GetString()! : fallback;
        bool B(string key) => p.TryGetProperty(key, out var x) && x.ValueKind == JsonValueKind.True;
        int I(string key, int fallback = 0) => p.TryGetProperty(key, out var x) && x.TryGetInt32(out var n) ? n : fallback;
        switch (method)
        {
            case "studio.bootstrap":
                var settings = SettingsStore.Load();
                return new { settings, projects = ProjectStore.Load().Select(Project), snippets = ReadSnippets(), version = "0.1-webview" };
            case "settings.read": return SettingsStore.Load();
            case "settings.save":
                await persistenceGate.WaitAsync(ct);
                try { SettingsStore.Save(p.GetProperty("settings").Deserialize<StudioSettings>(Json) ?? throw new ArgumentException("Settings không hợp lệ.")); return true; }
                finally { persistenceGate.Release(); }
            case "project.create":
                var created = ProjectImporter.Create(S("name"), S("server"), S("database"), S("sourcePath"));
                await SaveProjectAsync(created, ct); return Project(created);
            case "project.list": return ProjectStore.Load().Select(Project).ToArray();
            case "project.import.preview": return ProjectImporter.Read(S("xml")).Select(Project).ToArray();
            case "project.import":
                await persistenceGate.WaitAsync(ct);
                try { var merged = ProjectImporter.Merge(ProjectStore.Load(), ProjectImporter.Read(S("xml")), B("overwrite")); ProjectStore.Save(merged.Projects); return new { projects = merged.Projects.Select(Project), merged.Added, merged.Updated, merged.Skipped }; }
                finally { persistenceGate.Release(); }
            case "project.export": return ProjectImporter.Export(ProjectStore.Load());
            case "project.metadata":
                return ProjectImporter.Export([ProjectStore.Load().First(x => x.Id == S("id"))]);
            case "project.details":
                var detail = ProjectStore.Load().First(x => x.Id == S("id"));
                return new { project = Project(detail), programPath = detail.Field("ProgramPath"), metadata = ProjectImporter.Export([detail]) };
            case "project.update":
                await persistenceGate.WaitAsync(ct);
                try {
                    var all = ProjectStore.Load(); var previous = all.First(x => x.Id == S("id"));
                    var element = XmlTools.Parse(previous.RawXml).Root!;
                    foreach (var pair in new[] { ("FolderName", "name"), ("servername", "server"), ("df_Database", "database"), ("SourcePath", "sourcePath"), ("ProgramPath", "programPath") }) {
                        var field = element.Elements().FirstOrDefault(e => e.Name.LocalName.Equals(pair.Item1, StringComparison.OrdinalIgnoreCase));
                        if (field == null) element.Add(new XElement(pair.Item1, S(pair.Item2))); else field.Value = S(pair.Item2);
                    }
                    var updated = ProjectImporter.Read(element.ToString())[0];
                    ProjectStore.Save(all.Select(x => x.Id == previous.Id ? updated : x).ToList()); return Project(updated);
                } finally { persistenceGate.Release(); }
            case "project.program":
                var programProject = ProjectStore.Load().First(x => x.Id == S("id"));
                var programPath = programProject.Field("ProgramPath");
                if (string.IsNullOrWhiteSpace(programPath)) throw new InvalidOperationException("Khai báo đường dẫn chương trình trong chi tiết dự án.");
                if (!Uri.TryCreate(programPath, UriKind.Absolute, out var uri) || uri.IsFile) {
                    programPath = Path.GetFullPath(programPath, Path.IsPathFullyQualified(programProject.SourcePath) ? programProject.SourcePath : Environment.CurrentDirectory);
                    if (!File.Exists(programPath) && !Directory.Exists(programPath)) throw new FileNotFoundException("Không tìm thấy chương trình.", programPath);
                } else if (uri.Scheme is not ("http" or "https")) throw new InvalidOperationException("Chỉ hỗ trợ đường dẫn file, thư mục hoặc HTTP(S).");
                Process.Start(new ProcessStartInfo(programPath) { UseShellExecute = true }); return true;
            case "project.open":
                var project = ProjectStore.Load().First(x => x.Id == S("id"));
                var available = new List<TargetInfo>();
                if (!string.IsNullOrWhiteSpace(project.Field("SysConnectionString")) || !string.IsNullOrWhiteSpace(project.Field("WebConfigPath")))
                    available.AddRange((await ProjectDatabase.ConfiguredTargetsAsync(project).WaitAsync(ct)).Select(Register));
                else if (!string.IsNullOrWhiteSpace(project.Server) && !string.IsNullOrWhiteSpace(project.Database)) {
                    var legacyConnection = new SqlConnectionStringBuilder { DataSource = project.Server, InitialCatalog = project.Database, IntegratedSecurity = string.IsNullOrWhiteSpace(project.User), UserID = project.User, Password = project.Field("Password"), Encrypt = SqlConnectionEncryptOption.Optional, ConnectTimeout = 12, ApplicationName = "LS Code" };
                    available.Add(Register(new(project.Server, project.Database, legacyConnection.ConnectionString)));
                }
                var state = SettingsStore.Load(); state.ActiveProjectId = project.Id; state.ProjectLastAccess[project.Id] = DateTimeOffset.Now;
                state.Workspace = project.SourcePath; SettingsStore.Save(state);
                return new { project = Project(project), targets = available };
            case "project.discover": return await Task.Run(() => ProjectDiscovery.Find(S("root", @"\\172.168.5.14\CustomerPro"), S("name"), ct), ct);
            case "project.sync":
                var config = S("configPath"); var connections = await Task.Run(() => ProjectDiscovery.ReadConnections(config), ct);
                var identity = new SqlConnectionStringBuilder(connections.Sys);
                var profile = ProjectImporter.Create(S("name"), identity.DataSource, identity.InitialCatalog, Path.GetDirectoryName(config)!);
                var raw = XmlTools.Parse(profile.RawXml).Root!;
                raw.Add(new XElement("WebConfigPath", config), new XElement("AppConnectionString", connections.App), new XElement("SysConnectionString", connections.Sys));
                profile = profile with { RawXml = raw.ToString(SaveOptions.DisableFormatting) };
                await SaveProjectAsync(profile, ct); return Project(profile);
            case "sql.connect":
                var builder = new SqlConnectionStringBuilder { DataSource = S("server"), InitialCatalog = S("database"), IntegratedSecurity = B("integratedSecurity"),
                    UserID = B("integratedSecurity") ? "" : S("user"), Password = B("integratedSecurity") ? "" : S("password"), Encrypt = true,
                    TrustServerCertificate = B("trustCertificate"), ApplicationName = "LS Code", ConnectTimeout = 15 };
                await using (var connection = new SqlConnection(builder.ConnectionString)) await connection.OpenAsync(ct);
                return Register(new(builder.DataSource, builder.InitialCatalog, builder.ConnectionString));
            case "sql.execute":
                QueryResult result;
                try { result = await SqlRunner.ExecuteAsync(Target(p).ConnectionString, S("sql"), ct); }
                catch (SqlExecutionException ex) when (ct.IsCancellationRequested) { throw new OperationCanceledException("Đã dừng query.", ex, ct); }
                return new ExecutionResult(result.Tables.Select(Table).ToArray(), result.Messages.ToArray(), result.Duration.TotalMilliseconds, result.Truncated);
            case "sql.complete":
                IReadOnlyList<SqlSymbol> symbols = S("targetId").Length > 0 ? await SqlSchemaCompletion.LoadAsync(Target(p), S("text")).WaitAsync(ct) : [];
                var sqlSnippets = CompletionSnippets.Read(await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "Assets", "sqlsnippet.xml"), ct));
                return SqlSuggestions.Get(S("beforeCaret"), symbols, S("text")).Concat(CompletionSnippets.Get(sqlSnippets, S("beforeCaret"))).ToArray();
            case "javascript.complete":
                if (S("language") != "javascript" && !CompletionSnippets.IsInScript(S("text"), S("beforeCaret").Length)) return Array.Empty<SqlSuggestion>();
                var jsSnippets = CompletionSnippets.Read(await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "Assets", "jssnippet.xml"), ct));
                return CompletionSnippets.Get(jsSnippets, S("beforeCaret")).ToArray();
            case "sql.refresh": SqlSchemaCompletion.Clear(); return true;
            case "schema.objects":
                return Table(await ProjectDatabase.ReadAsync(Target(p), """
                    SELECT s.name AS [Schema],o.name AS [Name],o.type_desc AS [Type],o.object_id AS [Id]
                    FROM sys.objects o JOIN sys.schemas s ON s.schema_id=o.schema_id
                    LEFT JOIN sys.sql_modules m ON m.object_id=o.object_id
                    WHERE o.is_ms_shipped=0 AND o.type IN ('U','V','P','FN','IF','TF')
                    AND (o.name LIKE @q OR (@content=1 AND m.definition LIKE @q))
                    ORDER BY s.name,o.name OFFSET @offset ROWS FETCH NEXT 200 ROWS ONLY
                    """, ct, new SqlParameter("@q", "%" + S("query") + "%"), new SqlParameter("@content", B("content")), new SqlParameter("@offset", Math.Max(0, I("offset")))));
            case "schema.columns":
                return Table(await ProjectDatabase.ReadAsync(Target(p), "SELECT c.name AS [Column],t.name AS [Type],c.max_length AS [Length],c.is_nullable AS [Nullable],c.is_identity AS [Identity] FROM sys.columns c JOIN sys.types t ON t.user_type_id=c.user_type_id WHERE c.object_id=OBJECT_ID(@object) ORDER BY c.column_id", ct, new SqlParameter("@object", S("object"))));
            case "schema.script":
                var definition = await ProjectDatabase.ReadAsync(Target(p), "SELECT OBJECT_DEFINITION(OBJECT_ID(@object)) AS [Text]", ct, new SqlParameter("@object", S("object")));
                if (definition.Rows.Count > 0 && definition.Rows[0][0] is string script) return script;
                return await TableDefinition.ReadAsync(Target(p), S("object"), ct);
            case "table.read":
                // SELECT clauses are intentionally a developer SQL surface, executed with this target's permissions.
                // Object identifiers are always quoted independently of those expressions.
                var qualified = ProjectDatabase.Quote(S("schema", "dbo")) + "." + ProjectDatabase.Quote(S("table"));
                var columns = p.TryGetProperty("columns", out var cols) ? cols.Deserialize<string[]>() ?? [] : [];
                var select = columns.Length == 0 ? "*" : string.Join(",", columns.Select(ProjectDatabase.Quote));
                var readSql = "SELECT TOP (1000) " + select + " FROM " + qualified
                    + (S("where").Length > 0 ? " WHERE " + S("where") : "") + (S("orderBy").Length > 0 ? " ORDER BY " + S("orderBy") : "");
                return Table(await ProjectDatabase.ReadAsync(Target(p), ReadOnlyQuery.Validate(readSql), ct));
            case "project.commands": return await ProjectDatabase.CommandsAsync(Target(p), ct, S("appDatabase"));
            case "project.controller":
                return await Task.Run(async () =>
                {
                    var root = S("root"); var file = ProjectConventions.ResolveMenuFile(root, S("link")) ?? throw new FileNotFoundException("Không tìm thấy file menu.");
                    var names = ProjectConventions.Controllers(await File.ReadAllTextAsync(file, ct));
                    var controllerRoot=Path.Combine(root,"App_Data");
                    var files = WorkspaceFileIndex.Files(Directory.Exists(controllerRoot)?controllerRoot:root, ct,false).Take(100001).ToArray();
                    if(files.Length>100000)throw new IOException("Project vượt 100000 file; cần thu hẹp thư mục.");
                    var related = new HashSet<string>(names.Count == 0 ? [Path.GetFileNameWithoutExtension(file)] : names, StringComparer.OrdinalIgnoreCase);
                    var pending = new Queue<string>(related);
                    while (pending.TryDequeue(out var name))
                        foreach (var xmlFile in files.Where(f => Path.GetExtension(f).Equals(".xml", StringComparison.OrdinalIgnoreCase) && Path.GetFileNameWithoutExtension(f).Equals(name, StringComparison.OrdinalIgnoreCase)))
                            foreach (var child in ProjectConventions.GridControllers(await File.ReadAllTextAsync(xmlFile, ct))) if (related.Add(child)) pending.Enqueue(child);
                    return new { file, files = files.Where(f => related.Contains(Path.GetFileNameWithoutExtension(f))).Prepend(file).Distinct(StringComparer.OrdinalIgnoreCase).ToArray() };
                }, ct);
            case "text.format":
                return S("language") switch { "sql" => SqlLanguageService.Format(S("text")), "xml" => XmlTools.Format(S("text")), "json" => JsonSerializer.Serialize(JsonDocument.Parse(S("text")).RootElement, new JsonSerializerOptions { WriteIndented = true }), _ => S("text") };
            case "xml.analyze":
                var diagnostic = XmlTools.Validate(S("text"));
                if (diagnostic != null) return new { diagnostic, outline = Array.Empty<object>() };
                return new { diagnostic, outline = XmlTools.Parse(S("text")).Descendants().Take(3000).Select(e => new { name = e.Name.ToString(), line = ((IXmlLineInfo)e).LineNumber }).ToArray() };
            case "xml.viewWidth": return XmlViewWidths.Find(S("text"), I("offset"));
            case "xml.validate": return S("text").Length > 1000000 ? null : XmlTools.Validate(S("text"));
            case "xml.xpath":
                var xml = XmlTools.Parse(S("text")); var manager = new XmlNamespaceManager(new NameTable());
                if (xml.Root?.GetDefaultNamespace() is { NamespaceName.Length: > 0 } ns) manager.AddNamespace("d", ns.NamespaceName);
                var value = xml.XPathEvaluate(S("expression"), manager);
                return value is System.Collections.IEnumerable sequence and not string ? string.Join(Environment.NewLine, sequence.Cast<object>()) : Convert.ToString(value);
            case "xml.include":
                var openBuffers = p.TryGetProperty("buffers", out var bufferElement) ? bufferElement.Deserialize<Dictionary<string, string>>(Json) ?? [] : [];
                var byPath = new Dictionary<string, string>(openBuffers, StringComparer.OrdinalIgnoreCase);
                return XmlIncludes.Find(S("path"), S("text"), I("offset"), path => byPath.TryGetValue(path, out var buffer) ? buffer : File.Exists(path) ? File.ReadAllText(path) : null);
            case "snippet.list": return ReadSnippets();
            case "snippet.completions":
                var snippetFile = Path.Combine(AppContext.BaseDirectory, "Assets", S("language") == "javascript" ? "jssnippet.xml" : "sqlsnippet.xml");
                return CompletionSnippets.Read(await File.ReadAllTextAsync(snippetFile, ct));
            case "snippet.expand":
                var expanded = CompletionSnippets.Expand(S("body"), S("language") == "javascript");
                return new { expanded.Text, expanded.Offset, expanded.Length };
            case "snippet.save":
                var snippets = p.GetProperty("snippets").Deserialize<List<Snippet>>(Json) ?? [];
                await persistenceGate.WaitAsync(ct);
                try
                {
                    Directory.CreateDirectory(SettingsStore.Root);var snippetPath=Path.Combine(SettingsStore.Root,"webview-snippets.xml");
                    await File.WriteAllTextAsync(snippetPath+".tmp",SnippetImporter.Write(snippets),ct);File.Move(snippetPath+".tmp",snippetPath,true);return snippets;
                }
                finally{persistenceGate.Release();}
            case "snippet.import": return SnippetImporter.Read(S("xml"));
            case "snippet.export": return SnippetImporter.Write(ReadSnippets());
            case "diff.text": return TextDiff.Compare(S("left"), S("right"));
            case "diff.folder": return await Task.Run(() => FolderDiff.CompareAsync(S("left"), S("right"), ct), ct);
            case "translation.run": return await (translator ??= new(http)).TranslateAsync(S("text"), S("from", "en"), S("to", "vi"), ct);
            case "translation.browser": Process.Start(new ProcessStartInfo(TranslationClient.BrowserUrl(S("from", "auto"), S("to", "vi"), S("text"))) { UseShellExecute = true }); return true;
            case "profiler.start":
                await traceGate.WaitAsync(ct);
                try
                {
                    if (trace != null || extendedTrace != null) throw new InvalidOperationException("Profiler đang chạy. Dừng phiên hiện tại trước.");
                    var t = Target(p);
                    if (!B("useTargetCredentials")) {
                        var auth = new SqlConnectionStringBuilder(t.ConnectionString) { IntegratedSecurity = false, Authentication = SqlAuthenticationMethod.NotSpecified, UserID = S("user", "profiler"), Password = S("password", "fsd") };
                        t = t with { ConnectionString = auth.ConnectionString };
                    }
                    if (S("profilerPath").Length > 0) {
                        SqlTraceSession.ValidateProfilerPath(S("profilerPath"));
                        var preferences = SettingsStore.Load(); preferences.Profiler18Path = S("profilerPath"); SettingsStore.Save(preferences);
                    }
                    var template = ProfilerTemplate.Default(t.Database);
                    if (S("templatePath").Length > 0) template = template with { TraceTemplatePath = S("templatePath") };
                    if (S("filter").Length > 0) template = template with { DatabaseFilter = S("filter") };
                    if (S("engine") == "extendedEvents")
                    {
                        var prefix = template.DatabaseFilter ?? t.Database;
                        template = template with { DatabaseFilter = prefix.Replace("[", "[[]").Replace("%", "[%]").Replace("_", "[_]") + "%" };
                        extendedTrace = new(t, template); lastExtendedSequence = 0;
                        try { await extendedTrace.StartAsync(ct); } catch { await extendedTrace.DropAsync(); extendedTrace = null; throw; }
                        return true;
                    }
                    trace = new(t, template);
                    try { await trace.StartAsync(ct); } catch { await trace.DropAsync(); trace = null; throw; }
                    return true;
                }
                finally { traceGate.Release(); }
            case "profiler.read":
                if (extendedTrace != null)
                {
                    var captured = await extendedTrace.ReadAsync(ct);
                    var fresh = captured.Events.Where(e => e.Sequence > lastExtendedSequence).ToArray();
                    if (fresh.Length > 0) lastExtendedSequence = fresh.Max(e => e.Sequence);
                    return captured with { Events = fresh };
                }
                return trace == null ? new ProfilerBatch([], false, 0) : await trace.ReadAsync(ct);
            case "profiler.stop": await StopTraceAsync(); return true;
            case "external.crystal":
                var exe = SettingsStore.Load().CrystalPath;
                if (!File.Exists(exe)) throw new FileNotFoundException("Cấu hình đường dẫn Crystal trong Cài đặt.");
                var process = new ProcessStartInfo(exe) { UseShellExecute = true }; process.ArgumentList.Add(S("path")); Process.Start(process); return true;
            default: throw new NotSupportedException("Lệnh không được hỗ trợ: " + method);
        }
    }
    private static List<Snippet> ReadSnippets()
    {
        var file = Path.Combine(SettingsStore.Root, "webview-snippets.xml");
        if (!File.Exists(file)) file = Path.Combine(SettingsStore.Root, "snippets.xml");
        if (!File.Exists(file)) file = Path.Combine(AppContext.BaseDirectory, "Assets", "snippets.xml");
        return File.Exists(file) ? SnippetImporter.Read(File.ReadAllText(file)) : [];
    }
    private async Task SaveProjectAsync(LegacyProject project, CancellationToken ct)
    {
        await persistenceGate.WaitAsync(ct);
        try { ProjectStore.Save(ProjectImporter.Merge(ProjectStore.Load(), [project], true).Projects); }
        finally { persistenceGate.Release(); }
    }
    public async Task StopTraceAsync()
    {
        await traceGate.WaitAsync();
        try
        {
            if (trace != null) { await trace.DropAsync(); trace = null; }
            if (extendedTrace != null) { await extendedTrace.DropAsync(); extendedTrace = null; }
        }
        finally { traceGate.Release(); }
    }
    public async ValueTask DisposeAsync() { await StopTraceAsync(); http.Dispose(); traceGate.Dispose(); persistenceGate.Dispose(); }
}
