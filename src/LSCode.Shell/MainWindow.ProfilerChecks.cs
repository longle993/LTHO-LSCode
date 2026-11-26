using System.IO;
using System.Windows;
using LSCode.Shell.Services;
namespace LSCode.Shell;
public partial class MainWindow
{
    private async Task CheckObjectAndProfilerAsync(List<string> report)
    {
        var toolSettings = SettingsStore.Load(); var previousProfilerPath = toolSettings.Profiler18Path;
        var customCommon = Path.Combine(SettingsStore.Root, "custom-ssms", "Common7"); Directory.CreateDirectory(Path.Combine(customCommon, "IDE"));
        var customProfiler = Path.Combine(customCommon, "PROFILER.EXE");
        foreach (var file in new[] { customProfiler, Path.Combine(customCommon, "PFCLNT.DLL"), Path.Combine(customCommon, "IDE", "Microsoft.SqlServer.ConnectionInfo.dll"), Path.Combine(customCommon, "IDE", "Microsoft.SqlServer.ConnectionInfoExtended.dll") }) File.WriteAllText(file, "fixture");
        try
        {
            toolSettings.Profiler18Path = customProfiler; SettingsStore.Save(toolSettings);

            var versionTarget = new SqlTarget("fixture", "App", "Server=fixture;Database=App;Integrated Security=true;Encrypt=False");
            using (var versionRequest = System.Text.Json.JsonDocument.Parse(SqlTraceSession.BuildRequest(versionTarget, ProfilerTemplate.Default("App"))))
            {
                if (versionRequest.RootElement.GetProperty("ProfilerVersion").GetString() != "18" ||
                    versionRequest.RootElement.GetProperty("LibraryFolder").GetString() != Path.Combine(customCommon, "IDE")) throw new Exception("SSMS 18 path/version routing");
            }
            SqlTraceSession.ValidateProfilerPath(customProfiler);
            var definitions = Path.Combine(customCommon, "Profiler", "TraceDefinitions"); Directory.CreateDirectory(Path.Combine(definitions, "1033"));
            File.WriteAllText(Path.Combine(definitions, "Microsoft SQL Server TraceDefinition 12.0.0.xml"), "fixture-v1");
            File.WriteAllText(Path.Combine(definitions, "1033", "description.xml"), "localized-fixture");
            var prepared = SqlTraceSession.PrepareHost(Path.Combine(AppContext.BaseDirectory, "LSCode.TraceHost.exe"), Path.Combine(customCommon, "IDE"), SettingsStore.Root);
            if (!File.Exists(prepared) || !File.Exists(Path.Combine(Path.GetDirectoryName(prepared)!, "Profiler", "TraceDefinitions", "1033", "description.xml"))) throw new Exception("Trace host definitions layout missing");
            if (prepared != SqlTraceSession.PrepareHost(Path.Combine(AppContext.BaseDirectory, "LSCode.TraceHost.exe"), Path.Combine(customCommon, "IDE"), SettingsStore.Root)) throw new Exception("Trace cache not reused");
            File.WriteAllText(Path.Combine(definitions, "Microsoft SQL Server TraceDefinition 12.0.0.xml"), "fixture-v2");
            if (prepared == SqlTraceSession.PrepareHost(Path.Combine(AppContext.BaseDirectory, "LSCode.TraceHost.exe"), Path.Combine(customCommon, "IDE"), SettingsStore.Root)) throw new Exception("Trace cache did not refresh definitions");
            if (SqlTraceSession.LibraryFolder != Path.Combine(customCommon, "IDE") || SettingsStore.Load().Profiler18Path != customProfiler) throw new Exception("Profiler path persistence/resolution");
            File.Delete(Path.Combine(customCommon, "PFCLNT.DLL"));
            bool rejected = false; try { SqlTraceSession.ValidateProfilerPath(customProfiler); } catch (InvalidOperationException) { rejected = true; }
            if (!rejected) throw new Exception("Missing native trace library accepted");
        }
        finally { toolSettings.Profiler18Path = previousProfilerPath; SettingsStore.Save(toolSettings); }
        report.Add("PASS Profiler uses only SSMS 18 configured libraries and trace helper version");
        report.Add("PASS saved custom SSMS path resolves Common7/IDE and validates native dependencies");
        report.Add("PASS trace helper cache includes nested TraceDefinitions, reuses identical files and refreshes changed definitions");
        var defaultTarget = ProfilerStorage.Connection(new("fixture", "Fixture_App", "Server=fixture;Database=Fixture_App;Integrated Security=true;Encrypt=True"), ProfilerStorage.DefaultCredentials);
        var defaultLogin = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(defaultTarget.ConnectionString);
        if (defaultLogin.IntegratedSecurity || defaultLogin.UserID != "profile" || defaultLogin.Password != ProfilerStorage.DefaultCredentials.Password || defaultLogin.InitialCatalog != "Fixture_App" || defaultLogin.Encrypt != Microsoft.Data.SqlClient.SqlConnectionEncryptOption.Mandatory) throw new Exception("Profiler default identity/target mismatch");
        var defaultTemplate = ProfilerTemplate.Default(defaultTarget.Database);
        var originalTemplateBytes = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Template", "SQL2014.tdf"));
        var effectiveTemplateBytes = File.ReadAllBytes(defaultTemplate.TraceTemplatePath!);
        var reconstructed = new List<byte>(); int eventRecords = 0; var extraEvents = new HashSet<int>();
        for (int i = 0; i < effectiveTemplateBytes.Length;)
        {
            if (i >= 650 && i + 2 < effectiveTemplateBytes.Length && effectiveTemplateBytes[i] == 252 && effectiveTemplateBytes[i + 1] is 255 or 251)
            {
                int size = effectiveTemplateBytes[i + 2];
                if (size < 2 || i + 3 + size > effectiveTemplateBytes.Length || effectiveTemplateBytes[i + 3 + size - 2] != 35 || effectiveTemplateBytes[i + 3 + size - 1] != 0) throw new Exception("DatabaseName column missing from trace template");
                var eventId = effectiveTemplateBytes[i + 1] == 255
                    ? effectiveTemplateBytes[i + 3] | effectiveTemplateBytes[i + 4] << 8 : -1;
                if (eventId is 11 or 162)
                {
                    var columns = Enumerable.Range(0, (size - 2) / 2).Select(index => (int)effectiveTemplateBytes[i + 5 + index * 2]).ToArray();
                    if (eventId == 162 && !columns.Contains(20)) throw new Exception("Severity column missing from user error trace event");
                    extraEvents.Add(eventId); i += 3 + size; continue;
                }
                if (effectiveTemplateBytes[i + 1] == 255) eventRecords++;
                reconstructed.Add(effectiveTemplateBytes[i]); reconstructed.Add(effectiveTemplateBytes[i + 1]); reconstructed.Add((byte)(size - 2));
                reconstructed.AddRange(effectiveTemplateBytes.Skip(i + 3).Take(size - 2)); i += 3 + size;
            }
            else reconstructed.Add(effectiveTemplateBytes[i++]);
        }
        if (eventRecords != 6 || !extraEvents.SetEquals([11, 162]) || !reconstructed.SequenceEqual(originalTemplateBytes)) throw new Exception("Trace template changes original events/filters");
        report.Add("PASS SQL2014 template preserves its original events and adds RPC starting plus user error events");
        if (!File.Exists(defaultTemplate.TraceTemplatePath) || new FileInfo(defaultTemplate.TraceTemplatePath!).Length == 0 || defaultTemplate.DatabaseFilter != "Fixture") throw new Exception("Bundled SQL2014 template missing or wrong database filter");
        using (var defaultsJson = System.Text.Json.JsonDocument.Parse(SqlTraceSession.BuildRequest(defaultTarget, defaultTemplate)))
        {
            if (defaultsJson.RootElement.GetProperty("User").GetString() != "profile" || defaultsJson.RootElement.GetProperty("TemplatePath").GetString() != defaultTemplate.TraceTemplatePath) throw new Exception("Profiler bridge defaults mismatch");
        }
        report.Add("PASS Profiler defaults to profile SQL login and bundled SQL2014 template with current database");
        LSCode.Core.ProjectDiscovery.ConfiguredConnection[] bachviet = [
            new("appConnectionString", "Server=app-server;Database=%Database;Application Name=%UserID;Uid=wrong;Pwd=wrong", "System.Data.SqlClient"),
            new("sysConnectionString", "Server=sys-server;Database=BACHVIET_FBISP24_S;Uid=fixture-user;Pwd=fixture-password", "System.Data.SqlClient"),
            new("ExportFile", "Server=other;Database=UNRELATED", "System.Data.SqlClient")];
        foreach (var suffix in new[] { "_A", "_App" })
        {
            var pair = await ProjectDatabase.ResolveTargetsAsync(bachviet, (probe, names) =>
            {
                var login = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(probe.ConnectionString);
                if (probe.Server != "app-server" || probe.Database != "master" || login.UserID != "fixture-user" || login.Password != "fixture-password") throw new Exception("App must use app server and SYS credentials");
                if (!names.SequenceEqual(["BACHVIET_FBISP24_A", "BACHVIET_FBISP24_App"])) throw new Exception("Probe must be limited to two project candidates");
                return Task.FromResult<IReadOnlyList<string>>(["BACHVIET_FBISP24" + suffix]);
            });
            if (pair.Count != 2 || pair[0].Database != "BACHVIET_FBISP24" + suffix || pair[1].Database != "BACHVIET_FBISP24_S") throw new Exception("App first, SYS second, no extra databases");
        }
        var explicitPair = await ProjectDatabase.ResolveTargetsAsync([
            bachviet[0] with { ConnectionString = "Server=app-server;Database=Explicit_App;Uid=wrong;Pwd=wrong;Encrypt=True" }, bachviet[1]],
            (_, _) => throw new Exception("Concrete App must not probe server"));
        var explicitLogin = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(explicitPair[0].ConnectionString);
        if (explicitLogin.UserID != "fixture-user" || explicitLogin.Encrypt != Microsoft.Data.SqlClient.SqlConnectionEncryptOption.Mandatory) throw new Exception("Concrete App identity/TLS");
        report.Add("PASS BACHVIET placeholder resolves only A/App; defaults App; SYS identity; only App/SYS listed");
        var configured = ProjectDatabase.ConcreteTargets([
            new("appConnectionString", "Server=fixture;Database=%Database;User ID=app;Password=unused", "System.Data.SqlClient"),
            new("sysConnectionString", "Server=fixture;Database=DEMO_Sys;User ID=sys;Password=sys-fixture", "System.Data.SqlClient"),
            new("ExportFile", "Server=fixture;Database=DEMO_App;User ID=export;Password=export-fixture", "System.Data.SqlClient"),
            new("Duplicate", "Server=fixture;Database=demo_app;Integrated Security=true", "System.Data.SqlClient"),
            new("OtherProvider", "not a sql string", "Other.Provider")]);
        if (!configured.Select(t => t.Database).SequenceEqual(["DEMO_Sys", "DEMO_App"])) throw new Exception("Configured database scope");
        if (ProjectDatabase.DefaultAppTarget(configured, "DEMO_Sys", "DEMO_Sys")?.Database != "DEMO_App") throw new Exception("Saved SYS must not override default App");
        var bothApps = configured.Concat([new SqlTarget("fixture", "DEMO_A", configured[1].ConnectionString)]).ToList();
        if (ProjectDatabase.DefaultAppTarget(bothApps, "DEMO_Sys", "")?.Database != "DEMO_A") throw new Exception("Prefer A over App");
        if (ProjectDatabase.DefaultAppTarget(configured.Take(1).ToList(), "DEMO_Sys", "") != null) throw new Exception("Do not default to SYS");
        if (ProjectDatabase.DefaultAppTarget(configured, "UnusualSysName", "")?.Database != "DEMO_App") throw new Exception("App suffix fallback");
        var identity = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(configured[1].ConnectionString);
        if (identity.UserID != "sys" || identity.Password != "sys-fixture") throw new Exception("Configured database identity lost");
        var configuredRequest = SqlTraceSession.BuildRequest(configured[1], new ProfilerTemplate("Fixture", [], "DEMO_App", "fixture.tdf"));
        using (var requestJson = System.Text.Json.JsonDocument.Parse(configuredRequest))
        {
            if (requestJson.RootElement.GetProperty("User").GetString() != "sys") throw new Exception("Profiler did not use config identity");
        }
        report.Add("PASS config database scope excludes placeholders/duplicates and preserves credentials for profiler");
        var target = new SqlTarget("fixture", "App_A", "Server=fixture;Database=App_A;Integrated Security=true");
        var tab = AddDocument("object-script.sql", "SELECT 1;", sqlTarget: target);
        var initial = tab.Buffer.Text;
        ApplyObjectScript(tab, "CREATE VIEW dbo.fixture AS SELECT 2 AS value;", target);
        if (Active != tab || tab.SqlTarget != target || !tab.Buffer.Text.StartsWith("CREATE VIEW")) throw new Exception("Object script target/buffer mismatch");
        tab.Buffer.UndoStack.Undo(); if (tab.Buffer.Text != initial) throw new Exception("Object script replacement is not undoable");
        tabs.Remove(tab); Documents.Items.Remove(tab.Tab); tab.Dispose();
        report.Add("PASS DB object script uses explicit target and active undoable SQL buffer");
        var template = new ProfilerTemplate("Fixture", ["rpc_completed", "sql_batch_completed"], "App_A", "fixture.tdf"); template.Save();
        if (ProfilerTemplate.Load().DatabaseFilter != template.DatabaseFilter || ProfilerTemplate.Load().TraceTemplatePath != "fixture.tdf") throw new Exception("Template filter lost");
        var row = new ProfilerEvent(1, DateTime.Now, "rpc_completed", "App_A", "test", "host", "app", "3", 1, 2, 0, 1, "SELECT N'Tiếng Việt';");
        var path = Path.Combine(SettingsStore.Root, "fixture.lstrace"); ProfilerStorage.SaveTrace(path, new(1, "fixture", "App_A", template, [row]));
        if (ProfilerStorage.LoadTrace(path).Events.Single().Text != row.Text) throw new Exception("Trace round trip failed");
        var credentials = new ProfilerCredentials(false, "fixture", "fixture-not-a-real-password"); ProfilerStorage.SaveCredentials("fixture", credentials);
        if (ProfilerStorage.LoadCredentials("fixture") != credentials) throw new Exception("Profiler DPAPI round trip failed");
        report.Add("PASS Profiler template filters, trace round trip and DPAPI credentials");
        var fixturePassword = "fixture;pass=\"unicode-ệ\"";
        foreach (var trust in new[] { true, false })
        {
            var builder = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder { DataSource="fixture", UserID="TDTU_FAP", Password=fixturePassword, TrustServerCertificate=trust, Encrypt=trust ? Microsoft.Data.SqlClient.SqlConnectionEncryptOption.Mandatory : Microsoft.Data.SqlClient.SqlConnectionEncryptOption.Optional };
            var request = SqlTraceSession.BuildRequest(new SqlTarget("fixture","master",builder.ConnectionString), template);
            var info = new System.Diagnostics.ProcessStartInfo(Path.Combine(AppContext.BaseDirectory,"LSCode.TraceHost.exe"),"--validate-request") { UseShellExecute=false, CreateNoWindow=true, RedirectStandardInput=true, RedirectStandardOutput=true, StandardInputEncoding=new System.Text.UTF8Encoding(false), StandardOutputEncoding=System.Text.Encoding.UTF8 };
            using var host = System.Diagnostics.Process.Start(info)!;
            await host.StandardInput.WriteLineAsync(request); await host.StandardInput.FlushAsync(); host.StandardInput.Close();
            string output;
            try { output = await host.StandardOutput.ReadToEndAsync().WaitAsync(TimeSpan.FromSeconds(10)); await host.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10)); }
            finally { if (!host.HasExited) host.Kill(); }
            using var json = System.Text.Json.JsonDocument.Parse(output); var root=json.RootElement;
            if(host.ExitCode!=0 || root.GetProperty("Kind").GetString()!="validated" || root.GetProperty("User").GetString()!="TDTU_FAP" || root.GetProperty("TrustServerCertificate").GetBoolean()!=trust || root.GetProperty("Encrypt").GetBoolean()!=trust || root.GetProperty("PasswordLength").GetInt32()!=fixturePassword.Length) throw new Exception("Framework SQL Trace connection compatibility failed");
        }
        report.Add("PASS real Framework trace helper accepts structured credentials and both TLS settings");
        var diagnosticRequest = SqlTraceSession.BuildRequest(new SqlTarget("fixture", "App", "Server=fixture;Database=App;User ID=fixture;Password=diagnostic-secret"), template);
        var diagnosticInfo = new System.Diagnostics.ProcessStartInfo(Path.Combine(AppContext.BaseDirectory, "LSCode.TraceHost.exe"), "--validate-error") { UseShellExecute=false, CreateNoWindow=true, RedirectStandardInput=true, RedirectStandardOutput=true, StandardInputEncoding=new System.Text.UTF8Encoding(false), StandardOutputEncoding=System.Text.Encoding.UTF8 };
        using (var diagnostic = System.Diagnostics.Process.Start(diagnosticInfo)!)
        {
            await diagnostic.StandardInput.WriteLineAsync(diagnosticRequest); diagnostic.StandardInput.Close();
            try
            {
                var details = await diagnostic.StandardOutput.ReadToEndAsync().WaitAsync(TimeSpan.FromSeconds(10));
                await diagnostic.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10));
                if (diagnostic.ExitCode != 0 || !details.Contains("COMException") || !details.Contains("80004005") || !details.Contains("native cause") || details.Contains("diagnostic-secret")) throw new Exception("Reader diagnostics must preserve native cause and redact password");
            }
            finally { if (!diagnostic.HasExited) diagnostic.Kill(); }
        }
        report.Add("PASS trace reader diagnostics include nested native HRESULT and redact password");
        var preview = new ProfilerWindow(this, target, preview: true); preview.Show(); preview.UpdateLayout();
        preview.SmokeLayoutToggle();
        report.Add("PASS Profiler horizontal/vertical layout toggle retains selected event and SQL");
        var bitmap = new System.Windows.Media.Imaging.RenderTargetBitmap((int)preview.ActualWidth, (int)preview.ActualHeight, 96, 96, System.Windows.Media.PixelFormats.Pbgra32); bitmap.Render(preview);
        var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder(); encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));
        using (var stream = File.Create(Path.Combine(SettingsStore.Root, "profiler-window.png"))) encoder.Save(stream);
        await preview.StopAsync(); preview.Close();
        report.Add("PASS native Profiler window, event grid and SQL details render (fixture)");
    }
}
