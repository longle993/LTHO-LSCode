using System.Text.RegularExpressions;
using DynamicTemplateStudio.Core.Models;

namespace DynamicTemplateStudio.Core.Services;

public static class SqlTypes
{
    public static readonly string[] Choices = ["char", "varchar", "nvarchar", "smalldatetime", "bit", "numeric", "int"];
    public static readonly string[] Legacy = ["string", "bigint", "bool", "datetime", "date", "guid"];
    public static string Base(string? declaration) => (declaration ?? "").Split('(')[0].Trim().ToLowerInvariant();
    public static bool IsText(string declaration) => Base(declaration) is "char" or "varchar" or "nvarchar" or "string";
    public static bool IsNumeric(string declaration) => Base(declaration) is "numeric" or "decimal";
    public static int? Length(string declaration)
    {
        var m = Regex.Match(declaration, @"^(?:char|varchar|nvarchar)\((\d+)\)$", RegexOptions.IgnoreCase);
        return m.Success && int.TryParse(m.Groups[1].Value, out var length) ? length : null;
    }
    public static (int Precision, int Scale) PrecisionScale(string declaration)
    {
        var m = Regex.Match(declaration, @"^(?:numeric|decimal)\((\d+),(\d+)\)$", RegexOptions.IgnoreCase);
        return m.Success && int.TryParse(m.Groups[1].Value, out int p) && int.TryParse(m.Groups[2].Value, out int s) ? (p, s) : Base(declaration) == "numeric" ? (19, 5) : (18, 2);
    }
    public static bool IsValid(string? declaration)
    {
        if (declaration is null) return false;
        var type = declaration.ToLowerInvariant();
        if (Choices.Contains(type) || Legacy.Contains(type)) return true;
        if (Regex.IsMatch(type, @"^(numeric|decimal)\(\d{1,2},\d{1,2}\)$"))
        { var (p, s) = PrecisionScale(type); return p is >= 1 and <= 38 && s >= 0 && s <= p; }
        return Length(type) is { } n && n > 0 && n <= (Base(type) == "nvarchar" ? 4000 : 8000);
    }
    public static string Declaration(ColumnDefinition column)
    {
        var type = Base(column.Type);
        if (type is "char" or "varchar" or "nvarchar" && (column.MaxLength ?? Length(column.Type)) is { } length) return $"{type}({length})";
        return column.Type == "numeric" ? "numeric(19,5)" : column.Type;
    }
}
