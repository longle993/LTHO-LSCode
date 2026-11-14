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
    private readonly TemplateStudioTool templateStudio = new();
    private readonly ConcurrentDictionary<string, (SqlTarget Connection, bool IsProjectSys)> targets = new();
    private readonly HttpClient http = new() { Timeout = TimeSpan.FromSeconds(25) };
    private TranslationClient? translator;
    private SqlTraceSession? trace;
    private ProfilerSession? extendedTrace;
    private long lastExtendedSequence;
    private readonly SemaphoreSlim traceGate = new(1);
    private static readonly SemaphoreSlim persistenceGate = new(1);
    private readonly CatalogWorkspace catalog = new();
    private sealed record TableSnapshot(string Id, SqlTarget Target, string Qualified, DataTable Data);
    private readonly ConcurrentDictionary<string, TableSnapshot> tableSnapshots = new();
    public DeveloperTools() => http.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "Mozilla/5.0");
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private (SqlTarget Connection, bool IsProjectSys) RegisteredTarget(JsonElement p) =>
        targets.TryGetValue(p.GetProperty("targetId").GetString() ?? "", out var t)
            ? t : throw new InvalidOperationException("Chọn kết nối cho tab hiện tại.");
    private SqlTarget Target(JsonElement p) => RegisteredTarget(p).Connection;
    private TargetInfo Register(SqlTarget t, bool isProjectSys = false)
    {
        var id = Guid.NewGuid().ToString("N");
        targets[id] = (t, isProjectSys);
        return new(id, t.Server, t.Database);
    }
    private static ProjectInfo Project(LegacyProject p) => new(p.Id, p.Name, p.Server, p.Database, p.SourcePath);
    private static string Identifier(string name) =>
        string.IsNullOrWhiteSpace(name) || name.Length > 128 || name.Contains('\0')
            ? throw new ArgumentException("Tên schema, bảng hoặc cột không hợp lệ.")
            : name;
    private static string[] RequiredColumns(string[] names) => names.Length == 0
        ? throw new ArgumentException("Chọn ít nhất một cột.") : names;
    private static ResultSet Table(DataTable t) => new(t.Columns.Cast<DataColumn>().Select(c => c.ColumnName).ToArray(),
        t.Rows.Cast<DataRow>().Select(r => r.ItemArray.Select(x => x is null or DBNull ? null : Convert.ToString(x, System.Globalization.CultureInfo.InvariantCulture)).ToArray()).ToArray());
    public async Task<object?> ExecuteAsync(string method, JsonElement p, CancellationToken ct)
    {
        if (method.StartsWith("templateStudio.", StringComparison.Ordinal))
            return await templateStudio.ExecuteAsync(method, p, ct);
        if (method.StartsWith("catalog.", StringComparison.Ordinal)) return await catalog.ExecuteAsync(method, p, Target(p), ct);
        string S(string key, string fallback = "") => p.TryGetProperty(key, out var x) && x.ValueKind == JsonValueKind.String ? x.GetString()! : fallback;
        bool B(string key) => p.TryGetProperty(key, out var x) && x.ValueKind == JsonValueKind.True;
        Dictionary<string, string> OpenBuffers() => new(
            p.TryGetProperty("buffers", out var buffers) ? buffers.Deserialize<Dictionary<string, string>>(Json) ?? [] : [],
            StringComparer.OrdinalIgnoreCase);
        int I(string key, int fallback = 0) => p.TryGetProperty(key, out var x) && x.TryGetInt32(out var n) ? n : fallback;
        string QualifiedTable() => ProjectDatabase.Quote(Identifier(S("schema", "dbo"))) + "." + ProjectDatabase.Quote(Identifier(S("table")));
        string[] ColumnNames() => p.TryGetProperty("columns", out var values)
            ? (values.Deserialize<string[]>() ?? []).Select(Identifier).Distinct(StringComparer.Ordinal).ToArray() : [];
        switch (method)
        {
            case "studio.bootstrap":
                return await Task.Run(() => new
                {
                    settings = SettingsStore.Load(),
                    projects = ProjectStore.Load().Select(Project).ToArray(),
                    snippets = ReadSnippets(),
                    version = "0.1-webview"
                }, ct);
            case "settings.read": return SettingsStore.Load();
            case "settings.save":
                await persistenceGate.WaitAsync(ct);
                try
                {
                    var preferences = p.GetProperty("settings").Deserialize<StudioSettings>(Json) ?? throw new ArgumentException("Settings không hợp lệ.");
                    foreach (var (id, timestamp) in SettingsStore.Load().ProjectLastAccess)
                        if (!preferences.ProjectLastAccess.TryGetValue(id, out var previousAccess) || timestamp > previousAccess)
                            preferences.ProjectLastAccess[id] = timestamp;
                    SettingsStore.Save(preferences);
                    return true;
                }
                finally { persistenceGate.Release(); }
            case "project.create":
                var created = ProjectImporter.Create(S("name"), S("server"), S("database"), S("sourcePath"));
                created = WithUpdatePath(created, S("updatePath"));
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
                return new { project = Project(detail), programPath = detail.Field("ProgramPath"),
                    updatePath = UpdatePath(detail), metadata = ProjectImporter.Export([detail]) };
            case "project.delete":
                await persistenceGate.WaitAsync(ct);
                try
                {
                    var catalog = ProjectStore.Load();
                    var deleted = catalog.FirstOrDefault(item => item.Id == S("id"))
                        ?? throw new InvalidOperationException("Profile dự án đã được xóa.");
                    ProjectStore.Save(catalog.Where(item => item.Id != deleted.Id));
                    var preferences = SettingsStore.Load();
                    preferences.ProjectLastAccess.Remove(deleted.Id);
                    if (preferences.ActiveProjectId == deleted.Id) preferences.ActiveProjectId = null;
                    SettingsStore.Save(preferences);
                    return true;
                }
                finally { persistenceGate.Release(); }
            case "project.update":
                await persistenceGate.WaitAsync(ct);
                try {
                    var all = ProjectStore.Load(); var previous = all.First(x => x.Id == S("id"));
                    var element = XmlTools.Parse(previous.RawXml).Root!;
                    foreach (var pair in new[] { ("FolderName", "name"), ("servername", "server"), ("df_Database", "database"), ("SourcePath", "sourcePath"), ("ProgramPath", "programPath") }) {
                        var field = element.Elements().FirstOrDefault(e => e.Name.LocalName.Equals(pair.Item1, StringComparison.OrdinalIgnoreCase));
                        if (field == null) element.Add(new XElement(pair.Item1, S(pair.Item2))); else field.Value = S(pair.Item2);
                    }
                    var updated = WithUpdatePath(ProjectImporter.Read(element.ToString())[0], S("updatePath", previous.Field("UpdatePath")));
                    EnsureUniqueProject(all.Where(item => item.Id != previous.Id), updated);
                    ProjectStore.Save(all.Select(x => x.Id == previous.Id ? updated : x).ToList()); return Project(updated);
                } finally { persistenceGate.Release(); }
            case "project.history.list":
                await persistenceGate.WaitAsync(ct);
                try
                {
                    return ProjectStore.Load().Select(project =>
                        new { projectId = project.Id, files = ProjectFileActions.ReadHistory(project) })
                        .Where(entry => entry.files.Length > 0).ToArray();
                }
                finally { persistenceGate.Release(); }
            case "project.history":
            case "project.history.record":
            case "project.history.remove":
            case "project.refreshConfig":
            case "project.structure.preview":
            case "project.structure.clear":
                var actionProject = ProjectStore.Load().First(x => x.Id == S("id"));
                await persistenceGate.WaitAsync(ct);
                try
                {
                    if (method == "project.history") return ProjectFileActions.ReadHistory(actionProject);
                    if (method == "project.history.remove")
                    {
                        ProjectFileActions.RemoveHistory(actionProject, S("path"));
                        return true;
                    }
                    if (method == "project.history.record")
                    {
                        ProjectFileActions.RecordHistory(actionProject, S("path"));
                        return true;
                    }
                    if (method == "project.refreshConfig") return await ProjectFileActions.RefreshConfigAsync(actionProject, ct);
                    var structureFolder = ProjectFileActions.StructureFolder(actionProject);
                    if (method == "project.structure.preview")
                        return new { path = structureFolder, count = ProjectFileActions.StructureFiles(structureFolder).Count() };
                    if (!structureFolder.Equals(S("path"), StringComparison.OrdinalIgnoreCase))
                        throw new IOException("Đường dẫn Structure/App đã thay đổi. Hãy mở lại hộp xác nhận.");
                    var deletedCount = ProjectFileActions.ClearStructure(structureFolder, ct);
                    ProjectFileActions.RemoveStructureHistory(actionProject, structureFolder);
                    return deletedCount;
                }
                finally { persistenceGate.Release(); }
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
                    available.AddRange((await ProjectDatabase.ConfiguredTargetsAsync(project).WaitAsync(TimeSpan.FromSeconds(30), ct))
                        .Select((target, index) => Register(target, isProjectSys: index == 1)));
                else if (!string.IsNullOrWhiteSpace(project.Server) && !string.IsNullOrWhiteSpace(project.Database)) {
                    var legacyConnection = new SqlConnectionStringBuilder { DataSource = project.Server, InitialCatalog = project.Database, IntegratedSecurity = string.IsNullOrWhiteSpace(project.User), UserID = project.User, Password = project.Field("Password"), Encrypt = SqlConnectionEncryptOption.Optional, ConnectTimeout = 12, ApplicationName = "LS Code" };
                    available.Add(Register(new(project.Server, project.Database, legacyConnection.ConnectionString)));
                }
                await persistenceGate.WaitAsync(ct);
                try
                {
                    var state = SettingsStore.Load(); state.ActiveProjectId = project.Id; state.ProjectLastAccess[project.Id] = DateTimeOffset.Now;
                    state.Workspace = project.SourcePath; SettingsStore.Save(state);
                }
                finally { persistenceGate.Release(); }
                return new { project = Project(project), targets = available };
            case "workspace.reveal":
                var folder = S("path");
                if (!Path.IsPathFullyQualified(folder) ||
                    !await Task.Run(() => Directory.Exists(folder), ct))
                    throw new DirectoryNotFoundException("Không tìm thấy thư mục dự án.");
                Process.Start(new ProcessStartInfo(folder) { UseShellExecute = true });
                return true;
            case "workspace.duplicate":
                return await WorkspaceFiles.DuplicateAsync(S("path"), ct);
            case "project.discover": return await Task.Run(() => ProjectDiscovery.Find(S("root", @"\\172.168.5.14\CustomerPro"), S("name"), ct), ct);
            case "project.sync":
                var config = S("configPath"); var connections = await Task.Run(() => ProjectDiscovery.ReadConnections(config), ct);
                var identity = new SqlConnectionStringBuilder(connections.Sys);
                var projectName = S("name").Trim();
                var profile = ProjectImporter.Create(projectName, identity.DataSource, identity.InitialCatalog, Path.GetDirectoryName(config)!);
                var raw = XmlTools.Parse(profile.RawXml).Root!;
                raw.Add(new XElement("WebConfigPath", config), new XElement("AppConnectionString", connections.App), new XElement("SysConnectionString", connections.Sys));
                raw.SetElementValue("ProgramPath", $"http://172.168.5.14/{Uri.EscapeDataString(projectName)}/Main/Login.aspx");
                profile = profile with { RawXml = raw.ToString(SaveOptions.DisableFormatting) };
                await SaveProjectAsync(profile, ct, preserveProgramPath: true); return Project(profile);
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
                var fields = S("language") == "xml" ? XmlTools.ScriptFields(S("text"), S("beforeCaret")) : [];
                if (fields.Count > 0) return fields;
                var jsSnippets = CompletionSnippets.Read(await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "Assets", "jssnippet.xml"), ct));
                return CompletionSnippets.Get(jsSnippets, S("beforeCaret")).ToArray();
            case "sql.refresh": SqlSchemaCompletion.Clear(); return true;
            case "schema.objects":
                var search = S("query").Trim();
                var objectOffset = Math.Max(0, I("offset"));
                // Metadata-only searches avoid reading module definitions on every keystroke.
                var filter = search.Length == 0 ? "" : B("content")
                    ? "AND (CHARINDEX(@q, o.name) > 0 OR CHARINDEX(@q, OBJECT_DEFINITION(o.object_id)) > 0)"
                    : "AND CHARINDEX(@q, o.name) > 0";
                return Table(await ProjectDatabase.ReadAsync(Target(p), """
                    WITH matching AS (
                        SELECT s.name AS [Schema], o.name AS [Name], o.type_desc AS [Type], o.object_id AS [Id],
                            ROW_NUMBER() OVER (ORDER BY s.name, o.name, o.object_id) AS position
                        FROM sys.objects o JOIN sys.schemas s ON s.schema_id = o.schema_id
                        WHERE o.is_ms_shipped = 0 AND o.type IN ('U','V','P','FN','IF','TF')
                    """ + "\n" + filter + """

                    )
                    SELECT TOP (200) [Schema], [Name], [Type], [Id]
                    FROM matching WHERE position > @offset ORDER BY position
                    """, ct, new SqlParameter("@q", search), new SqlParameter("@offset", objectOffset)));
            case "schema.columns":
                return Table(await ProjectDatabase.ReadAsync(Target(p), "SELECT c.name AS [Column],t.name AS [Type],c.max_length AS [Length],c.is_nullable AS [Nullable],c.is_identity AS [Identity] FROM sys.columns c JOIN sys.types t ON t.user_type_id=c.user_type_id WHERE c.object_id=OBJECT_ID(@object) ORDER BY c.column_id", ct, new SqlParameter("@object", S("object"))));
            case "table.names":
                return Table(await ProjectDatabase.ReadAsync(Target(p), """
                    SELECT TOP (30) s.name AS [Schema], t.name AS [Name]
                    FROM sys.tables t JOIN sys.schemas s ON s.schema_id=t.schema_id
                    WHERE t.is_ms_shipped=0 AND (@q='' OR CHARINDEX(@q,t.name)>0 OR CHARINDEX(@q,s.name+'.'+t.name)>0)
                    ORDER BY CASE WHEN CHARINDEX(@q,t.name)=1 THEN 0 ELSE 1 END, s.name, t.name
                    """, ct, new SqlParameter("@q", S("query").Trim())));
            case "table.catalog":
                return Table(await ProjectDatabase.ReadAllAsync(Target(p), """
                    SELECT s.name AS [Schema], t.name AS [Name]
                    FROM sys.tables t JOIN sys.schemas s ON s.schema_id=t.schema_id
                    WHERE t.is_ms_shipped=0
                    ORDER BY t.name, s.name
                    """, ct));
            case "table.structure": return await TableInspection.StructureAsync(Target(p), QualifiedTable(), ct);
            case "table.createScript": return await TableDefinition.ReadAsync(Target(p), QualifiedTable(), ct);
            case "table.insertScript":
                if (!tableSnapshots.TryGetValue(S("scope"), out var snapshot) || snapshot.Id != S("snapshotId"))
                    throw new InvalidOperationException("Dữ liệu đã thay đổi. Hãy đọc lại bảng.");
                return await TableInspection.InsertScriptAsync(snapshot.Target, snapshot.Qualified, snapshot.Data, ct);
            case "table.releaseSnapshot":
                tableSnapshots.TryRemove(S("scope"), out _);
                return true;
            case "table.addScript":
            case "table.alterScript":
                var columnTarget = RegisteredTarget(p);
                return await TableInspection.ColumnScriptAsync(columnTarget.Connection, Identifier(S("schema", "dbo")),
                    Identifier(S("table")), RequiredColumns(ColumnNames()), method == "table.alterScript",
                    !columnTarget.IsProjectSys, ct);
            case "table.dropColumns":
                var dropTarget = Target(p);
                var dropTable = QualifiedTable();
                await TableInspection.DropColumnsAsync(dropTarget, dropTable, RequiredColumns(ColumnNames()), ct);
                foreach (var entry in tableSnapshots)
                {
                    var saved = entry.Value;
                    if (saved.Target.Server.Equals(dropTarget.Server, StringComparison.OrdinalIgnoreCase) &&
                        saved.Target.Database.Equals(dropTarget.Database, StringComparison.OrdinalIgnoreCase) &&
                        saved.Qualified.Equals(dropTable, StringComparison.Ordinal))
                        tableSnapshots.TryRemove(entry);
                }
                return true;
            case "schema.script":
                return await DatabaseObjectScript.ReadAsync(Target(p), S("object"), ct);
            case "table.read":
            case "table.view":
                // SELECT clauses are intentionally a developer SQL surface, executed with this target's permissions.
                // Object identifiers are always quoted independently of those expressions.
                var qualified = QualifiedTable();
                var columns = ColumnNames();
                var select = S("select").Trim();
                if (select.Length == 0)
                    select = columns.Length == 0 ? "*" : string.Join(",", columns.Select(ProjectDatabase.Quote));
                var readSql = "SELECT TOP (1000) " + select + " FROM " + qualified
                    + (S("where").Length > 0 ? " WHERE " + S("where") : "") + (S("orderBy").Length > 0 ? " ORDER BY " + S("orderBy") : "");
                var readTarget = Target(p);
                var data = await ProjectDatabase.ReadAsync(readTarget, ReadOnlyQuery.Validate(readSql), ct).ConfigureAwait(false);
                if (method == "table.read") return Table(data);
                var preview = TableInspection.Preview(data);
                ct.ThrowIfCancellationRequested();
                var snapshotId = Guid.NewGuid().ToString("N");
                tableSnapshots[S("scope")] = new(snapshotId, readTarget, qualified, data);
                return new { data = preview, snapshotId };
            case "project.commands": return await ProjectDatabase.CommandsAsync(Target(p), ct, S("appDatabase"));
            case "project.menu.details": return await MenuEditor.ReadAsync(Target(p), S("wmenuId"), ct);
            case "project.menu.new": return await MenuEditor.NewAsync(Target(p), S("selectedId"), ct);
            case "project.menu.resources": return await MenuEditor.ResourcesAsync(S("root"), S("link"), S("sysId"), ct);
            case "project.menu.save":
                var menuValues = p.GetProperty("values").Deserialize<Dictionary<string, string?>>(Json) ?? [];
                return await MenuEditor.SaveAsync(Target(p), S("mode"), S("originalId"), S("version"), menuValues, ct);
            case "project.employeeTabs": return await ProjectDatabase.EmployeeFunctionTabsAsync(Target(p), ct);
            case "project.controller":
                return await Task.Run(async () =>
                {
                    var root = S("root");
                    var link = S("link");
                    var file = ProjectConventions.ResolveMenuFile(root, link);
                    var names = file != null && Path.GetExtension(file).Equals(".aspx", StringComparison.OrdinalIgnoreCase)
                        ? ProjectConventions.Controllers(await File.ReadAllTextAsync(file, ct))
                        : Array.Empty<string>();
                    if (!Directory.Exists(Path.Combine(root, "Main")))
                    {
                        var metadataController = ProjectConventions.ControllerFromPageMetadata(root, S("sysId"), link);
                        if (!string.IsNullOrWhiteSpace(metadataController)) names = [metadataController];
                        else if (names.Count == 0 && !string.IsNullOrWhiteSpace(S("sysId"))) names = [S("sysId")];
                    }
                    var controllerRoots = new[] { Path.Combine(root, "App_Data"), Path.Combine(root, "AppData") }
                        .Where(Directory.Exists).ToArray();
                    var files = (controllerRoots.Length == 0 ? [root] : controllerRoots)
                        .SelectMany(folder => WorkspaceFileIndex.Files(folder, ct, false)).Take(100001).ToArray();
                    if(files.Length>100000)throw new IOException("Project vượt 100000 file; cần thu hẹp thư mục.");
                    var stem = Path.GetFileNameWithoutExtension(file ?? Uri.UnescapeDataString(link.Split('?', '#')[0]));
                    var related = new HashSet<string>(names.Count == 0 ? [stem] : names, StringComparer.OrdinalIgnoreCase);
                    var byName = files.ToLookup(f => Path.GetFileNameWithoutExtension(f), StringComparer.OrdinalIgnoreCase);
                    var pending = new Queue<string>(related);
                    while (pending.TryDequeue(out var name))
                        foreach (var definition in byName[name].Where(f => Path.GetExtension(f).ToLowerInvariant() is ".xml" or ".f"))
                            foreach (var child in ProjectConventions.GridControllers(await File.ReadAllTextAsync(definition, ct)))
                                if (related.Add(child)) pending.Enqueue(child);
                    var matches = files.Where(f => related.Contains(Path.GetFileNameWithoutExtension(f))).ToArray();
                    if (file == null && matches.Length == 0) throw new FileNotFoundException("Không tìm thấy file controller.");
                    var entry = file ?? matches[0];
                    return new { file = entry, files = matches.Prepend(entry).Distinct(StringComparer.OrdinalIgnoreCase).ToArray() };
                }, ct);
            case "text.format":
                return S("language") switch { "sql" => SqlLanguageService.Format(S("text")), "xml" => XmlTools.Format(S("text")), "json" => JsonSerializer.Serialize(JsonDocument.Parse(S("text")).RootElement, new JsonSerializerOptions { WriteIndented = true }), _ => S("text") };
            case "xml.analyze":
                var diagnostic = XmlTools.Validate(S("text"));
                if (diagnostic != null) return new { diagnostic, outline = Array.Empty<object>() };
                return new { diagnostic, outline = XmlTools.Parse(S("text")).Descendants().Take(3000).Select(e => new { name = e.Name.ToString(), line = ((IXmlLineInfo)e).LineNumber }).ToArray() };
            case "xml.viewWidth": return XmlViewWidths.Find(S("text"), I("offset"));
            case "xml.validate": return S("text").Length > 1000000 ? null : XmlTools.Validate(S("text"));
            case "xml.diagnostics": return await Task.Run(() => XmlTools.InspectEditor(S("text"), S("path"), OpenBuffers()), ct);
            case "xml.controllerPreview": return await Task.Run(() => XmlControllerPreview.Read(S("text"), S("path"), OpenBuffers()), ct);
            case "xml.xpath":
                var xml = XmlTools.Parse(S("text")); var manager = new XmlNamespaceManager(new NameTable());
                if (xml.Root?.GetDefaultNamespace() is { NamespaceName.Length: > 0 } ns) manager.AddNamespace("d", ns.NamespaceName);
                var value = xml.XPathEvaluate(S("expression"), manager);
                return value is System.Collections.IEnumerable sequence and not string ? string.Join(Environment.NewLine, sequence.Cast<object>()) : Convert.ToString(value);
            case "xml.include":
                var byPath = OpenBuffers();
                return await Task.Run(() => XmlIncludes.Find(S("path"), S("text"), I("offset"),
                    path => byPath.TryGetValue(path, out var buffer) ? buffer : File.Exists(path) ? File.ReadAllText(path) : null), ct);
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
                        var auth = new SqlConnectionStringBuilder(t.ConnectionString) { IntegratedSecurity = false, Authentication = SqlAuthenticationMethod.NotSpecified, UserID = S("user", "profile"), Password = S("password", "fsd") };
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
    private async Task SaveProjectAsync(LegacyProject project, CancellationToken ct, bool preserveProgramPath = false)
    {
        await persistenceGate.WaitAsync(ct);
        try
        {
            var existing = ProjectStore.Load();
            if (preserveProgramPath)
            {
                var previous = existing.FirstOrDefault(p => p.Id == project.Id);
                project = WithUpdatePath(project, previous?.Field("UpdatePath") ?? "");
                var customPath = previous?.Field("ProgramPath");
                if (!string.IsNullOrWhiteSpace(customPath))
                {
                    var raw = XmlTools.Parse(project.RawXml).Root!;
                    raw.SetElementValue("ProgramPath", customPath);
                    project = project with { RawXml = raw.ToString(SaveOptions.DisableFormatting) };
                }
            }
            else EnsureUniqueProject(existing, project);
            ProjectStore.Save(ProjectImporter.Merge(existing, [project], true).Projects);
        }
        finally { persistenceGate.Release(); }
    }
    private static string UpdatePath(LegacyProject project) =>
        project.Field("UpdatePath").Trim() is { Length: > 0 } saved ? saved :
            $@"\\172.168.5.14\CustomerUpdate\{project.Name}\update\";

    private static LegacyProject WithUpdatePath(LegacyProject project, string value)
    {
        var xml = XmlTools.Parse(project.RawXml).Root!;
        var path = xml.Elements().FirstOrDefault(field => field.Name.LocalName.Equals("UpdatePath", StringComparison.OrdinalIgnoreCase));
        var updated = string.IsNullOrWhiteSpace(value) ? UpdatePath(project) : value.Trim();
        if (path is null) xml.Add(new XElement("UpdatePath", updated));
        else path.Value = updated;
        return project with { RawXml = xml.ToString(SaveOptions.DisableFormatting) };
    }

    private static void EnsureUniqueProject(IEnumerable<LegacyProject> existing, LegacyProject candidate)
    {
        static bool Same(string left, string right) => left.Trim().Equals(right.Trim(), StringComparison.OrdinalIgnoreCase);
        if (existing.Any(item => item.Id == candidate.Id ||
            (Same(item.Name, candidate.Name) && Same(item.Server, candidate.Server) && Same(item.Database, candidate.Database))))
            throw new InvalidOperationException($"Profile dự án '{candidate.Name}' đã tồn tại.");
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
    public async ValueTask DisposeAsync() { await StopTraceAsync(); http.Dispose(); traceGate.Dispose(); }
}
