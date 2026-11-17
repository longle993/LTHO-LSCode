using System.Data;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Xml.Linq;
using Microsoft.Data.SqlClient;

namespace LSCode.Shell.Services;
public sealed record ProfilerTemplate(string Name, string[] Events, string? DatabaseFilter = null, string? TraceTemplatePath = null)
{
    public static readonly string[] AllowedEvents = ["rpc_completed", "sql_batch_completed", "sql_statement_completed", "sp_statement_completed", "error_reported"];
    public static string FilePath => Path.Combine(SettingsStore.Root, "profiler-template.json");
    public static string BundledTemplatePath => Path.Combine(AppContext.BaseDirectory, "Template", "SQL2014.WithDatabase.tdf");
    public static ProfilerTemplate Default(string database) => new("SQL2014", ["rpc_completed", "sql_batch_completed"], LSCode.Core.ProfilerDatabaseFilter.Prefix(database), BundledTemplatePath);
    public static ProfilerTemplate Load()
    {
        var template = File.Exists(FilePath) ? JsonSerializer.Deserialize<ProfilerTemplate>(File.ReadAllText(FilePath)) : new("LS Code — Standard", ["rpc_completed", "sql_batch_completed"]);
        Validate(template); return template!;
    }
    public static void Validate(ProfilerTemplate? template)
    {
        if (template == null || string.IsNullOrWhiteSpace(template.Name) || template.Events == null || template.Events.Length == 0 || template.Events.Any(e => !AllowedEvents.Contains(e)))
            throw new InvalidOperationException("Template Profiler không hợp lệ. Chọn ít nhất một sự kiện được hỗ trợ.");
    }
    public void Save()
    {
        Validate(this); Directory.CreateDirectory(SettingsStore.Root);
        var temp = FilePath + ".tmp"; File.WriteAllText(temp, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true })); File.Move(temp, FilePath, true);
    }
}
public sealed record ProfilerEvent(long Sequence, DateTime Time, string Event, string Database, string Login, string Host, string Application, string Session, double DurationMs, long Reads, long Writes, double CpuMs, string Text);
public sealed record ProfilerBatch(IReadOnlyList<ProfilerEvent> Events, bool Truncated, long Dropped);
public sealed class ProfilerSession(SqlTarget target, ProfilerTemplate template)
{
    public string Name { get; } = "LSCode_" + Guid.NewGuid().ToString("N");
    private string ConnectionString => new SqlConnectionStringBuilder(target.ConnectionString) { ApplicationName = "LSCode.Profiler", ConnectTimeout = 10 }.ConnectionString;
    public static string DatabasePattern(string database) => "%" + database.Replace("[", "[[]").Replace("%", "[%]").Replace("_", "[_]") + "%";
    public static string CreateSql(string session, string database, ProfilerTemplate template)
    {
        ProfilerTemplate.Validate(template);
        var filter = (template.DatabaseFilter ?? DatabasePattern(database)).Replace("'", "''");
        var events = template.Events.Distinct().Select(e => "ADD EVENT sqlserver." + e + "(ACTION(package0.event_sequence,sqlserver.database_name,sqlserver.client_app_name,sqlserver.client_hostname,sqlserver.username,sqlserver.session_id,sqlserver.sql_text) WHERE ([sqlserver].[like_i_sql_unicode_string]([sqlserver].[database_name],N'" + filter + "') AND [sqlserver].[not_equal_i_sql_unicode_string]([sqlserver].[client_app_name],N'LSCode.Profiler')))" );
        return "CREATE EVENT SESSION " + ProjectDatabase.Quote(session) + " ON SERVER\n" + string.Join(",\n", events) + "\nADD TARGET package0.ring_buffer(SET max_memory=1024,max_events_limit=2000) WITH (MAX_MEMORY=4096 KB,EVENT_RETENTION_MODE=ALLOW_SINGLE_EVENT_LOSS,MAX_DISPATCH_LATENCY=1 SECONDS,STARTUP_STATE=OFF);\nALTER EVENT SESSION " + ProjectDatabase.Quote(session) + " ON SERVER STATE=START;";
    }
    public async Task StartAsync(CancellationToken token)
    {
        using var connection = new SqlConnection(ConnectionString); await connection.OpenAsync(token);
        using var command = new SqlCommand(CreateSql(Name, target.Database, template), connection) { CommandTimeout = 0 };
        await command.ExecuteNonQueryAsync(token);
    }
    public async Task<ProfilerBatch> ReadAsync(CancellationToken token)
    {
        using var connection = new SqlConnection(ConnectionString); await connection.OpenAsync(token);
        using var command = new SqlCommand("SELECT t.target_data FROM sys.dm_xe_sessions s JOIN sys.dm_xe_session_targets t ON t.event_session_address=s.address WHERE s.name=@name AND t.target_name=N'ring_buffer'", connection) { CommandTimeout = 0 };
        command.Parameters.AddWithValue("@name", Name);
        var data = await command.ExecuteScalarAsync(token);
        if (data is not string xml) throw new InvalidOperationException("Phiên Profiler không còn chạy trên server.");
        return Parse(xml);
    }
    public static ProfilerBatch Parse(string xml)
    {
        using var input = new StringReader(xml);
        using var reader = System.Xml.XmlReader.Create(input, new System.Xml.XmlReaderSettings { DtdProcessing = System.Xml.DtdProcessing.Prohibit, XmlResolver = null });
        var root = XElement.Load(reader);
        var events = root.Elements("event").Select(e => {
            string Value(string name) => e.Elements().FirstOrDefault(x => (string?)x.Attribute("name") == name)?.Element("value")?.Value ?? "";
            long Number(string name) => long.TryParse(Value(name), NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? n : 0;
            var text = Value("statement"); if (text.Length == 0) text = Value("batch_text"); if (text.Length == 0) text = Value("message"); if (text.Length == 0) text = Value("sql_text");
            return new ProfilerEvent(Number("event_sequence"), DateTime.Parse((string)e.Attribute("timestamp")!, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal).ToLocalTime(),
                (string?)e.Attribute("name") ?? "", Value("database_name"), Value("username"), Value("client_hostname"), Value("client_app_name"), Value("session_id"), Number("duration") / 1000d, Number("logical_reads"), Number("writes"), Number("cpu_time") / 1000d, text);
        }).OrderBy(e => e.Sequence).ToList();
        return new(events, (string?)root.Attribute("truncated") == "1", long.TryParse((string?)root.Attribute("droppedCount"), out var dropped) ? dropped : 0);
    }
    public async Task DropAsync()
    {
        using var connection = new SqlConnection(ConnectionString); await connection.OpenAsync();
        using var command = new SqlCommand("IF EXISTS (SELECT 1 FROM sys.server_event_sessions WHERE name=@name) DROP EVENT SESSION " + ProjectDatabase.Quote(Name) + " ON SERVER;", connection) { CommandTimeout = 0 };
        command.Parameters.AddWithValue("@name", Name); await command.ExecuteNonQueryAsync();
    }
}
