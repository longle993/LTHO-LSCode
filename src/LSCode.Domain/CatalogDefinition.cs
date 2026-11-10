using System.Globalization;
using System.Text.RegularExpressions;

namespace LSCode.Domain;

public sealed record CatalogField(string Name, string Type = "nvarchar", string Size = "128", bool Required = false, bool Key = false,
    bool Unique = false, bool Identity = false, string? Default = null, string IdentitySeed = "1", string IdentityIncrement = "1",
    string? Collation = null, string[]? Reference = null, int KeyOrder = 0, bool NullDefault = false);

public static class CatalogDefinition
{
    public static readonly string[] Audit = ["user_id0", "user_id2", "datetime0", "datetime2"];
    public static readonly string[] Types = ["nvarchar", "varchar", "nchar", "char", "int", "bigint", "smallint", "tinyint", "decimal", "numeric", "bit", "date", "datetime", "datetime2", "uniqueidentifier"];
    public static string Quote(string id)
    {
        if (string.IsNullOrWhiteSpace(id) || id.Length > 128 || id.Any(char.IsControl)) throw new ArgumentException("Tên SQL phải từ 1 đến 128 ký tự, không chứa ký tự điều khiển.");
        return "[" + id.Replace("]", "]]") + "]";
    }
    public static string TypeSql(CatalogField field)
    {
        if (!Types.Contains(field.Type)) throw new ArgumentException("Kiểu dữ liệu không hỗ trợ: " + field.Type);
        var type = field.Type;
        if (type is "nvarchar" or "varchar" or "nchar" or "char")
        {
            if (field.Size.Equals("max", StringComparison.OrdinalIgnoreCase) && type is "nvarchar" or "varchar") return type + "(max)";
            if (!int.TryParse(field.Size, out var size) || size < 1 || size > (type.StartsWith('n') ? 4000 : 8000)) throw new ArgumentException(field.Name + ": độ dài không hợp lệ.");
            return type + "(" + size + ")";
        }
        if (type is "decimal" or "numeric")
        {
            var parts = field.Size.Split(',');
            if (parts.Length != 2 || !int.TryParse(parts[0], out var precision) || !int.TryParse(parts[1], out var scale) || precision is < 1 or > 38 || scale < 0 || scale > precision)
                throw new ArgumentException(field.Name + ": nhập precision,scale, ví dụ 18,2.");
            return type + "(" + precision + "," + scale + ")";
        }
        if (type == "datetime2")
        {
            if (!int.TryParse(field.Size, out var scale) || scale is < 0 or > 7) throw new ArgumentException(field.Name + ": datetime2 cần scale từ 0 đến 7.");
            return type + "(" + scale + ")";
        }
        return type;
    }
    public static object ConvertValue(string type, string value) => type switch
    {
        "int" => int.Parse(value, CultureInfo.InvariantCulture), "bigint" => long.Parse(value, CultureInfo.InvariantCulture),
        "smallint" => short.Parse(value, CultureInfo.InvariantCulture), "tinyint" => byte.Parse(value, CultureInfo.InvariantCulture),
        "decimal" or "numeric" => decimal.Parse(value, CultureInfo.InvariantCulture),
        "bit" => value is "1" or "true" ? true : value is "0" or "false" ? false : throw new FormatException("Giá trị bit phải là 0 hoặc 1."),
        "date" or "datetime" or "datetime2" => DateTime.Parse(value, CultureInfo.InvariantCulture),
        "uniqueidentifier" => Guid.Parse(value), _ => value
    };
    public static string Build(string schema, string table, bool creating, CatalogField[] fields, string[] existing, bool existingIdentity, bool existingKey)
    {
        var name = Quote(schema) + "." + Quote(table);
        var names = new HashSet<string>(existing, StringComparer.OrdinalIgnoreCase);
        if (creating) names.UnionWith(Audit);
        if (fields.Count(f => f.Identity) + (existingIdentity ? 1 : 0) > 1) throw new ArgumentException("Mỗi bảng chỉ có một trường tự tăng.");
        if (existingKey && fields.Any(f => f.Key)) throw new ArgumentException("Bảng đã có khóa chính.");
        var definitions = new List<string>();
        foreach (var f in fields)
        {
            if (!names.Add(f.Name.Trim())) throw new ArgumentException("Trùng tên trường: " + f.Name);
            var sql = Quote(f.Name.Trim()) + " " + TypeSql(f);
            if (f.Collation != null)
            {
                if (!Regex.IsMatch(f.Collation, @"^[A-Za-z0-9_]+$")) throw new ArgumentException("Collation không hợp lệ.");
                sql += " COLLATE " + f.Collation;
            }
            if (f.Identity)
            {
                if (f.Type is not ("int" or "bigint" or "smallint" or "tinyint") || f.Default != null || f.NullDefault ||
                    !long.TryParse(f.IdentitySeed, out var seed) || !long.TryParse(f.IdentityIncrement, out var step) || step == 0) throw new ArgumentException(f.Name + ": tự tăng không hợp lệ.");
                sql += $" IDENTITY({seed},{step})";
            }
            sql += f.Required || f.Key || f.Identity ? " NOT NULL" : " NULL";
            if (f.Unique) sql += " UNIQUE";
            if (f.Default != null)
            {
                var value = ConvertValue(f.Type, f.Default);
                var literal = value is DateTime d ? d.ToString("yyyy-MM-ddTHH:mm:ss.fffffff", CultureInfo.InvariantCulture) : value is bool b ? b ? "1" : "0" : Convert.ToString(value, CultureInfo.InvariantCulture)!;
                sql += " DEFAULT N'" + literal.Replace("'", "''") + "'";
            }
            else if (f.NullDefault) sql += " DEFAULT NULL";
            if (f.Reference is { Length: >= 3 } reference)
            {
                if (reference.Length > 3 && reference[3] != f.Type) throw new ArgumentException(f.Name + ": kiểu khóa ngoại không khớp cột đích.");
                sql += " REFERENCES " + Quote(reference[0]) + "." + Quote(reference[1]) + "(" + Quote(reference[2]) + ")";
            }
            definitions.Add(sql);
        }
        if (creating) definitions.AddRange(Audit.Select((n, i) => Quote(n) + (i < 2 ? " int NULL" : " datetime NULL")));
        if (fields.Any(f => f.Key)) definitions.Add("PRIMARY KEY (" + string.Join(", ", fields.Where(f => f.Key).OrderBy(f => f.KeyOrder == 0 ? int.MaxValue : f.KeyOrder).Select(f => Quote(f.Name.Trim()))) + ")");
        if (definitions.Count == 0) return "";
        return (creating ? "CREATE TABLE " : "ALTER TABLE ") + name + (creating ? " (\n    " : " ADD\n    ") + string.Join(",\n    ", definitions) + (creating ? "\n)" : "") + ";";
    }
}
