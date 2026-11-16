using System.Text.Json;
using System.Text.RegularExpressions;
using DynamicTemplateStudio.Core.Interfaces;
using DynamicTemplateStudio.Core.Models;
using DynamicTemplateStudio.Core.Services;
using DynamicTemplateStudio.Core.Validation;

namespace DynamicTemplateStudio.Infrastructure.JsonPath;

public sealed class MappingPreviewService : IMappingPreviewService
{
    private sealed record Context(TableDefinition Table, JsonElement Input, Dictionary<string, object?> Values, Context? Parent);
    public MappingResult Run(TemplateDefinition template, string sample, CancellationToken cancellation = default, int rowLimit = 100000)
    {
        var result = new MappingResult();
        result.Diagnostics.AddRange(TemplateValidator.Validate(template));
        if (result.Diagnostics.Any(e => !e.IsWarning)) return result;
        if (template.Extra.Count > 0 || template.Tables.Any(t => t.Extra.Count > 0 || t.ParentKey.Extra.Count > 0 || t.Columns.Any(c => c.Extra.Keys.Any(k => k is not ("key" or "required")))))
            result.Diagnostics.Add(new("Unknown Engine properties are retained in JSON, but their runtime behavior is not simulated by Studio.", IsWarning: true));
        using var document = JsonDocument.Parse(sample, new JsonDocumentOptions { MaxDepth = 512 });
        var counters = template.Tables.ToDictionary(t => t, _ => 0L);
        var unique = template.Tables.ToDictionary(t => t, _ => new HashSet<string>());
        foreach (var table in template.Tables) result.Tables[table.Name] = [];
        var stack = new Stack<(TableDefinition Table, JsonElement Input, Context? Parent)>();
        foreach (var root in template.Tables.Where(t => t.Parent.Length == 0).Reverse()) stack.Push((root, document.RootElement, null));
        int total = 0;
        while (stack.Count > 0)
        {
            cancellation.ThrowIfCancellationRequested();
            var (table, input, parent) = stack.Pop();
            var selected = PathExpression.Select(input, table.From);
            if (table.Mode == "array" && selected.Count == 1 && selected[0].ValueKind == JsonValueKind.Array) selected = selected[0].EnumerateArray().ToList();
            if (table.Mode == "object" && selected.Count > 1) { result.Diagnostics.Add(new("Object mode matched multiple values.", table.Name)); continue; }
            List<Context> contexts = [];
            foreach (var node in selected)
            {
                cancellation.ThrowIfCancellationRequested();
                if (node.ValueKind != JsonValueKind.Object) { result.Diagnostics.Add(new("Table source must select JSON objects. Primitive arrays and scalar table sources are unsupported.", table.Name)); continue; }
                if (++total > rowLimit) { result.Truncated = true; result.Diagnostics.Add(new("Preview row limit reached; results and validation are partial.", IsWarning: true)); return result; }
                Dictionary<string, object?> values = [];
                var ordinal = ++counters[table];
                if (table.RowKey) values["_RowKey"] = ordinal;
                var context = new Context(table, node, values, parent);
                foreach (var field in table.Columns)
                {
                    try
                    {
                        object? mapped;
                        var foreignKey = parent is not null && table.ParentKey.Strategy == "businessKey" ? TemplateValidator.Pairs(table).FirstOrDefault(p => p.Column.Equals(field.Name, StringComparison.OrdinalIgnoreCase)) : null;
                        if (foreignKey is not null)
                        {
                            parent!.Values.TryGetValue(foreignKey.References, out var inheritedValue);
                            mapped = ValueConverter.Convert(JsonSerializer.SerializeToElement(inheritedValue), field.Type, field.MaxLength);
                        }
                        else if (field.ConstantJson.Length > 0) { using var c = JsonDocument.Parse(field.ConstantJson); mapped = ValueConverter.Convert(c.RootElement, field.Type, field.MaxLength); }
                        else
                        {
                            var raw = Resolve(field.Path, context);
                            if (raw.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null && field.DefaultJson.Length > 0) { using var d = JsonDocument.Parse(field.DefaultJson); raw = d.RootElement.Clone(); }
                            mapped = ValueConverter.Convert(raw, field.Type, field.MaxLength);
                        }
                        if (mapped is null && (field.Required || field.Key)) throw new FormatException("Required/key value is missing or null.");
                        AllowedValuesRule.Validate(field, mapped);
                        values[field.Name] = mapped;
                    }
                    catch (Exception e) when (e is FormatException or OverflowException or JsonException or InvalidOperationException)
                    { values[field.Name] = null; result.Diagnostics.Add(new(e.Message, table.Name, field.Name, $"record {ordinal}")); }
                }
                if (parent is not null)
                    foreach (var pair in TemplateValidator.Pairs(table))
                    {
                        if (table.ParentKey.Strategy == "businessKey" && values.ContainsKey(pair.Column)) continue;
                        if (parent.Values.TryGetValue(pair.References, out var value)) values[pair.Column] = value;
                        else result.Diagnostics.Add(new("Parent mapped key was not found.", table.Name, pair.Column, $"record {ordinal}"));
                    }
                var keys = table.Columns.Where(c => c.Key).Select(c => values.GetValueOrDefault(c.Name)).ToArray();
                if (keys.Length > 0 && keys.All(v => v is not null) && !unique[table].Add(JsonSerializer.Serialize(keys))) result.Diagnostics.Add(new("Duplicate business key tuple (Studio policy: entire table).", table.Name, Location: $"record {ordinal}"));
                result.Tables[table.Name].Add(values); contexts.Add(context);
            }
            foreach (var ctx in contexts.AsEnumerable().Reverse())
                foreach (var child in template.Tables.Where(t => GraphEditor.ShortName(t.Parent).Equals(GraphEditor.ShortName(table.Name), StringComparison.OrdinalIgnoreCase)).Reverse()) stack.Push((child, ctx.Input, ctx));
        }
        return result;
    }
    private static JsonElement Resolve(string expression, Context context)
    {
        var current = context;
        if (expression.StartsWith("@#"))
        {
            int dot = expression.IndexOf('.'); var name = expression[1..dot]; var field = expression[(dot + 1)..];
            current = current.Parent;
            while (current is not null && GraphEditor.SqlName(current.Table.Name) != name) current = current.Parent;
            if (current is null) throw new FormatException("Named table is not an ancestor in this record context.");
            if (!current.Values.TryGetValue(field, out var value)) throw new FormatException("Mapped ancestor field not found.");
            return JsonSerializer.SerializeToElement(value);
        }
        if (expression.StartsWith("@parent.")) { current = current.Parent ?? throw new FormatException("Parent context not available."); expression = "$" + expression[7..]; }
        else if (expression.StartsWith("@ancestor("))
        {
            var match = Regex.Match(expression, @"^@ancestor\((\d+)\)(\..+)$");
            for (int i = 0; i < int.Parse(match.Groups[1].Value); i++) current = current.Parent ?? throw new FormatException("Ancestor depth is outside this context.");
            expression = "$" + match.Groups[2].Value;
        }
        var selected = PathExpression.Select(current.Input, expression);
        if (selected.Count > 1) throw new FormatException("A field mapping must return one scalar value.");
        return selected.Count == 0 ? default : selected[0];
    }
}
