using System.Data;
using LSCode.Core;
using Microsoft.Data.SqlClient;

namespace LSCode.Shell.Services;

public sealed record QueryResult(List<DataTable> Tables, List<string> Messages, TimeSpan Duration, bool Truncated);
public sealed record SqlTarget(string Server, string Database, string ConnectionString);
public sealed record SqlExecutionError(string Message, int Line, string Procedure);
public sealed class SqlExecutionException(IReadOnlyList<SqlExecutionError> errors, Exception inner) : Exception(inner.Message, inner)
{
    public IReadOnlyList<SqlExecutionError> Errors { get; } = errors;
}
public static class SqlRunner
{
    public static string AffectedRowsMessage(int count) => $"Thực hiện lệnh thành công, đã thay đổi {count} dòng.";

    public static string ConnectionString(StudioSettings s, string password) => new SqlConnectionStringBuilder
    {
        DataSource = s.Server, InitialCatalog = s.Database, IntegratedSecurity = s.IntegratedSecurity,
        UserID = s.IntegratedSecurity ? "" : s.SqlUser, Password = s.IntegratedSecurity ? "" : password,
        TrustServerCertificate = s.TrustCertificate, Encrypt = true, ConnectTimeout = 15,
        ApplicationName = "LS Code"
    }.ConnectionString;

    public static async Task<QueryResult> ExecuteAsync(string connectionString, string sql, CancellationToken ct)
    {
        var watch = System.Diagnostics.Stopwatch.StartNew();
        var tables = new List<DataTable>(); var messages = new List<string>(); var truncated = false;
        await using var connection = new SqlConnection(connectionString);
        connection.InfoMessage += (_, e) => messages.Add(e.Message);
        await connection.OpenAsync(ct);
        // One connection for all batches in this run: temp tables/transactions survive GO.
        foreach (var batch in SqlBatches.Split(sql))
        {
            await using var command = new SqlCommand(batch.Text, connection) { CommandTimeout = 0 };
            using var registration = ct.Register(() => { try { command.Cancel(); } catch (InvalidOperationException) { } });
            try
            {
                await using var reader = await command.ExecuteReaderAsync(ct);
                do
                {
                    if (reader.FieldCount == 0) continue;
                    if (tables.Count >= 30) { truncated = true; break; }
                    var table = new DataTable($"Result {tables.Count + 1}");
                    for (var i = 0; i < reader.FieldCount; i++)
                    {
                        var name = string.IsNullOrEmpty(reader.GetName(i)) ? $"Column{i + 1}" : reader.GetName(i);
                        var unique = name; var suffix = 2;
                        while (table.Columns.Contains(unique)) unique = name + "_" + suffix++;
                        table.Columns.Add(unique, typeof(string)).ExtendedProperties["OriginalName"] = reader.GetName(i);
                    }
                    var rows = 0;
                    while (await reader.ReadAsync(ct))
                    {
                        if (rows++ >= 5000) { truncated = true; break; }
                        var values = new object[reader.FieldCount];
                        for (var i = 0; i < values.Length; i++)
                            values[i] = reader.IsDBNull(i) ? DBNull.Value : reader.GetValue(i) is byte[] bytes ? Convert.ToHexString(bytes.AsSpan(0, Math.Min(bytes.Length, 4096))) : Convert.ToString(reader.GetValue(i), System.Globalization.CultureInfo.InvariantCulture) ?? "";
                        table.Rows.Add(values);
                    }
                    tables.Add(table);
                } while (await reader.NextResultAsync(ct));
                await reader.CloseAsync();
                if (reader.RecordsAffected >= 0)
                    messages.Add(AffectedRowsMessage(reader.RecordsAffected));
            }
            catch (SqlException ex)
            {
                throw new SqlExecutionException(ex.Errors.Cast<SqlError>().Select(error => new SqlExecutionError(
                    error.Message, string.IsNullOrEmpty(error.Procedure) ? SqlBatches.ErrorLocation(batch.StartLine, error.LineNumber).Line : error.LineNumber, error.Procedure)).ToArray(), ex);
            }
        }
        return new(tables, messages, watch.Elapsed, truncated);
    }
}

