using DynamicTemplateStudio.Core.Models;
using DynamicTemplateStudio.Core.Validation;

namespace DynamicTemplateStudio.Core.Services;

public static class GraphEditor
{
    public static string ShortName(string name) => name.StartsWith('#') ? name[1..] : name;
    public static string SqlName(string name) => "#" + ShortName(name.Trim());
    public static void RenameTable(TemplateDefinition template, TableDefinition table, string name)
    {
        name = ShortName(name.Trim());
        if (!TemplateValidator.Identifier(name) || name.StartsWith('#')) throw new InvalidOperationException("Use a valid table identifier with at most one leading #.");
        if (template.Tables.Any(t => t != table && string.Equals(ShortName(t.Name), name, StringComparison.OrdinalIgnoreCase))) throw new InvalidOperationException("Table name already exists.");
        var old = SqlName(table.Name); var replacement = SqlName(name);
        foreach (var t in template.Tables)
        {
            if (SqlName(t.Parent) == old) t.Parent = name;
            foreach (var c in t.Columns)
            {
                if (c.Path.StartsWith("@" + old + ".", StringComparison.Ordinal)) c.Path = "@" + replacement + c.Path[(old.Length + 1)..];
                if (c.References.StartsWith(old + ".", StringComparison.Ordinal)) c.References = replacement + c.References[old.Length..];
            }
        }
        table.Name = name;
    }
    public static void RenameField(TemplateDefinition template, TableDefinition table, ColumnDefinition field, string name)
    {
        if (!TemplateValidator.Identifier(name) || table.Columns.Any(c => c != field && c.Name.Equals(name, StringComparison.OrdinalIgnoreCase))) throw new InvalidOperationException("Use a unique field identifier.");
        var old = field.Name;
        if (table.ParentKey.Column == old) table.ParentKey.Column = name;
        foreach (var pair in table.ParentKey.Pairs.Where(p => p.Column == old)) pair.Column = name;
        foreach (var t in template.Tables)
        {
            if (ShortName(t.Parent) == ShortName(table.Name))
            {
                if (t.ParentKey.References == old) t.ParentKey.References = name;
                foreach (var p in t.ParentKey.Pairs.Where(p => p.References == old)) p.References = name;
            }
            foreach (var c in t.Columns)
            {
                if (c.Path == $"@{SqlName(table.Name)}.{old}") c.Path = $"@{SqlName(table.Name)}.{name}";
                if (c.References == $"{SqlName(table.Name)}.{old}") c.References = $"{SqlName(table.Name)}.{name}";
            }
        }
        field.Name = name;
    }
}
