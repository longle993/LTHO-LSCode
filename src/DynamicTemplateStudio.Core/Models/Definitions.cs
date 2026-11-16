using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DynamicTemplateStudio.Core.Models;

public abstract class Bindable : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    protected bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        PropertyChanged?.Invoke(this, new(name));
        return true;
    }
    protected void Raise(string name) => PropertyChanged?.Invoke(this, new(name));
}

public sealed class ProjectDefinition : Bindable
{
    private string name = "Untitled", description = "";
    public string ProjectName { get => name; set => Set(ref name, value); }
    public string Description { get => description; set => Set(ref description, value); }
    public int SchemaVersion { get; set; } = 1;
    public ObservableCollection<TemplateDefinition> Templates { get; set; } = [];
    public Dictionary<string, JsonElement> Extra { get; set; } = [];
}

public sealed class TemplateDefinition : Bindable
{
    public Guid Id { get; set; } = Guid.NewGuid();
    private string code = "new.template", execute = "dbo.usp_Import", displayName = "", description = "";
    private int version = 1;
    private bool transactionEnabled = true;
    public string TemplateCode { get => code; set => Set(ref code, value); }
    public string Execute { get => execute; set => Set(ref execute, value); }
    public int Version { get => version; set => Set(ref version, value); }
    public string DisplayName { get => displayName; set => Set(ref displayName, value); }
    public string Description { get => description; set => Set(ref description, value); }
    public bool TransactionEnabled { get => transactionEnabled; set => Set(ref transactionEnabled, value); }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset ModifiedAt { get; set; } = DateTimeOffset.UtcNow;
    public string SampleJson { get; set; } = "{}";
    public ObservableCollection<TableDefinition> Tables { get; set; } = [];
    public Dictionary<string, JsonElement> Extra { get; set; } = [];
}

public sealed class TableDefinition : Bindable
{
    public override string ToString() => Name;
    private string name = "Table", from = "$", parent = "", mode = "array";
    private bool rowKey;
    public string Name { get => name; set => Set(ref name, value); }
    public string From { get => from; set => Set(ref from, value); }
    public string Parent { get => parent; set => Set(ref parent, value ?? ""); }
    public string Mode { get => mode; set => Set(ref mode, value); }
    public bool RowKey { get => rowKey; set => Set(ref rowKey, value); }
    public ParentKeyDefinition ParentKey { get; set; } = new();
    public ObservableCollection<ColumnDefinition> Columns { get; set; } = [];
    public Dictionary<string, JsonElement> Extra { get; set; } = [];
}

public sealed class ParentKeyDefinition : Bindable
{
    private string strategy = "rowKey", column = "_ParentRowKey", references = "_RowKey";
    public string Strategy { get => strategy; set => Set(ref strategy, value); }
    public string Column { get => column; set => Set(ref column, value); }
    public string References { get => references; set => Set(ref references, value); }
    // Multiple pairs are retained in Studio metadata until the Engine wire contract is supplied.
    public ObservableCollection<KeyPair> Pairs { get; set; } = [];
    public Dictionary<string, JsonElement> Extra { get; set; } = [];
}

public sealed class KeyPair : Bindable
{
    private string column = "", references = "";
    public string Column { get => column; set => Set(ref column, value); }
    public string References { get => references; set => Set(ref references, value); }
}

public sealed class ColumnDefinition : Bindable
{
    private string name = "Field", path = "$.field", type = "nvarchar", description = "", defaultJson = "", constantJson = "", references = "";
    private bool required, key;
    private string allowedValuesText = "";
    private int? maxLength;
    public string Name { get => name; set => Set(ref name, value); }
    public string Path { get => path; set { if (Set(ref path, value ?? "")) Raise(nameof(Property)); } }
    [JsonIgnore]
    public string Property
    {
        get => Path.StartsWith("$.", StringComparison.Ordinal) ? Path[2..] : Path;
        set { Path = Services.PropertyMapping.Normalize(value); if (ConstantJson.Length > 0) ConstantJson = ""; }
    }
    public string Type { get => type; set => Set(ref type, value); }
    public bool Required { get => required; set => Set(ref required, value); }
    public bool Key { get => key; set => Set(ref key, value); }
    public string DefaultJson { get => defaultJson; set => Set(ref defaultJson, value); }
    public string ConstantJson { get => constantJson; set => Set(ref constantJson, value); }
    public int? MaxLength { get => maxLength; set => Set(ref maxLength, value); }
    public string AllowedValuesText { get => allowedValuesText; set => Set(ref allowedValuesText, value ?? ""); }
    public string References { get => references; set => Set(ref references, value); }
    public string Description { get => description; set => Set(ref description, value); }
    public Dictionary<string, JsonElement> Extra { get; set; } = [];
}

public sealed record Diagnostic(string Message, string Table = "", string Field = "", string Location = "", bool IsWarning = false)
{
    public override string ToString() => $"{(IsWarning ? "Warning" : "Error")} · {Table}{(Field.Length > 0 ? "." + Field : "")} {Location} — {Message}";
}

public sealed class MappingResult
{
    public Dictionary<string, List<Dictionary<string, object?>>> Tables { get; } = [];
    public List<Diagnostic> Diagnostics { get; } = [];
    public bool Truncated { get; set; }
}
