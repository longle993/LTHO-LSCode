using System.Text;
using LSCode.Core;

var root = Path.Combine(Path.GetTempPath(), "LSCode-tests-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(root);
var passed = 0;
void Assert(bool value, string name) { if (!value) throw new Exception("FAILED: " + name); passed++; Console.WriteLine("PASS " + name); }
async Task Throws<T>(Func<Task> action, string name) where T : Exception
{
    try { await action(); } catch (T) { Assert(true, name); return; } throw new Exception("Expected " + typeof(T).Name + ": " + name);
}
try
{
    var clipboardRows = new[] { new[] { "Tiếng Việt", "tab\ttrong ô", "dòng\r\nmới", "\"trích dẫn\"", "" }, new[] { "0", "", "NULL", ":null", ":text:literal" } };
    var tsv = CatalogClipboard.Write(clipboardRows);
    Assert(CatalogClipboard.Read(tsv).SelectMany(r => r).SequenceEqual(clipboardRows.SelectMany(r => r)), "Catalog TSV quoted Unicode/tab/newline/quote/empty round trip");
    Assert(CatalogClipboard.Read("a\tb\r\n").Count == 1, "Catalog TSV final newline does not add a record");
    Assert(CatalogClipboard.Read("a\tb\n\n").Count == 2, "Catalog TSV interior empty row is preserved");
    Assert(CatalogClipboard.Read("a\t")[0].Length == 2, "Catalog TSV trailing empty column preserved");
    Assert(CatalogClipboard.Read("\"\"")[0][0] == "", "Catalog TSV quoted empty record preserved");
    foreach (var literal in new[] { ":null", ":default", ":empty", ":text:abc", "", "NULL", "=1+1" })
        Assert(CatalogClipboard.DecodeLiteral(CatalogClipboard.Literal(literal)) == literal, "Catalog literal token round trip " + literal);
    await Throws<InvalidOperationException>(() => Task.Run(() => CatalogClipboard.Read("\"not closed")), "Catalog malformed quoted clipboard rejected");
    await Throws<InvalidOperationException>(() => Task.Run(() => CatalogClipboard.Read("\"closed\"x")), "Catalog trailing quoted garbage rejected");
    await Throws<InvalidOperationException>(() => Task.Run(() => CatalogClipboard.Read(new string('ữ', CatalogClipboard.MaxBytes / 2))), "Catalog clipboard UTF-8 size enforced");
    await Throws<InvalidOperationException>(() => Task.Run(() => CatalogClipboard.Write(Enumerable.Repeat(new[] { "x" }, 1001))), "Catalog copy row limit rejects whole batch");
    await Throws<InvalidOperationException>(() => Task.Run(() => CatalogClipboard.Read(string.Join('\n', Enumerable.Repeat("x", 1002)))), "Catalog paste bounded before materializing large grid");
    var hintXml = "<config><Hint><HintItem object='$func' exact='1'><Item keyword='get' description='Read'><![CDATA[get({{0-name}}, {{1}})]]></Item></HintItem></Hint><Snippet><Item keyword='select'><![CDATA[select {{-}}]]></Item></Snippet></config>";
    var hints = CompletionSnippets.Read(hintXml);
    Assert(hints.Count == 2 && CompletionSnippets.Get(hints, "$func.ge").Single().Text == "get", "Legacy object hint parsing and dollar qualifier");
    Assert(!CompletionSnippets.Get(hints, "$FUNC.ge").Any(), "Exact JS object matching");
    Assert(CompletionSnippets.Get(hints, "sel").Single().Text == "select", "Global snippets exclude member hints");
    var legacySql = CompletionSnippets.Read("<config><Snippet><Item keyword='ssproc#'>create proc</Item><Item keyword='@@id' description='Session'>@@id</Item></Snippet></config>");
    Assert(CompletionSnippets.Get(legacySql, "ss").All(item => !item.Text.StartsWith("ss", StringComparison.OrdinalIgnoreCase)) &&
        CompletionSnippets.Get(legacySql, "@@").Any(item => item.Text == "@@id"),
        "Legacy ss templates live in Snippet tool while session variables remain SQL completions");
    var expansion = CompletionSnippets.Expand("get({{0-name}}, {{1}}){{|}}");
    Assert(expansion == ("get(name, )", 4, 4), "Snippet expansion selects first argument and removes markers");
    Assert(CompletionSnippets.Expand("select {{-}}") == ("select ", 7, 0), "Snippet cursor marker");
    Assert(CompletionSnippets.Expand("get({{0-name}}, {{1}})", true) == ("get({{0-name}}, {{1}})", 4, 10), "JS keeps literal placeholder text and selects the entire first marker");
    Assert(CompletionSnippets.Placeholders("f({{}}, {{-}}, {{|}}, {{0-row}})").Count == 4, "Recognize empty, cursor and named placeholders for Tab");
    foreach (var database in new[] { "DEMO_App", "DEMO_Sys", "DEMO_A", "DEMO_S", "DEMO_app", "DEMOSys", "DEMOApp" })
        Assert(ProfilerDatabaseFilter.Prefix(database) == "DEMO", "Profiler strips database suffix " + database);
    Assert(ProfilerDatabaseFilter.Prefix("SALES") == "SALES", "Do not strip an ordinary database's final letter");
    Assert(ProfilerDatabaseFilter.Matches("DEMO_Sys", "DEMO") && ProfilerDatabaseFilter.Matches("demo_app", "DEMO") && !ProfilerDatabaseFilter.Matches("OTHER_App", "DEMO"), "Profiler prefix includes Sys and App, excludes unrelated DB");
    Assert(!ProfilerDatabaseFilter.Matches("DEMOX_App", "DEMO_"), "Profiler prefix treats underscores literally");
    foreach (var markup in new[] { "<root><script><![CDATA[f.ge", "<SCRIPT type='text/javascript'>f.ge", "<script title='>'>f.ge" })
        Assert(CompletionSnippets.IsInScript(markup, markup.Length), "Script body completion: " + markup);
    foreach (var markup in new[] { "<root>f.ge", "<!-- <script> -->f.ge", "<scripture>f.ge", "<script>f.ge</script>", "<script attr='f.ge", "<![CDATA[<script>f.ge", "<script/>f.ge", "<root attr='<script>'>f.ge" })
        Assert(!CompletionSnippets.IsInScript(markup, markup.Length), "No JS hint outside script: " + markup);
    foreach (var encoding in new Encoding[] { new UTF8Encoding(false, true), new UTF8Encoding(true, true), new UnicodeEncoding(false, true, true), new UnicodeEncoding(true, true, true) })
    {
        var file = Path.Combine(root, Guid.NewGuid() + ".xml");
        var content = "<?xml version=\"1.0\"?><root>Tiếng Việt <![CDATA[a < b]]></root>\r\n";
        await File.WriteAllTextAsync(file, content, encoding);
        var loaded = await DocumentStore.OpenAsync(file);
        await DocumentStore.SaveAsync(file, loaded.Text, loaded);
        var after = await File.ReadAllBytesAsync(file);
        var expected = new UTF8Encoding(true, true);
        Assert(expected.GetPreamble().Concat(expected.GetBytes(content)).SequenceEqual(after),
            "XML saves in " + expected.WebName + " from " + encoding.WebName + " BOM=" + loaded.Bom);
    }
    if (OperatingSystem.IsWindows())
    {
        var sharedPath = Path.Combine(root, "shared.xml");
        await File.WriteAllTextAsync(sharedPath, "<root>original longer content</root>", new UTF8Encoding(true));
        var sharedSnapshot = await DocumentStore.OpenAsync(sharedPath);
        var originalBytes = await File.ReadAllBytesAsync(sharedPath);
        using (var reader = new FileStream(sharedPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
        {
            // Reproduce ReplaceFile failing because the reader denies FileShare.Delete.
            var saved = await DocumentStore.SaveAsync(sharedPath, "<root>new</root>", sharedSnapshot);
            Assert((await DocumentStore.OpenAsync(sharedPath)).Text == saved.Text, "Save XML with reader denying delete and truncate old tail");
            Assert(!Directory.EnumerateFiles(root, "*.lscode*").Any(), "Save creates no backup or temporary files");
            Assert(saved.Bom && (await DocumentStore.OpenAsync(sharedPath)).Bom, "Fallback preserves BOM");
            await DocumentStore.SaveAsync(sharedPath, "<root>second save</root>", saved);
            Assert((await DocumentStore.OpenAsync(sharedPath)).Text == "<root>second save</root>", "Repeated direct save succeeds");
        }
        var lockedSnapshot = await DocumentStore.OpenAsync(sharedPath);
        using (var reader = new FileStream(sharedPath, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            await Throws<IOException>(() => DocumentStore.SaveAsync(sharedPath, "must not write", lockedSnapshot), "Respect reader denying writes");
            Assert((await DocumentStore.OpenAsync(sharedPath)).Text == lockedSnapshot.Text, "Failed save preserves original file");
        }
        Assert(!Directory.EnumerateFiles(root, "*.tmp").Any(), "Save cleans temporary files after success and failure");
        var readOnlyPath = Path.Combine(root, "readonly.xml");
        await File.WriteAllTextAsync(readOnlyPath, "<root>original</root>");
        var readOnlySnapshot = await DocumentStore.OpenAsync(readOnlyPath);
        File.SetAttributes(readOnlyPath, FileAttributes.ReadOnly);
        try
        {
            await Throws<UnauthorizedAccessException>(() => DocumentStore.SaveAsync(readOnlyPath, "<root>edited</root>", readOnlySnapshot), "Read-only XML requires explicit overwrite");
            Assert((await DocumentStore.OpenAsync(readOnlyPath)).Text == readOnlySnapshot.Text, "Denied read-only save preserves original");
            var saved = await DocumentStore.SaveAsync(readOnlyPath, "<root>edited</root>", readOnlySnapshot, overwriteReadOnly: true);
            Assert((await DocumentStore.OpenAsync(readOnlyPath)).Text == saved.Text, "Explicit read-only XML save succeeds");
            Assert((File.GetAttributes(readOnlyPath) & FileAttributes.ReadOnly) != 0, "Restore read-only attribute after saving");
            Assert(!Directory.EnumerateFiles(root, "*.lscode*").Any(), "Read-only save creates no sidecar files");
            await Throws<FileConflictException>(() => DocumentStore.SaveAsync(readOnlyPath, "stale", readOnlySnapshot, overwriteReadOnly: true), "Read-only overwrite still rejects external conflict");
            Assert((File.GetAttributes(readOnlyPath) & FileAttributes.ReadOnly) != 0, "Restore read-only attribute after failed save");
        }
        finally { File.SetAttributes(readOnlyPath, FileAttributes.Normal); }
    }
    var conflictPath = Path.Combine(root, "conflict.txt");
    await File.WriteAllTextAsync(conflictPath, "original"); var initial = await DocumentStore.OpenAsync(conflictPath);
    await File.WriteAllTextAsync(conflictPath, "external");
    await Throws<FileConflictException>(async () => await DocumentStore.SaveAsync(conflictPath, "mine", initial), "External edit conflict");
    Assert(await File.ReadAllTextAsync(conflictPath) == "external", "Conflict does not overwrite disk");
    var saveAs = Path.Combine(root, "copy.txt"); await DocumentStore.SaveAsync(saveAs, "mine", initial);
    Assert(await File.ReadAllTextAsync(saveAs) == "mine", "Save As after conflict");
    var invalid = Path.Combine(root, "invalid.txt"); await File.WriteAllBytesAsync(invalid, [0xff, 0x81, 0x00]);
    await Throws<DecoderFallbackException>(async () => await DocumentStore.OpenAsync(invalid), "Reject lossy unknown encoding");
    var batches = SqlBatches.Split("SELECT 'a\nGO\nb';\nGO\n/* nested /* GO */\nGO\n*/\nSELECT [GO], \"GO\";\nGO 2 -- repeat\nSELECT 3");
    Assert(batches.Count == 4 && batches[0].StartLine == 1 && batches[1].StartLine == 5 && batches[3].StartLine == 10, "GO string/comment/nesting/repeat and line offsets");
    Assert(SqlBatches.Split("SELECT 'it''s GO';\nGO\nSELECT 2").Count == 2, "Escaped SQL quote");
    Assert(SqlBatches.ErrorLocation(4, 2) == (5, 1), "SQL error after GO maps to document line");
    Assert(SqlBatches.ErrorLocation(4, 2, 10, 8) == (14, 1), "Selected SQL plus GO maps to source line");
    Assert(SqlBatches.ErrorLocation(1, 1, 10, 8) == (10, 8), "First selected SQL line preserves selection column");
    Assert(SqlBatches.ErrorLocation(1, 0) == (0, 0), "SQL error without a line is not navigable");
    await Throws<ArgumentException>(() => Task.FromResult(SqlBatches.Split("SELECT 1\nGO 101")), "Bound GO repeat");
    Assert(XmlTools.Validate("<root><x></root>")?.Line == 1, "XML diagnostic line");
    var includeRoot = Path.Combine(root, "includes");
    Directory.CreateDirectory(Path.Combine(includeRoot, "Include", "XML"));
    var includeMain = Path.Combine(includeRoot, "main.xml");
    var includePart = Path.Combine(includeRoot, "Include", "XML", "part.xml");
    var includeDtd = Path.Combine(includeRoot, "defs.dtd");
    await File.WriteAllTextAsync(includePart, "<part/>");
    const string includeDtdText = "<!ENTITY Report.Part SYSTEM 'part'>\n<!ENTITY Local 'inline'>\n<!ENTITY % cycle SYSTEM 'defs.dtd'>";
    await File.WriteAllTextAsync(includeDtd, includeDtdText);
    var includeText = "<!DOCTYPE root [<!ENTITY % defs SYSTEM 'defs.dtd'>%defs;]><root>&Report.Part;</root>";
    var externalEntity = XmlIncludes.Find(includeMain, includeText, includeText.IndexOf("&Report.Part;") + 3);
    Assert(externalEntity is { Path: var externalPath, Name: "Report.Part", Start: 0, Length: 7 } && externalPath == includePart, "F12 dotted entity returns the exact external file range");
    Assert(XmlIncludes.Find(includeMain, includeText, includeText.IndexOf("defs.dtd") + 2)?.Path == includeDtd, "F12 on SYSTEM path");
    Assert(XmlIncludes.Find(includeMain, includeText, includeText.IndexOf("%defs;") + 2)?.Path == includeDtd, "F12 parameter entity reference");
    Assert(XmlIncludes.Find(includeMain, includeText + "&missing;", includeText.Length + 2) == null, "Cyclic includes terminate for unresolved entity");
    var inlineText = includeText + "&Local;";
    var inlineEntity = XmlIncludes.Find(includeMain, inlineText, inlineText.Length - 3);
    Assert(inlineEntity is { Path: var inlinePath, Line: 2, Name: "Local" } && inlinePath == includeDtd &&
        includeDtdText.Substring(inlineEntity.Start, inlineEntity.Length) == "inline", "Inline entity returns only its declared value");
    var publicText = "<!DOCTYPE root PUBLIC 'public-id' 'defs.dtd'><root>&Report.Part;</root>";
    Assert(XmlIncludes.Find(includeMain, publicText, publicText.IndexOf("&Report.Part;") + 1)?.Path == includePart, "External PUBLIC DTD resolution");
    var commentText = "<!--\n<!ENTITY Local SYSTEM 'part'>\n-->\n<!ENTITY Local 'value'>\n&Local;";
    Assert(XmlIncludes.Find(includeMain, commentText, commentText.Length - 3)?.Line == 4, "Ignore commented declarations and preserve line numbers");
    Assert(XmlIncludes.Find(includeMain, "<root>&amp;</root>", 9) == null, "Built-in entity has no include target");
    Assert(XmlIncludes.Find(includeMain, "<!ENTITY Missing SYSTEM 'missing.xml'>", 12) == null, "Missing include does not navigate to itself");
    Assert(XmlIncludes.Find(includeMain, includeText, includeText.IndexOf("&Report.Part;") + 1,
        p => p == includeDtd ? "<!ENTITY Report.Part 'unsaved'>" : File.Exists(p) ? File.ReadAllText(p) : null)?.Path == includeDtd, "Include resolution uses unsaved buffers");
    var graphDtd = Path.Combine(includeRoot, "graph.dtd");
    var graphTarget = Path.Combine(includeRoot, "Include", "XML", "UnitBeforeInsert.txt");
    const string graphDtdText = "<!ENTITY UnitBeforeInsert SYSTEM 'UnitBeforeInsert'>\n<!ENTITY Job.Nested.Key.a '((ma_vv like rtrim(@ma_vv) + ''%''))'>";
    const string graphTargetText = "if exists(select 1 where &Job.Nested.Key.a;)";
    await File.WriteAllTextAsync(graphDtd, graphDtdText);
    await File.WriteAllTextAsync(graphTarget, graphTargetText);
    var graphMainText = "<!DOCTYPE root [<!ENTITY % graph SYSTEM 'graph.dtd'>%graph;]><root>&UnitBeforeInsert;</root>";
    var firstPeek = XmlIncludes.Find(includeMain, graphMainText, graphMainText.IndexOf("&UnitBeforeInsert;", StringComparison.Ordinal) + 2);
    Assert(firstPeek?.Path == graphTarget && firstPeek.Length == graphTargetText.Length, "First entity peek resolves an external body");
    var nestedPeek = XmlIncludes.Find(graphTarget, graphTargetText,
        graphTargetText.IndexOf("&Job.Nested.Key.a;", StringComparison.Ordinal) + 2,
        contextFile: includeMain, contextText: graphMainText);
    Assert(nestedPeek is { Path: var nestedPath, Name: "Job.Nested.Key.a" } && nestedPath == graphDtd &&
        graphDtdText.Substring(nestedPeek.Start, nestedPeek.Length) == "((ma_vv like rtrim(@ma_vv) + ''%''))",
        "Nested F12 keeps the root declaration graph and returns the child value only");
    Assert(XmlTools.Validate("<!DOCTYPE x [<!ENTITY a SYSTEM 'file:///secret'>]><x>&a;</x>") != null, "Block XML external entity");
    Assert(XmlTools.Format("<root><x><![CDATA[a < b]]></x></root>").Contains("<![CDATA[a < b]]>"), "Format preserves CDATA");
    Assert(XmlTools.Format("<root xml:space=\"preserve\">  <x/>  </root>").Contains("  <x"), "Preserve significant XML whitespace");
    var original = new Snippet("Tiếng Việt", "SQL", "SELECT '<x>';\r\n-- test", "Mô tả", 4, "legacy-param");
    var imported = SnippetImporter.Read(SnippetImporter.Write([original]));
    Assert(imported.Count == 1 && imported[0] == original, "Snippet lossless XML round-trip");
    var diff = TextDiff.Compare("a\nb\nc", "a\ninserted\nb\nc");
    Assert(diff.Count(r => r.Kind == "+") == 1 && diff.Count(r => r.Kind == "=") == 3, "Diff aligns insertion without cascading differences");
    Assert(TextDiff.Compare(" a ", "a").All(r => r.Kind != "="), "Diff preserves meaningful whitespace");
    var replacement = DiffPresentation.Create(TextDiff.Compare("same\nold\nremoved", "same\nnew"), true, false);
    Assert(replacement.Count == 3 && replacement[1] is { Kind: "~", Left: "old", Right: "new", LeftLine: 2, RightLine: 2 } && replacement[2].Kind == "−", "Split diff aligns replacements and unmatched removals");
    var manyLines = string.Join('\n', Enumerable.Range(0, 40).Select(i => "line " + i));
    var contextual = DiffPresentation.Create(TextDiff.Compare(manyLines, manyLines.Replace("line 20\n", "changed\n")), false, true);
    Assert(contextual.Count(r => r.Kind == "…") == 2 && contextual.Count(r => r.Kind == "=") == 6 && contextual.Any(r => r.RightLine == 21 && r.Kind == "+"), "GitHub context keeps three lines around changes and original line numbers");
    Assert(DiffPresentation.ChangedSpan("select old value", "select new value") == (7, 3), "Diff highlights changed characters only");
    var leftFolder = Path.Combine(root, "compare-left"); var rightFolder = Path.Combine(root, "compare-right");
    Directory.CreateDirectory(Path.Combine(leftFolder, "empty")); Directory.CreateDirectory(Path.Combine(rightFolder, "empty"));
    Directory.CreateDirectory(Path.Combine(leftFolder, "nested")); Directory.CreateDirectory(Path.Combine(rightFolder, "nested"));
    await File.WriteAllBytesAsync(Path.Combine(leftFolder, "nested", "same.bin"), [0, 255, 1]);
    await File.WriteAllBytesAsync(Path.Combine(rightFolder, "nested", "same.bin"), [0, 255, 1]);
    await File.WriteAllTextAsync(Path.Combine(leftFolder, "changed.txt"), "abc"); await File.WriteAllTextAsync(Path.Combine(rightFolder, "changed.txt"), "xyz");
    File.SetLastWriteTimeUtc(Path.Combine(rightFolder, "changed.txt"), File.GetLastWriteTimeUtc(Path.Combine(leftFolder, "changed.txt")));
    await File.WriteAllTextAsync(Path.Combine(leftFolder, "left.txt"), "left"); await File.WriteAllTextAsync(Path.Combine(rightFolder, "right.txt"), "right");
    Directory.CreateDirectory(Path.Combine(leftFolder, "conflict")); await File.WriteAllTextAsync(Path.Combine(rightFolder, "conflict"), "file");
    var folderRows = await FolderDiff.CompareAsync(leftFolder, rightFolder, CancellationToken.None);
    Assert(folderRows.Single(r => r.RelativePath == Path.Combine("nested", "same.bin")).Status == "Giống nhau", "Folder compare recursive binary equality");
    Assert(folderRows.Single(r => r.RelativePath == "changed.txt").Status == "Khác nội dung", "Folder compare detects same-length same-timestamp changes");
    Assert(folderRows.Single(r => r.RelativePath == "left.txt").Status == "Chỉ bên trái" && folderRows.Single(r => r.RelativePath == "right.txt").Status == "Chỉ bên phải", "Folder compare one-sided files");
    Assert(folderRows.Single(r => r.RelativePath == "empty").Status == "Thư mục" && folderRows.Single(r => r.RelativePath == "conflict").Status == "Khác loại", "Folder compare empty folders and file/directory conflict");
    await Throws<OperationCanceledException>(() => FolderDiff.CompareAsync(leftFolder, rightFolder, new CancellationToken(true)), "Folder compare cancellation");
    var projectXml = "<NewDataSet><project><FolderName>ERP</FolderName><servername>test</servername><df_Database>App</df_Database><Password>fixture-only</Password><AppData>Sys</AppData><CustomFlag>42</CustomFlag><Extra><Nested value=\"kept\"/></Extra></project></NewDataSet>";
    var importedProjects = ProjectImporter.Read(projectXml);
    Assert(importedProjects.Count == 1 && importedProjects[0].Field("CustomFlag") == "42" && importedProjects[0].RawXml.Contains("Nested"), "Project importer preserves unknown/nested metadata");
    var mergedProjects = ProjectImporter.Merge(importedProjects, importedProjects, false);
    Assert(mergedProjects.Added == 0 && mergedProjects.Skipped == 1 && mergedProjects.Projects.Count == 1, "Project import idempotence");
    var revisedProject = ProjectImporter.Read(projectXml.Replace("<CustomFlag>42", "<CustomFlag>43"));
    Assert(ProjectImporter.Merge(importedProjects, revisedProject, false).Projects[0].Field("CustomFlag") == "42", "Project merge keep existing");
    Assert(ProjectImporter.Merge(importedProjects, revisedProject, true).Projects[0].Field("CustomFlag") == "43", "Project merge explicit overwrite");
    Assert(!ProjectImporter.Export(importedProjects).Contains("fixture-only") && ProjectImporter.Export(importedProjects).Contains("Nested"), "Project export blanks credential fields, retains metadata");
    var catalogInput = Path.Combine(root, "catalog-input.xml");
    await File.WriteAllTextAsync(catalogInput, projectXml);
    var filePreview = ProjectImporter.PreviewFile([], catalogInput, false);
    Assert(filePreview is { Total: 1, Added: 1, Updated: 0, Skipped: 0 } &&
        ProjectImporter.ReadFile(catalogInput).Single().Field("CustomFlag") == "42",
        "Project file import streams XML and returns compact counts");
    var catalogOutput = Path.Combine(root, "catalog-output.xml");
    Assert(ProjectImporter.ExportFile(importedProjects, catalogOutput) == 1 &&
        (await File.ReadAllTextAsync(catalogOutput)).Contains("Nested") &&
        !(await File.ReadAllTextAsync(catalogOutput)).Contains("fixture-only") &&
        !Directory.EnumerateFiles(root, ".catalog-output.xml.*.tmp").Any(),
        "Project file export replaces atomically and redacts credentials");
    var dtdCatalog = Path.Combine(root, "catalog-dtd.xml");
    await File.WriteAllTextAsync(dtdCatalog, "<!DOCTYPE x [<!ENTITY e 'x'>]><NewDataSet/>");
    await Throws<System.Xml.XmlException>(() => Task.Run(() => ProjectImporter.ReadFile(dtdCatalog)),
        "Project file import prohibits DTD processing");
    var customerRoot = Path.Combine(root, "CustomerPro");
    var application = Path.Combine(customerRoot, "FBI", "DEMO", "FBISP24"); Directory.CreateDirectory(application);
    var configPath = Path.Combine(application, "web.config");
    await File.WriteAllTextAsync(configPath, "<configuration><connectionStrings><add name=\"appConnectionString\" connectionString=\"Database=%Database\"/><add name=\"sysConnectionString\" connectionString=\"Database=DEMO_S\"/></connectionStrings></configuration>");
    var foundProjects = ProjectDiscovery.Find(customerRoot, "DEMO", CancellationToken.None);
    Assert(foundProjects.Count == 1 && foundProjects[0].Folder == application, "Project discovery searches category/project/application");
    Assert(ProjectDiscovery.ReadConnections(configPath).Sys == "Database=DEMO_S", "Read named web.config connections without substituting app placeholders");
    var externalConfig = Path.Combine(application, "connections.config");
    await File.WriteAllTextAsync(externalConfig, "<connectionStrings><add name='removed' connectionString='Database=OLD'/><clear/><add name='removed2' connectionString='Database=OLD2'/><remove name='removed2'/><add name='ExportFile' connectionString='Server=fixture;Database=DEMO_App;Uid=fixture;Pwd=fixture-only' providerName='System.Data.SqlClient'/></connectionStrings>");
    var externalWeb = Path.Combine(application, "external.config");
    await File.WriteAllTextAsync(externalWeb, "<configuration><connectionStrings configSource='connections.config'/></configuration>");
    var allConnections = ProjectDiscovery.ReadAllConnections(externalWeb);
    Assert(allConnections.Count == 1 && allConnections[0].Name == "ExportFile" && allConnections[0].ProviderName == "System.Data.SqlClient", "Read configSource and honor clear/remove for configured databases");
    await Throws<ArgumentException>(() => Task.FromResult(ProjectDiscovery.Find(customerRoot, "../escape", CancellationToken.None)), "Reject project traversal");
    var secondApplication = Path.Combine(customerRoot, "FBI", "DEMO", "FBISP25"); Directory.CreateDirectory(secondApplication); File.Copy(configPath, Path.Combine(secondApplication, "web.config"));
    Assert(ProjectDiscovery.Find(customerRoot, "DEMO", CancellationToken.None).Count == 2, "Preserve multiple application candidates");
    var hierarchy = CommandHierarchy.Build([
        new("01", "", "Root", "", ""),
        new("01.01", "01", "Child", "", ""),
        new("01.01.01", "01.01", "Grandchild", "", ""),
        new("01.01.01.01", "01.01.01", "Fourth level", "", ""),
        new("orphan", "missing", "Orphan", "", ""),
        new("cycleA", "cycleB", "A", "", ""),
        new("cycleB", "cycleA", "B", "", "")]);
    var hierarchyRoot = hierarchy.Single();
    Assert(hierarchyRoot.Entry.Id == "01" && hierarchyRoot.Children.Single().Children.Single().Entry.Id == "01.01.01", "FSD menu tree maps root, child and grandchild");
    Assert(hierarchyRoot.Children.Single().Children.Single().Children.Count == 0, "FSD menu tree excludes fourth level, orphans and cycles");
    var mobileHierarchy = CommandHierarchy.BuildAll([
        new("M", "", "Mobile", "", "M"),
        new("M.1", "M", "Level 2", "", "M.1"),
        new("M.1.1", "M.1", "Level 3", "", "M.1.1"),
        new("M.1.1.1", "M.1.1", "Level 4", "", "M.1.1.1"),
        new("M.CYCLE", "M.CYCLE", "Cycle", "", "M.CYCLE")]);
    Assert(mobileHierarchy.Single().Children.Single().Children.Single().Children.Single().Entry.Id == "M.1.1.1",
        "Mobile menu keeps arbitrary tree depth without following cycles");
    var searchable = CommandHierarchy.Build([new("1", "", "Kế toán", "", "", "root"), new("2", "1", "Phiếu thu", "", "", "GLTran")]);
    Assert(CommandHierarchy.Filter(searchable, "gltran").Single().Children.Single().Entry.Id == "2", "Menu sysid search preserves ancestor path");
    Assert(CommandHierarchy.Filter(searchable, "phiếu").Count == 1 && CommandHierarchy.Filter(searchable, "missing").Count == 0, "Menu name search and no-result state");
    SqlSymbol[] symbols = [new("dbo", "orders", null, "table"), new("dbo", "orders", "order_id", "int"), new("dbo", "customers", "customer_id", "int")];
    Assert(SqlSuggestions.Get("SELECT o.", symbols, "SELECT o. FROM dbo.orders AS o").Single().Text == "order_id", "SQL alias column completion with FROM after caret");
    Assert(SqlSuggestions.Get("SELECT * FROM dbo.", symbols).Single().InsertText == "orders", "SQL schema-qualified object completion");
    Assert(SqlSuggestions.Get("SELECT * FROM d46", [new("dbo", "d46$000000", null, "TABLE")]).Single().InsertText == "dbo.d46$000000", "SQL completion inserts ERP table names without brackets");
    Assert(SqlSuggestions.Get("SEL", symbols).Any(s => s.Text == "SELECT"), "SQL syntax suggestions without schema qualifier");
    Assert(SqlSuggestions.Get("SELECT order_", symbols).Any(s => s.InsertText == "order_id"), "SQL unqualified column suggestions preserve underscore");
    Assert(ProjectConventions.Controllers("<FastBusiness:QueryExtender Controller='dmbp'/>").SequenceEqual(["dmbp"]), "QueryExtender fallback when ReportExtender absent");
    Assert(SqlSuggestions.Get("SELECT o.", symbols, "SELECT o. FROM [dbo].[orders] AS [o]").Single().Text == "order_id", "SQL bracketed identifier alias completion");
    var localSql = "DECLARE @from date, @to date\nCREATE TABLE #rows(id int)\nSELECT * INTO #copy FROM #rows";
    var localSuggestions = SqlSuggestions.Get("SELECT ", symbols, localSql);
    Assert(localSuggestions.Any(s => s.Text == "@from") && localSuggestions.Any(s => s.Text == "@to"), "SQL declared variable completion");
    Assert(localSuggestions.Any(s => s.Text == "#rows") && localSuggestions.Any(s => s.Text == "#copy"), "SQL temporary table completion");
    Assert(SqlSuggestions.Get("SELECT * FROM #", symbols, localSql).Any(s => s.Text == "#rows"), "SQL temporary table completion in FROM");
    const string procedureSql = """
        ALTER PROC [dbo].[Report]
            @DateFrom smalldatetime,
            @DateTo smalldatetime
        AS
        BEGIN
            SELECT 1 AS id INTO #temp;
            DECLARE @q nvarchar(4000) = 'SELECT 1 INTO #fake';
            -- SELECT 1 INTO #comment
            SELECT 1 AS id INTO #hddt;
        END
        """;
    Assert(SqlSuggestions.Get("WHERE value >= @DateF", symbols, procedureSql).Single().InsertText == "@DateFrom",
        "SQL completes procedure parameters by prefix");
    Assert(SqlSuggestions.Get("SELECT * FROM #", symbols, procedureSql).Select(s => s.InsertText).SequenceEqual(["#temp", "#hddt"]),
        "SQL completes temp tables from procedure bodies and ignores strings and comments");
    Assert(ProjectConventions.FindAppDatabase("ERP_Sys", ["ERP_App", "OTHER_A"]) == "ERP_App", "SYS to App suffix convention");
    Assert(ProjectConventions.FindAppDatabase("ERP_S", ["ERP_A", "ERP_App"]) == "ERP_A", "Prefer exact A suffix when both App databases exist");
    Assert(ProjectConventions.FindAppDatabase("ERP_TEST", ["ERP_A"]) == null, "Do not guess unrelated database prefix");
    Assert(ProjectConventions.Controllers("<%@ Page X='a' %><!-- <FastBusiness:ReportExtender Controller='Hidden'/> --><FastBusiness:QueryExtender Controller='Other'/><FastBusiness:ReportExtender ID=\"MainReport\" runat=\"server\" TargetControlID=\"panelReport\" ReadOnly=\"true\" Controller=\"IRTran\"/>").SequenceEqual(["IRTran"]), "Extract ASPX ReportExtender controller, ignore comments and QueryExtender");
    var webRoot = Path.Combine(root, "web"); Directory.CreateDirectory(Path.Combine(webRoot, "Main")); await File.WriteAllTextAsync(Path.Combine(webRoot, "Main", "voucher.aspx"), "fixture");
    Assert(ProjectConventions.ResolveMenuFile(webRoot, "voucher.aspx?x=1") == Path.Combine(webRoot, "Main", "voucher.aspx"), "Menu link resolves Main ASPX");
    Assert(ProjectConventions.ResolveMenuFile(webRoot, "~/virtual/pages/voucher.aspx?x=1") == Path.Combine(webRoot, "Main", "voucher.aspx"),
        "Menu link with a virtual folder falls back to the matching Main ASPX");
    Assert(ProjectConventions.ResolveMenuFile(webRoot, "../../search.sql") == null, "Menu link cannot escape project");
    var metadataRoot = Path.Combine(root, "page-metadata");
    Directory.CreateDirectory(Path.Combine(metadataRoot, "AppData"));
    await File.WriteAllTextAsync(Path.Combine(metadataRoot, "AppData", "PageMetadata.xml"),
        "<pages><page id='invt' controller='Item'/><page id='other' controller='Other'/></pages>");
    Assert(ProjectConventions.ControllerFromPageMetadata(metadataRoot, "INVT") == "Item" &&
        ProjectConventions.ControllerFromPageMetadata(metadataRoot, "missing") == null,
        "PageMetadata resolves exact sysid without matching another page");
    var metadataIncludes = Directory.CreateDirectory(Path.Combine(metadataRoot, "AppData", "Metadata")).FullName;
    await File.WriteAllTextAsync(Path.Combine(metadataIncludes, "Config.ent"), "<!ENTITY Page.Title 'ERP'>");
    await File.WriteAllTextAsync(Path.Combine(metadataIncludes, "Page.001.xml"),
        "<page id='invt' title='&Page.Title;' controller='ItemIncluded'/><page id='arkh' controller='Customer'/><page id='Customer' controller='Wrong'/>");
    await File.WriteAllTextAsync(Path.Combine(metadataRoot, "AppData", "PageMetadata.xml"),
        "<!DOCTYPE pages [<!ENTITY % Config SYSTEM 'Metadata/Config.ent'>%Config;<!ENTITY Page.001 SYSTEM 'Metadata/Page.001.xml'>]><pages>&Page.001;</pages>");
    Assert(ProjectConventions.ControllerFromPageMetadata(metadataRoot, "invt") == "ItemIncluded",
        "PageMetadata resolves page from nested DTD entity include");
    Assert(ProjectConventions.ControllerFromPageMetadata(metadataRoot, "Customer", "arkh.aspx") == "Customer",
        "PageMetadata uses link page id before menu sysid when they differ");
    Directory.CreateDirectory(Path.Combine(metadataRoot, "Main"));
    Assert(ProjectConventions.ControllerFromPageMetadata(metadataRoot, "invt") == null,
        "Legacy Main layout keeps ASPX controller resolution");
    Assert(ProjectConventions.GridControllers("<items controller='GLDetail' style='Grid'/><items style='AutoComplete' controller='Currency'/>").SequenceEqual(["GLDetail"]), "Controller file tree includes child grids only");
    Assert(TranslationText.ReadGoogle("""[[["Xin chào","Hello",null,null,1]],null,"en"]""") == "Xin chào", "Google translate payload keeps the full sentence");
    using var translateHandler = new TranslationHandler();
    using var translateHttp = new HttpClient(translateHandler);
    var translateService = new TranslationClient(translateHttp);
    Assert(await translateService.TranslateAsync("Hello & world", "en", "vi", CancellationToken.None) == "Xin chào", "Translation parses mocked HTTP response");
    await translateService.TranslateAsync("Hello & world", "en", "vi", CancellationToken.None);
    Assert(translateHandler.Calls == 1 && translateHandler.Body == "q=Hello+%26+world", "Translation caches identical requests and encodes text");
    using var limitedHandler = new TranslationHandler(true);
    using var limitedHttp = new HttpClient(limitedHandler);
    var limitedService = new TranslationClient(limitedHttp);
    try { await limitedService.TranslateAsync("first", "en", "vi", CancellationToken.None); throw new Exception("Expected rate limit"); }
    catch (TranslationRateLimitException error) { Assert(error.RetryAt > DateTimeOffset.UtcNow.AddSeconds(80), "Translation honors Retry-After"); }
    await Throws<TranslationRateLimitException>(() => limitedService.TranslateAsync("second", "en", "vi", CancellationToken.None), "Translation cooldown also blocks different text");
    Assert(limitedHandler.Calls == 1, "No automatic retry or extra HTTP call during cooldown");
    await Throws<ArgumentException>(() => translateService.TranslateAsync(new string('x', 4501), "en", "vi", CancellationToken.None), "Translation rejects overlong input without truncation");
    Assert(TranslationClient.BrowserUrl("auto", "vi", "a & b").Contains("text=a%20%26%20b"), "Browser translation fallback preserves text");
    Assert(TranslationText.ReadGoogle("""[[["Dòng 1\n","a"],["Dòng 2","b"]]]""") == "Dòng 1\nDòng 2", "Google translate joins every returned sentence");
    await File.WriteAllTextAsync(Path.Combine(root, "search.sql"), "one\nfind Tiếng Việt\nlast");
    Directory.CreateDirectory(Path.Combine(root, "bin")); await File.WriteAllTextAsync(Path.Combine(root, "bin", "ignored.sql"), "find");
    var hits = await WorkspaceSearch.FindAsync(root, "find", false, false, CancellationToken.None);
    Assert(hits.Count == 1 && hits[0].Line == 2 && hits[0].Column == 1, "Search line mapping and excluded directories");
    var overlay = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { [Path.Combine(root, "search.sql")] = "unsaved buffer" };
    Assert((await WorkspaceSearch.FindAsync(root, "unsaved", false, false, CancellationToken.None, overlay)).Count == 1, "Search includes unsaved open buffer");
    using var cts = new CancellationTokenSource(); cts.Cancel();
    await Throws<OperationCanceledException>(() => WorkspaceSearch.FindAsync(root, "find", false, false, cts.Token), "Search cancellation");
    Console.WriteLine($"\n{passed} checks passed.");
}
finally { Directory.Delete(root, true); }
