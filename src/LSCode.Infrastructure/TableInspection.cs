using System.Data;
using System.Globalization;
using System.Text;
using LSCode.Domain;
using LSCode.Shell.Services;
using Microsoft.Data.SqlClient;

namespace LSCode.Infrastructure;

internal static class TableInspection
{
    public static async Task DropColumnsAsync(SqlTarget target, string qualified, string[] names, CancellationToken ct)
    {
        await using var connection = new SqlConnection(target.ConnectionString);
        await connection.OpenAsync(ct).ConfigureAwait(false);
        var sql = $"ALTER TABLE {qualified} DROP COLUMN {string.Join(", ", names.Select(ProjectDatabase.Quote))};";
        await using var command = new SqlCommand(sql, connection) { CommandTimeout = 30 };
        await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    public static async Task<ResultSet> StructureAsync(SqlTarget target, string qualified, CancellationToken ct)
    {
        var columns = await TableDefinition.ColumnsAsync(target, qualified, ct);
        var keys = await ProjectDatabase.ReadAsync(target, """
            SELECT c.name, CASE WHEN i.is_primary_key=1 THEN 'PK' ELSE 'UQ' END AS kind, i.name AS key_name
            FROM sys.indexes i
            JOIN sys.index_columns ic ON ic.object_id=i.object_id AND ic.index_id=i.index_id
            JOIN sys.columns c ON c.object_id=ic.object_id AND c.column_id=ic.column_id
            WHERE i.object_id=OBJECT_ID(@object) AND (i.is_primary_key=1 OR i.is_unique_constraint=1) AND ic.key_ordinal>0
            UNION ALL
            SELECT c.name, 'FK', fk.name
            FROM sys.foreign_keys fk
            JOIN sys.foreign_key_columns fkc ON fkc.constraint_object_id=fk.object_id
            JOIN sys.columns c ON c.object_id=fkc.parent_object_id AND c.column_id=fkc.parent_column_id
            WHERE fk.parent_object_id=OBJECT_ID(@object)
            ORDER BY name, kind, key_name
            """, ct, new SqlParameter("@object", qualified));
        var byColumn = keys.Rows.Cast<DataRow>().ToLookup(r => (string)r[0], StringComparer.Ordinal);
        return new(["Cột", "Kiểu dữ liệu", "Khóa", "Cho phép NULL", "Identity", "Mặc định"],
            columns.Rows.Cast<DataRow>().Select(r =>
            {
                var name = (string)r["name"];
                return new string?[] { name, TableDefinition.TypeSql(r),
                    string.Join(", ", byColumn[name].Select(k => k[1] + " · " + k[2])),
                    (bool)r["is_nullable"] ? "Có" : "Không",
                    r.IsNull("seed") ? "" : "Có",
                    r.IsNull("default_value") ? null : (string)r["default_value"] };
            }).ToArray());
    }

    public static async Task<string> ColumnScriptAsync(SqlTarget target, string schema, string table, string[] names,
        bool alter, bool detectPartitions, CancellationToken ct)
    {
        var qualified = ProjectDatabase.Quote(schema) + "." + ProjectDatabase.Quote(table);
        var metadata = await TableDefinition.ColumnsAsync(target, qualified, ct);
        var byName = metadata.Rows.Cast<DataRow>().ToDictionary(r => (string)r["name"], StringComparer.Ordinal);
        var columns = names.Select(name => byName.TryGetValue(name, out var row) ? row
            : throw new InvalidOperationException("Cột đã thay đổi. Hãy đọc lại cấu trúc.")).ToArray();
        var definitions = columns.Select(row =>
        {
            if (!alter) return TableDefinition.ColumnSql(row);
            if (!row.IsNull("computed") || !row.IsNull("seed") || (bool)row["is_sparse"] ||
                Convert.ToInt32(row["system_type_id"]) == 189 ||
                (!row.IsNull("generated") && Convert.ToInt32(row["generated"]) != 0))
                throw new NotSupportedException("Không thể tạo ALTER COLUMN cho cột tính toán, tự sinh hoặc SPARSE.");
            return ProjectDatabase.Quote((string)row["name"]) + " " + TableDefinition.TypeSql(row)
                + ((bool)row["is_nullable"] ? " NULL" : " NOT NULL");
        }).ToArray();
        var partition = detectPartitions ? await PartitionColumnScript.DetectAsync(target, schema, table, ct) : null;
        if (partition is not null)
            return partition.Build(target.Database, names.Select((name, index) =>
                (name, definitions[index], !alter && (bool)columns[index]["is_nullable"] &&
                    columns[index].IsNull("computed") &&
                    Convert.ToInt32(columns[index]["system_type_id"]) is 167 or 175 or 231 or 239)), alter);
        if (alter)
            return string.Concat(definitions.Select(definition => $"ALTER TABLE {qualified} ALTER COLUMN {definition};\n"));
        return $"ALTER TABLE {qualified} ADD\n    {string.Join(",\n    ", definitions)};\n";
    }

    public static ResultSet Preview(DataTable data)
    {
        var columns = data.Columns.Cast<DataColumn>().ToArray();
        return new ResultSet(columns.Select(c => c.ColumnName).ToArray(),
            data.Rows.Cast<DataRow>().Select(r => columns.Select(c => r.IsNull(c) ? null : DisplayValue(r[c])).ToArray()).ToArray());
    }

    public static async Task<string> InsertScriptAsync(SqlTarget target, string qualified, DataTable data, CancellationToken ct)
    {
        if (data.Rows.Count == 0) return "";
        var metadata = await TableDefinition.ColumnsAsync(target, qualified, ct).ConfigureAwait(false);
        var byName = metadata.Rows.Cast<DataRow>().ToDictionary(r => (string)r["name"], StringComparer.Ordinal);
        var columns = data.Columns.Cast<DataColumn>().ToArray();
        if (columns.Any(c => !byName.ContainsKey(c.ColumnName)))
            throw new InvalidOperationException("Cấu trúc bảng đã thay đổi. Hãy đọc lại dữ liệu.");
        var writable = columns.Where(c =>
        {
            var definition = byName[c.ColumnName];
            return definition.IsNull("computed") && Convert.ToInt32(definition["system_type_id"]) != 189
                && (definition.IsNull("generated") || Convert.ToInt32(definition["generated"]) == 0);
        }).ToArray();
        if (writable.Length == 0) throw new NotSupportedException("Các cột đang hiển thị đều do SQL Server sinh tự động.");
        var identity = writable.Any(c => !byName[c.ColumnName].IsNull("seed"));
        var script = new StringBuilder();
        if (identity) script.AppendLine($"SET IDENTITY_INSERT {qualified} ON;");
        script.AppendLine($"INSERT INTO {qualified} ({string.Join(", ", writable.Select(c => ProjectDatabase.Quote(c.ColumnName)))}) VALUES");
        for (var i = 0; i < data.Rows.Count; i++)
        {
            ct.ThrowIfCancellationRequested();
            var row = data.Rows[i];
            script.Append("    (").AppendJoin(", ", writable.Select(c => Literal(row[c])))
                .AppendLine(i == data.Rows.Count - 1 ? ");" : "),");
            if (script.Length > 2_000_000) throw new NotSupportedException("Script quá lớn. Hãy lọc bớt dòng hoặc cột.");
        }
        if (identity) script.AppendLine($"SET IDENTITY_INSERT {qualified} OFF;");
        return script.ToString();
    }

    private static string DisplayValue(object value) => value switch
    {
        byte[] bytes => "0x" + Convert.ToHexString(bytes),
        DateTime time => time.ToString("yyyy-MM-ddTHH:mm:ss.fffffff", CultureInfo.InvariantCulture),
        DateTimeOffset offset => offset.ToString("o", CultureInfo.InvariantCulture),
        _ => Convert.ToString(value, CultureInfo.InvariantCulture) ?? ""
    };

    private static string Literal(object value) => value switch
    {
        DBNull => "NULL",
        bool boolean => boolean ? "1" : "0",
        byte or short or int or long or decimal or float or double or byte[] => DisplayValue(value),
        string or char or Guid or DateTime or DateTimeOffset or TimeSpan => "N'" + DisplayValue(value).Replace("'", "''") + "'",
        _ => throw new NotSupportedException("Script INSERT chưa hỗ trợ kiểu " + value.GetType().Name + ".")
    };
}
