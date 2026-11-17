using System.Text;
using DynamicTemplateStudio.Core.Models;

namespace DynamicTemplateStudio.Core.Services;

public static class AllowedValuesRule
{
    public static IReadOnlyList<string> Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return [];
        var result = new List<string>(); var value = new StringBuilder();
        bool quoted = false, inQuotes = false, afterQuote = false;
        void Finish()
        {
            var item = quoted ? value.ToString() : value.ToString().Trim();
            if (item.Length == 0) throw new FormatException("Allowed values cannot contain an empty item. Use comma-separated values, for example 1,2,0.");
            if (!result.Contains(item, StringComparer.Ordinal)) result.Add(item);
            value.Clear(); quoted = false; afterQuote = false;
        }
        for (int i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < text.Length && text[i + 1] == '"') { value.Append('"'); i++; }
                    else { inQuotes = false; afterQuote = true; }
                }
                else value.Append(c);
            }
            else if (c == ',') Finish();
            else if (afterQuote)
            { if (!char.IsWhiteSpace(c)) throw new FormatException("Expected a comma after a quoted allowed value."); }
            else if (c == '"')
            {
                if (value.ToString().Trim().Length > 0) throw new FormatException("Quote an entire allowed value, not part of it.");
                value.Clear(); quoted = true; inQuotes = true;
            }
            else value.Append(c);
        }
        if (inQuotes) throw new FormatException("Close the quote in the allowed-values list.");
        Finish(); return result;
    }
    public static string Format(IEnumerable<string> values) => string.Join(",", values.Distinct(StringComparer.Ordinal).Select(v => v.Contains(',') || v.Contains('"') || v != v.Trim() ? "\"" + v.Replace("\"", "\"\"") + "\"" : v));
    public static void Validate(ColumnDefinition column, object? value)
    {
        var allowed = Parse(column.AllowedValuesText);
        if (allowed.Count == 0) return;
        if (!SqlTypes.IsText(column.Type)) throw new FormatException("Allowed values are only supported for text types.");
        if (value is null) return;
        if (value is not string text || !allowed.Contains(text, StringComparer.Ordinal)) throw new FormatException($"Value '{value}' is not accepted. Allowed values: {Format(allowed)}.");
    }
}
