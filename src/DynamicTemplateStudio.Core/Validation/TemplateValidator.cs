using System.Text.Json;
using System.Text.RegularExpressions;
using DynamicTemplateStudio.Core.Models;
using DynamicTemplateStudio.Core.Services;

namespace DynamicTemplateStudio.Core.Validation;

public static class TemplateValidator
{
    public static readonly string[] Types = SqlTypes.Choices;
    public static bool Identifier(string? name) => name is not null && name.Length <= 116 && Regex.IsMatch(name, "^[A-Za-z_][A-Za-z0-9_]*$");
    public static bool SafeCode(string code) => code.Length is > 0 and <= 100 && Regex.IsMatch(code, "^[A-Za-z_][A-Za-z0-9_.-]*$") && !code.EndsWith('.') && !Regex.IsMatch(code.Split('.')[0], "^(CON|PRN|AUX|NUL|COM[1-9]|LPT[1-9])$", RegexOptions.IgnoreCase);
    public static bool ValidType(string? type) => SqlTypes.IsValid(type);
    public static (int Precision, int Scale) DecimalParts(string type)
    {
        return SqlTypes.PrecisionScale(type);
    }
    public static List<Diagnostic> Validate(TemplateDefinition template, bool runtimeExport = false)
    {
        List<Diagnostic> errors = [];
        void Add(string text, TableDefinition? t = null, ColumnDefinition? c = null) => errors.Add(new(text, t?.Name ?? "", c?.Name ?? ""));
        if (!SafeCode(template.TemplateCode)) Add("Template code must be a safe, unique filename (letters, numbers, dot, dash, underscore).");
        if (!Regex.IsMatch(template.Execute, @"^[A-Za-z_][A-Za-z0-9_]*\.[A-Za-z_][A-Za-z0-9_]*$")) Add("Procedure must be schema.name; SQL scripts are not allowed.");
        if (template.Version < 1) Add("Version must be positive.");
        if (template.Tables.Count == 0) Add("Add at least one table.");
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var t in template.Tables)
        {
            if (!Identifier(GraphEditor.ShortName(t.Name)) || t.Name.StartsWith("##")) Add("Invalid table identifier (maximum 116 characters).", t);
            if (!names.Add(GraphEditor.ShortName(t.Name))) Add("Duplicate table name.", t);
            if (t.Mode is not ("array" or "object")) Add("Mode must be object or array.", t);
            try { PathExpression.Parse(t.From); } catch (FormatException e) { Add(e.Message, t); }
            var ancestor = t; var seen = new HashSet<TableDefinition>();
            while (!string.IsNullOrEmpty(ancestor.Parent))
            {
                if (!seen.Add(ancestor)) { Add("Cyclic parent relationship.", t); break; }
                var p = template.Tables.FirstOrDefault(x => GraphEditor.ShortName(x.Name).Equals(GraphEditor.ShortName(ancestor.Parent), StringComparison.OrdinalIgnoreCase));
                if (p is null) { Add("Parent table does not exist.", t); break; }
                ancestor = p;
            }
            var parent = template.Tables.FirstOrDefault(x => GraphEditor.ShortName(x.Name).Equals(GraphEditor.ShortName(t.Parent), StringComparison.OrdinalIgnoreCase));
            var fieldNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var c in t.Columns)
            {
                if (!Identifier(c.Name)) Add("Invalid field identifier.", t, c);
                if (!fieldNames.Add(c.Name)) Add("Duplicate field name.", t, c);
                if (c.Name.Equals("_RowKey", StringComparison.OrdinalIgnoreCase)) Add("_RowKey is reserved for generated values.", t, c);
                if (!ValidType(c.Type)) Add("Unsupported data type or decimal precision/scale.", t, c);
                if (c.MaxLength is <= 0) Add("Max length must be positive.", t, c);
                if (SqlTypes.Base(c.Type) is "char" or "varchar" or "nvarchar" && c.MaxLength > (SqlTypes.Base(c.Type) == "nvarchar" ? 4000 : 8000)) Add("Max length exceeds the SQL type limit.", t, c);
                if (c.MaxLength.HasValue && !SqlTypes.IsText(c.Type)) Add("Max length is only valid for text types.", t, c);
                try
                {
                    var allowed = AllowedValuesRule.Parse(c.AllowedValuesText);
                    if (allowed.Count > 0 && !SqlTypes.IsText(c.Type)) Add("Allowed values are only supported for text types.", t, c);
                    else foreach (var item in allowed) ValueConverter.Convert(JsonSerializer.SerializeToElement(item), c.Type, c.MaxLength);
                }
                catch (Exception e) when (e is FormatException or InvalidOperationException or OverflowException) { Add("Allowed values: " + e.Message, t, c); }
                if (c.References.Length > 0)
                {
                    var dot = c.References.IndexOf('.');
                    var rt = dot > 0 ? template.Tables.FirstOrDefault(x => GraphEditor.SqlName(x.Name) == c.References[..dot]) : null;
                    var rc = rt?.Columns.FirstOrDefault(x => x.Name == c.References[(dot + 1)..]);
                    if (rc is null) Add("Reference table/column does not exist; use #Table.Field.", t, c);
                    else if (rc.Type != c.Type) Add("Referenced field type must match.", t, c);
                }
                if (c.ConstantJson.Length == 0)
                {
                    var path = c.Path;
                    if (path.StartsWith("@parent.")) { if (t.Parent.Length == 0) Add("No parent context.", t, c); path = "$" + path[7..]; }
                    else if (path.StartsWith("@ancestor("))
                    {
                        var match = Regex.Match(path, @"^@ancestor\(([1-9]\d*)\)(\..+)$");
                        if (!match.Success) Add("Use @ancestor(n).field with n > 0.", t, c);
                        path = match.Success ? "$" + match.Groups[2].Value : "invalid";
                    }
                    else if (path.StartsWith("@#"))
                    {
                        var split = path.IndexOf('.');
                        if (split < 3) Add("Use @#Table.Field.", t, c);
                        else
                        {
                            var target = template.Tables.FirstOrDefault(x => GraphEditor.SqlName(x.Name) == path[1..split]);
                            if (target is null || !target.Columns.Any(x => x.Name == path[(split + 1)..])) Add("Named mapped ancestor field does not exist.", t, c);
                            else if (!RelationshipEditor.Ancestors(template, t).Contains(target)) Add("Named mapped table is not an ancestor of this table.", t, c);
                        }
                        path = "$";
                    }
                    try { PathExpression.Parse(path); } catch (FormatException e) { Add(e.Message, t, c); }
                }
                foreach (var json in new[] { c.DefaultJson, c.ConstantJson }.Where(x => x.Length > 0))
                    try { using var doc = JsonDocument.Parse(json); var converted = ValueConverter.Convert(doc.RootElement, c.Type, c.MaxLength); AllowedValuesRule.Validate(c, converted); } catch (Exception e) when (e is FormatException or JsonException or OverflowException or InvalidOperationException) { Add("Default/constant: " + e.Message, t, c); }
                if (runtimeExport && (c.ConstantJson.Length > 0 || c.MaxLength.HasValue && SqlTypes.Base(c.Type) == "string" || c.References.Length > 0)) Add("Engine contract for constant/legacy string length/references is not confirmed. Save as Studio draft instead.", t, c);
            }
            if (parent is not null)
            {
                var pairs = Pairs(t).ToList();
                foreach (var parentField in parent.Columns.Where(c => c.Key))
                {
                    var inherited = RelationshipEditor.ChildKeyColumn(t, parent, parentField);
                    if (inherited is null) { Add($"Child must include parent key '{parentField.Name}'. Reassign Parent Table to generate missing fields.", t); continue; }
                    if (!RelationshipEditor.SameType(inherited, parentField)) Add($"Inherited key '{inherited.Name}' must match parent type/length {SqlTypes.Declaration(parentField)}.", t, inherited);
                    var mappedByPair = t.ParentKey.Strategy == "businessKey" && pairs.Any(p => p.Column.Equals(inherited.Name, StringComparison.OrdinalIgnoreCase) && p.References.Equals(parentField.Name, StringComparison.OrdinalIgnoreCase));
                    if (!mappedByPair && inherited.Path != RelationshipEditor.InheritedPath(parent, parentField)) Add($"Inherited key must map from {RelationshipEditor.InheritedPath(parent, parentField)}.", t, inherited);
                }
                if (t.ParentKey.Strategy is not ("rowKey" or "businessKey")) Add("Select a supported join strategy.", t);
                if (t.ParentKey.Strategy == "rowKey" && !parent.RowKey) Add("Parent must enable Generate _RowKey.", t);
                foreach (var pair in pairs)
                {
                    if (!Identifier(pair.Column)) Add("Parent key destination must be a valid identifier.", t);
                    if (pair.Column.Equals("_RowKey", StringComparison.OrdinalIgnoreCase)) Add("Parent key cannot overwrite the current _RowKey.", t);
                    if (t.Columns.Any(c => c.Name.Equals(pair.Column, StringComparison.OrdinalIgnoreCase)) && t.ParentKey.Strategy == "rowKey") Add("Generated parent key conflicts with a field.", t);
                    if (t.ParentKey.Strategy == "rowKey" && pair.References != "_RowKey") Add("RowKey relationship must reference _RowKey.", t);
                    if (t.ParentKey.Strategy == "businessKey")
                    {
                        var pc = parent.Columns.FirstOrDefault(c => c.Name == pair.References && c.Key);
                        if (pc is null) Add("Reference must be a parent business key field.", t);
                        var child = t.Columns.FirstOrDefault(c => c.Name == pair.Column);
                        if (child is null) Add($"Child key column '{pair.Column}' does not exist.", t);
                        if (pc is not null && child is not null && !RelationshipEditor.SameType(pc, child)) Add("Parent and child key types/length must match.", t);
                    }
                }
                if (pairs.Select(p => p.Column).Distinct(StringComparer.OrdinalIgnoreCase).Count() != pairs.Count) Add("Duplicate parent key destination.", t);
                if (t.ParentKey.Strategy == "businessKey" && parent.Columns.Where(c => c.Key).Any(c => !pairs.Any(p => p.References == c.Name))) Add("Map every component of the parent business key.", t);
                if (runtimeExport && pairs.Count > 1) Add("Composite parentKey runtime shape is unconfirmed; use Studio draft.", t);
            }
        }
        return errors;
    }
    public static IEnumerable<KeyPair> Pairs(TableDefinition table) => table.ParentKey.Pairs.Count > 0 ? table.ParentKey.Pairs : [new KeyPair { Column = table.ParentKey.Column, References = table.ParentKey.References }];
}
