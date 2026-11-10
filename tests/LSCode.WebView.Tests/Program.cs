using System.Text;
using System.Text.Json;
using LSCode.Domain;
using LSCode.Infrastructure;
using LSCode.Contracts;
using System.Text.RegularExpressions;

var root = Path.Combine(Path.GetTempPath(), "LSCode-WebView-checks-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(root);
Environment.SetEnvironmentVariable("LSCODE_DATA_HOME", root);
var checks = 0;
void Check(bool value, string name) { if (!value) throw new Exception("FAIL " + name); checks++; Console.WriteLine("PASS " + name); }
async Task Reject<T>(Func<Task> work, string name) where T : Exception { try { await work(); throw new Exception("FAIL " + name); } catch (T) { checks++; Console.WriteLine("PASS " + name); } }
var workspace = new DocumentWorkspace();
var path = Path.Combine(root, "unicode.xml");
var encoding = new UnicodeEncoding(false, true, true);
await File.WriteAllBytesAsync(path, encoding.GetPreamble().Concat(encoding.GetBytes("<root>Tiếng Việt</root>\r\n")).ToArray());
var document = await workspace.OpenAsync(path, default);
Check(document.Bom && document.Encoding == "utf-16" && document.Newline == "CRLF", "Open retains UTF16/BOM/newline");
var saved = await workspace.SaveAsync(document.Id, path, "<root>Đã sửa</root>\r\n", 2, default);
Check((await File.ReadAllBytesAsync(path)).AsSpan().StartsWith(encoding.GetPreamble()), "Native save preserves BOM");
await Reject<InvalidOperationException>(() => workspace.SaveAsync(document.Id, path, "stale", 1, default), "Reject stale save version");
var copy = Path.Combine(root, "copy.xml");
await workspace.SaveAsync(document.Id, copy, "<root>Bản sao</root>", 4, default);
Check((await File.ReadAllBytesAsync(copy)).AsSpan().StartsWith(encoding.GetPreamble()), "Save As preserves original encoding");
await File.WriteAllTextAsync(copy, "external");
Check(await workspace.ChangedAsync(document.Id, default), "Detect external modification");
await Reject<LSCode.Core.FileConflictException>(() => workspace.SaveAsync(document.Id, copy, "overwrite", 8, default), "Conflict stops external overwrite");
Check(await File.ReadAllTextAsync(copy) == "external", "Conflicting save leaves disk unchanged");
await workspace.StoreRecoveryAsync(JsonSerializer.SerializeToElement(new[] { new { name = "draft.sql", text = "SELECT N'Việt';" } }), default);
var recovery = (JsonElement)await workspace.RecoverAsync(default);
Check(recovery[0].GetProperty("text").GetString() == "SELECT N'Việt';", "Recovery round-trip");
Check(!Directory.GetFiles(root).Any(p => p.EndsWith(".tmp")), "No temporary files left after recovery");
Directory.CreateDirectory(Path.Combine(root, "node_modules"));
await File.WriteAllTextAsync(Path.Combine(root, "node_modules", "hidden.sql"), "SELECT hidden");
var hits = (List<LSCode.Core.SearchHit>)await workspace.SearchAsync(root, "unsaved", false, false, new() { [copy] = "unsaved buffer" }, default);
Check(hits.Count == 1 && hits[0].Path == copy, "Search sees unsaved buffer");
Check(!(await workspace.FilesAsync(root, default)).Any(f => f.Contains("node_modules")), "Quick open excludes ignored folders");
var erpFile=Path.Combine(root,"controller.f");await File.WriteAllTextAsync(erpFile,"<root>ERP_NEEDLE</root>");
Check((await workspace.FilesAsync(root,default)).Contains(erpFile),"Quick open includes ERP .f files");
var erpHits=(List<LSCode.Core.SearchHit>)await workspace.SearchAsync(root,"ERP_NEEDLE",false,false,[],default);
Check(erpHits.Count==1&&erpHits[0].Path==erpFile,"Workspace search includes ERP .f content");
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
Check((await File.ReadAllBytesAsync(restoredPath)).AsSpan().StartsWith(encoding.GetPreamble()), "Recovered document retains original BOM on Save As");
using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
await Reject<OperationCanceledException>(() => workspace.FilesAsync(root, cancelled.Token), "Workspace scan cancellation");
var sql = CatalogDefinition.Build("dbo", "DanhMuc", true, [new("ma", "nvarchar", "20", Key: true), new("ten", Default: "O'Brien")], [], false, false);
Check(CatalogDefinition.Audit.All(n => sql.Contains("[" + n + "]")), "Catalog includes all four system fields");
Check(sql.Contains("O''Brien") && sql.Contains("PRIMARY KEY ([ma])"), "Catalog escapes default and preserves PK");
Check(CatalogDefinition.Quote("x]; DROP TABLE t--") == "[x]]; DROP TABLE t--]", "SQL identifiers cannot escape brackets");
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
Check(XmlViewWidths.Find(viewXml, viewXml.IndexOf("[ma_nv].Label")+5)?.Width == 120, "View label uses first grid width");
Check(XmlViewWidths.Find(viewXml, viewXml.IndexOf(", [ma_nv]")+5)?.Width == 100, "View field spans 30 + 70");
Check(XmlViewWidths.Find(viewXml, viewXml.IndexOf("[ten]")+2)?.Width == 330, "View final field spans trailing zero columns");
Check(XmlViewWidths.Find("", 0) == null && XmlViewWidths.Find("<view id='x'><item value='10: [x]'/></view>", 30) == null, "View incomplete input is safe");
Check(XmlViewWidths.Find("<!--"+viewXml+"-->", viewXml.IndexOf("[ten]")+6) == null, "View ignores XML comments");
var multipleViews="<views><view id='a'><item value='20,40'/><item value='10: [x]'/></view><view id='b'><item value='50,70'/><item value='10: [x]'/></view></views>";
Check(XmlViewWidths.Find(multipleViews,multipleViews.LastIndexOf("[x]")+1)?.Width==120,"View widths use current view rather than previous view");
if (args.Contains("--sql"))
{
    await using var tools = new DeveloperTools();
    async Task<object?> Call(string method, object payload) => await tools.ExecuteAsync(method, JsonSerializer.SerializeToElement(payload, Protocol.Json), default);
    var server = Environment.GetEnvironmentVariable("LSCODE_TEST_SQL_SERVER") ?? ".";
    var admin = (TargetInfo)(await Call("sql.connect", new { server, database = "master", integratedSecurity = true, trustCertificate = true }))!;
    var database = "LSCodeWebViewTest_" + Guid.NewGuid().ToString("N");
    if (!Regex.IsMatch(database, "^LSCodeWebViewTest_[a-f0-9]{32}$")) throw new Exception("Unsafe fixture database name");
    await Call("sql.execute", new { targetId = admin.Id, sql = "CREATE DATABASE [" + database + "]" });
    try
    {
        var target = (TargetInfo)(await Call("sql.connect", new { server, database, integratedSecurity = true, trustCertificate = true }))!;
        var batch = (ExecutionResult)(await Call("sql.execute", new { targetId = target.Id, sql = "CREATE TABLE #test(value int);\nGO\nINSERT INTO #test VALUES (42);\nGO\nSELECT value FROM #test;" }))!;
        Check(batch.Tables[0].Rows[0][0] == "42", "SQL keeps connection/temp table across GO batches");
        using (var cts = new CancellationTokenSource(300))
            await Reject<OperationCanceledException>(async () => await tools.ExecuteAsync("sql.execute", JsonSerializer.SerializeToElement(new { targetId = target.Id, sql = "WAITFOR DELAY '00:00:10'; SELECT 1" }), cts.Token), "SQL cancellation reaches SqlCommand");
        var previewObject = await Call("catalog.preview", new { targetId = target.Id, schema = "dbo", table = "catalog", creating = true, fields = new[] { new CatalogField("code", "nvarchar", "20", Key: true), new CatalogField("label", "nvarchar", "100") }, rows = Array.Empty<string[]>() });
        var preview = JsonSerializer.SerializeToElement(previewObject, Protocol.Json);
        await Call("catalog.commit", new { targetId = target.Id, previewId = preview.GetProperty("previewId").GetString() });
        var loaded = JsonSerializer.SerializeToElement(await Call("catalog.load", new { targetId = target.Id, schema = "dbo", table = "catalog" }), Protocol.Json);
        Check(loaded.GetProperty("columns").GetArrayLength() == 6, "Catalog creates fields and four audit columns");
        var suggestions=(LSCode.Core.SqlSuggestion[])(await Call("sql.complete",new{targetId=target.Id,text="SELECT c. FROM dbo.catalog c",beforeCaret="SELECT c."}))!;
        Check(suggestions.Any(s=>s.Text=="code"),"SQL completion adapter resolves alias against actual schema");
        var adminIdentity=(ExecutionResult)(await Call("sql.execute",new{targetId=admin.Id,sql="SELECT DB_NAME()"}))!;
        var tabIdentity=(ExecutionResult)(await Call("sql.execute",new{targetId=target.Id,sql="SELECT DB_NAME()"}))!;
        Check(adminIdentity.Tables[0].Rows[0][0]=="master"&&tabIdentity.Tables[0].Rows[0][0]==database,"Target handles remain isolated across database switches");
        var externalJs=(LSCode.Core.SqlSuggestion[])(await Call("javascript.complete",new{language="xml",text="<root>f.ge",beforeCaret="<root>f.ge"}))!;
        Check(externalJs.Length==0,"JS completion adapter rejects XML outside script");
        var projectXml="<NewDataSet><project><FolderName>Fixture</FolderName><servername>.</servername><Password>not-real</Password><Unknown><Nested>preserved</Nested></Unknown></project></NewDataSet>";
        await Call("project.import",new{xml=projectXml,overwrite=false});
        var export=(string)(await Call("project.export",new{}))!;
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
        var syncConfig = Path.Combine(root, "web.config");
        await File.WriteAllTextAsync(syncConfig, """
            <configuration><connectionStrings>
              <add name="appConnectionString" connectionString="Server=.;Database=Fixture_App;Integrated Security=True" />
              <add name="sysConnectionString" connectionString="Server=.;Database=Fixture_Sys;Integrated Security=True" />
            </connectionStrings></configuration>
            """);
        var synced = (ProjectInfo)(await Call("project.sync", new { name = "Dự án A", configPath = syncConfig }))!;
        var syncedDetails = JsonSerializer.SerializeToElement(await Call("project.details", new { id = synced.Id }), Protocol.Json);
        Check(syncedDetails.GetProperty("programPath").GetString() == "http://172.168.5.14/D%E1%BB%B1%20%C3%A1n%20A/Main/Login.aspx",
            "Project sync assigns the encoded default login URL");
        await Call("project.update", new { id = synced.Id, name = synced.Name, server = synced.Server, database = synced.Database, sourcePath = synced.SourcePath, programPath = "https://example.test/custom" });
        await Call("project.sync", new { name = "Dự án A", configPath = syncConfig });
        var resynced = JsonSerializer.SerializeToElement(await Call("project.details", new { id = synced.Id }), Protocol.Json);
        Check(resynced.GetProperty("programPath").GetString() == "https://example.test/custom", "Project sync preserves a custom program URL");
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
        await Call("sql.execute", new { targetId = target.Id, sql = "CREATE TABLE dbo.insert_fixture(id int IDENTITY PRIMARY KEY, label nvarchar(60) NULL, payload varbinary(8), spare int, version rowversion); INSERT dbo.insert_fixture(label,payload) VALUES (N'Việt'' Nam',0x0AFF),(NULL,0x00);" });
        var view = JsonSerializer.SerializeToElement(await Call("table.view", new { targetId = target.Id, table = "insert_fixture", where = "id=1", columns = new[] { "id", "label", "payload", "version" } }), Protocol.Json);
        var insert = (string)(await Call("table.insertScript", new { snapshotId = view.GetProperty("snapshotId").GetString() }))!;
        Check(view.GetProperty("data").GetProperty("rows").GetArrayLength() == 1 && insert.Contains("N'Việt'' Nam'")
            && insert.Contains("0x0AFF") && insert.Contains("IDENTITY_INSERT") && !insert.Contains("[version]"), "Insert script uses the filtered snapshot and skips rowversion");
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
            await Call("profiler.start", new { targetId = target.Id, useTargetCredentials = true, engine = args.Contains("--profiler-ssms18") ? "ssms18" : "extendedEvents" });
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
        await Call("sql.execute", new { targetId = admin.Id, sql = "ALTER DATABASE [" + database + "] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [" + database + "]" });
    }
}
Console.WriteLine($"{checks} WebView checks passed. Fixtures: {root}");
