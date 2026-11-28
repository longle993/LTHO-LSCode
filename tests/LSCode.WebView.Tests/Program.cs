using System.Text;
using System.Text.Json;
using LSCode.Core;
using LSCode.Domain;
using LSCode.Infrastructure;
using LSCode.Contracts;
using LSCode.Application;
using LSCode.Shell.Services;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SqlServer.TransactSql.ScriptDom;
using System.Text.RegularExpressions;

var root = Path.Combine(Path.GetTempPath(), "LSCode-WebView-checks-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(root);
Environment.SetEnvironmentVariable("LSCODE_DATA_HOME", root);
var checks = 0;
void Check(bool value, string name) { if (!value) throw new Exception("FAIL " + name); checks++; Console.WriteLine("PASS " + name); }
async Task Reject<T>(Func<Task> work, string name) where T : Exception { try { await work(); throw new Exception("FAIL " + name); } catch (T) { checks++; Console.WriteLine("PASS " + name); } }
Check(SqlRunner.AffectedRowsMessage(4) == "Thực hiện lệnh thành công, đã thay đổi 4 dòng.",
    "SQL affected-row message is a clear Vietnamese success sentence");
var nextCommandMethod = typeof(DeveloperTools).Assembly.GetType("LSCode.Infrastructure.MenuEditor")!
    .GetMethod("NextCommandId", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!;
string NextCommand(HashSet<string> used) => (string)nextCommandMethod.Invoke(null, [used])!;
Check(NextCommand([]) == "AA.01.01", "New command IDs start at AA.01.01 independently of tree IDs");
Check(NextCommand(new(StringComparer.OrdinalIgnoreCase) { "AA.01.01", "AA.01.03", "03.10.01" }) == "AA.01.02",
    "New command IDs reuse the first free slot");
var fullFirstGroup = Enumerable.Range(1, 99).Select(number => $"AA.01.{number:00}").ToHashSet();
Check(NextCommand(fullFirstGroup) == "AA.02.00", "New command IDs carry from 99 to the next group");
var allCommandIds = Enumerable.Range(0, 10000).Select(number => $"AA.{number / 100:00}.{number % 100:00}").ToHashSet();
try { NextCommand(allCommandIds); throw new Exception("FAIL Exhausted command IDs"); }
catch (System.Reflection.TargetInvocationException ex) when (ex.InnerException is InvalidOperationException)
{ Check(true, "Exhausted command IDs fail instead of generating duplicates"); }
var nextSiblingMethod = typeof(DeveloperTools).Assembly.GetType("LSCode.Infrastructure.MenuEditor")!
    .GetMethod("NextSiblingId", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!;
string? NextSibling(string selectedId, HashSet<string> used) => (string?)nextSiblingMethod.Invoke(null, [selectedId, used]);
Check(NextSibling("03.69.43", []) == "03.69.44", "New sibling menu increments the final numeric suffix");
Check(NextSibling("03.69.43", new(StringComparer.OrdinalIgnoreCase) { "03.69.44" }) == "03.69.45",
    "New sibling menu skips an existing next code");
Check(NextSibling("leaf", []) is null, "Legacy non-numeric menu codes retain the branch-code fallback");
var debugScriptMethod = typeof(DeveloperTools).Assembly.GetType("LSCode.Infrastructure.SqlDebugLauncher")!
    .GetMethod("Script", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!;
var debugScript = (string)debugScriptMethod.Invoke(null, ["server\r\nnot-a-command", "db]name", "DECLARE @id int = 7;\r\nSELECT @id;"])!;
Check(debugScript.Contains("USE [db]]name];\r\nGO\r\nDECLARE @id int = 7;") &&
    debugScript.StartsWith("-- LSCode: kết nối tới server  not-a-command") &&
    debugScript.EndsWith("SELECT @id;"), "SSDT handoff quotes the database, keeps script text and confines server metadata to a comment");
try { debugScriptMethod.Invoke(null, ["server", "db", "  "]); throw new Exception("FAIL Empty debug script"); }
catch (System.Reflection.TargetInvocationException ex) when (ex.InnerException is ArgumentException)
{ Check(true, "SSDT handoff rejects an empty SQL script"); }
var profilerEngineMethod = typeof(DeveloperTools).GetMethod("AutomaticProfilerEngine",
    System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!;
string AutomaticProfilerEngine(bool profilerAvailable, bool dedicatedLogin, bool useTargetCredentials, int serverMajorVersion) =>
    (string)profilerEngineMethod.Invoke(null, [profilerAvailable, dedicatedLogin, useTargetCredentials, serverMajorVersion])!;
Check(AutomaticProfilerEngine(false, false, true, 10) == "sqlTrace" &&
    AutomaticProfilerEngine(false, false, true, 11) == "extendedEvents" &&
    AutomaticProfilerEngine(true, false, true, 10) == "ssms18",
    "Profiler auto mode routes SQL Server 2008 to SQL Trace");
var dedicatedProfilerMethod = typeof(DeveloperTools).GetMethod("UsesDedicatedProfilerLogin",
    System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!;
bool UsesDedicatedProfilerLogin(string server) => (bool)dedicatedProfilerMethod.Invoke(null, [server])!;
Check(UsesDedicatedProfilerLogin(@"172.168.5.14\SQL2008") &&
    UsesDedicatedProfilerLogin(@"172.168.5.14\SQL2014") &&
    !UsesDedicatedProfilerLogin(@"172.168.5.14\SQL2022"),
    "SQL2008 and SQL2014 known instances use the dedicated Profiler login");
var traceStreamType = typeof(DeveloperTools).Assembly.GetType("LSCode.Infrastructure.SqlTraceStream")!;
var decodeTraceText = traceStreamType.GetMethod("DecodeText",
    System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!;
Check((string)decodeTraceText.Invoke(null, [Encoding.Unicode.GetBytes("dòng 106\0dòng 107\0")])! == "dòng 106dòng 107",
    "SQL Trace removes embedded UTF-16 NUL separators before clipboard use");
string TraceBatch(string name) => (string)traceStreamType.GetField(name,
    System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!.GetRawConstantValue()!;
var configureTraceSql = TraceBatch("ConfigureSql");
var dropTraceSql = TraceBatch("DropSql");
IList<ParseError> ParseSql2008(string sql)
{
    var parser = new TSql100Parser(false);
    using var reader = new StringReader(sql);
    parser.Parse(reader, out var errors);
    return errors;
}
Check(ParseSql2008(configureTraceSql).Count == 0 && ParseSql2008(dropTraceSql).Count == 0 &&
    !Regex.IsMatch(configureTraceSql + dropTraceSql, @"\bTHROW\b", RegexOptions.IgnoreCase) &&
    configureTraceSql.Contains("VALUES (11), (13)", StringComparison.Ordinal) &&
    configureTraceSql.Contains("SELECT 162, id FROM @errorColumns", StringComparison.Ordinal) &&
    configureTraceSql.Contains("(20), (35)", StringComparison.Ordinal) &&
    Regex.Matches(configureTraceSql, @"\bRAISERROR\b", RegexOptions.IgnoreCase).Count == 3 &&
    Regex.Matches(dropTraceSql, @"\bRAISERROR\b", RegexOptions.IgnoreCase).Count == 2,
    "SQL Trace setup and cleanup batches parse with the SQL Server 2008 grammar");
var startBatchMethod = typeof(ProfilerSession).GetMethod("StartBatchSql",
    System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!;
var extendedStartSql = (string)startBatchMethod.Invoke(null,
    ["LSCode_fixture", "Fixture_A", ProfilerTemplate.Default("Fixture_A")])!;
Check(!Regex.IsMatch(extendedStartSql, @"\bTHROW\b", RegexOptions.IgnoreCase) &&
    extendedStartSql.Contains("sqlserver.rpc_starting", StringComparison.Ordinal) &&
    extendedStartSql.Contains("sqlserver.sql_batch_starting", StringComparison.Ordinal) &&
    extendedStartSql.Contains("sqlserver.error_reported", StringComparison.Ordinal),
    "Extended Events startup captures starting statements and user-facing SQL errors without THROW");
await using (var provider = new ServiceCollection().AddLSCodeInfrastructure().BuildServiceProvider())
    Check(provider.GetRequiredService<IDeveloperTools>() is DeveloperTools, "Profiler resolves through its single DI registration");
checks++;
Console.WriteLine("PASS Closing DI container disposes Profiler once");
await using (var provider = new ServiceCollection().AddLSCodeInfrastructure().BuildServiceProvider())
{
    var tools = provider.GetRequiredService<IDeveloperTools>();
    using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
    using var server = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
    server.Start();
    var port = ((System.Net.IPEndPoint)server.LocalEndpoint).Port;
    var capture = Task.Run(async () =>
    {
        using var socket = await server.AcceptTcpClientAsync(deadline.Token);
        using var stream = socket.GetStream();
        using var reader = new StreamReader(stream, Encoding.ASCII);
        using var writer = new StreamWriter(stream, Encoding.ASCII) { NewLine = "\r\n", AutoFlush = true };
        var transcript = new StringBuilder();
        await writer.WriteLineAsync("220 localhost SMTP fixture");
        while (await reader.ReadLineAsync(deadline.Token) is { } line)
        {
            transcript.AppendLine(line);
            if (line == "DATA")
            {
                await writer.WriteLineAsync("354 End with dot");
                while (await reader.ReadLineAsync(deadline.Token) is { } content && content != ".")
                    transcript.AppendLine(content);
                await writer.WriteLineAsync("250 Accepted");
                return transcript.ToString();
            }
            await writer.WriteLineAsync("250 OK");
        }
        return transcript.ToString();
    });
    var payload = JsonSerializer.SerializeToElement(new
    {
        host = "127.0.0.1", port, security = "none", from = "sender@example.test",
        to = "one@example.test;two@example.test", bcc = "hidden@example.test",
        subject = "Thử SMTP", body = "<p>Thử SMTP</p>", html = true
    });
    var result = await tools.ExecuteAsync("mail.sendTest", payload, deadline.Token);
    var transcript = await capture;
    Check(result is CheckMailResult { Success: true } &&
        transcript.Contains("RCPT TO:<one@example.test>") &&
        transcript.Contains("RCPT TO:<two@example.test>") &&
        transcript.Contains("RCPT TO:<hidden@example.test>") &&
        !transcript.Contains("Bcc:", StringComparison.OrdinalIgnoreCase) &&
        transcript.Contains("Content-Type: text/html", StringComparison.OrdinalIgnoreCase),
        "Check Mail sends HTML with multiple recipients and hides Bcc from headers");
}
var noteProject = "advance-note-project";
var noteItem = new AdvanceNoteItem("request-1", "Kiểm tra phiếu nhập", "Đang xử lý", "Đối chiếu controller",
    "01.01", "Phiếu nhập", "PNTran", "PNTran.aspx", [Path.Combine(root, "PNTran.xml")], DateTimeOffset.UtcNow);
var savedNotes = await AdvanceNoteStore.SaveAsync(noteProject, [noteItem], default);
var loadedNotes = await AdvanceNoteStore.LoadAsync(noteProject, default);
Check(File.Exists(savedNotes.FilePath) && loadedNotes.Items.Length == 1 &&
    loadedNotes.Items[0].MenuId == "01.01" && loadedNotes.Items[0].SourceFiles.Single().EndsWith("PNTran.xml"),
    "Advance Note persists request, ERP menu and source files in project XML");
await Reject<InvalidDataException>(() => AdvanceNoteStore.SaveAsync(noteProject,
    [noteItem with { Title = "" }], default), "Advance Note rejects requests without a title");
var updateProjectRoot = Directory.CreateDirectory(Path.Combine(root, "update-project")).FullName;
var updateSourceFolder = Directory.CreateDirectory(Path.Combine(updateProjectRoot, "App_Data", "Controllers", "Dir")).FullName;
var updateSource = Path.Combine(updateSourceFolder, "PNTran.f");
await File.WriteAllTextAsync(updateSource, "source");
var updateOutput = Path.Combine(root, "generated-update");
var updateItem = noteItem with
{
    Title = "Phiếu nhập / kiểm tra",
    SourceFiles = [updateSource],
    Sql = new Dictionary<string, AdvanceNoteSql>
    {
        ["App"] = new("ALTER PROCEDURE dbo.PNTran AS SELECT 1", "UPDATE dmvt SET status = '1'", "SET NOCOUNT ON"),
        ["Sys"] = new("", "UPDATE command SET status = '1'", "")
    }
};
var generatedUpdate = await AdvanceNoteUpdateGenerator.GenerateAsync(updateProjectRoot, updateOutput, updateItem, default);
var appScript = generatedUpdate.ScriptFiles.Single(path => path.Contains($"{Path.DirectorySeparatorChar}App{Path.DirectorySeparatorChar}"));
var scriptBytes = await File.ReadAllBytesAsync(appScript);
var scriptText = await File.ReadAllTextAsync(appScript, Encoding.Unicode);
Check(generatedUpdate.FileCount == 1 && Path.GetDirectoryName(generatedUpdate.OutputPath) == updateOutput &&
    File.Exists(Path.Combine(generatedUpdate.OutputPath, "App_Data", "Controllers", "Dir", "PNTran.f")) &&
    scriptBytes.AsSpan().StartsWith(Encoding.Unicode.GetPreamble()) &&
    scriptText.IndexOf("-- TOP SCRIPT", StringComparison.Ordinal) < scriptText.IndexOf("-- OBJECT", StringComparison.Ordinal) &&
    scriptText.IndexOf("-- OBJECT", StringComparison.Ordinal) < scriptText.IndexOf("-- DATA", StringComparison.Ordinal),
    "Generate Update copies selected source relatively and writes ordered UTF16 App/Sys SQL scripts");
var generatedAgain = await AdvanceNoteUpdateGenerator.GenerateAsync(updateProjectRoot, updateOutput, updateItem, default);
Check(generatedAgain.OutputPath != generatedUpdate.OutputPath && File.Exists(appScript),
    "Each Generate Update creates a separate package and retains the first package");
await Reject<InvalidOperationException>(() => AdvanceNoteUpdateGenerator.GenerateAsync(updateProjectRoot, updateOutput,
    updateItem with { SourceFiles = [], Sql = null }, default), "Generate Update rejects empty packages");
var workspace = new DocumentWorkspace();
var path = Path.Combine(root, "unicode.xml");
var encoding = new UnicodeEncoding(false, true, true);
var utf8Bom = new UTF8Encoding(true, true);
await File.WriteAllBytesAsync(path, encoding.GetPreamble().Concat(encoding.GetBytes("<root>Tiếng Việt</root>\r\n")).ToArray());
var document = await workspace.OpenAsync(path, default);
Check(document.Bom && document.Encoding == "utf-16" && document.Newline == "CRLF", "Open retains UTF16/BOM/newline");
var saved = await workspace.SaveAsync(document.Id, path, "<?xml version='1.0' encoding='UTF-16'?><root>Đã sửa</root>\r\n", 2, default);
Check(saved.Encoding == "utf-8" && saved.Text.Contains("encoding='utf-8'") &&
    (await File.ReadAllBytesAsync(path)).AsSpan().StartsWith(utf8Bom.GetPreamble()) &&
    await File.ReadAllTextAsync(path, utf8Bom) == saved.Text, "Native XML save writes UTF-8 BOM and synchronizes the encoding declaration");
await Reject<InvalidOperationException>(() => workspace.SaveAsync(document.Id, path, "stale", 1, default), "Reject stale save version");
var copy = Path.Combine(root, "copy.xml");
await workspace.SaveAsync(document.Id, copy, "<root>Bản sao</root>", 4, default);
Check((await File.ReadAllBytesAsync(copy)).AsSpan().StartsWith(utf8Bom.GetPreamble()), "XML Save As writes UTF-8 with BOM");
await File.WriteAllTextAsync(copy, "external");
Check(await workspace.ChangedAsync(document.Id, default), "Detect external modification");
await Reject<LSCode.Core.FileConflictException>(() => workspace.SaveAsync(document.Id, copy, "overwrite", 8, default), "Conflict stops external overwrite");
Check(await File.ReadAllTextAsync(copy) == "external", "Conflicting save leaves disk unchanged");
await workspace.StoreRecoveryAsync(JsonSerializer.SerializeToElement(new[] { new { name = "draft.sql", text = "SELECT N'Việt';" } }), default);
var recovery = (JsonElement)await workspace.RecoverAsync(default);
Check(recovery[0].GetProperty("text").GetString() == "SELECT N'Việt';", "Recovery round-trip");
Check(!Directory.GetFiles(root).Any(p => p.EndsWith(".tmp")), "No temporary files left after recovery");
var renameFolder = Directory.CreateDirectory(Path.Combine(root, "rename-source")).FullName;
var renameSource = Path.Combine(renameFolder, "original.xml");
await File.WriteAllBytesAsync(renameSource, encoding.GetPreamble().Concat(encoding.GetBytes("<root/>")).ToArray());
var renameDocument = await workspace.OpenAsync(renameSource, default);
var renamedFile = await workspace.RenameAsync(renameSource, "changed.xml", default);
var renamedFolder = await workspace.RenameAsync(renameFolder, "rename-target", default);
var renamedPath = Path.Combine(renamedFolder, Path.GetFileName(renamedFile));
Check(!File.Exists(renameSource) && !Directory.Exists(renameFolder) && File.Exists(renamedPath) &&
    !await workspace.ChangedAsync(renameDocument.Id, default), "Rename rebinds an open document through both file and folder moves");
var savedAfterRename = await workspace.SaveAsync(renameDocument.Id, renamedPath, "<root>Dirty buffer</root>", 2, default);
Check(savedAfterRename.Bom && savedAfterRename.Encoding == "utf-8" &&
    (await File.ReadAllTextAsync(renamedPath)).Contains("Dirty buffer"), "Dirty XML document saves to its renamed path in UTF-8 with BOM");
var caseRenamed = await workspace.RenameAsync(renamedPath, "Changed.xml", default);
Check(File.Exists(caseRenamed) && Directory.EnumerateFiles(renamedFolder).Single() == caseRenamed, "Case-only rename changes the on-disk name");
await File.WriteAllTextAsync(Path.Combine(renamedFolder, "occupied.xml"), "keep");
await Reject<IOException>(() => workspace.RenameAsync(caseRenamed, "occupied.xml", default), "Rename refuses to overwrite an existing file");
await Reject<ArgumentException>(() => workspace.RenameAsync(caseRenamed, "../outside.xml", default), "Rename rejects a name containing a path");
Directory.CreateDirectory(Path.Combine(root, "node_modules"));
await File.WriteAllTextAsync(Path.Combine(root, "node_modules", "hidden.sql"), "SELECT hidden");
var hits = (List<LSCode.Core.SearchHit>)await workspace.SearchAsync(root, "unsaved", false, false, new() { [copy] = "unsaved buffer" }, default);
Check(hits.Count == 1 && hits[0].Path == copy, "Search sees unsaved buffer");
Check(!(await workspace.FilesAsync(root, default)).Any(f => f.Contains("node_modules")), "Quick open excludes ignored folders");
var erpFile=Path.Combine(root,"controller.f");await File.WriteAllTextAsync(erpFile,"<root>ERP_NEEDLE</root>");
Check((await workspace.FilesAsync(root,default)).Contains(erpFile),"Quick open includes ERP .f files");
var erpHits=(List<LSCode.Core.SearchHit>)await workspace.SearchAsync(root,"ERP_NEEDLE",false,false,[],default);
Check(erpHits.Count==1&&erpHits[0].Path==erpFile,"Workspace search includes ERP .f content");
var matchByContent = (List<LSCode.Core.SearchHit>)await workspace.SearchAsync(root, "ERP_NEEDLE", false, false,
    new() { [erpFile] = "first\nerp_needle\nERP_NEEDLE", [copy] = "ERP_NEEDLE" }, default, firstPerFile: true);
Check(matchByContent.Count == 2 && matchByContent.Single(hit => hit.Path == erpFile).Line == 2 &&
    matchByContent.Any(hit => hit.Path == copy), "File search returns each matching file once at its first content match");
var readOnlyPath = Path.Combine(root, "readonly.sql"); await File.WriteAllTextAsync(readOnlyPath, "SELECT 1");
var readOnly = await workspace.OpenAsync(readOnlyPath, default); File.SetAttributes(readOnlyPath, FileAttributes.ReadOnly);
try
{
    await Reject<DocumentReadOnlyException>(() => workspace.SaveAsync(readOnly.Id, readOnlyPath, "SELECT 2", 2, default), "Readonly save requires explicit override");
    await workspace.SaveAsync(readOnly.Id, readOnlyPath, "SELECT 2", 2, default, true);
    Check((File.GetAttributes(readOnlyPath) & FileAttributes.ReadOnly) != 0, "Readonly override restores original attribute");
}
finally { File.SetAttributes(readOnlyPath, FileAttributes.Normal); }
var restored = workspace.Restore(document with { Path = null, Text = "Khôi phục" });
var restoredPath = Path.Combine(root, "restored.xml"); await workspace.SaveAsync(restored.Id, restoredPath, restored.Text, 2, default);
Check((await File.ReadAllBytesAsync(restoredPath)).AsSpan().StartsWith(utf8Bom.GetPreamble()), "Recovered XML saves as UTF-8 with BOM");
using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
await Reject<OperationCanceledException>(() => workspace.FilesAsync(root, cancelled.Token), "Workspace scan cancellation");
var sql = CatalogDefinition.Build("dbo", "DanhMuc", true, [new("ma", "nvarchar", "20", Key: true), new("ten", Default: "O'Brien")], [], false, false);
Check(CatalogDefinition.Audit.All(n => sql.Contains("[" + n + "]")), "Catalog includes all four system fields");
Check(sql.Contains("O''Brien") && sql.Contains("PRIMARY KEY ([ma])"), "Catalog escapes default and preserves PK");
Check(CatalogDefinition.Quote("x]; DROP TABLE t--") == "[x]]; DROP TABLE t--]", "SQL identifiers cannot escape brackets");
Check(CatalogDefinition.TypeSql(new("he_so", "numeric", "19,5")) == "numeric(19,5)", "Catalog description decimal convention is accepted by SQL builder");
await Reject<ArgumentException>(() => Task.FromResult(CatalogDefinition.Build("dbo", "t", true, [new("user_id0")], [], false, false)), "System field collision rejected");
await Reject<ArgumentException>(() => Task.FromResult(CatalogDefinition.Build("dbo", "t", true, [new("x", "nvarchar", Identity: true)], [], false, false)), "Invalid identity rejected");
await Reject<ArgumentException>(() => Task.FromResult(CatalogDefinition.TypeSql(new("x", "decimal", "99,2"))), "Invalid precision rejected");
await Reject<ArgumentException>(() => Task.FromResult(CatalogDefinition.Build("dbo", "t", false, [new("x")], ["x"], false, false)), "Existing column collision rejected");
var request = JsonSerializer.Deserialize<Request>("{\"protocolVersion\":1,\"requestId\":\"test\",\"method\":\"document.open\",\"payload\":{}}", Protocol.Json);
Check(request?.ProtocolVersion == 1 && request.Method == "document.open", "Transport uses camelCase DTOs");
Check(ReadOnlyQuery.Validate("SELECT TOP (1000) * FROM [dbo].[t] WHERE name='; UPDATE'").StartsWith("SELECT"), "Read-only SELECT accepts literal punctuation");
await Reject<ArgumentException>(() => Task.FromResult(ReadOnlyQuery.Validate("SELECT * FROM t; DELETE FROM t")), "Table viewer rejects additional write statement");
await Reject<ArgumentException>(() => Task.FromResult(ReadOnlyQuery.Validate("SELECT * INTO copied FROM t")), "Table viewer rejects SELECT INTO");
var viewXml = "<views><view id='Dir'><item value='120,30,70,100,100,130'/><item value='110100: [ma_nv].Label, [ma_nv], [ten]'/></view></views>";
var controllers = Path.Combine(root, "App_Data", "Controllers");
var includeDirectory = Directory.CreateDirectory(Path.Combine(controllers, "Include", "XML")).FullName;
var dirFile = Path.Combine(controllers, "Dir", "EmployeeStatus.xml");
var includeFile = Path.Combine(includeDirectory, "EditLog.ent");
const string includeSource = "<!ENTITY ListWhenBeforeUpdateLog \"exec FastBusiness$App$List$EditLog\">";
await File.WriteAllTextAsync(includeFile, includeSource);
const string xmlWithInclude = """
    <!DOCTYPE dir [<!ENTITY % Log SYSTEM "EditLog">%Log;]>
    <dir><title v="Trạng thái nhân viên"/><command>&ListWhenBeforeUpdateLog;</command></dir>
    """;
var includeLocation = XmlIncludes.Find(dirFile, xmlWithInclude,
    xmlWithInclude.IndexOf("&ListWhenBeforeUpdateLog;", StringComparison.Ordinal) + 1);
Check(XmlTools.InspectEditor(xmlWithInclude, dirFile).Diagnostic is null &&
    XmlControllerPreview.Read(xmlWithInclude, dirFile).Title == "Trạng thái nhân viên" &&
    includeLocation is { Path: var resolvedInclude, Name: "ListWhenBeforeUpdateLog" } && resolvedInclude == includeFile &&
    includeSource.Substring(includeLocation.Start, includeLocation.Length) == "exec FastBusiness$App$List$EditLog",
    "XML diagnostics, preview and F12 resolve the same nested ERP entity through Include/XML");
Check(XmlTools.InspectEditor(xmlWithInclude, dirFile, new Dictionary<string, string>
    { [includeFile] = "<!ENTITY ListWhenBeforeUpdateLog 'SQL đang sửa'>" }).Diagnostic is null &&
    XmlTools.InspectEditor(xmlWithInclude.Replace("&ListWhenBeforeUpdateLog;", "&Missing;"), dirFile).Diagnostic is { Line: 2 },
    "Include validation supports unsaved entity buffers and still reports genuinely undeclared entities");
var gridPreview = XmlControllerPreview.Read("""
    <?xml version="1.0" encoding="utf-8"?>
    <grid xmlns="urn:erp">
      <title v="Cập nhật thông tin lương nhân viên" e="Employee Salary Information Input"/>
      <subTitle v="Cập nhật thông tin lương, tài khoản ngân hàng..."/>
      <toolBar><button id="Edit"><title v="Sửa"/></button><separator/><button id="Find"><title v="Tìm"/></button></toolBar>
      <fields>
        <field name="ma_nv" width="110"><header v="Mã nhân viên"/></field>
        <field name="ten" width="160"><header e="Name"/></field>
        <field name="stt_rec" width="99" hidden="true"/>
      </fields>
      <views><view id="Grid"><item value="ten, [stt_rec], [ma_nv], [ma_nv].Label"/></view></views>
    </grid>
    """);
Check(gridPreview.Title == "Cập nhật thông tin lương nhân viên" &&
    gridPreview.Subtitle == "Cập nhật thông tin lương, tài khoản ngân hàng..." &&
    gridPreview.Toolbar.SequenceEqual(new string?[] { "Sửa", null, "Tìm" }) &&
    gridPreview.Views.Single().Columns.SequenceEqual([
        new GridPreviewColumn("ten", "Name", 160), new GridPreviewColumn("ma_nv", "Mã nhân viên", 110)
    ]), "Grid preview reads localized captions, toolbar order, view column widths and hidden fields");
var dirPreview = XmlControllerPreview.Read("""
    <dir xmlns="urn:erp">
      <title v="Khai báo nhân viên"/><subTitle v="Thông tin nhân viên"/>
      <toolbar><button id="Save"><title v="Lưu"/></button></toolbar>
      <fields>
        <field name="nam"><header v="Năm hiển thị trong bảng"/><label v="Năm"/></field>
        <field name="internal" hidden="true"/>
        <field name="ma_nv"><header v="Mã nhân viên"/></field>
      </fields>
      <views><view id="Dir">
        <item value="120,40,60,320,0"/>
        <item value="110-: [nam].Label, [nam]"/>
        <item value="1-11: [internal].Label, [internal], [ma_nv]"/>
      </view></views>
    </dir>
    """);
var dirLayout = dirPreview.Views.Single().Layout!;
Check(dirPreview is { Kind: "dir", Title: "Khai báo nhân viên", Subtitle: "Thông tin nhân viên" } &&
    dirPreview.Toolbar.SequenceEqual(["Lưu"]) && dirLayout.Widths.SequenceEqual([120, 40, 60, 320, 0]) &&
    dirLayout.Rows[0].SequenceEqual([
        new DirPreviewCell("nam", "Năm", true, 1, 1), new DirPreviewCell("nam", "Năm", false, 2, 2)
    ]) && dirLayout.Rows[1].SequenceEqual([new DirPreviewCell("ma_nv", "Mã nhân viên", false, 4, 1)]),
    "Dir preview honors label cells, zero spans, skipped columns and hidden-field gaps");
var detailGridDirectory = Directory.CreateDirectory(Path.Combine(controllers, "Grid")).FullName;
var detailGridFile = Path.Combine(detailGridDirectory, "SalaryDetail.f");
await File.WriteAllTextAsync(detailGridFile, """
    <grid><fields><field name="rate" width="90"><header v="Mức lương"/></field></fields>
    <views><view id="Grid"><field name="rate"/></view></views></grid>
    """);
const string dirWithDetail = """
    <dir>
      <fields>
        <field name="method" categoryIndex="1"><footer v="1 - Theo thời gian"/></field>
        <field name="salary" external="true" categoryIndex="3"><label v="Chi tiết"/>
          <items style="Grid" controller="SalaryDetail" row="2"/></field>
      </fields>
      <views><view id="Dir">
        <item value="1: [salary]"/>
        <item value="11: [method], [method].Description"/>
        <categories>
          <category index="1" columns="100,150"><header v="Thông tin"/></category>
          <category index="3" columns="300,400"><header v="Chi tiết"/></category>
        </categories>
      </view></views>
    </dir>
    """;
var detailCategories = XmlControllerPreview.Read(dirWithDetail, dirFile).Views.Single().Layout!.Categories;
Check(XmlTools.InspectEditor(dirWithDetail).Warnings.Count == 0 &&
    detailCategories.Select(category => category.Label).SequenceEqual(["Thông tin", "Chi tiết"]) &&
    detailCategories[0].Rows[0][1].Description == "1 - Theo thời gian" &&
    detailCategories[1].Widths.SequenceEqual([300, 400]) &&
    detailCategories[1].Rows[0][0] is { Span: 2, Detail.Columns: [{ Label: "Mức lương", Width: 90 }] },
    "Dir preview excludes descriptions from field counts, uses footers, and renders Grid detail in view category order");
var xmlDiagnostics = XmlTools.InspectEditor("\uFEFF" + """
    <?xml version="1.0" encoding="utf-8"?>
    <!DOCTYPE dir [<!ENTITY example "ERP">]>
    <dir title="&example;">
      <fields>
        <field name="ma_nv"/>
        <field name="MA_NV"/>
        <field name="missing"/>
      </fields>
      <views>
        <view id="Dir"><item value="111: [ma_nv].Label, [ma_nv], [ma_nv]"/></view>
        <view id="dir"><item value="1: [ma_nv]"/></view>
      </views>
    </dir>
    """);
Check(xmlDiagnostics.Diagnostic is null && xmlDiagnostics.Warnings.Select(w => w.Line).Order().SequenceEqual([6, 7, 10, 11]),
    "XML editor accepts BOM, declaration and DTD and locates missing or duplicate ERP fields and view IDs");
Check(XmlTools.InspectEditor("<?xml version=\"1.0\" encoding=\"utf-8\"?><dir><fields><field name='x'/></fields><views><view id='a'><item value='11: [x].Label, [x]'/></view><view id='b'><item value='1: [x]'/></view></views></dir>") is { Diagnostic: null, Warnings.Count: 0 },
    "Labels and references shared between different views are valid");
Check(XmlTools.InspectEditor("<?xml version=\"1.0\" encoding=\"utf-8\"?><dir></grid>").Diagnostic?.Line == 1,
    "XML editor still reports malformed markup");
var dashLayout = """
    <dir>
      <fields><field name="thang"/><field name="nam"/><field name="ten_bp"/></fields>
      <views><view id="Dir">
        <item value="120, 40, 60, 320, 0"/>
        <item value="11--: [thang].Label, [thang]"/>
        <item value="110-: [nam].Label, [nam]"/>
        <item value="1-: [ten_bp%l], [extra]"/>
        <item value="11-: [extra2]"/>
        <item value="1-: [decorative].Label, [decorative].Label"/>
        <item value="11-: [heading].Label"/>
      </view></views>
      <script><![CDATA[g.getValue('th');]]></script>
    </dir>
    """;
var dashWarnings = XmlTools.InspectEditor(dashLayout).Warnings;
Check(dashWarnings.Count == 2 && dashWarnings.All(w => w.Message.StartsWith("Layout")),
    "Dash masks resolve, both field slot mismatches are reported, and labels alone do not cause warnings");
Check(XmlViewWidths.Find(dashLayout, dashLayout.LastIndexOf("[thang]") + 2)?.Width == 40 &&
    XmlViewWidths.Find(dashLayout, dashLayout.LastIndexOf("[nam]") + 2)?.Width == 100,
    "Dash layout hover widths stop at skipped columns and retain zero spans");
var jsCaret = dashLayout.IndexOf("g.getValue('th", StringComparison.Ordinal) + "g.getValue('th".Length;
Check(XmlTools.ScriptFields(dashLayout, dashLayout[..jsCaret]).Select(suggestion => suggestion.Text).SequenceEqual(["thang"]),
    "A quoted g. argument suggests matching fields from views");
Check(XmlViewWidths.Find(viewXml, viewXml.IndexOf("[ma_nv].Label")+5)?.Width == 120, "View label uses first grid width");
Check(XmlViewWidths.Find(viewXml, viewXml.IndexOf(", [ma_nv]")+5)?.Width == 100, "View field spans 30 + 70");
Check(XmlViewWidths.Find(viewXml, viewXml.IndexOf("[ten]")+2)?.Width == 330, "View final field spans trailing zero columns");
Check(XmlViewWidths.Find("", 0) == null && XmlViewWidths.Find("<view id='x'><item value='10: [x]'/></view>", 30) == null, "View incomplete input is safe");
Check(XmlViewWidths.Find("<!--"+viewXml+"-->", viewXml.IndexOf("[ten]")+6) == null, "View ignores XML comments");
var multipleViews="<views><view id='a'><item value='20,40'/><item value='10: [x]'/></view><view id='b'><item value='50,70'/><item value='10: [x]'/></view></views>";
Check(XmlViewWidths.Find(multipleViews,multipleViews.LastIndexOf("[x]")+1)?.Width==120,"View widths use current view rather than previous view");
await using (var projectTools = new DeveloperTools())
{
    Task<object?> CallProject(string method, object payload) =>
        projectTools.ExecuteAsync(method, JsonSerializer.SerializeToElement(payload, Protocol.Json), default);
    foreach (var (markup, language, hint) in new[]
    {
        ("<dir><field type='Dec¦oy' /></dir>", "xml", "Decimal"),
        ("<dir><loo¦></dir>", "xml", "<lookup"),
        ("<dir><script><![CDATA[f.getI¦]]></script></dir>", "javascript", "getItem"),
        ("<dir><response><![CDATA[select @@prime$partition$c¦]]></response></dir>", "sql", "@@prime$partition$current"),
        ("<dir><css>aside { background-c¦olor: #333; }</css></dir>", "css", "background-color"),
    })
    {
        var offset = markup.IndexOf('¦');
        var result = JsonSerializer.SerializeToElement(await CallProject("xml.complete", new { text = markup.Remove(offset, 1), offset }), Protocol.Json);
        var item = result.GetProperty("suggestions").EnumerateArray().FirstOrDefault(entry => entry.GetProperty("text").GetString() == hint);
        Check(result.GetProperty("language").GetString() == language && item.ValueKind == JsonValueKind.Object &&
            result.GetProperty("start").GetInt32() < offset && result.GetProperty("end").GetInt32() >= offset,
            $"FAST XML bridge returns {language} hint {hint} and its source range");
        if (language == "css") Check(item.GetProperty("insertText").GetString() == hint, "Existing CSS value survives bridge completion");
    }
    var duplicatePaths = await Task.WhenAll(
        CallProject("workspace.duplicate", new { path }),
        CallProject("workspace.duplicate", new { path }));
    Check(duplicatePaths.Cast<string>().Distinct(StringComparer.OrdinalIgnoreCase).Count() == 2 &&
        duplicatePaths.Cast<string>().All(file => File.ReadAllBytes(file).SequenceEqual(File.ReadAllBytes(path))),
        "Concurrent file duplication creates separate copies preserving every byte and BOM");
    Check(duplicatePaths.Cast<string>().All(file => Path.GetDirectoryName(file) == Path.GetDirectoryName(path) &&
        Path.GetExtension(file) == Path.GetExtension(path)), "File copies stay beside the source with the same extension");
    var deleteFixture = (string)duplicatePaths[0]!;
    Check(await CallProject("workspace.deleteFile", new { path = deleteFixture }) is true &&
        !File.Exists(deleteFixture) && File.Exists(path) && File.Exists((string)duplicatePaths[1]!),
        "Deleting a file removes only the requested file");
    await Reject<FileNotFoundException>(() => CallProject("workspace.deleteFile", new { path = root }),
        "File deletion rejects directories");
    var resourceRoot = Directory.CreateDirectory(Path.Combine(root, "menu-resources")).FullName;
    var resourceMain = Directory.CreateDirectory(Path.Combine(resourceRoot, "Main")).FullName;
    var resourceControllers = Path.Combine(resourceRoot, "App_Data", "Controllers");
    Directory.CreateDirectory(Path.Combine(resourceControllers, "Dir"));
    Directory.CreateDirectory(Path.Combine(resourceControllers, "Grid"));
    Directory.CreateDirectory(Path.Combine(resourceControllers, "Report"));
    await File.WriteAllTextAsync(Path.Combine(resourceMain, "fixture.aspx"), "<FastBusiness:ReportExtender Controller='Fixture' />");
    await File.WriteAllTextAsync(Path.Combine(resourceControllers, "Dir", "Fixture.xml"),
        "<controller tableName='d46$000000' detailTable='dbo.d47$000000'><items style='Grid' controller='FixtureDetail'/><!-- <table>Ignored</table> --></controller>");
    await File.WriteAllTextAsync(Path.Combine(resourceControllers, "Dir", "FixtureDetail.f"),
        "<controller><table>[dbo].[FixtureLines]</table></controller>");
    await File.WriteAllTextAsync(Path.Combine(resourceControllers, "Grid", "Fixture.f"), "<controller/>");
    var reportFixture = Path.Combine(resourceControllers, "Report", "Fixture.xml");
    await File.WriteAllTextAsync(reportFixture, "<controller/>");
    var menuResources = JsonSerializer.SerializeToElement(await CallProject("project.menu.resources", new { root = resourceRoot, link = "fixture.aspx" }), Protocol.Json);
    var resourceStates = menuResources.GetProperty("resources").EnumerateArray().ToDictionary(
        item => item.GetProperty("kind").GetString()!, item => item.GetProperty("status").GetString());
    Check(menuResources.GetProperty("controllers")[0].GetString() == "Fixture" &&
        menuResources.GetProperty("tables").EnumerateArray().Select(table => table.GetString()).ToHashSet()
            .SetEquals(["d46$000000", "dbo.d47$000000", "[dbo].[FixtureLines]"]) &&
        resourceStates["Dir"] == "multiple" && resourceStates["Grid"] == "found" &&
        resourceStates["Filter"] == "missing" && resourceStates["Main"] == "found" && resourceStates["Report"] == "found",
        "Menu editor resolves master and detail Dir tables, including a linked controller, and missing file groups");
    var dirResources = menuResources.GetProperty("resources").EnumerateArray()
        .Single(item => item.GetProperty("kind").GetString() == "Dir").GetProperty("files");
    var dirResourceFile = dirResources.EnumerateArray()
        .Single(file => Path.GetFileName(file.GetProperty("path").GetString()) == "Fixture.xml");
    Check(dirResourceFile.GetProperty("tables").EnumerateArray().Select(table => table.GetString()).ToHashSet()
        .SetEquals(["d46$000000", "dbo.d47$000000"]), "Menu editor returns the table declarations for each Dir file");
    File.Delete(reportFixture);
    Directory.CreateDirectory(Path.Combine(resourceControllers, "Filter"));
    await File.WriteAllTextAsync(Path.Combine(resourceControllers, "Filter", "Fixture.xml"),
        "<filter><title v='Mẫu' e='Sample'/><fields><field name='old_code'/></fields></filter>", new UTF8Encoding(true));
    await File.AppendAllTextAsync(Path.Combine(resourceMain, "fixture.aspx"),
        """<script>window.loadForm('FixtureMainForm');</script>""");
    await File.WriteAllTextAsync(Path.Combine(resourceControllers, "Grid", "Fixture.f"),
        """
        <grid><script><text><![CDATA[
          g.showForm('FixtureRun');
          g.showForm("fixturedel", args);
          g.showForm(formVariable);
          g.showForm('Ignored' + suffix);
          // g.showForm('Ignored');
          /* g.openForm('Ignored'); */
          const label = "g.showForm('Ignored')";
        ]]></text></script><!-- g.showForm('Ignored'); --></grid>
        """);
    var forms = new[] { "FixtureRun.f", "FixtureRun.xml", "FixtureDel.f", "FixtureDel.xml", "FixtureNested.f", "FixtureMainForm.xml" };
    foreach (var name in forms.Append("Ignored.xml"))
    {
        var content = name == "FixtureRun.xml"
            ? "<filter><script><![CDATA[f.openForm('FixtureNested'); f.showForm('Fixture');]]></script></filter>"
            : "<filter/>";
        await File.WriteAllTextAsync(Path.Combine(resourceControllers, "Filter", name), content);
    }
    var formPayload = new { root = resourceRoot, link = "fixture.aspx" };
    var formTree = JsonSerializer.SerializeToElement(await CallProject("project.controller", formPayload), Protocol.Json);
    var treePaths = formTree.GetProperty("files").EnumerateArray().Select(value => value.GetString()).ToArray();
    var treeNames = treePaths.Select(Path.GetFileName).ToArray();
    Check(forms.All(name => treeNames.Contains(name)) && treeNames.Contains("FixtureDetail.f") &&
        !treeNames.Contains("Ignored.xml") && treePaths.Length == treePaths.Distinct(StringComparer.OrdinalIgnoreCase).Count(),
        "Controller tree follows literal form calls from Main and nested .f/.xml sources, preserving grids and excluding comments");
    var formResources = JsonSerializer.SerializeToElement(await CallProject("project.menu.resources", formPayload), Protocol.Json);
    var filterPaths = formResources.GetProperty("resources").EnumerateArray()
        .Single(item => item.GetProperty("kind").GetString() == "Filter").GetProperty("paths").EnumerateArray()
        .Select(value => Path.GetFileName(value.GetString())).ToHashSet();
    Check(filterPaths.SetEquals(forms.Append("Fixture.xml")), "Menu editor lists nested form resources in their controller folders");
    var createList = new
    {
        root = resourceRoot, template = "fixture.aspx", controller = "CatalogDemo", titleVi = "Danh mục A & B", titleEn = "Catalog demo",
        tables = new Dictionary<string, string> { ["d46$000000"] = "demo_master", ["dbo.d47$000000"] = "dbo.demo_detail" },
        fields = new Dictionary<string, string> { ["old_code"] = "new_code" }
    };
    var listDraft = JsonSerializer.SerializeToElement(await CallProject("catalogSource.preview", createList), Protocol.Json);
    var listFiles = listDraft.GetProperty("files").EnumerateArray().ToDictionary(
        entry => entry.GetProperty("path").GetString()!, entry => entry.GetProperty("text").GetString()!);
    string ControllerPath(string kind, string name) => Path.Combine("App_Data", "Controllers", kind, name);
    Check(listFiles.Count == 5 &&
        listFiles[Path.Combine("Main", "CatalogDemo.aspx")].Contains("Controller='CatalogDemo'") &&
        listFiles[ControllerPath("Dir", "CatalogDemo.xml")].Contains("tableName='demo_master' detailTable='dbo.demo_detail'") &&
        listFiles[ControllerPath("Dir", "CatalogDemo.xml")].Contains("controller='CatalogDemo_FixtureDetail'") &&
        listFiles.ContainsKey(ControllerPath("Dir", "CatalogDemo_FixtureDetail.f")) &&
        listFiles[ControllerPath("Filter", "CatalogDemo.xml")].Contains("Danh mục A &amp; B") &&
        listFiles[ControllerPath("Filter", "CatalogDemo.xml")].Contains("name='new_code'"),
        "Create catalog previews renamed Main, master/detail controllers, table mappings and escaped labels");
    var exportParent = Directory.CreateDirectory(Path.Combine(root, "catalog-output")).FullName;
    var listExport = new
    {
        createList.root, createList.template, createList.controller, createList.titleVi, createList.titleEn,
        createList.tables, createList.fields, folder = exportParent,
        fingerprint = listDraft.GetProperty("fingerprint").GetString()
    };
    await CallProject("catalogSource.export", listExport);
    var exportedList = Path.Combine(exportParent, "CatalogDemo");
    var exportedFilter = await File.ReadAllBytesAsync(Path.Combine(exportedList, ControllerPath("Filter", "CatalogDemo.xml")));
    Check(exportedFilter.AsSpan().StartsWith(new UTF8Encoding(true).GetPreamble()) &&
        !File.Exists(Path.Combine(resourceMain, "CatalogDemo.aspx")) &&
        File.Exists(Path.Combine(exportedList, Path.Combine("Main", "CatalogDemo.aspx"))),
        "Create catalog exports a separate package and preserves template BOM");
    await Reject<IOException>(() => CallProject("catalogSource.export", listExport), "Create catalog refuses to replace an existing package");
    await File.AppendAllTextAsync(Path.Combine(resourceMain, "fixture.aspx"), "<!-- Mẫu đã sửa -->");
    await Reject<InvalidOperationException>(() => CallProject("catalogSource.export", listExport), "Create catalog rejects a stale source preview");
    var modernRoot = Directory.CreateDirectory(Path.Combine(root, "metadata-menu-resources")).FullName;
    var modernMetadata = Directory.CreateDirectory(Path.Combine(modernRoot, "AppData")).FullName;
    var modernIncludes = Directory.CreateDirectory(Path.Combine(modernMetadata, "Metadata")).FullName;
    var modernDir = Directory.CreateDirectory(Path.Combine(modernMetadata, "Controllers", "Dir")).FullName;
    await File.WriteAllTextAsync(Path.Combine(modernMetadata, "PageMetadata.xml"),
        "<!DOCTYPE pages [<!ENTITY Page.001 SYSTEM 'Metadata/Page.001.xml'>]><pages>&Page.001;</pages>");
    await File.WriteAllTextAsync(Path.Combine(modernIncludes, "Page.001.xml"),
        "<page id='invt' v='Danh mục hàng hóa, vật tư' controller='Item'/><page id='other' controller='Other'/><page id='arkh' controller='Customer'/>");
    await File.WriteAllTextAsync(Path.Combine(modernDir, "Item.f"), "<controller tableName='item$000000'/>");
    await File.WriteAllTextAsync(Path.Combine(modernDir, "Other.f"), "<controller/>");
    await File.WriteAllTextAsync(Path.Combine(modernDir, "Customer.f"), "<controller/>");
    var modernResources = JsonSerializer.SerializeToElement(await CallProject("project.menu.resources",
        new { root = modernRoot, link = "missing.aspx", sysId = "invt" }), Protocol.Json);
    Check(modernResources.GetProperty("controllers")[0].GetString() == "Item" &&
        modernResources.GetProperty("tables")[0].GetString() == "item$000000" &&
        modernResources.GetProperty("resources").EnumerateArray().Any(item =>
            item.GetProperty("kind").GetString() == "Dir" && item.GetProperty("status").GetString() == "found"),
        "Menu resources resolve sysid from PageMetadata without Main");
    var modernFiles = JsonSerializer.SerializeToElement(await CallProject("project.controller",
        new { root = modernRoot, link = "missing.aspx", sysId = "invt" }), Protocol.Json);
    Check(modernFiles.GetProperty("files").EnumerateArray().Select(item => item.GetString())
        .SequenceEqual([Path.Combine(modernDir, "Item.f")]),
        "Controller explorer loads PageMetadata controller files without Main or ASPX");
    var diagnosticRoot = Directory.CreateDirectory(Path.Combine(root, "controller-diagnostics")).FullName;
    var diagnosticDirectory = Directory.CreateDirectory(Path.Combine(diagnosticRoot, "App_Data", "Controllers", "Dir")).FullName;
    var invalidController = Path.Combine(diagnosticDirectory, "Invalid.xml");
    var bufferedController = Path.Combine(diagnosticDirectory, "Buffered.f");
    var validController = Path.Combine(diagnosticDirectory, "Valid.xml");
    var missingController = Path.Combine(diagnosticDirectory, "Missing.xml");
    var ignoredText = Path.Combine(diagnosticDirectory, "Ignored.txt");
    var outsideController = Path.Combine(root, "OutsideDiagnostics.xml");
    await File.WriteAllTextAsync(invalidController, "<dir>\n  &MissingControllerEntity;\n</dir>");
    await File.WriteAllTextAsync(bufferedController, "<dir>&DiskEntityIsMissing;</dir>");
    await File.WriteAllTextAsync(validController, "<dir/>");
    await File.WriteAllTextAsync(ignoredText, "<dir>&IgnoredTextEntity;</dir>");
    await File.WriteAllTextAsync(outsideController, "<dir>&OutsideEntity;</dir>");
    var diagnosticPayload = new
    {
        root = diagnosticRoot,
        files = new[] { missingController, invalidController, bufferedController, validController, ignoredText, outsideController },
        buffers = new Dictionary<string, string> { [bufferedController] = "<dir/>" }
    };
    var controllerDiagnostics = JsonSerializer.SerializeToElement(
        await CallProject("project.controllerDiagnostics", diagnosticPayload), Protocol.Json);
    var controllerProblems = controllerDiagnostics.GetProperty("diagnostics").EnumerateArray().ToArray();
    var invalidProblem = controllerProblems.Single(problem =>
        problem.GetProperty("path").GetString()!.Equals(invalidController, StringComparison.OrdinalIgnoreCase));
    Check(controllerProblems.Length == 2 &&
        controllerProblems.Any(problem => problem.GetProperty("path").GetString()!
            .Equals(missingController, StringComparison.OrdinalIgnoreCase)) &&
        invalidProblem.GetProperty("message").GetString()!.Contains("MissingControllerEntity", StringComparison.Ordinal) &&
        invalidProblem.GetProperty("line").GetInt32() == 2 && invalidProblem.GetProperty("column").GetInt32() > 0 &&
        invalidProblem.GetProperty("severity").GetString() == "error",
        "Controller diagnostics scan only project XML/F files, prefer buffers, and continue after a file read error");
    using (var diagnosticCancellation = new CancellationTokenSource())
    {
        diagnosticCancellation.Cancel();
        await Reject<OperationCanceledException>(async () =>
            await projectTools.ExecuteAsync("project.controllerDiagnostics",
                JsonSerializer.SerializeToElement(diagnosticPayload, Protocol.Json), diagnosticCancellation.Token),
            "Controller diagnostics honor cancellation");
    }
    Directory.CreateDirectory(Path.Combine(modernRoot, "Main"));
    var mobileFiles = JsonSerializer.SerializeToElement(await CallProject("project.controller",
        new { root = modernRoot, link = "", sysId = "Customer" }), Protocol.Json);
    Check(mobileFiles.GetProperty("files").EnumerateArray().Select(item => item.GetString())
        .SequenceEqual([Path.Combine(modernDir, "Customer.f")]),
        "Mobile controller explorer falls back to dvcommand sysid when a legacy Main folder exists");
    Check(typeof(DeveloperTools).Assembly.GetManifestResourceNames()
        .Contains("LSCode.Infrastructure.StandardCatalogConnection"),
        "Standard catalog connection ships inside LSCode without an FSD Studio source checkout");
    var standardFolder = Directory.CreateDirectory(Path.Combine(root, "standard-project")).FullName;
    var oldProfile = ProjectImporter.Create("ABC", "old-server", "old-sys", standardFolder);
    var oldXml = LSCode.Core.XmlTools.Parse(oldProfile.RawXml).Root!;
    oldXml.SetElementValue("ProgramPath", "https://example.test/custom");
    oldXml.SetElementValue("UpdatePath", @"D:\PrivateUpdates");
    oldProfile = oldProfile with { RawXml = oldXml.ToString(System.Xml.Linq.SaveOptions.DisableFormatting) };
    var standardRows = new[] {
        new StandardProjectRow("ABC", "Dự án ABC", "new-server", "new-sys", standardFolder, "", "", "", @"D:\StandardUpdates",
            new Dictionary<string, string> { ["web_host1"] = "https://abc.test", ["ghi_chu"] = "Nguồn chuẩn", ["status"] = "1", ["s9"] = "custom-source" }),
        new StandardProjectRow("ABC", "Trùng", "new-server", "new-sys", standardFolder, "", "", "", ""),
        new StandardProjectRow("XYZ", "Thiếu nguồn", "new-server", "new-sys", "", "", "", "", "")
    };
    var standardPlan = StandardProjectSync.Plan([oldProfile], standardRows);
    Check(standardPlan.Summary.Updated == 1 && standardPlan.Summary.Skipped == 2 &&
        standardPlan.Projects.Count == 1 && standardPlan.Projects[0].Id == oldProfile.Id &&
        standardPlan.Projects[0].SourcePath == standardFolder &&
        standardPlan.Projects[0].Field("ProgramPath") == "https://abc.test" &&
        standardPlan.Projects[0].Field("UpdatePath") == @"D:\StandardUpdates" &&
        standardPlan.Projects[0].Field("Notes") == "Nguồn chuẩn" &&
        standardPlan.Projects[0].Field("Fsd_s9") == "custom-source",
        "Bulk sync matches ma_da, applies authoritative fields and retains all source metadata");
    var newStandardFolder = Directory.CreateDirectory(Path.Combine(root, "standard-new")).FullName;
    var newPlan = StandardProjectSync.Plan([], [new StandardProjectRow("NEW", "Dự án mới", ".", "NEW_S",
        newStandardFolder, "", "", "", @"D:\StandardUpdates")]);
    var changedRow = standardRows[0] with { Title = "Danh mục đã sửa" };
    var syncedProfile = standardPlan.Projects.Single();
    var initialSync = StandardProjectSync.Plan([syncedProfile], [changedRow], preserveInactive: true);
    Check(initialSync.Summary.Updated == 1 && initialSync.Projects.Single().Id == syncedProfile.Id &&
        initialSync.Projects.Single().Field("StandardProjectTitle") == changedRow.Title,
        "Bulk sync refreshes existing active profile from the standard catalog");
    var inactiveXml = LSCode.Core.XmlTools.Parse(syncedProfile.RawXml).Root!;
    inactiveXml.SetElementValue("IsActive", "0");
    var inactive = syncedProfile with { RawXml = inactiveXml.ToString(System.Xml.Linq.SaveOptions.DisableFormatting) };
    var inactiveSync = StandardProjectSync.Plan([inactive], [changedRow], preserveInactive: true);
    Check(inactiveSync.Summary.Unchanged == 1 && inactiveSync.Projects.Single() == inactive,
        "Bulk sync preserves existing inactive profile");
    var fsdConfig = Path.Combine(root, "fsd-fixture.db");
    using (var fsdConnection = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={fsdConfig}"))
    {
        fsdConnection.Open();
        using var fsdCommand = fsdConnection.CreateCommand();
        fsdCommand.CommandText = """
            CREATE TABLE options (id TEXT, val TEXT, ma_phan_he TEXT);
            INSERT INTO options VALUES ('CurrUser', 'ignored', 'XX'), (' curruser ', ' FixtureUser ', 'RE');
            """;
        fsdCommand.ExecuteNonQuery();
    }
    Check(StandardProjectSync.ReadFsdUser(fsdConfig) == "FixtureUser",
        "FSD current user is read from the RE options in its SQLite config");
    var explicitSync = StandardProjectSync.Plan([syncedProfile], [changedRow]);
    Check(explicitSync.Summary.Updated == 1 && explicitSync.Projects.Single().Id == syncedProfile.Id &&
        explicitSync.Projects.Single().Field("StandardProjectTitle") == changedRow.Title,
        "Explicit sync refreshes the selected existing profile");
    Check(newPlan.Summary.Added == 1 && newPlan.Projects[0].Field("StandardProjectCode") == "NEW" &&
        newPlan.Projects[0].Field("WebConfigPath") == Path.Combine(newStandardFolder, "web.config") &&
        string.IsNullOrEmpty(newPlan.Projects[0].Field("AppConnectionString")) &&
        newPlan.Projects[0].Field("UpdatePath") == @"D:\StandardUpdates",
        "Bulk sync defers web.config access until project open and imports update path");
    var customerFiles = JsonSerializer.SerializeToElement(await CallProject("project.controller",
        new { root = modernRoot, link = "arkh.aspx", sysId = "Customer" }), Protocol.Json);
    Check(customerFiles.GetProperty("files").EnumerateArray().Select(item => item.GetString())
        .SequenceEqual([Path.Combine(modernDir, "Customer.f")]),
        "Controller explorer maps menu link arkh.aspx to page id arkh and Customer files");
    var configPath = Path.Combine(root, "erp", "web.config");
    Directory.CreateDirectory(Path.GetDirectoryName(configPath)!);
    Task WriteConfig(string app, string sys) => File.WriteAllTextAsync(configPath, $"""
        <configuration><connectionStrings>
          <add name="appConnectionString" connectionString="Server=.;Database={app};Integrated Security=True" />
          <add name="sysConnectionString" connectionString="Server=.;Database={sys};Integrated Security=True" />
        </connectionStrings></configuration>
        """);
    await WriteConfig("Saved_App", "Saved_Sys");
    var syncedSeed = ProjectImporter.Create("ERP startup fixture", ".", "Saved_Sys", Path.GetDirectoryName(configPath)!);
    var syncedXml = LSCode.Core.XmlTools.Parse(syncedSeed.RawXml).Root!;
    syncedXml.SetElementValue("WebConfigPath", configPath);
    syncedXml.SetElementValue("AppConnectionString", "Server=.;Database=Saved_App;Integrated Security=True");
    syncedXml.SetElementValue("SysConnectionString", "Server=.;Database=Saved_Sys;Integrated Security=True");
    syncedXml.SetElementValue("UpdatePath", @"\\172.168.5.14\CustomerUpdate\ERP startup fixture\update\");
    syncedSeed = syncedSeed with { RawXml = syncedXml.ToString(System.Xml.Linq.SaveOptions.DisableFormatting) };
    var syncedCatalog = Path.Combine(root, "synced-project.xml");
    ProjectImporter.ExportFile([syncedSeed], syncedCatalog, includeSensitive: true);
    var syncedImport = JsonSerializer.SerializeToElement(await CallProject("project.import.applyFile",
        new { path = syncedCatalog, overwrite = false }), Protocol.Json);
    var syncedProject = syncedImport.GetProperty("projects").EnumerateArray()
        .Select(item => item.Deserialize<ProjectInfo>(Protocol.Json)!).Single(item => item.Name == "ERP startup fixture");
    var details = JsonSerializer.SerializeToElement(await CallProject("project.details", new { id = syncedProject.Id }), Protocol.Json);
    Check(details.GetProperty("updatePath").GetString() == @"\\172.168.5.14\CustomerUpdate\ERP startup fixture\update\",
        "Project sync saves the default update folder");
    var created = (ProjectInfo)(await CallProject("project.create", new { code = "TMP", title = "Temporary profile", version = "SP30",
        active = true, server = ".", database = "Test", webSourcePath = root, webProgramPath = @"D:\WebProgram",
        winSourcePath = @"D:\WinSource", winProgramPath = @"D:\WinProgram", webHost1 = "https://tmp.test",
        webHost2 = "http://tmp.local", updatePath = @"D:\Updates", authentication = "sql", user = "fixture-user",
        password = "fixture-secret", defaultUser = "ABC", listAllData = "1", sortOrder = "12", homeUpdateCode = "TMP_HOME", notes = "Fixture notes" }))!;
    var createdDetails = JsonSerializer.SerializeToElement(await CallProject("project.details", new { id = created.Id }), Protocol.Json)
        .GetProperty("profile");
    var profileExportPath = Path.Combine(root, "profile-export.xml");
    await CallProject("project.export.file", new { path = profileExportPath });
    var profileExport = await File.ReadAllTextAsync(profileExportPath);
    Check(created.Code == "TMP" && created.Title == "Temporary profile" && created.ProgramPath == @"D:\WebProgram" &&
        createdDetails.GetProperty("version").GetString() == "SP30" &&
        createdDetails.GetProperty("webHost1").GetString() == "https://tmp.test" &&
        createdDetails.GetProperty("homeUpdateCode").GetString() == "TMP_HOME" &&
        !profileExport.Contains("fixture-secret", StringComparison.Ordinal),
        "Full project profile persists and export removes its password");
    var collectionVersions = JsonSerializer.SerializeToElement(await CallProject("sourceCollection.versions", new { }), Protocol.Json);
    await CallProject("sourceCollection.default.save", new { version = "sp30" });
    await CallProject("settings.save", new { saveShortcuts = true,
        settings = new { shortcuts = new Dictionary<string, string[]> { ["file.new"] = ["Ctrl+Alt+Q"] } } });
    await CallProject("settings.save", new { settings = new { theme = "Dark" } });
    var shortcutSettings = JsonSerializer.SerializeToElement(await CallProject("settings.read", new { }), Protocol.Json);
    Check(shortcutSettings.GetProperty("shortcuts").GetProperty("file.new")[0].GetString() == "Ctrl+Alt+Q" &&
        File.Exists(Path.Combine(root, "shortcuts.dat")) &&
        !File.ReadAllText(Path.Combine(root, "settings.json")).Contains("Ctrl+Alt+Q", StringComparison.Ordinal),
        "Personal shortcuts persist separately through ordinary settings saves");
    Check((string?)await CallProject("sourceCollection.default.read", new { }) == "SP30",
        "Source Collection default version persists through ordinary settings saves");
    var syncedSettings = SettingsStore.Load();
    syncedSettings.StandardCatalogUser = "DEV02";
    SettingsStore.Save(syncedSettings);
    await CallProject("settings.save", new { settings = new { theme = "Dark", standardCatalogUser = "DEV01" } });
    var trackingInfo = JsonSerializer.SerializeToElement(await CallProject("requestTracking.info", new { }), Protocol.Json);
    Check(trackingInfo.GetProperty("defaultUser").GetString() == "DEV02",
        "QLYC reads the latest project sync user after a save with stale preferences");
    await CallProject("sourceCollection.default.save", new { version = "" });
    Check((string?)await CallProject("sourceCollection.default.read", new { }) == "",
        "Source Collection default version can be cleared");
    Check(collectionVersions.EnumerateArray().Any(item => item.GetProperty("version").GetString() == "SP30" &&
        item.GetProperty("roots").EnumerateArray().Any(source => source.GetString() == root)),
        "Source Collection groups saved project source paths by project version");
    await Reject<InvalidOperationException>(() => CallProject("project.create", new { name = created.Name, server = ".", database = "Test", sourcePath = root }),
        "Duplicate project creation is rejected");
    await CallProject("project.delete", new { id = created.Id });
    Check(((ProjectInfo[])(await CallProject("project.list", new { }))!).All(p => p.Id != created.Id) && Directory.Exists(root),
        "Deleting a project removes only its catalog profile");
    async Task<IEnumerable<string?>> OpenDatabases()
    {
        var opened = await CallProject("project.open", new { id = syncedProject.Id });
        return JsonSerializer.SerializeToElement(opened, Protocol.Json).GetProperty("targets")
            .EnumerateArray().Select(target => target.GetProperty("database").GetString()).ToArray();
    }
    await WriteConfig("Current_App", "Current_Sys");
    Check((await OpenDatabases()).SequenceEqual(new[] { "Current_App", "Current_Sys" }),
        "Opening ERP uses current App/SYS when web.config is readable");
    var originalConfig = await File.ReadAllBytesAsync(configPath);
    File.SetLastWriteTimeUtc(configPath, DateTime.UtcNow.AddDays(-1));
    var oldWriteTime = File.GetLastWriteTimeUtc(configPath);
    await CallProject("project.refreshConfig", new { id = syncedProject.Id });
    Check((await File.ReadAllBytesAsync(configPath)).SequenceEqual(originalConfig) &&
        File.GetLastWriteTimeUtc(configPath) > oldWriteTime, "Refresh rewrites web.config without changing its bytes");
    var structure = Directory.CreateDirectory(Path.Combine(syncedProject.SourcePath, "Controller", "Structure", "App")).FullName;
    var nested = Directory.CreateDirectory(Path.Combine(structure, "child")).FullName;
    var generated = Path.Combine(nested, "cached.xml");
    var neighbor = Path.Combine(structure, "..", "keep.xml");
    await File.WriteAllTextAsync(generated, "<cache/>");
    await File.WriteAllTextAsync(neighbor, "<keep/>");
    await CallProject("project.history.record", new { id = syncedProject.Id, path = generated });
    await CallProject("project.history.record", new { id = syncedProject.Id, path = generated });
    Check(((string[])(await CallProject("project.history", new { id = syncedProject.Id }))!).SequenceEqual([generated]),
        "Project history persists and deduplicates file paths");
    var secondHistoryFile = Path.Combine(nested, "second.xml");
    await File.WriteAllTextAsync(secondHistoryFile, "<cache/>");
    await CallProject("project.history.record", new { id = syncedProject.Id, path = secondHistoryFile });
    await CallProject("project.history.removeMany", new { id = syncedProject.Id, paths = new[] { generated, secondHistoryFile } });
    var emptyHistory = JsonSerializer.SerializeToElement(await CallProject("project.history.list", new { }), Protocol.Json);
    Check(emptyHistory.GetArrayLength() == 0 && File.Exists(generated) && File.Exists(secondHistoryFile) &&
        !Directory.EnumerateFiles(Path.Combine(root, "file-history"), "*.json").Any(),
        "Batch history removal writes once, removes the group, and preserves source files");
    File.Delete(secondHistoryFile);
    await CallProject("project.history.record", new { id = syncedProject.Id, path = generated });
    await Reject<IOException>(() => CallProject("project.structure.clear", new { id = syncedProject.Id, path = nested }),
        "Clear Structure rejects a different path");
    var cleared = await CallProject("project.structure.clear", new { id = syncedProject.Id, path = structure });
    Check(cleared is 1 && !File.Exists(generated) && File.Exists(neighbor) && Directory.Exists(nested),
        "Clear Structure deletes nested files and preserves folders and siblings");
    File.Delete(configPath);
    Check((await OpenDatabases()).SequenceEqual(new[] { "Saved_App", "Saved_Sys" }),
        "Opening ERP registers saved App/SYS when web.config is unavailable");
}
{
    await using var tools = new DeveloperTools();
    async Task<object?> Call(string method, object payload) => await tools.ExecuteAsync(method, JsonSerializer.SerializeToElement(payload, Protocol.Json), default);
    var syncedHrm = JsonSerializer.SerializeToElement(await Call("sourceCollection.sync", new { group = "HRM" }), Protocol.Json);
    var hrmRoots = syncedHrm.EnumerateArray().ToDictionary(item => item.GetProperty("version").GetString()!,
        item => item.GetProperty("root").GetString());
    Check(hrmRoots.Count == 2 && hrmRoots["SP24"] == @"\\172.168.5.14\CustomerPro\HRM\DEMO24\SP24" &&
        hrmRoots["SP227"] == @"\\172.168.5.14\CustomerPro\HRM\HRM227\SP227", "Source catalog registers exactly the two HRM paths");
    await Call("sourceCollection.sync", new { group = "HRM" });
    var savedVersions = JsonSerializer.SerializeToElement(await Call("sourceCollection.catalog", new { }), Protocol.Json);
    Check(savedVersions.GetArrayLength() == 2, "Repeated HRM synchronization persists without duplicate entries");
    var searchVersions = JsonSerializer.SerializeToElement(await Call("sourceCollection.versions", new { }), Protocol.Json);
    Check(searchVersions.EnumerateArray().Any(item => item.GetProperty("group").GetString() == "HRM" &&
        item.GetProperty("version").GetString() == "SP227" &&
        item.GetProperty("roots").EnumerateArray().Any(source => source.GetString() == hrmRoots["SP227"])),
        "Synchronized paths retain their major version group for search");
    var collectionSource = Directory.CreateDirectory(Path.Combine(root, "collection-source")).FullName;
    var collectionTarget = Directory.CreateDirectory(Path.Combine(root, "collection-target")).FullName;
    foreach (var relative in new[] { Path.Combine("Grid", "Item.f"), Path.Combine("Dir", "Item.xml"), Path.Combine("Grid", "Other.xml") })
    {
        var input = Path.Combine(collectionSource, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(input)!);
        await File.WriteAllTextAsync(input, relative);
    }
    var collection = JsonSerializer.SerializeToElement(await Call("sourceCollection.find", new
    {
        roots = new[] { Path.Combine(root, "missing-source"), collectionSource },
        samples = new[] { Path.Combine(collectionTarget, "GRID", "item.xml") }
    }), Protocol.Json);
    var matches = collection.GetProperty("files");
    Check(matches.GetArrayLength() == 2 && matches[0].GetProperty("checked").GetBoolean() &&
        !matches[1].GetProperty("checked").GetBoolean() && collection.GetProperty("errors").GetArrayLength() == 1,
        "Source Collection matches stems, checks matching parents, and continues after an unavailable source");
    var contentMatches = JsonSerializer.SerializeToElement(await Call("sourceCollection.content", new
    {
        files = matches.EnumerateArray().ToArray(), query = "ITEM.F"
    }), Protocol.Json);
    Check(contentMatches.GetProperty("indices").EnumerateArray().Select(index => index.GetInt32()).SequenceEqual([0]),
        "Source Collection searches ERP .f content without changing match indices");
    var copyRequest = new { destination = collectionTarget, files = new[] { matches[0] } };
    var firstCollectionCopy = JsonSerializer.SerializeToElement(await Call("sourceCollection.copy", copyRequest), Protocol.Json);
    var copiedCollectionFile = Path.Combine(collectionTarget, "Grid", "Item.f");
    Check(firstCollectionCopy.GetProperty("copied").GetInt32() == 1 && File.Exists(copiedCollectionFile),
        "Source Collection copies selected files preserving their relative paths");
    await File.WriteAllTextAsync(copiedCollectionFile, "existing");
    var retryCollection = JsonSerializer.SerializeToElement(await Call("sourceCollection.copy", copyRequest), Protocol.Json);
    Check(retryCollection.GetProperty("skipped").GetInt32() == 1 && await File.ReadAllTextAsync(copiedCollectionFile) == "existing",
        "Source Collection skips an existing destination");
    var escapedCollection = JsonSerializer.SerializeToElement(await Call("sourceCollection.copy", new
    {
        destination = collectionTarget,
        files = new[] { new { root = collectionSource, relativePath = Path.Combine("..", "outside-collection.txt") } }
    }), Protocol.Json);
    Check(escapedCollection.GetProperty("errors").GetArrayLength() == 1 &&
        !File.Exists(Path.Combine(root, "outside-collection.txt")), "Source Collection rejects paths outside the selected root");
}
if (args.Contains("--sql"))
{
    await using var tools = new DeveloperTools();
    async Task<object?> Call(string method, object payload) => await tools.ExecuteAsync(method, JsonSerializer.SerializeToElement(payload, Protocol.Json), default);
    var server = Environment.GetEnvironmentVariable("LSCODE_TEST_SQL_SERVER") ?? ".";
    var admin = (TargetInfo)(await Call("sql.connect", new { server, database = "master", integratedSecurity = true, trustCertificate = true }))!;
    var watchResult = (ExecutionResult)(await Call("sql.execute", new { targetId = admin.Id,
        sql = "DECLARE @id int = 7; DECLARE @items table (id int); INSERT @items VALUES (@id);\n;\nSELECT (@id + 1) AS Watch; SELECT * FROM @items;" }))!;
    Check(watchResult.Tables.Count() == 2 && watchResult.Tables[0].Rows[0][0] == "8" && watchResult.Tables[1].Rows[0][0] == "7",
        "SQL Watch context and scalar/table queries share variable scope in a single execution");
    var database = "LSCodeWebViewTest_" + Guid.NewGuid().ToString("N");
    if (!Regex.IsMatch(database, "^LSCodeWebViewTest_[a-f0-9]{32}$")) throw new Exception("Unsafe fixture database name");
    var menuAppDatabase = database + "_App";
    var menuAppCreated = false;
    await Call("sql.execute", new { targetId = admin.Id, sql = "CREATE DATABASE [" + database + "]" });
    try
    {
        var target = (TargetInfo)(await Call("sql.connect", new { server, database, integratedSecurity = true, trustCertificate = true }))!;
        await Call("sql.execute", new { targetId = target.Id, sql = """
            CREATE TABLE dbo.nbphyc (stt_rec varchar(20), ma_da varchar(20), ngay_ht datetime,
                ma_lt1 varchar(20), trang_thai varchar(2), noi_dung nvarchar(200));
            CREATE TABLE dbo.nbdsdaumuclt (ma_daumuc varchar(20), ten_daumuc nvarchar(200));
            CREATE TABLE dbo.nbctdaumuc (stt_rec varchar(20), stt_rec0 varchar(20), ma_daumuc varchar(20),
                ten_daumuc nvarchar(200), chon bit, so_luong decimal(18,2), ma_lt varchar(20), ma_tester varchar(20), stt int);
            INSERT dbo.nbphyc VALUES
                ('YC01','DA01','20261105 08:30','DEV01','HT',N'Hoàn thành'),
                ('YC02','DA02','20261106 09:15','DEV01','UP',N'Cập nhật'),
                ('YC03','DA03','20261107 10:00','DEV02','MO',N'Đang mở');
            INSERT dbo.nbdsdaumuclt VALUES ('01',N'Bổ sung chứng từ'),('02',N'Thêm báo cáo');
            INSERT dbo.nbctdaumuc VALUES
                ('YC01','001','01',N'Bổ sung chứng từ',1,2,'DEV01','QA01',1),
                ('YC02','001','02',N'Thêm báo cáo',1,3,'DEV01','QA02',1);
            """ });
        var requestRows = (ResultSet)(await Call("requestTracking.list", new
        {
            targetId = target.Id, legacy = false, user = "DEV01", from = "2026-11-01", to = "2026-11-30"
        }))!;
        Check(requestRows.Rows.Length == 2 && requestRows.Rows.All(row => row[3] == "DEV01"),
            "QLYC filters requests by user and date through the selected LSCode connection");
        var requestStatistics = (ResultSet)(await Call("requestTracking.statistics", new
        {
            targetId = target.Id, legacy = false, year = 2026
        }))!;
        var devStatistics = requestStatistics.Rows.Single(row => row[1] == "DEV01");
        Check(decimal.Parse(devStatistics[14]!, System.Globalization.CultureInfo.InvariantCulture) == 2m &&
            decimal.Parse(devStatistics[28]!, System.Globalization.CultureInfo.InvariantCulture) == 5m,
            "QLYC View All returns request and quantity totals without hard-coded employee groups");
        var requestCategories = (ResultSet)(await Call("requestTracking.categories", new
        {
            targetId = target.Id, legacy = false, requestId = "YC01"
        }))!;
        var pickedCategory = requestCategories.Rows.Single(row => row[0] == "01");
        var remainingCategory = requestCategories.Rows.Single(row => row[0] == "02");
        Check(pickedCategory[1] == "Bổ sung chứng từ" && pickedCategory[2] == "1" &&
            decimal.Parse(pickedCategory[3]!, System.Globalization.CultureInfo.InvariantCulture) == 2m &&
            pickedCategory[4] == "DEV01" && pickedCategory[5] == "QA01",
            "QLYC categories show assigned details for the selected request");
        Check(remainingCategory[1] == "Thêm báo cáo" && remainingCategory[2] == "0" &&
            decimal.Parse(remainingCategory[3]!, System.Globalization.CultureInfo.InvariantCulture) == 0m &&
            string.IsNullOrEmpty(remainingCategory[4]) && string.IsNullOrEmpty(remainingCategory[5]),
            "QLYC categories include unassigned master entries without borrowing details from other requests");
        await Call("sql.execute", new { targetId = admin.Id, sql = $"CREATE DATABASE [{menuAppDatabase}]" });
        menuAppCreated = true;
        var menuAppTarget = (TargetInfo)(await Call("sql.connect", new { server, database = menuAppDatabase, integratedSecurity = true, trustCertificate = true }))!;
        await Call("sql.execute", new { targetId = menuAppTarget.Id, sql = """
            CREATE TABLE dbo.denyunitright (r_deny varchar(100));
            INSERT dbo.denyunitright VALUES ('root,branch,denied');
            GO
            CREATE PROCEDURE dbo.[FastBusiness$App$Startup] AS
            BEGIN
                UPDATE #t SET bar = N'Startup' WHERE menu_id = 'leaf';
                SELECT 'startup-result' AS menu_id, '' AS menu_id0, 'Ignore' AS bar;
            END;
            """ });
        await Call("sql.execute", new { targetId = target.Id, sql = """
            CREATE TABLE dbo.wcommand (wmenu_id varchar(20), wmenu_id0 varchar(20), bar nvarchar(80),
                link varchar(80), status char(1), sysid varchar(80));
            INSERT dbo.wcommand VALUES
                ('root', '', N'Nhóm', '', '', ''),
                ('branch', 'root', N'Nhánh', '', '', ''),
                ('leaf', 'branch', N'Menu', 'leaf.aspx', '', 'ControllerLeaf'),
                ('denied', 'branch', N'Ẩn theo App', '', '', ''),
                ('disabled', 'branch', N'Ngừng', '', '9', ''),
                ('orphan', 'missing', N'Mồ côi', '', '', ''),
                ('empty', '', N'Nhóm trống', '', '', ''),
                ('separator', 'empty', N'-', '', '', '');
            CREATE TABLE dbo.dvcommand (wmenu_id varchar(20), wmenu_id0 varchar(20), bar nvarchar(80), bar2 nvarchar(80), sysid varchar(80), hide_yn bit);
            INSERT dbo.dvcommand VALUES
                ('M', '', N'Mobile', N'', '', 0),
                ('M.1', 'M', N'Bán hàng', N'', 'MobileSales', 0),
                ('M.1.1', 'M.1', N'Phiếu bán', N'', 'MobileVoucher', 0),
                ('M.1.1.1', 'M.1.1', N'Chi tiết', N'', 'MobileDetail', 0);
            """ });
        var menu = (List<CommandNode>)(await Call("project.commands", new { targetId = target.Id, appDatabase = menuAppDatabase }))!;
        Check(menu is [{ Entry.Id: "root", Children: [{ Entry.Id: "branch", Children: [{ Entry: { Id: "leaf", Caption: "Startup", Link: "leaf.aspx", CommandId: "leaf", SysId: "ControllerLeaf" } }] }] }],
            "Menu loads Sys wcommand without userinfo, uses App deny/Startup and retains the required ancestors");
        var mobileMenu = (List<CommandNode>)(await Call("project.commands", new { targetId = target.Id, appDatabase = menuAppDatabase, kind = "mobile" }))!;
        Check(mobileMenu.Single().Children.Single().Children.Single().Children.Single().Entry.SysId == "MobileDetail",
            "Mobile menu loads dbo.dvcommand recursively and keeps sysid for source lookup");
        await Call("sql.execute", new { targetId = target.Id, sql = "UPDATE dbo.dvcommand SET bar=N'Đã đổi' WHERE wmenu_id='M.1.1.1'" });
        var cachedMobile = (List<CommandNode>)(await Call("project.commands", new { targetId = target.Id, appDatabase = menuAppDatabase, kind = "mobile" }))!;
        var refreshedMobile = (List<CommandNode>)(await Call("project.commands", new { targetId = target.Id, appDatabase = menuAppDatabase, kind = "mobile", refresh = true }))!;
        Check(cachedMobile.Single().Children.Single().Children.Single().Children.Single().Entry.Caption == "Chi tiết" &&
            refreshedMobile.Single().Children.Single().Children.Single().Children.Single().Entry.Caption == "Đã đổi",
            "Menu cache avoids repeated queries and explicit refresh reloads Mobile data");
        await Call("sql.execute", new { targetId = target.Id, sql = "ALTER TABLE dbo.wcommand ADD menu_id varchar(20) NULL;\nGO\nUPDATE dbo.wcommand SET menu_id='AA.01.02' WHERE wmenu_id='leaf'; CREATE TABLE dbo.command(menu_id varchar(20), parameter nvarchar(80), enabled bit); INSERT dbo.command VALUES ('AA.01.02',N'Việt'' Nam',1);" });
        var menuInsertScript = (string)(await Call("project.menu.insertScript", new { targetId = target.Id, wmenuId = "leaf" }))!;
        Check(menuInsertScript.Contains("INSERT INTO dbo.wcommand") && menuInsertScript.Contains("INSERT INTO dbo.command")
            && menuInsertScript.Contains("N'Việt'' Nam'") && menuInsertScript.Contains("GO"),
            "Menu F12 script includes matching wcommand and command rows with Unicode literals");
        var menuDetails = JsonSerializer.SerializeToElement(await Call("project.menu.details", new { targetId = target.Id, wmenuId = "leaf" }), Protocol.Json);
        Check(menuDetails.GetProperty("values").GetProperty("bar").GetString() == "Menu" &&
            menuDetails.GetProperty("values").GetProperty("wmenu_id").GetString() == "leaf",
            "Menu editor reads schema-driven wcommand details");
        await Call("sql.execute", new { targetId = target.Id,
            sql = "INSERT dbo.wcommand(wmenu_id,wmenu_id0,bar,link,status,sysid,menu_id) VALUES ('03.69.43','03.69.02',N'Menu cùng cấp','','1','',NULL)" });
        var siblingDraft = JsonSerializer.SerializeToElement(await Call("project.menu.new", new { targetId = target.Id, selectedId = "03.69.43" }), Protocol.Json);
        Check(siblingDraft.GetProperty("values").GetProperty("wmenu_id0").GetString() == "03.69.02" &&
            siblingDraft.GetProperty("values").GetProperty("wmenu_id").GetString() == "03.69.44",
            "Menu editor keeps the selected parent and increments the final tree-code suffix for a new sibling");
        var menuDraft = JsonSerializer.SerializeToElement(await Call("project.menu.new", new { targetId = target.Id, selectedId = "leaf" }), Protocol.Json);
        var draftValues = menuDraft.GetProperty("values").Deserialize<Dictionary<string, string?>>(Protocol.Json)!;
        draftValues["bar"] = "Menu mới";
        draftValues["link"] = null;
        draftValues.Remove("sysid");
        var insertedMenu = JsonSerializer.SerializeToElement(await Call("project.menu.save", new
        {
            targetId = target.Id, mode = "new", originalId = "", version = "", values = draftValues
        }), Protocol.Json);
        var insertedId = insertedMenu.GetProperty("values").GetProperty("wmenu_id").GetString()!;
        Check(insertedId == "leaf.01" && insertedMenu.GetProperty("values").GetProperty("bar").GetString() == "Menu mới" &&
            insertedMenu.GetProperty("values").EnumerateObject().All(column => column.Value.ValueKind != JsonValueKind.Null),
            "Menu editor generates a free branch code and inserts wcommand without NULL, including omitted and null fields");
        var insertedVersion = insertedMenu.GetProperty("version").GetString()!;
        var editValues = insertedMenu.GetProperty("values").Deserialize<Dictionary<string, string?>>(Protocol.Json)!;
        editValues["bar"] = "Đã sửa";
        await Call("project.menu.save", new { targetId = target.Id, mode = "edit", originalId = insertedId, version = insertedVersion, values = editValues });
        await Reject<InvalidOperationException>(async () => await Call("project.menu.save", new
        {
            targetId = target.Id, mode = "edit", originalId = insertedId, version = insertedVersion, values = editValues
        }), "Menu editor rejects a stale edit instead of overwriting it");
        await Call("sql.execute", new { targetId = target.Id,
            sql = "ALTER TABLE dbo.wcommand ADD fixture_nullable varchar(10) NULL DEFAULT (NULL);" });
        var rejectedValues = new Dictionary<string, string?>(draftValues)
        {
            ["wmenu_id"] = "leaf.02", ["menu_id"] = "AA.99.99", ["bar"] = "Menu fixture"
        };
        await Reject<InvalidOperationException>(async () => await Call("project.menu.save", new
        {
            targetId = target.Id, mode = "new", originalId = "", version = "", values = rejectedValues
        }), "Menu editor rejects a new row when a SQL default would leave a NULL column");
        await Reject<KeyNotFoundException>(async () => await Call("project.menu.details", new
        {
            targetId = target.Id, wmenuId = "leaf.02"
        }), "Menu editor rolls back a new row that still contains NULL");
        var batch = (ExecutionResult)(await Call("sql.execute", new { targetId = target.Id, sql = "CREATE TABLE #test(value int);\nGO\nINSERT INTO #test VALUES (42);\nGO\nSELECT value FROM #test;" }))!;
        Check(batch.Tables[0].Rows[0][0] == "42", "SQL keeps connection/temp table across GO batches");
        await Call("sql.execute", new { targetId = target.Id, sql = """
            CREATE TABLE dbo.hrtabinfo (id char(2), sysid char(40), bar nvarchar(80), bar2 nvarchar(80));
            INSERT dbo.hrtabinfo VALUES ('12', 'hrPIPassportVisa', N' ', N'Passport'),
                ('10', '', N'Thông tin cá nhân', N'Personal information'),
                ('11', 'hrPIGeneralInformation', N'Thông tin chung', N'General information');
            """ });
        var employeeTabs = JsonSerializer.SerializeToElement(await Call("project.employeeTabs", new { targetId = target.Id }), Protocol.Json);
        Check(employeeTabs.GetArrayLength() == 2 &&
            employeeTabs[0].GetProperty("caption").GetString() == "Thông tin chung" &&
            employeeTabs[1].GetProperty("sysId").GetString() == "hrPIPassportVisa" &&
            employeeTabs[1].GetProperty("caption").GetString() == "Passport",
            "Employee function tabs omit heading rows, preserve ERP order and return trimmed sysid and localized captions");
        await Call("sql.execute", new { targetId = target.Id, sql = """
            CREATE TABLE dbo.sysgendata (basetable char(30));
            INSERT dbo.sysgendata VALUES ('d46$000000');
            CREATE TABLE dbo.dmstt (ngay_gh1 smalldatetime, ngay_gh2 smalldatetime);
            INSERT dbo.dmstt VALUES ('20260101', '20260131'), ('20260201', '20260228');
            CREATE TABLE dbo.[d46$000000] (ngay_ct date, ky_hieu_nhap varchar(24) NULL);
            INSERT dbo.[d46$000000] (ngay_ct) VALUES ('20260101');
            CREATE TABLE dbo.[d46$log] (ngay_ct date);
            CREATE TABLE dbo.[d46$202601] (ngay_ct date);
            CREATE TABLE dbo.[d46$202602] (ngay_ct date);
            CREATE TABLE dbo.[d46$202603] (ngay_ct date);
            INSERT dbo.[d46$log] VALUES ('20260101');
            INSERT dbo.[d46$202601] VALUES ('20260101');
            INSERT dbo.[d46$202602] VALUES ('20260201');
            GO
            CREATE PROCEDURE dbo.[FastBusiness$Partition$Execute]
                @sql nvarchar(4000), @filter nvarchar(200), @dateField sysname,
                @from smalldatetime, @to smalldatetime, @user int, @admin int
            AS
            BEGIN
                IF @dateField <> 'ngay_ct' OR @user <> 1 OR @admin <> 1
                    THROW 50000, 'Incorrect ERP procedure arguments', 1;
                DECLARE @day date = DATEFROMPARTS(YEAR(@from), MONTH(@from), 1);
                WHILE @day <= @to
                BEGIN
                    DECLARE @statement nvarchar(4000) = REPLACE(REPLACE(REPLACE(
                        @sql, '%Partition', CONVERT(char(6), @day, 112)), '%[', ''), ']%', '');
                    EXEC sys.sp_executesql @statement;
                    SET @day = DATEADD(month, 1, @day);
                END
            END
            """ });
        var partitionColumn = new { targetId = target.Id, table = "d46$000000", columns = new[] { "ky_hieu_nhap" } };
        var partitionAdd = (string)(await Call("table.addScript", partitionColumn))!;
        await Call("sql.execute", new { targetId = target.Id, sql = partitionAdd });
        await Call("sql.execute", new { targetId = target.Id, sql = partitionAdd });
        var filled = (ExecutionResult)(await Call("sql.execute", new { targetId = target.Id, sql = """
            SELECT (SELECT ky_hieu_nhap FROM dbo.[d46$000000]), (SELECT ky_hieu_nhap FROM dbo.[d46$log]),
                (SELECT ky_hieu_nhap FROM dbo.[d46$202601]), (SELECT ky_hieu_nhap FROM dbo.[d46$202602]),
                COL_LENGTH(N'dbo.d46$202603', N'ky_hieu_nhap');
            """ }))!;
        Check(filled.Tables[0].Rows[0].SequenceEqual(new string?[] { "", "", "", "", null }),
            "Partition ADD backfills template, log and both ERP months, supports rerun and skips the outside month");
        await Call("sql.execute", new { targetId = target.Id, sql = "ALTER TABLE dbo.[d46$000000] ALTER COLUMN ky_hieu_nhap varchar(32) NULL;" });
        var partitionAlter = (string)(await Call("table.alterScript", partitionColumn))!;
        await Call("sql.execute", new { targetId = target.Id, sql = partitionAlter });
        var lengths = (ExecutionResult)(await Call("sql.execute", new { targetId = target.Id, sql = """
            SELECT COL_LENGTH(N'dbo.d46$log', N'ky_hieu_nhap'), COL_LENGTH(N'dbo.d46$202601', N'ky_hieu_nhap'),
                COL_LENGTH(N'dbo.d46$202602', N'ky_hieu_nhap'), COL_LENGTH(N'dbo.d46$202603', N'ky_hieu_nhap');
            """ }))!;
        Check(lengths.Tables[0].Rows[0].SequenceEqual(new string?[] { "32", "32", "32", null }),
            "Partition ALTER applies template type to log and in-range months only");
        await Call("sql.execute", new { targetId = target.Id, sql = "CREATE TABLE dbo.grid_save_fixture(id int PRIMARY KEY); INSERT dbo.grid_save_fixture VALUES (1),(2);" });
        var gridWrites = new[]
        {
            new { mode = "update", columns = new[] { "id" }, values = new[] { "3" }, keyColumns = new[] { "id" }, keyValues = new[] { "1" } },
            new { mode = "insert", columns = new[] { "id" }, values = new[] { "2" }, keyColumns = Array.Empty<string>(), keyValues = Array.Empty<string>() },
        };
        var duplicateGridKey = JsonSerializer.SerializeToElement(await Call("table.writeBatch", new { targetId = target.Id, table = "grid_save_fixture", rows = gridWrites }), Protocol.Json);
        Check(duplicateGridKey.GetProperty("conflict").GetInt32() == 1, "Grid save identifies the duplicate row");
        var retry = JsonSerializer.SerializeToElement(await Call("table.writeBatch", new { targetId = target.Id, table = "grid_save_fixture", rows = gridWrites.Take(1).ToArray() }), Protocol.Json);
        Check(!retry.TryGetProperty("conflict", out var retryConflict) || retryConflict.ValueKind == JsonValueKind.Null,
            "Duplicate grid key rolls back the earlier update and its original key can be used to retry");
        var deleteRows = new[]
        {
            new { mode = "delete", columns = Array.Empty<string>(), values = Array.Empty<string>(), keyColumns = new[] { "id" }, keyValues = new[] { "2" } },
        };
        var deleted = JsonSerializer.SerializeToElement(await Call("table.writeBatch", new { targetId = target.Id, table = "grid_save_fixture", rows = deleteRows }), Protocol.Json);
        Check(!deleted.TryGetProperty("conflict", out var deleteConflict) || deleteConflict.ValueKind == JsonValueKind.Null,
            "Grid F8 delete removes rows transactionally by primary key");
        var afterDelete = (ResultSet)(await Call("table.read", new { targetId = target.Id, schema = "dbo", table = "grid_save_fixture" }))!;
        Check(afterDelete.Rows.Length == 1 && afterDelete.Rows[0][0] == "3", "Grid delete refresh source contains only the retained row");
        await Call("sql.execute", new { targetId = target.Id, sql = "CREATE TABLE dbo.viewer_fixture (value int); INSERT dbo.viewer_fixture VALUES (1),(2),(3);" });
        var tableCatalog = (ResultSet)(await Call("table.catalog", new { targetId = target.Id }))!;
        Check(tableCatalog.Rows.Any(row => row.SequenceEqual(new[] { "dbo", "viewer_fixture" })), "Table catalog returns the new table and its schema");
        var expressionPreview = JsonSerializer.SerializeToElement(await Call("table.view", new
        {
            targetId = target.Id, schema = "dbo", table = "viewer_fixture",
            select = "SUM(value) AS total", where = "value BETWEEN 1 AND 2", orderBy = "total DESC",
        }), Protocol.Json);
        Check(expressionPreview.GetProperty("data").GetProperty("rows")[0][0].GetString() == "3",
            "Table viewer executes SELECT expressions with WHERE and ORDER BY syntax");
        await Call("sql.execute", new
        {
            targetId = target.Id, sql = """
                CREATE TABLE dbo.paged_fixture (id int NOT NULL PRIMARY KEY, label nvarchar(max), payload varbinary(max), detail xml);
                WITH digits(n) AS (SELECT n FROM (VALUES (0),(1),(2),(3),(4),(5),(6),(7),(8),(9)) AS d(n))
                INSERT dbo.paged_fixture (id, label, payload, detail)
                SELECT n, N'Mẫu', 0x0123, N'<r>ok</r>'
                FROM (SELECT 1+a.n+10*b.n+100*c.n+1000*d.n AS n FROM digits a CROSS JOIN digits b CROSS JOIN digits c CROSS JOIN digits d) ids
                WHERE n<=1201;
                UPDATE dbo.paged_fixture SET label=REPLICATE(CONVERT(nvarchar(max), N'X'), 300000) WHERE id BETWEEN 1 AND 4;
                """,
        });
        async Task<JsonElement> ReadFixturePage(int offset, string select, string where = "") =>
            JsonSerializer.SerializeToElement(await Call("table.view", new
            {
                targetId = target.Id, table = "paged_fixture", offset, select, where, scope = "paging-fixture",
            }), Protocol.Json);
        var largePage = await ReadFixturePage(0, "id, label", "id <= 4");
        var largeRows = largePage.GetProperty("data").GetProperty("rows");
        var nextLargePage = await ReadFixturePage(largeRows.GetArrayLength(), "id, label", "id <= 4");
        Check(largePage.GetProperty("hasMore").GetBoolean() && largeRows.GetArrayLength() == 2 &&
            largeRows[0][1].GetString()!.Length == 300000 &&
            nextLargePage.GetProperty("data").GetProperty("rows")[0][0].GetString() == "3",
            "Table viewer bounds large pages and resumes without dropping the complete text values");
        await Call("sql.execute", new
        {
            targetId = target.Id,
            sql = "UPDATE dbo.paged_fixture SET label=N'MAX filter ' + REPLICATE(CONVERT(nvarchar(max), N'X'), 600000) WHERE id=5;",
        });
        var shortenedPage = await ReadFixturePage(0, "label, id", "label LIKE N'MAX filter %'");
        var shortenedRow = shortenedPage.GetProperty("data").GetProperty("rows")[0];
        Check(shortenedPage.GetProperty("shortenedText").GetBoolean() &&
            shortenedRow[0].GetString()!.StartsWith("MAX filter ", StringComparison.Ordinal) &&
            shortenedRow[0].GetString()!.Length < 600000 && shortenedRow[1].GetString() == "5",
            "Table viewer keeps filtered rows and later columns when an nvarchar(max) value exceeds the preview budget");
        var pagePastThousand = await ReadFixturePage(1000, "id, payload, detail");
        var pageEnd = await ReadFixturePage(1200, "id, payload, detail");
        Check(pagePastThousand.GetProperty("hasMore").GetBoolean() &&
            pagePastThousand.GetProperty("data").GetProperty("rows")[0][0].GetString() == "1001" &&
            !pageEnd.GetProperty("hasMore").GetBoolean() &&
            pageEnd.GetProperty("data").GetProperty("rows")[0].EnumerateArray().Select(cell => cell.GetString())
                .SequenceEqual(new[] { "1201", "0x0123", "<r>ok</r>" }),
            "Table viewer reaches rows past 1000 in key order and retains binary and XML values");
        await Reject<ArgumentException>(async () => await Call("table.view", new
        {
            targetId = target.Id, schema = "dbo", table = "viewer_fixture",
            select = "value FROM dbo.viewer_fixture; DROP TABLE dbo.viewer_fixture; SELECT value",
        }), "Table SELECT expressions still reject additional SQL statements");
        await Call("sql.execute", new { targetId = target.Id, sql = "/* header */ CREATE OR ALTER PROC dbo.script_fixture @value int AS SELECT @value AS value;" });
        var procedureScript = (string)(await Call("schema.script", new { targetId = target.Id, @object = "[dbo].[script_fixture]" }))!;
        Check(procedureScript.Contains("IF OBJECT_ID(N'[dbo].[script_fixture]', N'P') IS NULL") &&
            Regex.IsMatch(procedureScript, @"ALTER\s+PROC dbo\.script_fixture") &&
            !procedureScript.Contains("CREATE OR ALTER", StringComparison.OrdinalIgnoreCase) &&
            procedureScript.TrimEnd().EndsWith("\nGO"), "DB Object generates a guarded stub and an ALTER batch");
        await Call("sql.execute", new { targetId = target.Id, sql = "DROP PROCEDURE dbo.script_fixture;" });
        var replay = (ExecutionResult)(await Call("sql.execute", new
        {
            targetId = target.Id,
            sql = procedureScript + "EXEC dbo.script_fixture @value = 42;"
        }))!;
        Check(replay.Tables[0].Rows[0][0] == "42", "Generated script recreates a missing procedure and accepts a following command");
        await Call("sql.execute", new { targetId = target.Id, sql = "CREATE TABLE dbo.script_table_fixture (id int);\nGO\nCREATE VIEW dbo.script_view_fixture AS SELECT 1 AS id;" });
        foreach (var objectName in new[] { "dbo.script_table_fixture", "dbo.script_view_fixture" })
        {
            var objectScript = (string)(await Call("schema.script", new { targetId = target.Id, @object = objectName }))!;
            Check(objectScript.TrimEnd().EndsWith("\nGO") && !objectScript.TrimEnd().EndsWith("\nGO\nGO"),
                $"DB Object terminates {objectName} with one final GO");
        }
        using (var cts = new CancellationTokenSource(300))
            await Reject<OperationCanceledException>(async () => await tools.ExecuteAsync("sql.execute", JsonSerializer.SerializeToElement(new { targetId = target.Id, sql = "WAITFOR DELAY '00:00:10'; SELECT 1" }), cts.Token), "SQL cancellation reaches SqlCommand");
        var previewObject = await Call("catalog.preview", new { targetId = target.Id, schema = "dbo", table = "catalog", creating = true, fields = new[] { new CatalogField("code", "nvarchar", "20", Key: true), new CatalogField("label", "nvarchar", "100") }, rows = Array.Empty<string[]>() });
        var preview = JsonSerializer.SerializeToElement(previewObject, Protocol.Json);
        await Call("catalog.commit", new { targetId = target.Id, previewId = preview.GetProperty("previewId").GetString() });
        var loaded = JsonSerializer.SerializeToElement(await Call("catalog.load", new { targetId = target.Id, schema = "dbo", table = "catalog" }), Protocol.Json);
        Check(loaded.GetProperty("columns").GetArrayLength() == 6, "Catalog creates fields and four audit columns");
        var suggestions=(LSCode.Core.SqlSuggestion[])(await Call("sql.complete",new{targetId=target.Id,text="SELECT c. FROM dbo.catalog c",beforeCaret="SELECT c."}))!;
        Check(suggestions.Any(s=>s.Text=="code"),"SQL completion adapter resolves alias against actual schema");
        const string embeddedSql = "<dir><query><![CDATA[SELECT c.co FROM dbo.catalog c]]></query></dir>";
        var embeddedHints = JsonSerializer.SerializeToElement(await Call("xml.complete", new { targetId = target.Id,
            text = embeddedSql, offset = embeddedSql.IndexOf(" FROM", StringComparison.Ordinal) }), Protocol.Json);
        Check(embeddedHints.GetProperty("suggestions").EnumerateArray().Any(item => item.GetProperty("text").GetString() == "code"),
            "SQL inside XML resolves aliases against the document database target");
        var adminIdentity=(ExecutionResult)(await Call("sql.execute",new{targetId=admin.Id,sql="SELECT DB_NAME()"}))!;
        var tabIdentity=(ExecutionResult)(await Call("sql.execute",new{targetId=target.Id,sql="SELECT DB_NAME()"}))!;
        Check(adminIdentity.Tables[0].Rows[0][0]=="master"&&tabIdentity.Tables[0].Rows[0][0]==database,"Target handles remain isolated across database switches");
        var externalJs=(LSCode.Core.SqlSuggestion[])(await Call("javascript.complete",new{language="xml",text="<root>f.ge",beforeCaret="<root>f.ge"}))!;
        Check(externalJs.Length==0,"JS completion adapter rejects XML outside script");
        var projectXml="<NewDataSet><project><FolderName>Fixture</FolderName><servername>.</servername><Password>not-real</Password><Unknown><Nested>preserved</Nested></Unknown></project></NewDataSet>";
        var projectImportPath = Path.Combine(root, "project-import.xml");
        await File.WriteAllTextAsync(projectImportPath, projectXml);
        var projectPreview = JsonSerializer.SerializeToElement(await Call("project.import.previewFile", new { path = projectImportPath, overwrite = false }), Protocol.Json);
        Check(projectPreview.GetProperty("total").GetInt32() == 1 && projectPreview.GetProperty("added").GetInt32() == 1,
            "File import preview returns compact counts without sending XML through the bridge");
        await Call("project.import.applyFile",new{path=projectImportPath,overwrite=false});
        var projectExportPath = Path.Combine(root, "project-export.xml");
        await Call("project.export.file",new{path=projectExportPath});
        var export=await File.ReadAllTextAsync(projectExportPath);
        Check(export.Contains("<Nested>preserved</Nested>")&&!export.Contains("not-real"),"DPAPI catalog import/export preserves metadata and redacts password");
        var profileId=LSCode.Core.ProjectImporter.Read(projectXml)[0].Id;
        var updatedProfile=(ProjectInfo)(await Call("project.update",new{id=profileId,name="Fixture updated",server=".",database,sourcePath=root,programPath=Path.Combine(root,"program.exe")}))!;
        var details=JsonSerializer.SerializeToElement(await Call("project.details",new{id=updatedProfile.Id}),Protocol.Json);
        Check(details.GetProperty("programPath").GetString()!.EndsWith("program.exe")&&details.GetProperty("metadata").GetString()!.Contains("<Nested>preserved</Nested>")&&!details.GetProperty("metadata").GetString()!.Contains("not-real"),"Project details/edit preserve custom XML and redact credentials");
        var basicProject=JsonSerializer.SerializeToElement(await Call("project.open",new{id=updatedProfile.Id}),Protocol.Json);
        Check(basicProject.GetProperty("targets").GetArrayLength()==1,"Basic project registers database target without synced connections");
        Directory.CreateDirectory(Path.Combine(root,"App_Data"));
        await File.WriteAllTextAsync(Path.Combine(root,"menu.aspx"),"<FastBusiness:QueryExtender Controller='employee' />");
        await File.WriteAllTextAsync(Path.Combine(root,"App_Data","employee.xml"),"<controller><items style='Grid' controller='detail'/></controller>");
        await File.WriteAllTextAsync(Path.Combine(root,"App_Data","detail.f"),"<controller/>");
        var related=JsonSerializer.SerializeToElement(await Call("project.controller",new{root,link="menu.aspx"}),Protocol.Json);
        Check(related.GetProperty("files").GetArrayLength()==3,"Controller explorer includes ASPX and child grid files");
        Directory.CreateDirectory(Path.Combine(root, "Main"));
        var mainFixture = Path.Combine(root, "Main", "virtual-menu.aspx");
        await File.WriteAllTextAsync(mainFixture, "<FastBusiness:QueryExtender Controller='employee' />");
        var relatedFromMain = JsonSerializer.SerializeToElement(
            await Call("project.controller", new { root, link = "~/virtual/routes/virtual-menu.aspx" }), Protocol.Json);
        Check(relatedFromMain.GetProperty("files").EnumerateArray().Any(item =>
                string.Equals(item.GetString(), mainFixture, StringComparison.OrdinalIgnoreCase)),
            "Controller explorer keeps the resolved Main file when menu links contain virtual folders");
        var fOnly = Path.Combine(root, "App_Data", "legacy.f");
        await File.WriteAllTextAsync(fOnly, "<controller><items style='Grid' controller='legacyGrid'/></controller>");
        var fGrid = Path.Combine(root, "App_Data", "legacyGrid.f");
        await File.WriteAllTextAsync(fGrid, "<controller/>");
        var legacyController = JsonSerializer.SerializeToElement(
            await Call("project.controller", new { root, link = "legacy.aspx" }), Protocol.Json);
        Check(legacyController.GetProperty("files").EnumerateArray()
            .Select(item => item.GetString()).ToHashSet().SetEquals(new[] { fOnly, fGrid }),
            "Controller discovery follows grid references in an F-only controller without an ASPX");
        var signature = loaded.GetProperty("signature").GetString();
        var records = new string?[][] { [":text::null", "Tiếng Việt", null, null, null, null], ["second", ":empty", null, null, null, null] };
        var insertion = JsonSerializer.SerializeToElement(await Call("catalog.preview", new { targetId = target.Id, schema = "dbo", table = "catalog", creating = false, signature, fields = Array.Empty<CatalogField>(), rows = records }), Protocol.Json);
        var insertId = insertion.GetProperty("previewId").GetString();
        await Call("catalog.commit", new { targetId = target.Id, previewId = insertId });
        await Reject<InvalidOperationException>(async () => await Call("catalog.commit", new { targetId = target.Id, previewId = insertId }), "Commit tokens prevent accidental replay");
        var read = (ResultSet)(await Call("table.read", new { targetId = target.Id, schema = "dbo", table = "catalog", orderBy = "code", columns = new[] { "code", "label" } }))!;
        Check(read.Rows.Length == 2 && read.Rows[0][0] == ":null" && read.Rows[0][1] == "Tiếng Việt", "Catalog parameterized inserts preserve literal tokens/Unicode");
        var duplicate = (List<CatalogField>)(await Call("catalog.duplicate", new { targetId = target.Id, schema = "dbo", table = "catalog" }))!;
        Check(duplicate.Count == 2 && duplicate[0].Key, "Duplicate template preserves PK and excludes audit columns");
        var copyPreview = JsonSerializer.SerializeToElement(await Call("catalog.preview", new { targetId = target.Id, schema = "dbo", table = "catalog_copy", creating = true, fields = duplicate, rows = Array.Empty<string[]>() }), Protocol.Json);
        await Call("catalog.commit", new { targetId = target.Id, previewId = copyPreview.GetProperty("previewId").GetString() });
        var emptyCopy = (ResultSet)(await Call("table.read", new { targetId = target.Id, schema = "dbo", table = "catalog_copy" }))!;
        Check(emptyCopy.Rows.Length == 0, "Duplicate template never copies data");
        var duplicateRows = new string?[][] { ["duplicate", "first", null, null, null, null], ["duplicate", "second", null, null, null, null] };
        var failedPreview = JsonSerializer.SerializeToElement(await Call("catalog.preview", new { targetId = target.Id, schema = "dbo", table = "catalog", creating = false, signature, fields = Array.Empty<CatalogField>(), rows = duplicateRows }), Protocol.Json);
        await Reject<Microsoft.Data.SqlClient.SqlException>(async () => await Call("catalog.commit", new { targetId = target.Id, previewId = failedPreview.GetProperty("previewId").GetString() }), "Catalog rejects duplicate keys in transaction");
        var rolledBack = (ResultSet)(await Call("table.read", new { targetId = target.Id, schema = "dbo", table = "catalog", where = "code=N'duplicate'" }))!;
        Check(rolledBack.Rows.Length == 0, "Failed insert batch rolls back every draft row");
        await Call("sql.execute",new{targetId=target.Id,sql="CREATE TABLE dbo.precision_fixture(value datetime2(7) NULL, version rowversion NOT NULL); CREATE TABLE dbo.parent_fixture(id int PRIMARY KEY); CREATE TABLE dbo.child_fixture(id int PRIMARY KEY,parent_id int NULL REFERENCES dbo.parent_fixture(id),label nvarchar(20) UNIQUE,created datetime2(3) DEFAULT NULL);"});
        var preciseLoad=JsonSerializer.SerializeToElement(await Call("catalog.load",new{targetId=target.Id,schema="dbo",table="precision_fixture"}),Protocol.Json);
        Check(preciseLoad.GetProperty("columns")[1].GetProperty("computed").GetBoolean(),"Rowversion columns are managed by SQL Server");
        var precisePreview=JsonSerializer.SerializeToElement(await Call("catalog.preview",new{targetId=target.Id,schema="dbo",table="precision_fixture",creating=false,signature=preciseLoad.GetProperty("signature").GetString(),fields=Array.Empty<CatalogField>(),rows=new string?[][]{["2026-11-10T12:34:56.1234567",null]}}),Protocol.Json);
        await Call("catalog.commit",new{targetId=target.Id,previewId=precisePreview.GetProperty("previewId").GetString()});
        var precise=(ExecutionResult)(await Call("sql.execute",new{targetId=target.Id,sql="SELECT CONVERT(nvarchar(40),value,126),DATALENGTH(version) FROM dbo.precision_fixture"}))!;
        Check(precise.Tables[0].Rows[0][0]=="2026-11-10T12:34:56.1234567"&&precise.Tables[0].Rows[0][1]=="8","Typed parameters preserve datetime2 precision and automatic rowversion");
        var richTemplate=(List<CatalogField>)(await Call("catalog.duplicate",new{targetId=target.Id,schema="dbo",table="child_fixture"}))!;
        Check(richTemplate.Single(f=>f.Name=="label").Unique&&richTemplate.Single(f=>f.Name=="parent_id").Reference?.SequenceEqual(new[]{"dbo","parent_fixture","id","int"})==true,"Template copy preserves UNIQUE and foreign-key reference");
        var structure = (ResultSet)(await Call("table.structure", new { targetId = target.Id, table = "child_fixture" }))!;
        Check(structure.Rows.Single(row => row[0] == "id")[2]!.StartsWith("PK")
            && structure.Rows.Single(row => row[0] == "label")[2]!.StartsWith("UQ")
            && structure.Rows.Single(row => row[0] == "parent_id")[2]!.StartsWith("FK"), "Table structure shows primary, unique and foreign keys");
        var createScript = (string)(await Call("table.createScript", new { targetId = target.Id, table = "child_fixture" }))!;
        Check(createScript.Contains("CREATE TABLE [dbo].[child_fixture]") && createScript.Contains("PRIMARY KEY")
            && createScript.Contains("UNIQUE") && createScript.Contains("FOREIGN KEY"), "Create table script includes table keys");
        var addColumns = (string)(await Call("table.addScript", new { targetId = target.Id, table = "child_fixture", columns = new[] { "label", "parent_id" } }))!;
        Check(addColumns.Contains("ALTER TABLE [dbo].[child_fixture] ADD") && addColumns.Contains("[label] NVARCHAR(20) NULL")
            && addColumns.Contains("[parent_id] INT NULL"), "Add column script preserves selected type and nullability");
        await Call("sql.execute", new { targetId = target.Id, sql = """
            CREATE TABLE dbo.insert_fixture(id int IDENTITY PRIMARY KEY, label nvarchar(60) NULL, payload varbinary(8),
                spare int, version rowversion, short_date smalldatetime, full_date datetime, precise_date datetime2(7));
            INSERT dbo.insert_fixture(label,payload,short_date,full_date,precise_date) VALUES
                (N'Việt'' Nam',0x0AFF,'2026-11-19T13:07:00','2026-11-19T13:07:48','2026-11-19T13:07:48.1234567'),
                (NULL,0x00,NULL,NULL,NULL);
            """ });
        var view = JsonSerializer.SerializeToElement(await Call("table.view", new { targetId = target.Id, table = "insert_fixture", where = "id=1", columns = new[] { "id", "label", "payload", "version", "short_date", "full_date", "precise_date" } }), Protocol.Json);
        var insert = (string)(await Call("table.insertScript", new { snapshotId = view.GetProperty("snapshotId").GetString() }))!;
        Check(view.GetProperty("data").GetProperty("rows").GetArrayLength() == 1 && insert.Contains("N'Việt'' Nam'")
            && insert.Contains("0x0AFF") && insert.Contains("IDENTITY_INSERT") && !insert.Contains("[version]"), "Insert script uses the filtered snapshot and skips rowversion");
        Check(insert.Contains("CONVERT(smalldatetime, '19/11/2026', 103)") &&
            insert.Contains("CONVERT(datetime, '19/11/2026 13:07', 103)") &&
            insert.Contains("2026-11-19T13:07:48.1234567"), "Table INSERT formats dates by SQL column type");
        var replayed = (ExecutionResult)(await Call("sql.execute", new { targetId = target.Id, sql =
            "SET DATEFORMAT mdy; BEGIN TRANSACTION; DELETE dbo.insert_fixture WHERE id=1;\n" + insert +
            "\nSELECT CONVERT(varchar(19),short_date,120),CONVERT(varchar(19),full_date,120) FROM dbo.insert_fixture WHERE id=1; ROLLBACK;" }))!;
        Check(replayed.Tables.Single().Rows.Single().SequenceEqual(new[] { "2026-11-19 00:00:00", "2026-11-19 13:07:00" }),
            "Table INSERT replays day-first values regardless of the SQL session date format");
        await Call("sql.execute", new { targetId = target.Id, sql = """
            CREATE PROCEDURE dbo.advance_proc_fixture AS SELECT 1;
            GO
            CREATE FUNCTION dbo.advance_function_fixture() RETURNS int AS BEGIN RETURN 1 END;
            """ });
        var updateConnection = new SqlConnectionStringBuilder
        {
            DataSource = server, InitialCatalog = database, IntegratedSecurity = true,
            TrustServerCertificate = true, Encrypt = SqlConnectionEncryptOption.Optional,
        };
        var generatedSqlUpdate = await AdvanceNoteUpdateGenerator.GenerateAsync(updateProjectRoot, updateOutput,
            noteItem with
            {
                Title = "Object và data",
                SourceFiles = [],
                Sql = new Dictionary<string, AdvanceNoteSql>
                {
                    ["App"] = new("dbo.insert_fixture\ndbo.advance_proc_fixture\ndbo.advance_function_fixture",
                        "dbo.insert_fixture: id=1", "SET NOCOUNT ON"),
                },
            }, new Dictionary<string, SqlTarget>
            {
                ["App"] = new(server, database, updateConnection.ConnectionString),
            }, default);
        var generatedSql = await File.ReadAllTextAsync(generatedSqlUpdate.ScriptFiles.Single(), Encoding.Unicode);
        Check(generatedSql.Contains("DROP TABLE [dbo].[insert_fixture]") &&
            generatedSql.Contains("CREATE TABLE [dbo].[insert_fixture]") &&
            generatedSql.Contains("DROP PROCEDURE [dbo].[advance_proc_fixture]") &&
            generatedSql.Contains("CREATE PROCEDURE dbo.advance_proc_fixture") &&
            generatedSql.Contains("DROP FUNCTION [dbo].[advance_function_fixture]") &&
            generatedSql.Contains("CREATE FUNCTION dbo.advance_function_fixture") &&
            generatedSql.Contains("-- dbo.insert_fixture: id=1") &&
            generatedSql.Contains("N'Việt'' Nam'") && !generatedSql.Contains("0x00"),
            "Advance Note expands object names to DROP/CREATE and filtered table keys to INSERT data");
        var secondView = JsonSerializer.SerializeToElement(await Call("table.view", new
        {
            scope = "inspection-2", targetId = target.Id, table = "insert_fixture", where = "id=2",
        }), Protocol.Json);
        var secondSnapshot = secondView.GetProperty("snapshotId").GetString();
        var firstSnapshot = view.GetProperty("snapshotId").GetString();
        Check((string?)await Call("table.insertScript", new { snapshotId = firstSnapshot }) == insert &&
            ((string)(await Call("table.insertScript", new { scope = "inspection-2", snapshotId = secondSnapshot }))!).Contains("0x00"),
            "Independent inspection tabs generate INSERT from their own filtered previews");
        await Call("table.releaseSnapshot", new { scope = "inspection-2" });
        await Reject<InvalidOperationException>(() => Call("table.insertScript", new { scope = "inspection-2", snapshotId = secondSnapshot }),
            "Closing an inspection tab releases its preview");
        Check((string?)await Call("table.insertScript", new { snapshotId = firstSnapshot }) == insert,
            "Closing one inspection tab preserves other previews");
        await Reject<Microsoft.Data.SqlClient.SqlException>(async () => await Call("table.dropColumns", new { targetId = target.Id, table = "insert_fixture", columns = new[] { "spare", "id" } }), "Dependent column drop is rejected atomically");
        await Call("table.dropColumns", new { targetId = target.Id, table = "insert_fixture", columns = new[] { "spare" } });
        var afterDrop = (ResultSet)(await Call("table.structure", new { targetId = target.Id, table = "insert_fixture" }))!;
        Check(afterDrop.Rows.All(row => row[0] != "spare") && afterDrop.Rows.Any(row => row[0] == "id"), "Drop column changes only the explicitly selected column");
        Check(richTemplate.Single(f=>f.Name=="created") is {Size:"3",NullDefault:true},"Template copy preserves datetime2 scale and DEFAULT NULL");
        await Call("sql.execute",new{targetId=target.Id,sql="ALTER TABLE dbo.child_fixture ADD CHECK(parent_id>0)"});
        await Reject<InvalidOperationException>(async()=>await Call("catalog.duplicate",new{targetId=target.Id,schema="dbo",table="child_fixture"}),"Unsupported template constraints fail rather than disappear");
        var alteredPreview = JsonSerializer.SerializeToElement(await Call("catalog.preview", new { targetId = target.Id, schema = "dbo", table = "catalog", creating = false, signature, fields = new[] { new CatalogField("extra") }, rows = Array.Empty<string[]>() }), Protocol.Json);
        await Call("sql.execute", new { targetId = target.Id, sql = "ALTER TABLE dbo.catalog ADD external_change int NULL" });
        await Reject<InvalidOperationException>(async () => await Call("catalog.commit", new { targetId = target.Id, previewId = alteredPreview.GetProperty("previewId").GetString() }), "Catalog rechecks schema before write");
        var schemaResult = JsonSerializer.SerializeToElement(await Call("schema.objects", new {targetId=target.Id,query="",offset=0}),Protocol.Json);
        Check(schemaResult.GetProperty("rows").GetArrayLength()>0,"Database explorer loads real SQL objects");
        await Call("sql.execute", new { targetId = admin.Id, sql = $"ALTER DATABASE [{database}] SET COMPATIBILITY_LEVEL = 100" });
        var tableFixture = string.Join("\n", Enumerable.Range(0, 203).Select(i => $"CREATE TABLE dbo.[paging_{i:D3}](id int);"));
        await Call("sql.execute", new { targetId = target.Id, sql = tableFixture });
        async Task<ResultSet> ObjectPage(string query, int offset, bool content = false) =>
            (ResultSet)(await Call("schema.objects", new { targetId = target.Id, query, offset, content }))!;
        var firstPage = await ObjectPage("paging_", 0);
        var secondPage = await ObjectPage("paging_", 200);
        Check(firstPage.Rows.Length == 200 && secondPage.Rows.Select(r => r[1]).SequenceEqual(new[] { "paging_200", "paging_201", "paging_202" }),
            "Object search pages deterministically at compatibility level 100");
        Check((await ObjectPage("paging%", 0)).Rows.Length == 0, "Object search treats wildcard characters literally");
        await Call("sql.execute", new { targetId = target.Id, sql = "CREATE PROCEDURE dbo.search_fixture AS SELECT 'lscode_definition_marker';" });
        Check((await ObjectPage("lscode_definition_marker", 0)).Rows.Length == 0
            && (await ObjectPage("lscode_definition_marker", 0, content: true)).Rows.Single()[1] == "search_fixture",
            "Object definition search is opt-in");
        if (args.Contains("--profiler"))
        {
            var defaults = JsonSerializer.SerializeToElement(await Call("profiler.defaults", new { targetId = target.Id }), Protocol.Json);
            Check(defaults.GetProperty("filter").GetString() == database && defaults.GetProperty("authentication").GetString() == "Windows",
                "Profiler defaults use the selected database and authentication");
            var engine = args.Contains("--profiler-ssms18") ? "ssms18" :
                args.Contains("--profiler-sql-trace") ? "sqlTrace" : "auto";
            await Call("profiler.start", new { targetId = target.Id, useTargetCredentials = true, engine });
            try
            {
                await Call("sql.execute", new { targetId = target.Id, sql = "SELECT N'LSCODE_WEBVIEW_TRACE_FIXTURE' AS marker" });
                var captured = false;
                for (var i = 0; i < 40 && !captured; i++)
                {
                    await Task.Delay(250);
                    var events = JsonSerializer.SerializeToElement(await Call("profiler.read", new { }), Protocol.Json);
                    captured = events.GetProperty("events").EnumerateArray().Any(e => e.GetProperty("text").GetString()?.Contains("LSCODE_WEBVIEW_TRACE_FIXTURE") == true);
                }
                Check(captured, "Profiler captures live local SQL via selected provider");
            }
            finally { await Call("profiler.stop", new { }); }
            Check(true, "Profiler stop confirms native trace cleanup");
        }
        Console.WriteLine("Local SQL fixture completed: " + database);
    }
    finally
    {
        // This exact database was created above in this test, never a user-selected database.
        if (menuAppCreated)
            await Call("sql.execute", new { targetId = admin.Id, sql = $"ALTER DATABASE [{menuAppDatabase}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{menuAppDatabase}]" });
        await Call("sql.execute", new { targetId = admin.Id, sql = "ALTER DATABASE [" + database + "] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [" + database + "]" });
    }
}
{
    var folder = Path.Combine(root, "StudioDiscovery");
    var templates = Directory.CreateDirectory(Path.Combine(folder, "templates")).FullName;
    const string manifest = """{"projectName":"Discovery","schemaVersion":1,"templates":["registered"]}""";
    var manifestPath = Path.Combine(folder, "project.json");
    await File.WriteAllTextAsync(manifestPath, manifest);
    const string registered = """{"template":"registered","execute":"dbo.usp_Import","tables":[{"name":"#Rows","from":"$","mode":"object","columns":{"id":{"path":"$.id","type":"int"}}}]}""";
    await File.WriteAllTextAsync(Path.Combine(templates, "registered.json"), registered);
    var importedPath = Path.Combine(templates, "existing.json");
    await File.WriteAllTextAsync(importedPath, registered.Replace("registered", "existing", StringComparison.Ordinal));
    var invalidPath = Path.Combine(templates, "invalid.json");
    await File.WriteAllTextAsync(invalidPath, "{}");
    await using var studio = new DeveloperTools();
    async Task<JsonElement> StudioCall(string method, object payload) =>
        JsonSerializer.SerializeToElement(await studio.ExecuteAsync("templateStudio." + method,
            JsonSerializer.SerializeToElement(payload, Protocol.Json), default), Protocol.Json);
    var opened = await StudioCall("open", new { folder = templates });
    var projectElement = opened.GetProperty("project");
    Check(projectElement.GetProperty("templates").EnumerateArray().Select(item => item.GetProperty("templateCode").GetString())
        .SequenceEqual(new[] { "registered", "existing" }) &&
        opened.GetProperty("skippedTemplates")[0].GetString() == "invalid.json" &&
        await File.ReadAllTextAsync(manifestPath) == manifest,
        "Studio discovers templates absent from the manifest and reports invalid files without writing");
    var project = System.Text.Json.Nodes.JsonNode.Parse(projectElement.GetRawText())!;
    project["templates"]![1]!["templateCode"] = "renamed";
    var column = project["templates"]![1]!["tables"]![0]!["columns"]![0]!;
    column["defaultJson"] = "{";
    foreach (var invalidSave in new[] { await StudioCall("save", new { project }), await StudioCall("saveTemplate", new { project, template = 1 }) })
    {
        var issue = invalidSave.GetProperty("issues")[0];
        Check(!invalidSave.GetProperty("valid").GetBoolean() &&
            issue.GetProperty("templateCode").GetString() == "renamed" &&
            issue.GetProperty("table").GetString() == "Rows" &&
            issue.GetProperty("tableIndex").GetInt32() == 0 &&
            issue.GetProperty("field").GetString() == "id" &&
            issue.GetProperty("fieldIndex").GetInt32() == 0 &&
            issue.GetProperty("location").GetString() == "Default (JSON)" &&
            File.Exists(Path.Combine(folder, ".studio", "draft.json")) && await File.ReadAllTextAsync(manifestPath) == manifest,
            "Studio invalid save reports template, table, field and setting and retains the runtime manifest");
    }
    column["defaultJson"] = "";
    Check((await StudioCall("save", new { project })).GetProperty("valid").GetBoolean(), "Studio saves a project with a discovered template");
    var reopened = await StudioCall("open", new { folder });
    Check(reopened.GetProperty("project").GetProperty("templates").EnumerateArray()
        .Select(item => item.GetProperty("templateCode").GetString()).SequenceEqual(new[] { "registered", "renamed" }) &&
        !File.Exists(importedPath) && File.Exists(invalidPath),
        "Renamed discovered template is not rediscovered and invalid JSON is preserved");
}
if (Environment.GetEnvironmentVariable("LSCODE_STUDIO_SAMPLE") is { } studioSample)
{
    var studioFolder = Path.Combine(root, "Studio");
    Directory.CreateDirectory(studioFolder);
    foreach (var relative in new[] { "project.json", "templates/order.import.json", "samples/order.import.sample.json" })
    {
        var destination = Path.Combine(studioFolder, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        File.Copy(Path.Combine(studioSample, relative), destination);
    }
    await using var studio = new DeveloperTools();
    async Task<JsonElement> StudioCall(string method, object payload) =>
        JsonSerializer.SerializeToElement(await studio.ExecuteAsync("templateStudio." + method,
            JsonSerializer.SerializeToElement(payload, Protocol.Json), default), Protocol.Json);

    var opened = await StudioCall("open", new { folder = studioFolder });
    var project = System.Text.Json.Nodes.JsonNode.Parse(opened.GetProperty("project").GetRawText())!;
    var template = project["templates"]![0]!;
    template["execute"] = "dbo.usp_ImportOrderEdited";
    var preview = await StudioCall("json", new { template });
    Check(preview.GetProperty("json").GetString()!.Contains("dbo.usp_ImportOrderEdited"), "Studio renders edited runtime JSON");
    var mapped = await StudioCall("mapping", new { template });
    Check(mapped.GetProperty("tables").EnumerateObject().Any(), "Studio maps sample data through the WebView bridge");
    var inferred = await StudioCall("infer", new { sample = template["sampleJson"]!.GetValue<string>() });
    Check(inferred.GetProperty("tables").GetArrayLength() >= 2, "Studio proposes nested tables from sample JSON");
    var relationTemplate = System.Text.Json.Nodes.JsonNode.Parse(template.ToJsonString())!;
    relationTemplate["tables"]![0]!["columns"]![1]!["key"] = true;
    relationTemplate["tables"]![0]!["rowKey"] = false;
    var synchronized = await StudioCall("synchronize", new { template = relationTemplate, table = 0, generateMissing = true });
    var synchronizedTables = synchronized.GetProperty("tables");
    Check(synchronizedTables[1].GetProperty("columns").EnumerateArray().Any(column =>
        column.GetProperty("name").GetString() == "CustomerID" && column.GetProperty("path").GetString() == "@#Header.CustomerID"),
        "Studio materializes a new master key in the child table");
    Check(synchronizedTables[1].GetProperty("parentKey").GetProperty("pairs").GetArrayLength() == 2,
        "Studio refreshes composite child-parent key pairs when the master key changes");
    Check((await StudioCall("save", new { project })).GetProperty("valid").GetBoolean(), "Studio saves edited project");
    var reopened = await StudioCall("open", new { folder = studioFolder });
    Check(reopened.GetProperty("project").GetProperty("templates")[0].GetProperty("execute").GetString() == "dbo.usp_ImportOrderEdited",
        "Studio reopens the saved edit");
    var currentOnly = System.Text.Json.Nodes.JsonNode.Parse(reopened.GetProperty("project").GetRawText())!;
    currentOnly["templates"]![0]!["execute"] = "dbo.usp_CurrentOnly";
    var unsavedTemplate = System.Text.Json.Nodes.JsonNode.Parse(currentOnly["templates"]![0]!.ToJsonString())!;
    unsavedTemplate["id"] = Guid.NewGuid();
    unsavedTemplate["templateCode"] = "unsaved.template";
    currentOnly["templates"]!.AsArray().Add(unsavedTemplate);
    var savedCurrent = await StudioCall("saveTemplate", new { project = currentOnly, template = 0 });
    Check(savedCurrent.GetProperty("valid").GetBoolean(), "Studio saves one selected template");
    var afterCurrentSave = await StudioCall("open", new { folder = studioFolder });
    Check(afterCurrentSave.GetProperty("project").GetProperty("templates").GetArrayLength() == 1 &&
        afterCurrentSave.GetProperty("project").GetProperty("templates")[0].GetProperty("execute").GetString() == "dbo.usp_CurrentOnly",
        "Studio selected-template save leaves other unsaved templates out of the project");
    var exportedTemplate = await StudioCall("exportTemplate", new { template = afterCurrentSave.GetProperty("project").GetProperty("templates")[0] });
    Check(exportedTemplate.GetString()!.Contains("dbo.usp_CurrentOnly"), "Studio exports one selected runtime template");
    var newFolder = Path.Combine(root, "StudioNew");
    Directory.CreateDirectory(newFolder);
    var created = await StudioCall("create", new { folder = newFolder });
    Check(created.GetProperty("project").GetProperty("templates").GetArrayLength() == 0 &&
        File.Exists(Path.Combine(newFolder, "project.json")), "Studio creates a project in an empty folder");
}
Console.WriteLine($"{checks} WebView checks passed. Fixtures: {root}");
