namespace LSCode.Core;

public record DocumentContext(string? Path, string Text, string Selection, long Version);
public record AgentContext(string? Workspace, DocumentContext Document, IReadOnlyList<string> OpenFiles);
public record AgentProposal(string Message, string? SuggestedText);
// No provider receives implicit filesystem, process or database access.
public interface IAgentProvider
{
    string Name { get; }
    Task<AgentProposal> AskAsync(string prompt, AgentContext context, CancellationToken cancellationToken);
}
public record IdeCommand(string Id, string Name, string Shortcut, Func<Task> Execute)
{
    public override string ToString() => $"{Name}    {Shortcut}";
}
