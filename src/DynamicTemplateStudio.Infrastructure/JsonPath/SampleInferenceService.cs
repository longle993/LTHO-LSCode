using System.Text.Json;
using System.Text.RegularExpressions;
using DynamicTemplateStudio.Core.Interfaces;
using DynamicTemplateStudio.Core.Models;
using DynamicTemplateStudio.Core.Services;

namespace DynamicTemplateStudio.Infrastructure.JsonPath;

public sealed class SampleInferenceService : ISampleInferenceService
{
    public TemplateDefinition Infer(string sample)
    {
        using var doc = JsonDocument.Parse(sample, new JsonDocumentOptions { MaxDepth = 512 });
        var result = new TemplateDefinition { TemplateCode = "sample.import", SampleJson = sample };
        var queue = new Queue<(JsonElement Element, string Name, string From, string Parent)>();
        if (doc.RootElement.ValueKind == JsonValueKind.Object && doc.RootElement.EnumerateObject().All(p => p.Value.ValueKind is JsonValueKind.Array or JsonValueKind.Object))
            foreach (var p in doc.RootElement.EnumerateObject()) queue.Enqueue((p.Value, p.Name, PathExpression.PropertyPath(p.Name) + (p.Value.ValueKind == JsonValueKind.Array ? "[*]" : ""), ""));
        else queue.Enqueue((doc.RootElement, "Root", "$", ""));
        while (queue.Count > 0)
        {
            var (element, name, from, parent) = queue.Dequeue();
            name = Regex.Replace(name, "[^A-Za-z0-9_]", "_"); if (name.Length == 0 || char.IsDigit(name[0])) name = "Table_" + name;
            var baseName = name; int n = 2; while (result.Tables.Any(t => t.Name == name)) name = baseName + n++;
            var table = new TableDefinition { Name = name, From = from, Parent = parent, Mode = element.ValueKind == JsonValueKind.Array ? "array" : "object" };
            result.Tables.Add(table);
            var samples = element.ValueKind == JsonValueKind.Array ? element.EnumerateArray().Take(100).ToList() : [element];
            var properties = samples.Where(x => x.ValueKind == JsonValueKind.Object).SelectMany(x => x.EnumerateObject()).GroupBy(p => p.Name);
            foreach (var group in properties)
            {
                var values = group.Select(p => p.Value).Where(v => v.ValueKind != JsonValueKind.Null).ToList();
                var first = values.FirstOrDefault();
                if (first.ValueKind is JsonValueKind.Object or JsonValueKind.Array) queue.Enqueue((first, group.Key, PathExpression.PropertyPath(group.Key) + (first.ValueKind == JsonValueKind.Array ? "[*]" : ""), name));
                else
                {
                    var fieldName = Regex.Replace(group.Key, "[^A-Za-z0-9_]", "_"); if (fieldName.Length == 0 || char.IsDigit(fieldName[0])) fieldName = "Field_" + fieldName;
                    var type = values.Count > 0 && values.All(v => v.ValueKind is JsonValueKind.True or JsonValueKind.False) ? "bit" : values.Count > 0 && values.All(v => v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out _)) ? "int" : values.Count > 0 && values.All(v => v.ValueKind == JsonValueKind.Number && v.TryGetInt64(out _)) ? "bigint" : values.Count > 0 && values.All(v => v.ValueKind == JsonValueKind.Number) ? "numeric(19,5)" : "nvarchar";
                    table.Columns.Add(new() { Name = fieldName, Path = PathExpression.PropertyPath(group.Key), Type = type, Description = "Inferred from up to 100 sample records; review type and nullability." });
                }
            }
            if (table.Parent.Length > 0) table.ParentKey.Column = "_" + table.Parent + "RowKey";
        }
        return result;
    }
}
