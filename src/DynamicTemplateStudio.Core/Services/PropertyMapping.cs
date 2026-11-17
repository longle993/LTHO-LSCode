namespace DynamicTemplateStudio.Core.Services;

public static class PropertyMapping
{
    public static string Normalize(string? value)
    {
        value = value?.Trim() ?? "";
        if (value.Length == 0 || value.StartsWith('$') || value.StartsWith('@')) return value;
        return value.StartsWith('.') || value.StartsWith('[') ? "$" + value : "$." + value;
    }
}
