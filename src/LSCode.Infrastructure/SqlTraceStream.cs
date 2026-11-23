using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Data;
using System.Globalization;
using System.Text;
using LSCode.Core;
using LSCode.Shell.Services;
using Microsoft.Data.SqlClient;

namespace LSCode.Infrastructure;

internal sealed class SqlTraceStream(SqlTarget target, string databasePrefix)
{
    private const int QueueLimit = 5000;
    private const string ConfigureSql = """
        DECLARE @result int;
        DECLARE @columns table (id int);
        INSERT @columns VALUES (1), (8), (10), (11), (12), (13), (14), (16), (17), (18), (35);
        DECLARE @event int, @column int, @enabled bit = 1;
        DECLARE trace_columns CURSOR LOCAL FAST_FORWARD FOR
            SELECT eventid, id FROM (VALUES (10), (12)) AS trace_events(eventid) CROSS JOIN @columns;
        OPEN trace_columns;
        FETCH NEXT FROM trace_columns INTO @event, @column;
        WHILE @@FETCH_STATUS = 0
        BEGIN
            EXEC @result = sys.sp_trace_setevent @traceId, @event, @column, @enabled;
            IF @result <> 0
            BEGIN
                RAISERROR(N'Không cấu hình được cột SQL Trace.', 16, 1);
                RETURN;
            END;
            FETCH NEXT FROM trace_columns INTO @event, @column;
        END;
        CLOSE trace_columns;
        DEALLOCATE trace_columns;
        EXEC @result = sys.sp_trace_setfilter @traceId, 35, 0, 6, @database;
        IF @result <> 0
        BEGIN
            RAISERROR(N'Không đặt được bộ lọc database cho SQL Trace.', 16, 1);
            RETURN;
        END;
        EXEC @result = sys.sp_trace_setstatus @traceId, 1;
        IF @result <> 0
        BEGIN
            RAISERROR(N'Không bắt đầu được SQL Trace.', 16, 1);
            RETURN;
        END;
        """;
    private const string DropSql = """
        DECLARE @result int;
        EXEC @result = sys.sp_trace_setstatus @traceId, 0;
        IF @result NOT IN (0, 9)
        BEGIN
            RAISERROR(N'Không dừng được phiên SQL Trace.', 16, 1);
            RETURN;
        END;
        EXEC @result = sys.sp_trace_setstatus @traceId, 2;
        IF @result NOT IN (0, 9)
        BEGIN
            RAISERROR(N'Không đóng được phiên SQL Trace.', 16, 1);
            RETURN;
        END;
        """;
    private readonly ConcurrentQueue<ProfilerEvent> events = new();
    private readonly CancellationTokenSource stop = new();
    private int traceId;
    private long sequence;
    private long dropped;
    private Task? reader;

    private string ConnectionString => new SqlConnectionStringBuilder(target.ConnectionString)
    {
        InitialCatalog = "master", ApplicationName = "LSCode.Profiler", ConnectTimeout = 10, Pooling = false
    }.ConnectionString;

    public async Task StartAsync(CancellationToken token)
    {
        if (string.IsNullOrWhiteSpace(databasePrefix))
            throw new InvalidOperationException("Chọn database trước khi bắt đầu SQL Trace.");

        var connection = new SqlConnection(ConnectionString);
        try
        {
            await connection.OpenAsync(token);
            await ConfigureAsync(connection, token);
            reader = ReadStreamAsync(connection, stop.Token);
        }
        catch
        {
            await connection.DisposeAsync();
            throw;
        }
    }

    private async Task ConfigureAsync(SqlConnection connection, CancellationToken token)
    {
        using var create = new SqlCommand("sys.sp_trace_create", connection) { CommandType = CommandType.StoredProcedure };
        var id = create.Parameters.Add("@traceid", SqlDbType.Int);
        id.Direction = ParameterDirection.Output;
        create.Parameters.Add("@options", SqlDbType.Int).Value = 1;
        create.Parameters.Add("@tracefile", SqlDbType.NVarChar, 245).Value = DBNull.Value;
        var result = create.Parameters.Add("@result", SqlDbType.Int);
        result.Direction = ParameterDirection.ReturnValue;
        await create.ExecuteNonQueryAsync(token);
        if (result.Value is not 0 || id.Value is not int created || created <= 0)
            throw new InvalidOperationException($"SQL Server từ chối tạo SQL Trace (mã {result.Value}).");
        traceId = created;

        using var setup = new SqlCommand(ConfigureSql, connection);
        setup.Parameters.Add("@traceId", SqlDbType.Int).Value = traceId;
        setup.Parameters.Add("@database", SqlDbType.NVarChar, 1024).Value =
            databasePrefix.Replace("[", "[[]").Replace("_", "[_]").Replace("%", "[%]") + "%";
        await setup.ExecuteNonQueryAsync(token);
    }

    private async Task ReadStreamAsync(SqlConnection connection, CancellationToken token)
    {
        await using var owner = connection;
        await using var command = new SqlCommand("sys.sp_trace_getdata", connection)
        {
            CommandType = CommandType.StoredProcedure, CommandTimeout = 0
        };
        command.Parameters.Add("@traceid", SqlDbType.Int).Value = traceId;
        command.Parameters.Add("@records", SqlDbType.Int).Value = 0;
        await using var rows = await command.ExecuteReaderAsync(CommandBehavior.SingleResult, token);
        var fields = new Dictionary<int, byte[]>();
        var eventClass = 0;
        while (await rows.ReadAsync(token))
        {
            var column = rows.GetInt32(0);
            if (column > 64)
            {
                if (eventClass is 10 or 12) Publish(eventClass, fields);
                fields.Clear();
                eventClass = column == 65526 && rows.GetValue(2) is byte[] { Length: >= 2 } marker
                    ? BinaryPrimitives.ReadUInt16LittleEndian(marker) : 0;
            }
            else if (eventClass != 0 && rows.GetValue(2) is byte[] value)
                fields[column] = value;
        }
        if (!token.IsCancellationRequested) throw new IOException("Luồng SQL Trace đã kết thúc trên server.");
    }

    private void Publish(int eventClass, IReadOnlyDictionary<int, byte[]> fields)
    {
        string Text(int id) => fields.TryGetValue(id, out var bytes) ? Encoding.Unicode.GetString(bytes).TrimEnd('\0') : "";
        long Number(int id) => fields.GetValueOrDefault(id) switch
        {
            { Length: 8 } bytes => BinaryPrimitives.ReadInt64LittleEndian(bytes),
            { Length: 4 } bytes => BinaryPrimitives.ReadInt32LittleEndian(bytes),
            _ => 0
        };
        var database = Text(35);
        var application = Text(10);
        if (!ProfilerDatabaseFilter.Matches(database, databasePrefix) || application == "LSCode.Profiler") return;

        var time = DateTime.Now;
        if (fields.GetValueOrDefault(14) is { Length: 16 } date)
        {
            int Part(int offset) => BinaryPrimitives.ReadUInt16LittleEndian(date.AsSpan(offset, 2));
            try { time = new DateTime(Part(0), Part(2), Part(6), Part(8), Part(10), Part(12), Part(14)); }
            catch (ArgumentOutOfRangeException) { }
        }
        events.Enqueue(new(++sequence, time, eventClass == 10 ? "RPC:Completed" : "SQL:BatchCompleted",
            database, Text(11), Text(8), application, Number(12).ToString(CultureInfo.InvariantCulture),
            Number(13) / 1000d, Number(16), Number(17), Number(18), Text(1)));
        while (events.Count > QueueLimit && events.TryDequeue(out _)) Interlocked.Increment(ref dropped);
    }

    public async Task<ProfilerBatch> ReadAsync()
    {
        if (reader is { IsFaulted: true }) await reader;
        var batch = new List<ProfilerEvent>();
        while (batch.Count < QueueLimit && events.TryDequeue(out var entry)) batch.Add(entry);
        return new(batch, false, Interlocked.Read(ref dropped));
    }

    public async Task DropAsync()
    {
        if (traceId != 0)
        {
            await using var connection = new SqlConnection(ConnectionString);
            await connection.OpenAsync();
            await using var command = new SqlCommand(DropSql, connection);
            command.Parameters.Add("@traceId", SqlDbType.Int).Value = traceId;
            await command.ExecuteNonQueryAsync();
            traceId = 0;
        }
        await stop.CancelAsync();
        if (reader is not null)
        {
            try { await reader; }
            catch (Exception) when (stop.IsCancellationRequested) { }
        }
        stop.Dispose();
    }
}
