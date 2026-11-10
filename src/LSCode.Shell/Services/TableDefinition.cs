using System.Data;
using Microsoft.Data.SqlClient;

namespace LSCode.Shell.Services;

internal static class TableDefinition
{
    internal static Task<DataTable> ColumnsAsync(SqlTarget target, string qualified, CancellationToken token) =>
        ProjectDatabase.ReadAsync(target, """
            SELECT c.name, t.name AS type_name, SCHEMA_NAME(t.schema_id) AS type_schema,
                t.is_user_defined, c.max_length, c.precision, c.scale, c.is_nullable,
                c.is_rowguidcol, c.is_sparse, c.system_type_id,
                COLUMNPROPERTY(c.object_id, c.name, 'GeneratedAlwaysType') AS generated,
                cc.definition AS computed, cc.is_persisted,
                CONVERT(varchar(100), ic.seed_value) AS seed, CONVERT(varchar(100), ic.increment_value) AS increment_value,
                dc.definition AS default_value
            FROM sys.columns c
            JOIN sys.types t ON t.user_type_id=c.user_type_id
            LEFT JOIN sys.computed_columns cc ON cc.object_id=c.object_id AND cc.column_id=c.column_id
            LEFT JOIN sys.identity_columns ic ON ic.object_id=c.object_id AND ic.column_id=c.column_id
            LEFT JOIN sys.default_constraints dc ON dc.object_id=c.default_object_id
            WHERE c.object_id=OBJECT_ID(@object) ORDER BY c.column_id
            """, token, new SqlParameter("@object", qualified));

    public static async Task<string> ReadAsync(SqlTarget target, string qualified, CancellationToken token)
    {
        var columns = await ColumnsAsync(target, qualified, token);
        if (columns.Rows.Count == 0) throw new InvalidOperationException("Không đọc được cấu trúc bảng; kiểm tra quyền VIEW DEFINITION.");
        var definitions = columns.Rows.Cast<DataRow>().Select(ColumnSql);
        var keys = await ProjectDatabase.ReadAsync(target, """
            SELECT i.name, i.type_desc, i.is_primary_key, c.name AS column_name, ic.is_descending_key, ds.name AS filegroup_name
            FROM sys.indexes i JOIN sys.index_columns ic ON ic.object_id=i.object_id AND ic.index_id=i.index_id
            JOIN sys.columns c ON c.object_id=ic.object_id AND c.column_id=ic.column_id
            LEFT JOIN sys.filegroups ds ON ds.data_space_id=i.data_space_id
            WHERE i.object_id=OBJECT_ID(@object) AND (i.is_primary_key=1 OR i.is_unique_constraint=1) AND ic.key_ordinal>0
            ORDER BY i.is_primary_key DESC, i.name, ic.key_ordinal
            """, token, new SqlParameter("@object", qualified));
        var script = "CREATE TABLE " + qualified + " (\n    " + string.Join(",\n    ", definitions) + "\n)\nGO\n";
        foreach (var group in keys.Rows.Cast<DataRow>().GroupBy(row => Value(row, "name")))
        {
            var key = group.First();
            var kind = Flag(key, "is_primary_key") ? " PRIMARY KEY " : " UNIQUE ";
            script += "\nALTER TABLE " + qualified + " ADD CONSTRAINT " + ProjectDatabase.Quote(group.Key) + kind + Value(key, "type_desc")
                + " (" + string.Join(", ", group.Select(r => ProjectDatabase.Quote(Value(r, "column_name")) + (Flag(r, "is_descending_key") ? " DESC" : ""))) + ")";
            if (Value(key, "filegroup_name") is { Length: > 0 } filegroup) script += " ON " + ProjectDatabase.Quote(filegroup);
            script += "\nGO\n";
        }
        var foreign = await ProjectDatabase.ReadAsync(target, """
            SELECT fk.name, SCHEMA_NAME(t.schema_id) AS ref_schema, t.name AS ref_table,
                c.name AS column_name, rc.name AS ref_column, fk.delete_referential_action_desc, fk.update_referential_action_desc
            FROM sys.foreign_keys fk JOIN sys.foreign_key_columns fkc ON fkc.constraint_object_id=fk.object_id
            JOIN sys.tables t ON t.object_id=fkc.referenced_object_id
            JOIN sys.columns c ON c.object_id=fkc.parent_object_id AND c.column_id=fkc.parent_column_id
            JOIN sys.columns rc ON rc.object_id=fkc.referenced_object_id AND rc.column_id=fkc.referenced_column_id
            WHERE fk.parent_object_id=OBJECT_ID(@object) ORDER BY fk.name, fkc.constraint_column_id
            """, token, new SqlParameter("@object", qualified));
        foreach (var group in foreign.Rows.Cast<DataRow>().GroupBy(row => Value(row, "name")))
        {
            var key = group.First();
            var referenced = ProjectDatabase.Quote(Value(key, "ref_schema")) + "." + ProjectDatabase.Quote(Value(key, "ref_table"));
            var columnsSql = string.Join(", ", group.Select(row => ProjectDatabase.Quote(Value(row, "column_name"))));
            var refSql = string.Join(", ", group.Select(row => ProjectDatabase.Quote(Value(row, "ref_column"))));
            script += $"\nALTER TABLE {qualified} ADD CONSTRAINT {ProjectDatabase.Quote(group.Key)} FOREIGN KEY ({columnsSql}) REFERENCES {referenced} ({refSql})";
            var onDelete = Value(key, "delete_referential_action_desc");
            var onUpdate = Value(key, "update_referential_action_desc");
            if (onDelete != "NO_ACTION") script += " ON DELETE " + onDelete.Replace('_', ' ');
            if (onUpdate != "NO_ACTION") script += " ON UPDATE " + onUpdate.Replace('_', ' ');
            script += ";\nGO\n";
        }
        return script;
    }
    internal static string TypeSql(DataRow row)
    {
        var type = Value(row, "type_name");
        if (Flag(row, "is_user_defined"))
            return ProjectDatabase.Quote(Value(row, "type_schema")) + "." + ProjectDatabase.Quote(type);
        var sql = type.ToUpperInvariant();
        if (type is "varchar" or "char" or "varbinary" or "binary" or "nvarchar" or "nchar")
        {
            var length = Convert.ToInt32(row["max_length"]);
            return sql + "(" + (length == -1 ? "MAX" : (type is "nvarchar" or "nchar" ? length / 2 : length).ToString()) + ")";
        }
        if (type is "decimal" or "numeric") return sql + "(" + row["precision"] + "," + row["scale"] + ")";
        if (type is "datetime2" or "datetimeoffset" or "time") return sql + "(" + row["scale"] + ")";
        return sql;
    }
    internal static string ColumnSql(DataRow row)
    {
        var column = ProjectDatabase.Quote(Value(row, "name"));
        if (Value(row, "computed") is { Length: > 0 } computed)
            return column + " AS " + computed + (Flag(row, "is_persisted") ? " PERSISTED" : "");
        column += " " + TypeSql(row);
        if (Value(row, "seed") is { Length: > 0 } seed) column += " IDENTITY(" + seed + "," + Value(row, "increment_value") + ")";
        if (Flag(row, "is_rowguidcol")) column += " ROWGUIDCOL";
        if (Flag(row, "is_sparse")) column += " SPARSE";
        column += Flag(row, "is_nullable") ? " NULL" : " NOT NULL";
        if (Value(row, "default_value") is { Length: > 0 } defaultValue) column += " DEFAULT " + defaultValue;
        return column;
    }
    private static string Value(DataRow row, string name) => Convert.ToString(row[name]) ?? "";
    private static bool Flag(DataRow row, string name) => row[name] != DBNull.Value && Convert.ToBoolean(row[name]);
}
