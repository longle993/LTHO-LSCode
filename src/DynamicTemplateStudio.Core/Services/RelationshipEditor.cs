using DynamicTemplateStudio.Core.Models;

namespace DynamicTemplateStudio.Core.Services;

public static class RelationshipEditor
{
    public static TableDefinition? Parent(TemplateDefinition? template, TableDefinition? table) => template?.Tables.FirstOrDefault(p => table is not null && p != table && GraphEditor.ShortName(p.Name).Equals(GraphEditor.ShortName(table.Parent), StringComparison.OrdinalIgnoreCase));
    public static IEnumerable<TableDefinition> Ancestors(TemplateDefinition template, TableDefinition table)
    {
        var seen = new HashSet<TableDefinition>();
        while (Parent(template, table) is { } parent && seen.Add(parent)) { yield return parent; table = parent; }
    }
    public static bool SameType(ColumnDefinition left, ColumnDefinition right) => CanonicalType(left).Equals(CanonicalType(right), StringComparison.OrdinalIgnoreCase);
    private static string CanonicalType(ColumnDefinition column) => SqlTypes.Declaration(column).Replace("decimal(", "numeric(", StringComparison.OrdinalIgnoreCase);
    public static string InheritedPath(TableDefinition parent, ColumnDefinition key) => $"@{GraphEditor.SqlName(parent.Name)}.{key.Name}";
    public static ColumnDefinition? ChildKeyColumn(TableDefinition child, TableDefinition parent, ColumnDefinition key)
    {
        var pairs = child.ParentKey.Pairs.Count > 0 ? child.ParentKey.Pairs : [new KeyPair { Column = child.ParentKey.Column, References = child.ParentKey.References }];
        var pair = child.ParentKey.Strategy == "businessKey" ? pairs.FirstOrDefault(p => p.References.Equals(key.Name, StringComparison.OrdinalIgnoreCase)) : null;
        if (pair is not null && Validation.TemplateValidator.Identifier(pair.Column)) return child.Columns.FirstOrDefault(c => c.Name.Equals(pair.Column, StringComparison.OrdinalIgnoreCase));
        return child.Columns.FirstOrDefault(c => c.Path == InheritedPath(parent, key))
            ?? child.Columns.FirstOrDefault(c => c.Name.Equals(key.Name, StringComparison.OrdinalIgnoreCase));
    }
    public static void Synchronize(TemplateDefinition template, bool generateMissingColumns = true)
    {
        foreach (var table in template.Tables)
        {
            var parent = Parent(template, table); if (parent is null) continue;
            var key = table.ParentKey;
            if (parent.RowKey)
            {
                key.Strategy = "rowKey"; key.References = "_RowKey";
                key.Column = "_" + GraphEditor.ShortName(parent.Name) + "RowKey";
                if (key.Pairs.Count > 0) key.Pairs.Clear();
            }
            else
            {
                var wasGenerated = key.Strategy == "rowKey";
                key.Strategy = "businessKey";
                var keys = parent.Columns.Where(c => c.Key).ToList();
                if (!keys.Any(c => c.Name == key.References)) key.References = keys.Count == 1 ? keys[0].Name : "";
                if (wasGenerated) { key.Column = key.References; if (key.Pairs.Count > 0) key.Pairs.Clear(); }
            }
            if (!generateMissingColumns) continue;
            var inheritedPairs = new List<KeyPair>();
            foreach (var parentKey in parent.Columns.Where(c => c.Key).ToList())
            {
                var child = ChildKeyColumn(table, parent, parentKey);
                if (child is null)
                {
                    var existingPair = key.Strategy == "businessKey" ? key.Pairs.FirstOrDefault(p => p.References == parentKey.Name) : null;
                    var destination = existingPair is not null && Validation.TemplateValidator.Identifier(existingPair.Column) ? existingPair.Column : key.Strategy == "businessKey" && key.References == parentKey.Name && Validation.TemplateValidator.Identifier(key.Column) ? key.Column : parentKey.Name;
                    child = new ColumnDefinition { Name = destination, Path = InheritedPath(parent, parentKey), Type = SqlTypes.Length(parentKey.Type).HasValue ? SqlTypes.Base(parentKey.Type) : parentKey.Type, MaxLength = parentKey.MaxLength ?? SqlTypes.Length(parentKey.Type), Required = true, Key = false };
                    table.Columns.Add(child);
                }
                else if (SameType(child, parentKey))
                {
                    child.Path = InheritedPath(parent, parentKey); child.ConstantJson = ""; child.Required = true;
                }
                inheritedPairs.Add(new() { Column = child.Name, References = parentKey.Name });
            }
            if (!parent.RowKey)
            {
                key.Pairs.Clear();
                if (inheritedPairs.Count == 1) { key.Column = inheritedPairs[0].Column; key.References = inheritedPairs[0].References; }
                else if (inheritedPairs.Count > 1) foreach (var pair in inheritedPairs) key.Pairs.Add(pair);
            }
        }
    }
}
