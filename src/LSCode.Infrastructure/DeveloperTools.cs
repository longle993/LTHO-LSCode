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
    private SqlTraceStream? traceStream;
    private ProfilerSession? extendedTrace;
    private long lastExtendedSequence;
    private readonly SemaphoreSlim traceGate = new(1);
    private static readonly SemaphoreSlim persistenceGate = new(1);
    private readonly CatalogWorkspace catalog = new();
    private sealed record TableSnapshot(string Id, SqlTarget Target, string Qualified, DataTable Data);
    private sealed record ControllerProblem(string Path, string Message, int Line, int Column, string Severity);
    private readonly ConcurrentDictionary<string, TableSnapshot> tableSnapshots = new();
    public DeveloperTools() => http.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "Mozilla/5.0");
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private sealed record SourceVersion(string Version, string[] Roots);
    private static SourceVersion[] SourceVersions() => ProjectStore.Load()
        .Select(project => new { Version = project.Field("ProjectVersion").Trim(), Root = project.SourcePath.Trim() })
        .Where(item => item.Version.Length != 0 && Path.IsPathFullyQualified(item.Root))
        .Concat(SourceVersionCatalog.Load().Select(item => new { item.Version, item.Root }))
        .GroupBy(item => item.Version, StringComparer.OrdinalIgnoreCase)
        .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
        .Select(group => new SourceVersion(group.Key, group.Select(item => item.Root).Distinct(StringComparer.OrdinalIgnoreCase).ToArray()))
        .ToArray();
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
    private static ProjectInfo Project(LegacyProject p)
    {
        var root = XmlTools.Parse(p.RawXml).Root!;
        string Field(string name) => root.Elements().FirstOrDefault(element =>
            element.Name.LocalName.Equals(name, StringComparison.OrdinalIgnoreCase))?.Value ?? "";
        var code = Field("StandardProjectCode").Trim() is { Length: > 0 } savedCode ? savedCode : p.Name;
        var title = Field("StandardProjectTitle").Trim() is { Length: > 0 } savedTitle ? savedTitle : code;
        var programPath = new[] { Field("WebProgramPath"), Field("WinProgramPath"), Field("ProgramPath"), Field("SourcePath") }
            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim() ?? "";
        return new(p.Id, p.Name, Field("servername"), Field("df_Database"), Field("SourcePath"),
            code, title, programPath, Field("IsActive") is not ("0" or "false" or "False"));
    }
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
        if (method == "mail.sendTest")
            return await CheckMail.SendAsync(p.Deserialize<LSCode.Contracts.CheckMailRequest>(Json)
                ?? throw new ArgumentException("Cấu hình SMTP không hợp lệ."), ct);
        if (method == "templateStudio.save")
            return await Task.Run(() => templateStudio.ExecuteAsync(method, p, ct), ct);
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
        LegacyProject ApplyProjectInput(LegacyProject seed, bool keepPassword)
        {
            var xml = XmlTools.Parse(seed.RawXml).Root!;
            void Set(string name, string value)
            {
                var field = xml.Elements().FirstOrDefault(element => element.Name.LocalName.Equals(name, StringComparison.OrdinalIgnoreCase));
                if (field is null) xml.Add(new XElement(name, value)); else field.Value = value;
            }
            var code = S("code", S("name")).Trim();
            if (code.Length == 0) throw new ArgumentException("Nhập mã dự án.");
            var title = S("title", code).Trim();
            var webSource = S("webSourcePath"); var webProgram = S("webProgramPath");
            var winSource = S("winSourcePath"); var winProgram = S("winProgramPath");
            var source = S("sourcePath", new[] { webSource, webProgram, winSource, winProgram }
                .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? "");
            var webHost1 = S("webHost1"); var webHost2 = S("webHost2");
            var active = !p.TryGetProperty("active", out var activeValue) || activeValue.ValueKind != JsonValueKind.False;
            var program = S("programPath", new[] { webHost1, webHost2, webProgram, winProgram }
                .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? "");
            foreach (var pair in new (string Xml, string Value)[] {
                ("FolderName", code), ("StandardProjectCode", code), ("StandardProjectTitle", title),
                ("ProjectVersion", S("version")), ("IsActive", active ? "1" : "0"),
                ("WinProgramPath", winProgram), ("WebProgramPath", webProgram),
                ("WinSourcePath", winSource), ("WebSourcePath", webSource),
                ("WebHost1", webHost1), ("WebHost2", webHost2), ("UpdatePath", S("updatePath")),
                ("servername", S("server")), ("Authentication", S("authentication", "sql")),
                ("User", S("user")), ("df_Database", S("database")), ("DefaultUser", S("defaultUser")),
                ("ListAllData", S("listAllData")), ("SortOrder", S("sortOrder")),
                ("HomeUpdateCode", S("homeUpdateCode")), ("Notes", S("notes")),
                ("SourcePath", source), ("ProgramPath", program),
                ("Fsd_ma_da", code), ("Fsd_ten_da", title), ("Fsd_dir_pro_app", winProgram),
                ("Fsd_dir_src_app", winSource), ("Fsd_dir_pro_web", webProgram), ("Fsd_dir_src_web", webSource),
                ("Fsd_web_host1", webHost1), ("Fsd_web_host2", webHost2), ("Fsd_dir_update", S("updatePath")),
                ("Fsd_ten_server", S("server")), ("Fsd_xuser", S("user")), ("Fsd_db_sys", S("database")),
                ("Fsd_ghi_chu", S("notes")), ("Fsd_status", active ? "1" : "0") }) Set(pair.Xml, pair.Value);
            var password = S("password");
            if (password.Length > 0 || !keepPassword) Set("Password", password);
            return seed with { Name = code, RawXml = xml.ToString(SaveOptions.DisableFormatting) };
        }
        switch (method)
        {
            case "advanceNote.load":
                return await AdvanceNoteStore.LoadAsync(S("projectId"), ct);
            case "advanceNote.save":
                var advanceNotes = p.TryGetProperty("items", out var noteItems)
                    ? noteItems.Deserialize<AdvanceNoteItem[]>(Json) ?? [] : [];
                return await AdvanceNoteStore.SaveAsync(S("projectId"), advanceNotes, ct);
            case "advanceNote.generateUpdate":
                var updateItem = p.GetProperty("item").Deserialize<AdvanceNoteItem>(Json)
                    ?? throw new ArgumentException("Yêu cầu Generate Update không hợp lệ.");
                var updateProject = ProjectStore.Load().FirstOrDefault(project => project.Id == S("projectId"))
                    ?? throw new InvalidOperationException("Dự án đã bị xóa khỏi danh sách.");
                var updateTargets = new Dictionary<string, SqlTarget>(StringComparer.OrdinalIgnoreCase);
                foreach (var (database, property) in new[] { ("App", "appTargetId"), ("Sys", "sysTargetId") })
                {
                    var id = S(property);
                    if (id.Length > 0 && targets.TryGetValue(id, out var registered))
                        updateTargets[database] = registered.Connection;
                }
                return await AdvanceNoteUpdateGenerator.GenerateAsync(S("root"), UpdatePath(updateProject), updateItem,
                    updateTargets, ct);
            case "studio.bootstrap":
                return await Task.Run(() => new
                {
                    settings = SettingsStore.Load(),
                    projects = ProjectStore.Load().Select(Project).ToArray(),
                    snippets = B("deferSnippets") ? [] : ReadSnippets(),
                    version = "0.1-webview"
                }, ct);
            case "settings.read": return SettingsStore.Load();
            case "settings.save":
                await persistenceGate.WaitAsync(ct);
                try
                {
                    var preferences = p.GetProperty("settings").Deserialize<StudioSettings>(Json) ?? throw new ArgumentException("Settings không hợp lệ.");
                    var storedPreferences = SettingsStore.Load();
                    preferences.SourceCollectionVersion = storedPreferences.SourceCollectionVersion;
                    preferences.StandardCatalogUser = storedPreferences.StandardCatalogUser;
                    foreach (var (id, timestamp) in storedPreferences.ProjectLastAccess)
                        if (!preferences.ProjectLastAccess.TryGetValue(id, out var previousAccess) || timestamp > previousAccess)
                            preferences.ProjectLastAccess[id] = timestamp;
                    SettingsStore.Save(preferences);
                    return true;
                }
                finally { persistenceGate.Release(); }
            case "project.create":
                var created = ProjectImporter.Create(S("code", S("name")), S("server"), S("database"), S("sourcePath"));
                created = ApplyProjectInput(created, false);
                await SaveProjectAsync(created, ct); return Project(created);
            case "project.list": return ProjectStore.Load().Select(Project).ToArray();
            case "project.import.previewFile":
                return ProjectImporter.PreviewFile(ProjectStore.Load(), S("path"), B("overwrite"), ct);
            case "project.import.applyFile":
                await persistenceGate.WaitAsync(ct);
                try
                {
                    var incoming = ProjectImporter.ReadFile(S("path"), ct);
                    var merged = ProjectImporter.Merge(ProjectStore.Load(), incoming, B("overwrite"));
                    if (merged.Added > 0 || merged.Updated > 0) ProjectStore.Save(merged.Projects);
                    return new { projects = merged.Projects.Select(Project).ToArray(), merged.Added, merged.Updated, merged.Skipped };
                }
                finally { persistenceGate.Release(); }
            case "project.export.file":
                var exportPath = S("path");
                var exported = ProjectImporter.ExportFile(ProjectStore.Load(), exportPath, cancellationToken: ct);
                return new { path = exportPath, projectCount = exported, byteCount = new FileInfo(exportPath).Length };
            case "project.metadata":
                return ProjectImporter.Export([ProjectStore.Load().First(x => x.Id == S("id"))]);
            case "project.details":
                var detail = ProjectStore.Load().First(x => x.Id == S("id"));
                var detailRoot = XmlTools.Parse(detail.RawXml).Root!;
                string Detail(string name) => detailRoot.Elements().FirstOrDefault(element =>
                    element.Name.LocalName.Equals(name, StringComparison.OrdinalIgnoreCase))?.Value ?? "";
                return new { project = Project(detail), programPath = Detail("ProgramPath"), updatePath = UpdatePath(detail), profile = new {
                    code = Detail("StandardProjectCode") is { Length: > 0 } detailCode ? detailCode : detail.Name,
                    title = Detail("StandardProjectTitle"), version = Detail("ProjectVersion"),
                    active = Detail("IsActive") is not ("0" or "false" or "False"),
                    winProgramPath = Detail("WinProgramPath"), webProgramPath = Detail("WebProgramPath"),
                    winSourcePath = Detail("WinSourcePath"), webSourcePath = Detail("WebSourcePath"),
                    webHost1 = Detail("WebHost1"), webHost2 = Detail("WebHost2"), updatePath = UpdatePath(detail),
                    server = Detail("servername"), authentication = Detail("Authentication"), user = Detail("User"),
                    database = Detail("df_Database"), defaultUser = Detail("DefaultUser"),
                    listAllData = Detail("ListAllData"), sortOrder = Detail("SortOrder"),
                    homeUpdateCode = Detail("HomeUpdateCode"), notes = Detail("Notes")
                }, metadata = ProjectImporter.Export([detail]) };
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
                    var updated = ApplyProjectInput(previous, true) with { Id = previous.Id };
                    EnsureUniqueProject(all.Where(item => item.Id != previous.Id), updated);
                    ProjectStore.Save(all.Select(x => x.Id == previous.Id ? updated : x).ToList()); return Project(updated);
                } finally { persistenceGate.Release(); }
            case "project.history.clear":
                await persistenceGate.WaitAsync(ct);
                try
                {
                    var historyProjectId = S("id");
                    foreach (var historyProject in ProjectStore.Load().Where(item => historyProjectId.Length == 0 || item.Id == historyProjectId))
                        ProjectFileActions.ClearHistory(historyProject);
                    return true;
                }
                finally { persistenceGate.Release(); }
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
            case "project.history.removeMany":
            case "project.refreshConfig":
            case "project.structure.preview":
            case "project.structure.clear":
                var actionProject = ProjectStore.Load().First(x => x.Id == S("id"));
                await persistenceGate.WaitAsync(ct);
                try
                {
                    if (method == "project.history") return ProjectFileActions.ReadHistory(actionProject);
                    if (method is "project.history.remove" or "project.history.removeMany")
                    {
                        if (method == "project.history.remove") ProjectFileActions.RemoveHistory(actionProject, S("path"));
                        else ProjectFileActions.RemoveHistory(actionProject,
                            p.GetProperty("paths").Deserialize<string[]>(Json) ?? []);
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
            case "project.targets":
                var project = ProjectStore.Load().First(x => x.Id == S("id"));
                var available = new List<TargetInfo>();
                var deferTargets = method == "project.open" && B("deferTargets");
                if (!deferTargets && (!string.IsNullOrWhiteSpace(project.Field("SysConnectionString")) || !string.IsNullOrWhiteSpace(project.Field("WebConfigPath"))))
                    available.AddRange((await ProjectDatabase.ConfiguredTargetsAsync(project).WaitAsync(TimeSpan.FromSeconds(30), ct))
                        .Select((target, index) => Register(target, isProjectSys: index == 1)));
                else if (!deferTargets && !string.IsNullOrWhiteSpace(project.Server) && !string.IsNullOrWhiteSpace(project.Database)) {
                    var legacyConnection = new SqlConnectionStringBuilder { DataSource = project.Server, InitialCatalog = project.Database, IntegratedSecurity = string.IsNullOrWhiteSpace(project.User), UserID = project.User, Password = project.Field("Password"), Encrypt = SqlConnectionEncryptOption.Optional, ConnectTimeout = 12, ApplicationName = "LS Code" };
                    available.Add(Register(new(project.Server, project.Database, legacyConnection.ConnectionString)));
                }
                if (method == "project.targets") return available;
                await persistenceGate.WaitAsync(ct);
                try
                {
                    var state = SettingsStore.Load(); state.ActiveProjectId = project.Id; state.ProjectLastAccess[project.Id] = DateTimeOffset.Now;
                    state.Workspace = project.SourcePath; SettingsStore.Save(state);
                }
                finally { persistenceGate.Release(); }
                return new { project = Project(project), targets = available, targetsDeferred = deferTargets };
            case "workspace.reveal":
                var path = S("path");
                if (!Path.IsPathFullyQualified(path))
                    throw new ArgumentException("Đường dẫn phải là tuyệt đối.");

                var isFile = await Task.Run(() => File.Exists(path), ct);
                var isDirectory = isFile || await Task.Run(() => Directory.Exists(path), ct);
                if (!isDirectory)
                    throw new FileNotFoundException("Không tìm thấy file hoặc thư mục.", path);

                if (isFile)
                    Process.Start(new ProcessStartInfo("explorer.exe", "/select,\"" + path + "\"") { UseShellExecute = true });
                else
                    Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
                return true;
            case "workspace.duplicate":
                return await WorkspaceFiles.DuplicateAsync(S("path"), ct);
            case "sourceCollection.versions":
                return SourceVersions();
            case "sourceCollection.catalog":
                return SourceVersionCatalog.Load();
            case "sourceCollection.default.read":
                return SettingsStore.Load().SourceCollectionVersion;
            case "sourceCollection.default.save":
                var requestedVersion = S("version").Trim();
                await persistenceGate.WaitAsync(ct);
                try
                {
                    var sourceVersion = requestedVersion.Length == 0 ? "" : SourceVersions().FirstOrDefault(item =>
                        item.Version.Equals(requestedVersion, StringComparison.OrdinalIgnoreCase))?.Version
                        ?? throw new ArgumentException("Phiên bản nguồn không tồn tại.");
                    var sourceSettings = SettingsStore.Load();
                    sourceSettings.SourceCollectionVersion = sourceVersion;
                    SettingsStore.Save(sourceSettings);
                    return sourceVersion;
                }
                finally { persistenceGate.Release(); }
            case "sourceCollection.sync":
                var sourceGroup = S("group");
                var discovered = SourceVersionCatalog.Discover(sourceGroup, ct);
                await persistenceGate.WaitAsync(ct);
                try { return SourceVersionCatalog.SaveGroup(sourceGroup, discovered, ct); }
                finally { persistenceGate.Release(); }
            case "sourceCollection.find":
                var sourceRoots = p.GetProperty("roots").Deserialize<string[]>(Json) ?? [];
                var sourceSamples = p.GetProperty("samples").Deserialize<string[]>(Json) ?? [];
                return await Task.Run(() => SourceCollection.Find(sourceRoots, sourceSamples, ct), ct);
            case "sourceCollection.copy":
                var sourceFiles = p.GetProperty("files").Deserialize<SourceCollection.Match[]>(Json) ?? [];
                return await Task.Run(() => SourceCollection.CopyAsync(S("destination"), sourceFiles, ct), ct);
            case "sourceCollection.content":
                var contentFiles = p.GetProperty("files").Deserialize<SourceCollection.Match[]>(Json) ?? [];
                return await SourceCollection.FindContentAsync(contentFiles, S("query"), ct);
            case "project.standard.user":
                return StandardProjectSync.FindFsdUser();
            case "project.standard.sync":
                var standardUser = S("currentUser").Trim();
                var syncRows = await StandardProjectSync.ReadAsync(standardUser, ct);
                if (syncRows.Length == 0 && B("useFsdFallback"))
                {
                    var fsdUser = StandardProjectSync.FindFsdUser();
                    if (fsdUser.Length > 0 && !fsdUser.Equals(standardUser, StringComparison.OrdinalIgnoreCase))
                    {
                        var fsdRows = await StandardProjectSync.ReadAsync(fsdUser, ct);
                        if (fsdRows.Length > 0) (standardUser, syncRows) = (fsdUser, fsdRows);
                    }
                }
                await persistenceGate.WaitAsync(ct);
                try
                {
                    var saved = ProjectStore.Load();
                    var projectId = S("projectId");
                    var selected = projectId.Length > 0
                        ? saved.Find(project => project.Id == projectId) ?? throw new InvalidOperationException("Dự án không còn trong danh sách.")
                        : null;
                    var code = selected?.Field("StandardProjectCode");
                    if (string.IsNullOrWhiteSpace(code)) code = selected?.Name;
                    var matchingRows = selected is null ? syncRows
                        : syncRows.Where(row => row.Code.Trim().Equals(code, StringComparison.OrdinalIgnoreCase)).ToArray();
                    if (selected is not null && matchingRows.Length == 0)
                        throw new InvalidOperationException("Không tìm thấy dự án này trong danh mục chuẩn của bạn.");
                    var plan = selected is null
                        ? StandardProjectSync.Plan(saved, matchingRows, ct, preserveInactive: true)
                        : StandardProjectSync.Plan([selected], matchingRows, ct);
                    if (selected is not null)
                    {
                        if (plan.Summary.Skipped > 0)
                            throw new InvalidOperationException(plan.Summary.Projects.First(item => item.Status == "skipped").Note);
                        saved[saved.FindIndex(project => project.Id == selected.Id)] = plan.Projects.Single();
                        plan = plan with { Projects = saved };
                    }
                    ct.ThrowIfCancellationRequested();
                    if (plan.Summary.Added > 0 || plan.Summary.Updated > 0)
                        ProjectStore.Save(plan.Projects);
                    var standardSettings = SettingsStore.Load();
                    if (syncRows.Length > 0)
                    {
                        standardSettings.StandardCatalogUser = standardUser;
                        SettingsStore.Save(standardSettings);
                    }
                    return new { currentUser = standardUser, summary = new { plan.Summary.Added, plan.Summary.Updated, plan.Summary.Unchanged, plan.Summary.Skipped },
                        projects = plan.Projects.Select(Project).ToArray() };
                }
                finally { persistenceGate.Release(); }
            case "sql.connect":
                var builder = new SqlConnectionStringBuilder { DataSource = S("server"), InitialCatalog = S("database"), IntegratedSecurity = B("integratedSecurity"),
                    UserID = B("integratedSecurity") ? "" : S("user"), Password = B("integratedSecurity") ? "" : S("password"), Encrypt = true,
                    TrustServerCertificate = B("trustCertificate"), ApplicationName = "LS Code", ConnectTimeout = 15 };
                await using (var connection = new SqlConnection(builder.ConnectionString)) await connection.OpenAsync(ct);
                return Register(new(builder.DataSource, builder.InitialCatalog, builder.ConnectionString));
            case "sql.debug.available":
                return new { available = await SqlDebugLauncher.FindAsync(ct) != null };
            case "sql.debug.open":
                return await SqlDebugLauncher.OpenAsync(Target(p), S("sql"), ct);
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
            case "requestTracking.info":
                return new { legacyAvailable = RequestTracking.LegacyConnectionAvailable, defaultUser = SettingsStore.Load().StandardCatalogUser };
            case "requestTracking.list":
            {
                var useLegacy = B("legacy");
                var selected = S("targetId").Length > 0 ? Target(p) : null;
                var requestTarget = RequestTracking.Resolve(selected, useLegacy);
                if (!DateTime.TryParse(S("from"), out var from) || !DateTime.TryParse(S("to"), out var to))
                    throw new ArgumentException("Khoảng ngày QLYC không hợp lệ.");
                return Table(await RequestTracking.ReadAsync(requestTarget, S("user"), from, to, ct));
            }
            case "requestTracking.categories":
            {
                var selected = S("targetId").Length > 0 ? Target(p) : null;
                var requestTarget = RequestTracking.Resolve(selected, B("legacy"));
                return Table(await RequestTracking.ReadCategoriesAsync(requestTarget, S("requestId"), ct));
            }
            case "requestTracking.statistics":
            {
                var useLegacy = B("legacy");
                var selected = S("targetId").Length > 0 ? Target(p) : null;
                var requestTarget = RequestTracking.Resolve(selected, useLegacy);
                var year = I("year", DateTime.Today.Year);
                if (year is < 2000 or > 9999) throw new ArgumentException("Năm thống kê không hợp lệ.");
                return Table(await RequestTracking.StatisticsAsync(requestTarget, year, ct));
            }
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
            case "table.metadata": return new { columns = await TableInspection.MetadataAsync(Target(p), QualifiedTable(), ct) };
            case "table.createScript": return await TableDefinition.ReadAsync(Target(p), QualifiedTable(), ct);
            case "table.insertScript":
                if (!tableSnapshots.TryGetValue(S("scope"), out var snapshot) || snapshot.Id != S("snapshotId"))
                    throw new InvalidOperationException("Dữ liệu đã thay đổi. Hãy đọc lại bảng.");
                return await TableInspection.InsertScriptAsync(snapshot.Target, snapshot.Qualified, snapshot.Data, ct,
                    useTableDateFormats: true);
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
            case "table.script":
                var scriptTarget = RegisteredTarget(p);
                var scriptSchema = Identifier(S("schema", "dbo"));
                var scriptTable = Identifier(S("table"));
                var scriptAction = S("action").Trim().ToLowerInvariant();
                return scriptAction switch
                {
                    "add" => await TableInspection.ColumnScriptAsync(scriptTarget.Connection, scriptSchema, scriptTable,
                        RequiredColumns(ColumnNames()), false, !scriptTarget.IsProjectSys, ct),
                    "alter" => await TableInspection.ColumnScriptAsync(scriptTarget.Connection, scriptSchema, scriptTable,
                        RequiredColumns(ColumnNames()), true, !scriptTarget.IsProjectSys, ct),
                    "drop" => TableInspection.DropScript(scriptSchema, scriptTable, RequiredColumns(ColumnNames())),
                    "create" => await TableDefinition.ReadAsync(scriptTarget.Connection, ProjectDatabase.Quote(scriptSchema) + "." + ProjectDatabase.Quote(scriptTable), ct),
                    "log" => TableInspection.LogColumnScript(scriptSchema, scriptTable),
                    "addmanual" => TableInspection.ManualColumnScript(scriptSchema, scriptTable, Identifier(S("column")), S("dataType"), !p.TryGetProperty("nullable", out var addNull) || addNull.GetBoolean(), false),
                    "altermanual" => TableInspection.ManualColumnScript(scriptSchema, scriptTable, Identifier(S("column")), S("dataType"), !p.TryGetProperty("nullable", out var alterNull) || alterNull.GetBoolean(), true),
                    _ => throw new ArgumentException("Loại script không hợp lệ."),
                };
            case "table.writeBatch":
                var batchTarget = Target(p);
                var batchTable = QualifiedTable();
                var batch = p.GetProperty("rows").Deserialize<TableInspection.GridWrite[]>(Json) ?? [];
                var conflict = await TableInspection.WriteBatchAsync(batchTarget, batchTable, batch, ct);
                if (conflict is null)
                    foreach (var entry in tableSnapshots)
                        if (entry.Value.Target.Server.Equals(batchTarget.Server, StringComparison.OrdinalIgnoreCase) &&
                            entry.Value.Target.Database.Equals(batchTarget.Database, StringComparison.OrdinalIgnoreCase) &&
                            entry.Value.Qualified.Equals(batchTable, StringComparison.Ordinal))
                            tableSnapshots.TryRemove(entry);
                return new { conflict };
            case "table.write":
                var writeTarget = Target(p);
                var writeNames = ColumnNames();
                var writeValues = p.TryGetProperty("values", out var valueElement)
                    ? valueElement.Deserialize<string?[]>(Json) ?? [] : [];
                var keyNames = p.TryGetProperty("keyColumns", out var keyElement)
                    ? (keyElement.Deserialize<string[]>(Json) ?? []).Select(Identifier).ToArray() : [];
                var keyValues = p.TryGetProperty("keyValues", out var keyValueElement)
                    ? keyValueElement.Deserialize<string?[]>(Json) ?? [] : [];
                int affected;
                try
                {
                    affected = await TableInspection.WriteAsync(writeTarget, QualifiedTable(), S("mode"), writeNames,
                        writeValues, keyNames, keyValues, ct);
                }
                catch (SqlException ex) when (ex.Number is 2601 or 2627)
                {
                    throw new InvalidOperationException("Dữ liệu bị trùng khóa chính hoặc khóa UNIQUE.", ex);
                }
                if (S("mode").Equals("update", StringComparison.OrdinalIgnoreCase) && affected == 0)
                    throw new InvalidOperationException("Dòng không còn tồn tại hoặc đã thay đổi. Hãy đọc lại bảng.");
                foreach (var entry in tableSnapshots)
                {
                    var saved = entry.Value;
                    if (saved.Target.Server.Equals(writeTarget.Server, StringComparison.OrdinalIgnoreCase) &&
                        saved.Target.Database.Equals(writeTarget.Database, StringComparison.OrdinalIgnoreCase) &&
                        saved.Qualified.Equals(QualifiedTable(), StringComparison.Ordinal))
                        tableSnapshots.TryRemove(entry);
                }
                return affected;
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
                var readSql = "SELECT " + select + "\nFROM " + qualified
                    + (S("where").Length > 0 ? "\nWHERE " + S("where") : "") + (S("orderBy").Length > 0 ? "\nORDER BY " + S("orderBy") : "");
                var readTarget = Target(p);
                var offset = p.TryGetProperty("offset", out var offsetElement) ? offsetElement.GetInt64() : 0;
                if (offset < 0) throw new ArgumentOutOfRangeException(nameof(offset));
                var (data, hasMore) = await TableInspection.ReadPageAsync(readTarget, qualified, readSql, offset, ct).ConfigureAwait(false);
                if (method == "table.read")
                {
                    if (TableInspection.HasShortenedText(data))
                        throw new InvalidOperationException("Dữ liệu chuỗi vượt giới hạn đọc đầy đủ. Hãy thu hẹp SELECT hoặc WHERE.");
                    return Table(data);
                }
                var preview = TableInspection.Preview(data);
                ct.ThrowIfCancellationRequested();
                var snapshotId = Guid.NewGuid().ToString("N");
                tableSnapshots[S("scope")] = new(snapshotId, readTarget, qualified, data);
                return new { data = preview, snapshotId, hasMore, shortenedText = TableInspection.HasShortenedText(data) };
            case "project.commands": return await ProjectDatabase.CommandsAsync(Target(p), ct, S("appDatabase"), S("kind"), B("refresh"), S("context"));
            case "catalogDescription.preview":
            case "catalogDescription.apply":
                var design = p.GetProperty("design").Deserialize<CatalogFromDescription.Input>(Json)
                    ?? throw new ArgumentException("Thiếu thiết kế danh mục.");
                var designSys = Target(p);
                var designApp = Target(p.GetProperty("app"));
                if (method == "catalogDescription.preview")
                {
                    var prepared = await CatalogFromDescription.PreviewAsync(design, designApp, designSys, ct);
                    return new { prepared.Sql, prepared.Fingerprint,
                        files = prepared.Source.Files.Select(file => new { file.Path, file.Text }).ToArray() };
                }
                var createdFiles = await CatalogFromDescription.ApplyAsync(design, designApp, designSys, S("fingerprint"), ct);
                SqlSchemaCompletion.Clear();
                ProjectDatabase.InvalidateCommands(designSys);
                return createdFiles;
            case "catalogSource.discover":
                return await CatalogSourceGenerator.DiscoverAsync(S("root"), S("template"), ct);
            case "catalogSource.preview":
                var catalogSourceDraft = await CatalogSourceGenerator.PreviewAsync(p.Deserialize<CatalogSourceGenerator.Input>(Json)
                    ?? throw new ArgumentException("Thiếu cấu hình Tạo danh mục."), ct);
                return new { catalogSourceDraft.Fingerprint, catalogSourceDraft.HasMain,
                    files = catalogSourceDraft.Files.Select(file => new { file.Path, file.Text }).ToArray() };
            case "catalogSource.export":
                return await CatalogSourceGenerator.ExportAsync(p.Deserialize<CatalogSourceGenerator.Input>(Json)
                    ?? throw new ArgumentException("Thiếu cấu hình Tạo danh mục."), S("fingerprint"), S("folder"), ct);
            case "project.menu.details": return await MenuEditor.ReadAsync(Target(p), S("wmenuId"), ct);
            case "project.menu.new": return await MenuEditor.NewAsync(Target(p), S("selectedId"), ct);
            case "project.menu.insertScript": return await MenuEditor.InsertScriptAsync(Target(p), S("wmenuId"), ct, B("includeChildren"));
            case "project.menu.resources": return await MenuEditor.ResourcesAsync(S("root"), S("link"), S("sysId"), ct);
            case "project.menu.save":
                var menuValues = p.GetProperty("values").Deserialize<Dictionary<string, string?>>(Json) ?? [];
                var menuTarget = Target(p);
                var savedMenu = await MenuEditor.SaveAsync(menuTarget, S("mode"), S("originalId"), S("version"), menuValues, ct);
                ProjectDatabase.InvalidateCommands(menuTarget);
                return savedMenu;
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
                    }
                    if (names.Count == 0 && !string.IsNullOrWhiteSpace(S("sysId"))) names = [S("sysId")];
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
            case "project.controllerDiagnostics":
                var controllerDiagnosticFiles = p.TryGetProperty("files", out var diagnosticFileValues) &&
                    diagnosticFileValues.ValueKind == JsonValueKind.Array
                    ? diagnosticFileValues.Deserialize<string[]>(Json) ?? []
                    : [];
                return new
                {
                    diagnostics = await ControllerDiagnosticsAsync(S("root"), controllerDiagnosticFiles, OpenBuffers(), ct)
                };
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
                    path => byPath.TryGetValue(path, out var buffer) ? buffer : File.Exists(path) ? File.ReadAllText(path) : null,
                    S("contextPath"), p.TryGetProperty("contextText", out var includeContext) && includeContext.ValueKind == JsonValueKind.String
                        ? includeContext.GetString() : null), ct);
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
            case "profiler.defaults":
                var profilerTarget = Target(p);
                var configuredProfiler = SettingsStore.Load().Profiler18Path;
                return new
                {
                    filter = ProfilerTemplate.Default(profilerTarget.Database).DatabaseFilter,
                    profilerPath = !string.IsNullOrWhiteSpace(configuredProfiler) ? configuredProfiler
                        : File.Exists(SqlTraceSession.DefaultProfilerPath) ? SqlTraceSession.DefaultProfilerPath : "",
                    authentication = "SQL Server · profile"
                };
            case "profiler.start":
                await traceGate.WaitAsync(ct);
                try
                {
                    if (trace != null || traceStream != null || extendedTrace != null) throw new InvalidOperationException("Profiler đang chạy. Dừng phiên hiện tại trước.");
                    var t = Target(p);
                    var engine = S("engine");
                    var dedicatedLogin = engine == "auto" && UsesDedicatedProfilerLogin(t.Server);
                    if (!B("useTargetCredentials") || dedicatedLogin) {
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
                    if (engine == "auto")
                    {
                        var profilerAvailable = !string.IsNullOrWhiteSpace(SettingsStore.Load().Profiler18Path) ||
                            File.Exists(SqlTraceSession.DefaultProfilerPath);
                        var useTargetCredentials = B("useTargetCredentials");
                        var serverMajorVersion = profilerAvailable || dedicatedLogin || !useTargetCredentials
                            ? int.MaxValue
                            : await ServerMajorVersionAsync(t, ct);
                        engine = AutomaticProfilerEngine(profilerAvailable, dedicatedLogin, useTargetCredentials, serverMajorVersion);
                    }
                    if (engine == "sqlTrace")
                    {
                        traceStream = new(t, template.DatabaseFilter ?? t.Database);
                        try { await traceStream.StartAsync(ct); }
                        catch { await traceStream.DropAsync(); traceStream = null; throw; }
                        return new { engine };
                    }
                    if (engine == "extendedEvents")
                    {
                        var prefix = template.DatabaseFilter ?? t.Database;
                        template = template with { DatabaseFilter = prefix.Replace("[", "[[]").Replace("%", "[%]").Replace("_", "[_]") + "%" };
                        extendedTrace = new(t, template); lastExtendedSequence = 0;
                        try { await extendedTrace.StartAsync(ct); } catch { await extendedTrace.DropAsync(); extendedTrace = null; throw; }
                        return S("engine") == "auto" ? new { engine } : (object)true;
                    }
                    trace = new(t, template);
                    try { await trace.StartAsync(ct); } catch { await trace.DropAsync(); trace = null; throw; }
                    return S("engine") == "auto" ? new { engine } : (object)true;
                }
                finally { traceGate.Release(); }
            case "profiler.read":
                if (traceStream is not null) return await traceStream.ReadAsync();
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
        var values = File.Exists(file) ? SnippetImporter.Read(File.ReadAllText(file)) : [];
        var sqlFile = Path.Combine(AppContext.BaseDirectory, "Assets", "sqlsnippet.xml");
        if (!File.Exists(sqlFile)) return values;
        foreach (var item in CompletionSnippets.Read(File.ReadAllText(sqlFile))
            .Where(item => item.Key.StartsWith("ss", StringComparison.OrdinalIgnoreCase)))
        {
            var quickKey = "/" + item.Key;
            var legacyIndex = values.FindIndex(value => value.Key.Equals(item.Key, StringComparison.OrdinalIgnoreCase));
            if (values.Any(value => value.Key.Equals(quickKey, StringComparison.OrdinalIgnoreCase)))
            {
                if (legacyIndex >= 0) values.RemoveAt(legacyIndex);
                continue;
            }
            if (legacyIndex >= 0)
            {
                values[legacyIndex] = values[legacyIndex] with { Key = quickKey };
                continue;
            }
            var expanded = CompletionSnippets.Expand(item.Body);
            values.Add(new Snippet(quickKey, "SQL dựng sẵn", expanded.Text,
                item.Description.Length > 0 ? item.Description : "Mẫu SQL chuyển từ gợi ý ss", expanded.Offset));
        }
        return values;
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
        var code = candidate.Field("StandardProjectCode").Trim() is { Length: > 0 } savedCode ? savedCode : candidate.Name;
        if (existing.Any(item => item.Id == candidate.Id || Same(item.Field("StandardProjectCode") is { Length: > 0 } itemCode ? itemCode : item.Name, code)))
            throw new InvalidOperationException($"Profile dự án '{candidate.Name}' đã tồn tại.");
    }

    private static async Task<ControllerProblem[]> ControllerDiagnosticsAsync(string root, IEnumerable<string> files,
        IReadOnlyDictionary<string, string> buffers, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(root) || !Path.IsPathFullyQualified(root))
            throw new ArgumentException("Thư mục dự án không hợp lệ.");
        root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
        if (!Directory.Exists(root)) throw new DirectoryNotFoundException("Thư mục dự án không tồn tại.");

        bool IsInsideRoot(string path)
        {
            var relative = Path.GetRelativePath(root, path);
            return !Path.IsPathRooted(relative) && relative != ".." &&
                !relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal) &&
                !relative.StartsWith(".." + Path.AltDirectorySeparatorChar, StringComparison.Ordinal);
        }

        string? Normalize(string candidate)
        {
            if (string.IsNullOrWhiteSpace(candidate)) return null;
            try
            {
                var path = Path.GetFullPath(Path.IsPathFullyQualified(candidate)
                    ? candidate
                    : Path.Combine(root, candidate));
                return IsInsideRoot(path) ? path : null;
            }
            catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
            {
                return null;
            }
        }

        var openBuffers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (candidate, text) in buffers)
        {
            token.ThrowIfCancellationRequested();
            if (Normalize(candidate) is { } path) openBuffers[path] = text;
        }

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var controllerFiles = new List<string>();
        foreach (var candidate in files)
        {
            token.ThrowIfCancellationRequested();
            if (Normalize(candidate) is not { } path ||
                Path.GetExtension(path).ToLowerInvariant() is not (".f" or ".xml") || !seen.Add(path)) continue;
            controllerFiles.Add(path);
        }

        var diagnostics = new List<ControllerProblem>();
        foreach (var path in controllerFiles)
        {
            token.ThrowIfCancellationRequested();
            try
            {
                var text = openBuffers.TryGetValue(path, out var buffer)
                    ? buffer
                    : await File.ReadAllTextAsync(path, token).ConfigureAwait(false);
                token.ThrowIfCancellationRequested();
                var inspected = XmlTools.InspectEditor(text, path, openBuffers);
                token.ThrowIfCancellationRequested();
                if (inspected.Diagnostic is { } diagnostic)
                    diagnostics.Add(new(path, diagnostic.Message, diagnostic.Line, diagnostic.Column, "error"));
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                diagnostics.Add(new(path, $"Không đọc được file XML: {ex.Message}", 1, 1, "error"));
            }
        }
        return diagnostics.ToArray();
    }

    private static bool UsesDedicatedProfilerLogin(string server) =>
        server.Equals(@"172.168.5.14\SQL2014", StringComparison.OrdinalIgnoreCase) ||
        server.Equals(@"172.168.5.14\SQL2008", StringComparison.OrdinalIgnoreCase);

    private static string AutomaticProfilerEngine(bool profilerAvailable, bool dedicatedLogin,
        bool useTargetCredentials, int serverMajorVersion) =>
        profilerAvailable ? "ssms18" : dedicatedLogin || !useTargetCredentials || serverMajorVersion < 11
            ? "sqlTrace" : "extendedEvents";

    private static async Task<int> ServerMajorVersionAsync(SqlTarget target, CancellationToken token)
    {
        var configuration = new SqlConnectionStringBuilder(target.ConnectionString)
        {
            InitialCatalog = "master",
            ApplicationName = "LSCode.Profiler",
            ConnectTimeout = 10
        };
        await using var connection = new SqlConnection(configuration.ConnectionString);
        await connection.OpenAsync(token);
        var version = connection.ServerVersion.Split('.', 2)[0];
        return int.TryParse(version, out var major) && major > 0
            ? major
            : throw new InvalidOperationException($"Không xác định được phiên bản SQL Server '{connection.ServerVersion}'.");
    }

    public async Task StopTraceAsync()
    {
        await traceGate.WaitAsync();
        try
        {
            if (trace != null) { await trace.DropAsync(); trace = null; }
            if (traceStream != null) { await traceStream.DropAsync(); traceStream = null; }
            if (extendedTrace != null) { await extendedTrace.DropAsync(); extendedTrace = null; }
        }
        finally { traceGate.Release(); }
    }
    public async ValueTask DisposeAsync() { await StopTraceAsync(); http.Dispose(); traceGate.Dispose(); }
}
