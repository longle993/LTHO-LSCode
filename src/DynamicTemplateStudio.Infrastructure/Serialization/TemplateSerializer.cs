using System.Text.Json;
using System.Text.Json.Nodes;
using DynamicTemplateStudio.Core.Interfaces;
using DynamicTemplateStudio.Core.Models;
using DynamicTemplateStudio.Core.Services;

namespace DynamicTemplateStudio.Infrastructure.Serialization;

public sealed class TemplateSerializer : ITemplateSerializer
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };
    public string Serialize(TemplateDefinition template)
    {
        if (template.Tables.Any(t => t.Parent.Length > 0 && t.ParentKey.Pairs.Count > 1)) throw new InvalidOperationException("Composite parentKey runtime encoding is unconfirmed. All fields and key pairs remain in Studio draft; runtime JSON preview/export is unavailable for this configuration.");
        var root = Extras(template.Extra);
        root["template"] = template.TemplateCode; root["version"] = template.Version; root["execute"] = template.Execute;
        JsonArray tables = [];
        foreach (var t in template.Tables)
        {
            var table = Extras(t.Extra);
            table["name"] = GraphEditor.SqlName(t.Name); table["from"] = t.From; table["mode"] = t.Mode; table["rowKey"] = t.RowKey;
            if (t.Parent.Length > 0)
            {
                table["parent"] = GraphEditor.SqlName(t.Parent);
                var pk = Extras(t.ParentKey.Extra);
                var pair = Core.Validation.TemplateValidator.Pairs(t).First();
                pk["strategy"] = t.ParentKey.Strategy; pk["column"] = pair.Column; pk["references"] = pair.References;
                table["parentKey"] = pk;
            }
            JsonObject columns = [];
            foreach (var c in t.Columns)
            {
                var column = Extras(c.Extra);
                column["path"] = c.Path; column["type"] = SqlTypes.Declaration(c);
                if (c.Key || column.ContainsKey("key")) column["key"] = c.Key;
                if (c.Required || column.ContainsKey("required")) column["required"] = c.Required;
                if (c.DefaultJson.Length > 0) column["default"] = JsonNode.Parse(c.DefaultJson);
                var allowed = AllowedValuesRule.Parse(c.AllowedValuesText);
                if (allowed.Count > 0) column["allowedValues"] = new JsonArray(allowed.Select(v => (JsonNode?)JsonValue.Create(v)).ToArray());
                columns.Add(c.Name, column);
            }
            table["columns"] = columns; tables.Add(table);
        }
        root["tables"] = tables;
        return root.ToJsonString(Options);
    }
    public TemplateDefinition Deserialize(string json)
    {
        using var doc = JsonDocument.Parse(json);
        RejectDuplicates(doc.RootElement);
        var root = JsonNode.Parse(json)?.AsObject() ?? throw new JsonException("Template must be an object.");
        var template = new TemplateDefinition { TemplateCode = Text(root, "template"), Execute = Text(root, "execute"), Version = root["version"]?.GetValue<int>() ?? 1, Extra = Unknown(root, "template", "version", "execute", "tables") };
        foreach (var node in root["tables"]?.AsArray() ?? throw new JsonException("Missing tables array."))
        {
            var t = node?.AsObject() ?? throw new JsonException("Invalid table.");
            var table = new TableDefinition { Name = GraphEditor.ShortName(Text(t, "name")), Parent = GraphEditor.ShortName(t["parent"]?.GetValue<string>() ?? ""), From = Text(t, "from"), Mode = Text(t, "mode"), RowKey = t["rowKey"]?.GetValue<bool>() ?? false, Extra = Unknown(t, "name", "parent", "from", "mode", "rowKey", "columns", "parentKey") };
            if (t["parentKey"] is JsonObject pk) table.ParentKey = new() { Strategy = Text(pk, "strategy"), Column = Text(pk, "column"), References = Text(pk, "references"), Extra = Unknown(pk, "strategy", "column", "references") };
            foreach (var (name, definition) in t["columns"]?.AsObject() ?? throw new JsonException("Missing columns object."))
            {
                var c = definition?.AsObject() ?? throw new JsonException("Invalid column.");
                var declaration = Text(c, "type"); var length = SqlTypes.Length(declaration);
                var allowed = c["allowedValues"] is null ? "" : AllowedValuesRule.Format(c["allowedValues"]!.AsArray().Select(v => v?.GetValue<string>() ?? throw new JsonException("allowedValues must contain strings.")));
                table.Columns.Add(new() { Name = name, Path = Text(c, "path"), Type = length.HasValue ? SqlTypes.Base(declaration) : declaration, MaxLength = length, AllowedValuesText = allowed, Key = c["key"]?.GetValue<bool>() ?? false, Required = c["required"]?.GetValue<bool>() ?? false, DefaultJson = c.ContainsKey("default") ? c["default"]?.ToJsonString() ?? "null" : "", Extra = Unknown(c, "path", "type", "default", "allowedValues") });
            }
            template.Tables.Add(table);
        }
        return template;
    }
    private static string Text(JsonObject obj, string key) => obj[key]?.GetValue<string>() ?? throw new JsonException($"Missing string '{key}'.");
    private static JsonObject Extras(Dictionary<string, JsonElement> extras) => JsonNode.Parse(JsonSerializer.Serialize(extras))!.AsObject();
    private static Dictionary<string, JsonElement> Unknown(JsonObject obj, params string[] known) => obj.Where(p => !known.Contains(p.Key)).ToDictionary(p => p.Key, p => JsonSerializer.SerializeToElement(p.Value));
    private static void RejectDuplicates(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>();
            foreach (var p in element.EnumerateObject()) { if (!names.Add(p.Name)) throw new JsonException($"Duplicate property '{p.Name}'."); RejectDuplicates(p.Value); }
        }
        else if (element.ValueKind == JsonValueKind.Array) foreach (var item in element.EnumerateArray()) RejectDuplicates(item);
    }
}
