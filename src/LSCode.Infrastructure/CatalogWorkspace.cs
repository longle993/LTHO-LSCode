using System.Collections.Concurrent;
using System.Data;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using LSCode.Core;
using LSCode.Domain;
using LSCode.Shell.Services;
using Microsoft.Data.SqlClient;

namespace LSCode.Infrastructure;

internal sealed class CatalogWorkspace
{
    private sealed record Draft(SqlTarget Target, string Schema, string Table, bool Creating, string Signature, string Sql, CatalogField[] Fields, string?[][] Rows);
    private readonly ConcurrentDictionary<string, Draft> previews = new();
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private const string MetadataSql = """
        SELECT c.name,TYPE_NAME(c.user_type_id) AS type,c.max_length,c.precision,c.scale,c.is_nullable,c.is_identity,c.is_computed,
          d.definition,c.generated_always_type,c.column_id,c.collation_name,
          CAST(CASE WHEN EXISTS (SELECT 1 FROM sys.indexes i WHERE i.object_id=c.object_id AND i.is_primary_key=1) THEN 1 ELSE 0 END AS bit) AS has_pk,
          CONVERT(nvarchar(50),IDENT_SEED(QUOTENAME(OBJECT_SCHEMA_NAME(c.object_id))+'.'+QUOTENAME(OBJECT_NAME(c.object_id)))) AS seed,
          CONVERT(nvarchar(50),IDENT_INCR(QUOTENAME(OBJECT_SCHEMA_NAME(c.object_id))+'.'+QUOTENAME(OBJECT_NAME(c.object_id)))) AS increment,
          CAST(CASE WHEN marker.name IS NOT NULL AND c.name IN ('user_id0','user_id2','datetime0','datetime2') THEN 1 ELSE 0 END AS bit) AS managed
        FROM sys.columns c LEFT JOIN sys.default_constraints d ON d.object_id=c.default_object_id
        LEFT JOIN sys.extended_properties marker ON marker.class=1 AND marker.major_id=c.object_id AND marker.minor_id=0 AND marker.name=N'LSCode.Catalog'
        WHERE c.object_id=OBJECT_ID(@object,'U') ORDER BY c.column_id
        """;
    private static string Qualified(string schema, string table) => CatalogDefinition.Quote(schema) + "." + CatalogDefinition.Quote(table);
    private static string Signature(DataTable metadata) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join("\n", metadata.Rows.Cast<DataRow>().Select(r => string.Join("\u001f", r.ItemArray))))));
    public async Task<object> ExecuteAsync(string method, JsonElement p, SqlTarget target, CancellationToken ct)
    {
        string S(string key, string fallback = "") => p.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString()! : fallback;
        var schema = S("schema", "dbo"); var table = S("table");
        switch (method)
        {
            case "catalog.references":
                return ToTable(await ProjectDatabase.ReadAsync(target, """
                    SELECT s.name AS [Schema],t.name AS [Table],c.name AS [Column],TYPE_NAME(c.user_type_id) AS [Type]
                    FROM sys.indexes i JOIN sys.tables t ON t.object_id=i.object_id JOIN sys.schemas s ON s.schema_id=t.schema_id
                    JOIN sys.index_columns k ON k.object_id=i.object_id AND k.index_id=i.index_id AND k.key_ordinal=1
                    JOIN sys.columns c ON c.object_id=k.object_id AND c.column_id=k.column_id
                    WHERE i.is_unique=1 AND i.has_filter=0 AND i.is_disabled=0 AND i.is_hypothetical=0
                    AND NOT EXISTS(SELECT 1 FROM sys.index_columns x WHERE x.object_id=i.object_id AND x.index_id=i.index_id AND x.key_ordinal>1)
                    ORDER BY s.name,t.name,c.name
                    """, ct));
            case "catalog.clipboard.read":
                var rows = CatalogClipboard.Read(S("text"));
                return rows.Select(r => r.Select(CatalogClipboard.Literal).ToArray()).ToArray();
            case "catalog.clipboard.write": return CatalogClipboard.Write(p.GetProperty("rows").Deserialize<string?[][]>(Json) ?? []);
            case "catalog.load":
                var metadata = await MetadataAsync(target, Qualified(schema, table), ct);
                var page = p.TryGetProperty("page", out var n) ? Math.Max(0, n.GetInt32()) : 0;
                var data = await ProjectDatabase.ReadAsync(target, "SELECT * FROM " + Qualified(schema, table) + " ORDER BY (SELECT NULL) OFFSET @offset ROWS FETCH NEXT 100 ROWS ONLY", ct, new SqlParameter("@offset", page * 100));
                return new { signature = Signature(metadata), columns = metadata.Rows.Cast<DataRow>().Select(Column).ToArray(), data = ToTable(data) };
            case "catalog.duplicate":
                var original = await MetadataAsync(target, Qualified(schema, table), ct);
                if (original.Rows.Count == 0) throw new InvalidOperationException("Không đọc được cấu trúc template.");
                var unsupported = await ProjectDatabase.ReadAsync(target, """
                    SELECT 'CHECK: '+name AS issue FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID(@object)
                    UNION ALL SELECT 'UNIQUE ghép hoặc index khóa không hỗ trợ: '+i.name FROM sys.indexes i
                    WHERE i.object_id=OBJECT_ID(@object) AND (i.is_primary_key=1 OR i.is_unique_constraint=1)
                    AND (i.is_disabled=1 OR i.has_filter=1 OR i.type NOT IN (1,2) OR i.is_unique_constraint=1 AND
                      (SELECT COUNT(*) FROM sys.index_columns k WHERE k.object_id=i.object_id AND k.index_id=i.index_id AND k.key_ordinal>0)>1)
                    UNION ALL SELECT 'Khóa ngoại ghép/cascade/disabled: '+f.name FROM sys.foreign_keys f WHERE f.parent_object_id=OBJECT_ID(@object)
                    AND (f.delete_referential_action<>0 OR f.update_referential_action<>0 OR f.is_disabled=1 OR f.is_not_trusted=1 OR
                      (SELECT COUNT(*) FROM sys.foreign_key_columns k WHERE k.constraint_object_id=f.object_id)>1)
                    UNION ALL SELECT 'Bảng temporal/memory optimized/filetable' FROM sys.tables WHERE object_id=OBJECT_ID(@object)
                    AND (temporal_type<>0 OR is_memory_optimized=1 OR is_filetable=1)
                    UNION ALL SELECT 'Cột mã hóa/masking/sparse/ROWGUID/UDT/rule/default cũ: '+c.name FROM sys.columns c WHERE c.object_id=OBJECT_ID(@object)
                    AND (c.encryption_type IS NOT NULL OR c.is_sparse=1 OR c.is_rowguidcol=1 OR c.rule_object_id<>0 OR TYPEPROPERTY(TYPE_NAME(c.user_type_id),'IsUserDefined')=1
                      OR c.default_object_id<>0 AND NOT EXISTS(SELECT 1 FROM sys.default_constraints d WHERE d.object_id=c.default_object_id)
                      OR EXISTS(SELECT 1 FROM sys.masked_columns m WHERE m.object_id=c.object_id AND m.column_id=c.column_id AND m.is_masked=1))
                    """, ct, new SqlParameter("@object", Qualified(schema, table)));
                if (unsupported.Rows.Count > 0) throw new InvalidOperationException("Không thể nhân bản đầy đủ: " + string.Join("; ", unsupported.Rows.Cast<DataRow>().Select(r => r[0])));
                var fields = new List<CatalogField>();
                foreach (DataRow row in original.Rows)
                {
                    var type = (string)row[1];
                    if (CatalogDefinition.Audit.Contains((string)row[0], StringComparer.OrdinalIgnoreCase))
                    {
                        if (type != (((string)row[0]).StartsWith("user_") ? "int" : "datetime") || !(bool)row[5] || (bool)row[6] || row[8] is not DBNull)
                            throw new InvalidOperationException(row[0] + ": trường hệ thống không khớp hợp đồng.");
                        continue;
                    }
                    if (!CatalogDefinition.Types.Contains(type) || (bool)row[7] || Convert.ToInt32(row[9]) != 0) throw new InvalidOperationException("Template có kiểu/cột sinh tự động chưa hỗ trợ: " + row[0]);
                    var size = type == "datetime2" ? Convert.ToString(row[4])! : type is "decimal" or "numeric" ? row[3] + "," + row[4] : Convert.ToInt32(row[2]) < 0 ? "max" : (type.StartsWith('n') ? Convert.ToInt32(row[2]) / 2 : Convert.ToInt32(row[2])).ToString();
                    // Reject expression defaults rather than silently losing them.
                    string? def = row[8] is string raw ? raw.Trim().Trim('(', ')').Trim() : null;
                    var nullDefault = def?.Equals("NULL", StringComparison.OrdinalIgnoreCase) == true;
                    if (nullDefault) def = null;
                    if (def != null)
                    {
                        if (def.StartsWith("N'")) def = def[1..];
                        if (def.StartsWith('\'') && def.EndsWith('\'')) def = def[1..^1].Replace("''", "'");
                        else if (!decimal.TryParse(def, NumberStyles.Number, CultureInfo.InvariantCulture, out _)) throw new InvalidOperationException("Template có DEFAULT biểu thức; cần kiểm tra thủ công: " + row[0]);
                    }
                    fields.Add(new((string)row[0], type, size, !(bool)row[5], Identity: (bool)row[6], Default: def,
                        IdentitySeed: Convert.ToString(row[13]) ?? "1", IdentityIncrement: Convert.ToString(row[14]) ?? "1", Collation: row[11] as string, NullDefault: nullDefault));
                }
                // Key order is read separately, not inferred from column order.
                var keys = await ProjectDatabase.ReadAsync(target, "SELECT c.name,ic.key_ordinal,i.is_primary_key FROM sys.indexes i JOIN sys.index_columns ic ON ic.object_id=i.object_id AND ic.index_id=i.index_id JOIN sys.columns c ON c.object_id=ic.object_id AND c.column_id=ic.column_id WHERE i.object_id=OBJECT_ID(@object) AND (i.is_primary_key=1 OR i.is_unique_constraint=1) AND ic.key_ordinal>0", ct, new SqlParameter("@object", Qualified(schema, table)));
                foreach (DataRow key in keys.Rows) { var index = fields.FindIndex(f => f.Name == (string)key[0]); if (index < 0) throw new InvalidOperationException("Trường hệ thống có khóa không thể nhân bản."); fields[index] = (bool)key[2] ? fields[index] with { Key = true, KeyOrder = Convert.ToInt32(key[1]) } : fields[index] with { Unique = true }; }
                var references = await ProjectDatabase.ReadAsync(target, """
                    SELECT c.name,s.name,t.name,rc.name,TYPE_NAME(rc.user_type_id)
                    FROM sys.foreign_key_columns f JOIN sys.columns c ON c.object_id=f.parent_object_id AND c.column_id=f.parent_column_id
                    JOIN sys.tables t ON t.object_id=f.referenced_object_id JOIN sys.schemas s ON s.schema_id=t.schema_id
                    JOIN sys.columns rc ON rc.object_id=f.referenced_object_id AND rc.column_id=f.referenced_column_id WHERE f.parent_object_id=OBJECT_ID(@object)
                    """, ct, new SqlParameter("@object", Qualified(schema, table)));
                foreach (DataRow reference in references.Rows)
                {
                    var index = fields.FindIndex(f => f.Name == (string)reference[0]);
                    if (index < 0 || fields[index].Reference != null) throw new InvalidOperationException("Khóa ngoại trên trường hệ thống hoặc nhiều khóa trên một trường chưa hỗ trợ.");
                    fields[index] = fields[index] with { Reference = reference.ItemArray.Skip(1).Cast<string>().ToArray() };
                }
                return fields;
            case "catalog.preview":
                var creating = p.TryGetProperty("creating", out var cr) && cr.GetBoolean();
                var definitions = p.GetProperty("fields").Deserialize<CatalogField[]>(Json) ?? [];
                var records = p.TryGetProperty("rows", out var rr) ? rr.Deserialize<string?[][]>(Json) ?? [] : [];
                if (records.Length > 1000) throw new ArgumentException("Chỉ lưu tối đa 1000 dòng mỗi đợt.");
                if (records.Length > 0 && definitions.Length > 0) throw new ArgumentException("Lưu cấu trúc trước khi nhập bản ghi.");
                var before = creating ? new DataTable() : await MetadataAsync(target, Qualified(schema, table), ct);
                if (!creating && S("signature") != Signature(before)) throw new InvalidOperationException("Cấu trúc đã thay đổi. Mở lại bảng trước khi lưu.");
                var sql = CatalogDefinition.Build(schema, table, creating, definitions, before.Rows.Cast<DataRow>().Select(r => (string)r[0]).ToArray(), before.Rows.Cast<DataRow>().Any(r => (bool)r[6]), before.Rows.Cast<DataRow>().Any(r => (bool)r[12]));
                if (records.Length > 0) sql = string.Join("\n", records.Select((_, i) => "-- INSERT bản ghi " + (i + 1) + " bằng tham số; giá trị không ghép vào SQL."));
                var id = Guid.NewGuid().ToString("N"); if (previews.Count > 64) previews.Clear();
                previews[id] = new(target, schema, table, creating, Signature(before), sql, definitions, records);
                return new { previewId = id, sql };
            case "catalog.commit":
                if (!previews.TryRemove(S("previewId"), out var draft) || draft.Target != target) throw new InvalidOperationException("Bản xem trước hết hạn hoặc đã lưu. Tạo preview mới.");
                await CommitAsync(draft, ct); SqlSchemaCompletion.Clear(); return true;
            default: throw new NotSupportedException(method);
        }
    }
    private static object Column(DataRow r) => new { name = r[0], type = r[1], nullable = r[5], identity = r[6], computed = (bool)r[7] || (string)r[1] is "timestamp" or "rowversion", generated = r[9], managed = r[15], hasDefault = r[8] is not DBNull };
    private static ResultSet ToTable(DataTable t) => new(t.Columns.Cast<DataColumn>().Select(c => c.ColumnName).ToArray(), t.Rows.Cast<DataRow>().Select(r => r.ItemArray.Select(x => x is DBNull ? null : x is DateTime date ? date.ToString("O",CultureInfo.InvariantCulture) : Convert.ToString(x, CultureInfo.InvariantCulture)).ToArray()).ToArray());
    private static Task<DataTable> MetadataAsync(SqlTarget target, string name, CancellationToken ct) => ProjectDatabase.ReadAsync(target, MetadataSql, ct, new SqlParameter("@object", name));
    private static async Task CommitAsync(Draft draft, CancellationToken ct)
    {
        await using var connection = new SqlConnection(draft.Target.ConnectionString); await connection.OpenAsync(ct);
        await using var tx = (SqlTransaction)await connection.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var name = Qualified(draft.Schema, draft.Table);
        // Re-read metadata on the same connection/transaction before every write.
        var current = new DataTable();
        await using (var check = new SqlCommand(MetadataSql, connection, tx) { CommandTimeout = 0 })
        {
            check.Parameters.AddWithValue("@object", name);
            await using var reader = await check.ExecuteReaderAsync(ct); current.Load(reader);
        }
        if (!draft.Creating && Signature(current) != draft.Signature) throw new InvalidOperationException("Cấu trúc đã thay đổi. Tạo preview mới.");
        if (draft.Rows.Length == 0 && draft.Sql.Length > 0)
        {
            await using var cmd = new SqlCommand(draft.Sql, connection, tx) { CommandTimeout = 0 }; await cmd.ExecuteNonQueryAsync(ct);
            if (draft.Creating)
            {
                await using var marker = new SqlCommand("EXEC sys.sp_addextendedproperty @name=N'LSCode.Catalog',@value=1,@level0type=N'SCHEMA',@level0name=@schema,@level1type=N'TABLE',@level1name=@table", connection, tx) { CommandTimeout = 0 };
                marker.Parameters.AddWithValue("@schema", draft.Schema); marker.Parameters.AddWithValue("@table", draft.Table); await marker.ExecuteNonQueryAsync(ct);
            }
        }
        foreach (var row in draft.Rows)
        {
            if (row.Length != current.Rows.Count) throw new ArgumentException("Số cột bản nháp không khớp cấu trúc.");
            var columns = new List<string>(); var values = new List<string>(); var parameters = new List<SqlParameter>();
            for (var i = 0; i < row.Length; i++)
            {
                var meta = current.Rows[i]; var fieldName = (string)meta[0]; var type = (string)meta[1]; var value = row[i];
                if ((bool)meta[6] || (bool)meta[7] || Convert.ToInt32(meta[9]) != 0 || type is "timestamp" or "rowversion") continue;
                if ((bool)meta[15]) { columns.Add(CatalogDefinition.Quote(fieldName)); values.Add(fieldName.StartsWith("datetime") ? "GETDATE()" : "NULL"); continue; }
                if (value is null or ":default") continue;
                object actual = value == ":null" ? DBNull.Value : value == ":empty" ? "" : CatalogDefinition.ConvertValue(type, CatalogClipboard.DecodeLiteral(value));
                if (actual == DBNull.Value && !(bool)meta[5]) throw new ArgumentException(fieldName + ": không được NULL.");
                if (actual is string s && Convert.ToInt32(meta[2]) > 0 && s.Length > (type.StartsWith('n') ? Convert.ToInt32(meta[2]) / 2 : Convert.ToInt32(meta[2]))) throw new ArgumentException(fieldName + ": vượt độ dài cột.");
                var parameterType = type == "numeric" ? SqlDbType.Decimal : Enum.TryParse<SqlDbType>(type, true, out var parsedType) ? parsedType : throw new ArgumentException(fieldName + ": kiểu dữ liệu chưa hỗ trợ nhập.");
                var parameter = new SqlParameter("@p" + i, parameterType) { Value = actual };
                if (type is "nvarchar" or "nchar" or "varchar" or "char") parameter.Size = Convert.ToInt32(meta[2]) < 0 ? -1 : type.StartsWith('n') ? Convert.ToInt32(meta[2]) / 2 : Convert.ToInt32(meta[2]);
                if (type is "decimal" or "numeric") parameter.Precision = Convert.ToByte(meta[3]);
                if (type is "decimal" or "numeric" or "datetime2") parameter.Scale = Convert.ToByte(meta[4]);
                columns.Add(CatalogDefinition.Quote(fieldName)); values.Add("@p" + i); parameters.Add(parameter);
            }
            var sql = columns.Count == 0 ? "INSERT INTO " + name + " DEFAULT VALUES" : "INSERT INTO " + name + " (" + string.Join(",", columns) + ") VALUES (" + string.Join(",", values) + ")";
            await using var insert = new SqlCommand(sql, connection, tx) { CommandTimeout = 0 }; insert.Parameters.AddRange(parameters.ToArray()); await insert.ExecuteNonQueryAsync(ct);
        }
        ct.ThrowIfCancellationRequested();
        // Never retry an ambiguous commit; the preview token has already been consumed.
        try { await tx.CommitAsync(CancellationToken.None); }
        catch (Exception ex) { throw new InvalidOperationException("Chưa xác nhận được kết quả lưu. Kiểm tra SQL Server trước khi tạo preview và thử lại.", ex); }
    }
}
