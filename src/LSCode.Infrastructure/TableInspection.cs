using System.Data;
using System.Globalization;
using System.Text;
using LSCode.Domain;
using LSCode.Shell.Services;
using Microsoft.Data.SqlClient;

namespace LSCode.Infrastructure;

internal static class TableInspection
{
    public static async Task<object[]> MetadataAsync(SqlTarget target, string qualified, CancellationToken ct)
    {
        var columns = await TableDefinition.ColumnsAsync(target, qualified, ct).ConfigureAwait(false);
        var keys = await ProjectDatabase.ReadAsync(target, """
            SELECT c.name, CASE WHEN i.is_primary_key=1 THEN 'PK' ELSE 'UQ' END AS kind, i.name AS key_name,
                i.is_primary_key
            FROM sys.indexes i
            JOIN sys.index_columns ic ON ic.object_id=i.object_id AND ic.index_id=i.index_id
            JOIN sys.columns c ON c.object_id=ic.object_id AND c.column_id=ic.column_id
            WHERE i.object_id=OBJECT_ID(@object) AND (i.is_primary_key=1 OR i.is_unique_constraint=1) AND ic.key_ordinal>0
            UNION ALL
            SELECT c.name, 'FK', fk.name, CAST(0 AS bit)
            FROM sys.foreign_keys fk
            JOIN sys.foreign_key_columns fkc ON fkc.constraint_object_id=fk.object_id
            JOIN sys.columns c ON c.object_id=fkc.parent_object_id AND c.column_id=fkc.parent_column_id
            WHERE fk.parent_object_id=OBJECT_ID(@object)
            ORDER BY name, kind, key_name
            """, ct, new SqlParameter("@object", qualified)).ConfigureAwait(false);
        var byColumn = keys.Rows.Cast<DataRow>().ToLookup(r => Convert.ToString(r[0]) ?? "", StringComparer.OrdinalIgnoreCase);
        return columns.Rows.Cast<DataRow>().Select(row =>
        {
            var name = Convert.ToString(row["name"]) ?? "";
            var computed = !row.IsNull("computed");
            var identity = !row.IsNull("seed");
            var generated = row.IsNull("generated") ? 0 : Convert.ToInt32(row["generated"], CultureInfo.InvariantCulture);
            var rowversion = Convert.ToInt32(row["system_type_id"], CultureInfo.InvariantCulture) == 189;
            return new
            {
                name,
                sqlType = TableDefinition.TypeSql(row),
                key = string.Join(", ", byColumn[name].Select(k => k[1] + " · " + k[2])),
                primaryKey = byColumn[name].Any(k => Convert.ToBoolean(k[3], CultureInfo.InvariantCulture)),
                nullable = Convert.ToBoolean(row["is_nullable"], CultureInfo.InvariantCulture),
                identity,
                computed,
                generated,
                writable = !computed && !identity && generated == 0 && !rowversion,
            };
        }).ToArray();
    }

    public static string ManualColumnScript(string schema, string table, string name, string dataType, bool nullable, bool alter)
    {
        var qualified = ProjectDatabase.Quote(schema) + "." + ProjectDatabase.Quote(table);
        var column = ProjectDatabase.Quote(name);
        var type = NormalizeDataType(dataType);
        return alter
            ? $"ALTER TABLE {qualified} ALTER COLUMN {column} {type} {(nullable ? "NULL" : "NOT NULL")};\n"
            : $"ALTER TABLE {qualified} ADD {column} {type} {(nullable ? "NULL" : "NOT NULL")};\n";
    }

    public static string DropScript(string schema, string table, string[] names)
    {
        var qualified = ProjectDatabase.Quote(schema) + "." + ProjectDatabase.Quote(table);
        return $"ALTER TABLE {qualified} DROP COLUMN {string.Join(", ", names.Select(ProjectDatabase.Quote))};\n";
    }

    public static string LogColumnScript(string schema, string table)
    {
        var qualified = ProjectDatabase.Quote(schema) + "." + ProjectDatabase.Quote(table);
        var literal = qualified.Replace("'", "''", StringComparison.Ordinal);
        return $"SELECT c.column_id AS [Ordinal], c.name AS [Column]\nFROM sys.columns c\nWHERE c.object_id = OBJECT_ID(N'{literal}')\nORDER BY c.column_id;\n";
    }

    public static async Task<int> WriteAsync(SqlTarget target, string qualified, string mode,
        string[] names, string?[] values, string[] keyNames, string?[] keyValues, CancellationToken ct)
    {
        if (names.Length != values.Length) throw new ArgumentException("Số cột và giá trị không khớp.");
        if (keyNames.Length != keyValues.Length) throw new ArgumentException("Số cột khóa và giá trị khóa không khớp.");
        var metadata = await TableDefinition.ColumnsAsync(target, qualified, ct).ConfigureAwait(false);
        var byName = metadata.Rows.Cast<DataRow>().ToDictionary(r => Convert.ToString(r["name"]) ?? "", StringComparer.OrdinalIgnoreCase);
        DataRow Column(string name) => byName.TryGetValue(name, out var row)
            ? row : throw new ArgumentException("Cột không tồn tại: " + name);
        bool Writable(DataRow row) => row.IsNull("computed") && row.IsNull("seed") &&
            (row.IsNull("generated") || Convert.ToInt32(row["generated"], CultureInfo.InvariantCulture) == 0) &&
            Convert.ToInt32(row["system_type_id"], CultureInfo.InvariantCulture) != 189;

        await using var connection = new SqlConnection(target.ConnectionString);
        await connection.OpenAsync(ct).ConfigureAwait(false);
        await using var command = new SqlCommand { Connection = connection, CommandTimeout = 30 };
        if (mode.Equals("insert", StringComparison.OrdinalIgnoreCase))
        {
            var insertColumns = new List<string>();
            var insertValues = new List<string>();
            for (var i = 0; i < names.Length; i++)
            {
                var row = Column(names[i]);
                if (!Writable(row)) continue;
                var value = values[i];
                var typeName = (Convert.ToString(row["type_name"]) ?? "nvarchar").ToLowerInvariant();
                var emptyValue = value is null || (value.Length == 0 && typeName is not ("nvarchar" or "nchar" or "varchar" or "char" or "text" or "ntext" or "xml"));
                if (emptyValue && !Convert.ToBoolean(row["is_nullable"], CultureInfo.InvariantCulture) && row.IsNull("default_value"))
                    throw new ArgumentException($"Cột {names[i]} không cho phép NULL.");
                var parameterName = "@p" + insertValues.Count;
                insertColumns.Add(ProjectDatabase.Quote(Convert.ToString(row["name"])!));
                insertValues.Add(parameterName);
                command.Parameters.Add(CreateParameter(parameterName, row, value));
            }
            command.CommandText = insertColumns.Count == 0
                ? "INSERT INTO " + qualified + " DEFAULT VALUES"
                : "INSERT INTO " + qualified + " (" + string.Join(", ", insertColumns) + ") VALUES (" + string.Join(", ", insertValues) + ")";
        }
        else if (mode.Equals("update", StringComparison.OrdinalIgnoreCase))
        {
            if (keyNames.Length == 0) throw new InvalidOperationException("Bảng không có khóa chính để cập nhật dòng.");
            var assignments = new List<string>();
            for (var i = 0; i < names.Length; i++)
            {
                var row = Column(names[i]);
                if (!Writable(row) || keyNames.Contains(names[i], StringComparer.OrdinalIgnoreCase)) continue;
                var parameterName = "@p" + assignments.Count;
                assignments.Add(ProjectDatabase.Quote(Convert.ToString(row["name"])!) + " = " + parameterName);
                command.Parameters.Add(CreateParameter(parameterName, row, values[i]));
            }
            if (assignments.Count == 0) return 0;
            var predicates = new List<string>();
            for (var i = 0; i < keyNames.Length; i++)
            {
                var row = Column(keyNames[i]);
                var parameterName = "@k" + i;
                predicates.Add(ProjectDatabase.Quote(Convert.ToString(row["name"])!) + " = " + parameterName);
                command.Parameters.Add(CreateParameter(parameterName, row, keyValues[i]));
            }
            command.CommandText = "UPDATE " + qualified + " SET " + string.Join(", ", assignments)
                + " WHERE " + string.Join(" AND ", predicates);
        }
        else throw new ArgumentException("Chế độ ghi không hợp lệ.");
        return await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    private static SqlParameter CreateParameter(string name, DataRow metadata, string? value)
    {
        var type = (Convert.ToString(metadata["type_name"]) ?? "nvarchar").ToLowerInvariant();
        var actual = ConvertValue(type, value);
        var sqlType = type == "numeric" ? SqlDbType.Decimal
            : Enum.TryParse<SqlDbType>(type, true, out var parsed) ? parsed : SqlDbType.NVarChar;
        var parameter = new SqlParameter(name, sqlType) { Value = actual };
        if (type is "nvarchar" or "nchar" or "varchar" or "char" or "varbinary" or "binary")
        {
            var length = Convert.ToInt32(metadata["max_length"], CultureInfo.InvariantCulture);
            parameter.Size = length < 0 ? -1 : type.StartsWith("n", StringComparison.Ordinal) ? length / 2 : length;
        }
        if (type is "decimal" or "numeric")
        {
            parameter.Precision = Convert.ToByte(metadata["precision"], CultureInfo.InvariantCulture);
            parameter.Scale = Convert.ToByte(metadata["scale"], CultureInfo.InvariantCulture);
        }
        if (type is "datetime2" or "datetimeoffset" or "time")
            parameter.Scale = Convert.ToByte(metadata["scale"], CultureInfo.InvariantCulture);
        return parameter;
    }

    private static object ConvertValue(string type, string? value)
    {
        if (value is null) return DBNull.Value;
        if (type is "nvarchar" or "nchar" or "varchar" or "char" or "text" or "ntext" or "xml") return value;
        if (value.Length == 0) return DBNull.Value;
        if (type is "bit") return value is "1" or "true" or "TRUE";
        if (type is "tinyint" && byte.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var b)) return b;
        if (type is "smallint" && short.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var s)) return s;
        if (type is "int" && int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var i)) return i;
        if (type is "bigint" && long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var l)) return l;
        if (type is ("decimal" or "numeric" or "money" or "smallmoney") && decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var dec)) return dec;
        if (type is "float" && double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var dbl)) return dbl;
        if (type is "real" && float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var real)) return real;
        if (type is "uniqueidentifier" && Guid.TryParse(value, out var guid)) return guid;
        if (type is ("date" or "datetime" or "smalldatetime" or "datetime2") && DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out var date)) return date;
        if (type is "datetimeoffset" && DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out var offset)) return offset;
        if (type is "time" && TimeSpan.TryParse(value, CultureInfo.InvariantCulture, out var time)) return time;
        if (type is ("binary" or "varbinary" or "image") && value.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) return Convert.FromHexString(value[2..]);
        return value;
    }

    private static string NormalizeDataType(string type)
    {
        var value = type.Trim();
        if (value.Length == 0 || value.Length > 64 || value.Any(char.IsControl) ||
            !System.Text.RegularExpressions.Regex.IsMatch(value, "(?i)^(bigint|int|integer|smallint|tinyint|bit|date|datetime|smalldatetime|datetime2(\\(\\d{1,2}\\))?|datetimeoffset(\\(\\d{1,2}\\))?|time(\\(\\d{1,2}\\))?|uniqueidentifier|real|float(\\(\\d{1,3}\\))?|money|smallmoney|xml|text|ntext|image|rowversion|timestamp|varchar\\((max|\\d+)\\)|nvarchar\\((max|\\d+)\\)|char\\(\\d+\\)|nchar\\(\\d+\\)|varbinary\\((max|\\d+)\\)|binary\\(\\d+\\)|decimal\\(\\d{1,3},\\d{1,3}\\)|numeric\\(\\d{1,3},\\d{1,3}\\))$"))
            throw new ArgumentException("Kiểu dữ liệu không được hỗ trợ.");
        return value.ToUpperInvariant();
    }

    public static async Task DropColumnsAsync(SqlTarget target, string qualified, string[] names, CancellationToken ct)
    {
        await using var connection = new SqlConnection(target.ConnectionString);
        await connection.OpenAsync(ct).ConfigureAwait(false);
        var sql = $"ALTER TABLE {qualified} DROP COLUMN {string.Join(", ", names.Select(ProjectDatabase.Quote))};";
        await using var command = new SqlCommand(sql, connection) { CommandTimeout = 0 };
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
