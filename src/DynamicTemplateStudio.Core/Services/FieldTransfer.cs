using System.Text.Json;
using DynamicTemplateStudio.Core.Models;

namespace DynamicTemplateStudio.Core.Services;

public static class FieldTransfer
{
    public const string ClipboardFormat = "DynamicTemplateStudio.Fields.v1";
    public static string Capture(IEnumerable<ColumnDefinition> fields) => JsonSerializer.Serialize(fields.ToArray());

    public static IReadOnlyList<ColumnDefinition> Read(string json)
    {
        if (json.Length > 8_000_000) throw new FormatException("Field clipboard is too large.");
        ColumnDefinition[]? fields;
        try { fields = JsonSerializer.Deserialize<ColumnDefinition[]>(json); }
        catch (JsonException e) { throw new FormatException("Invalid field clipboard.", e); }
        if (fields is null || fields.Length is 0 or > 10000 || fields.Any(f => f is null ||
            string.IsNullOrWhiteSpace(f.Name) || f.Path is null || f.Type is null || f.Description is null ||
            f.DefaultJson is null || f.ConstantJson is null || f.References is null || f.Extra is null))
            throw new FormatException("Invalid field clipboard.");
        return fields;
    }

    // Prepare every clone before mutating the destination; clipboard objects remain independent.
    public static IReadOnlyList<ColumnDefinition> Prepare(TableDefinition destination, string json)
    {
        var fields = Read(json);
        var names = destination.Columns.Select(c => c.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var field in fields)
        {
            var original = field.Name;
            int suffix = 1;
            while (!names.Add(field.Name)) field.Name = original + "_copy" + (suffix++ == 1 ? "" : (suffix - 1).ToString());
        }
        return fields;
    }
}
