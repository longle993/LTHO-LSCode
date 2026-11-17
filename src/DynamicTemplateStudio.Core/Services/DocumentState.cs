using System.Text.Json;
using DynamicTemplateStudio.Core.Models;

namespace DynamicTemplateStudio.Core.Services;

public static class DocumentState
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };
    public static string Capture(ProjectDefinition project) => JsonSerializer.Serialize(project, Options);
    public static ProjectDefinition Restore(string json) => JsonSerializer.Deserialize<ProjectDefinition>(json, Options) ?? throw new InvalidDataException("Invalid draft.");
    public static T Clone<T>(T value) => JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(value))!;
}

public sealed class History
{
    private readonly List<string> states = [];
    private int index = -1;
    public bool CanUndo => index > 0;
    public bool CanRedo => index < states.Count - 1;
    public void Reset(ProjectDefinition project) { states.Clear(); index = -1; Push(project); }
    public void Push(ProjectDefinition project)
    {
        var state = DocumentState.Capture(project);
        if (index >= 0 && states[index] == state) return;
        states.RemoveRange(index + 1, states.Count - index - 1);
        states.Add(state); index++;
        if (states.Count > 200) { states.RemoveAt(0); index--; }
    }
    public ProjectDefinition? Undo() => CanUndo ? DocumentState.Restore(states[--index]) : null;
    public ProjectDefinition? Redo() => CanRedo ? DocumentState.Restore(states[++index]) : null;
}
