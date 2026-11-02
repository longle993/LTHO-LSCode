using System.Data;
using Microsoft.Data.SqlClient;

namespace LSCode.Shell.Services;

internal static class TableDefinition
{
    public static async Task<string> ReadAsync(SqlTarget target, string qualified, CancellationToken token)
    {
        Task<DataTable> Read(string sql) => ProjectDatabase.ReadAsync(target, sql, token, new SqlParameter("@object", qualified));
        var columns = await Read("""
            SELECT c.name, t.name AS type_name, SCHEMA_NAME(t.schema_id) AS type_schema,
                t.is_user_defined, c.max_length, c.precision, c.scale, c.is_nullable,
                c.is_rowguidcol, c.is_sparse,
                cc.definition AS computed, cc.is_persisted,
                CONVERT(varchar(100), ic.seed_value) AS seed, CONVERT(varchar(100), ic.increment_value) AS increment_value
            FROM sys.columns c
            JOIN sys.types t ON t.user_type_id=c.user_type_id
            LEFT JOIN sys.computed_columns cc ON cc.object_id=c.object_id AND cc.column_id=c.column_id
            LEFT JOIN sys.identity_columns ic ON ic.object_id=c.object_id AND ic.column_id=c.column_id
            WHERE c.object_id=OBJECT_ID(@object) ORDER BY c.column_id
            """);
        if (columns.Rows.Count == 0) throw new InvalidOperationException("Không đọc được cấu trúc bảng; kiểm tra quyền VIEW DEFINITION.");
        var definitions = new List<string>();
        foreach (DataRow row in columns.Rows)
        {
            var column = ProjectDatabase.Quote(Value(row, "name"));
            if (Value(row, "computed") is { Length: > 0 } computed)
            {
                definitions.Add(column + " AS " + computed + (Flag(row, "is_persisted") ? " PERSISTED" : ""));
                continue;
            }
            var type = Value(row, "type_name");
            var typeSql = type.ToUpperInvariant();
            if (Flag(row, "is_user_defined")) typeSql = ProjectDatabase.Quote(Value(row, "type_schema")) + "." + ProjectDatabase.Quote(type);
            else if (type is "varchar" or "char" or "varbinary" or "binary" or "nvarchar" or "nchar")
            {
                var length = Convert.ToInt32(row["max_length"]);
                typeSql += "(" + (length == -1 ? "MAX" : (type is "nvarchar" or "nchar" ? length / 2 : length).ToString()) + ")";
            }
            else if (type is "decimal" or "numeric") typeSql += "(" + row["precision"] + "," + row["scale"] + ")";
            else if (type is "datetime2" or "datetimeoffset" or "time") typeSql += "(" + row["scale"] + ")";
            column += " " + typeSql;
            if (Value(row, "seed") is { Length: > 0 } seed) column += " IDENTITY(" + seed + "," + Value(row, "increment_value") + ")";
            if (Flag(row, "is_rowguidcol")) column += " ROWGUIDCOL";
            if (Flag(row, "is_sparse")) column += " SPARSE";
            if (!Flag(row, "is_nullable")) column += " NOT NULL";
            definitions.Add(column);
        }
        var keys = await Read("""
            SELECT i.name, i.type_desc, c.name AS column_name, ic.is_descending_key, ds.name AS filegroup_name
            FROM sys.indexes i JOIN sys.index_columns ic ON ic.object_id=i.object_id AND ic.index_id=i.index_id
            JOIN sys.columns c ON c.object_id=ic.object_id AND c.column_id=ic.column_id
            LEFT JOIN sys.filegroups ds ON ds.data_space_id=i.data_space_id
            WHERE i.object_id=OBJECT_ID(@object) AND i.is_primary_key=1 AND ic.key_ordinal>0
            ORDER BY ic.key_ordinal
            """);
        var script = "CREATE TABLE " + qualified + " (\n    " + string.Join(",\n    ", definitions) + "\n)\nGO\n";
        if (keys.Rows.Count > 0)
        {
            var key = keys.Rows[0];
            script += "\nALTER TABLE " + qualified + " ADD CONSTRAINT " + ProjectDatabase.Quote(Value(key, "name")) + " PRIMARY KEY " + Value(key, "type_desc")
                + " (" + string.Join(", ", keys.Rows.Cast<DataRow>().Select(r => ProjectDatabase.Quote(Value(r, "column_name")) + (Flag(r, "is_descending_key") ? " DESC" : ""))) + ")";
            if (Value(key, "filegroup_name") is { Length: > 0 } filegroup) script += " ON " + ProjectDatabase.Quote(filegroup);
            script += "\nGO\n";
        }
        return script;
    }
    private static string Value(DataRow row, string name) => Convert.ToString(row[name]) ?? "";
    private static bool Flag(DataRow row, string name) => row[name] != DBNull.Value && Convert.ToBoolean(row[name]);
}