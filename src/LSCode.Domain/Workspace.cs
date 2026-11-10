namespace LSCode.Domain;

public sealed record DocumentInfo(string Id, string? Path, string Name, string Text, string Encoding, bool Bom, string Newline, long Version);
public sealed record Entry(string Path, string Name, bool Directory);
public sealed record TargetInfo(string Id, string Server, string Database);
public sealed record ResultSet(string[] Columns, string?[][] Rows);
public sealed record ExecutionResult(ResultSet[] Tables, string[] Messages, double DurationMs, bool Truncated);
public sealed record ProjectInfo(string Id, string Name, string Server, string Database, string SourcePath);
public sealed record SnippetInfo(string Key, string Category, string Body, string Description, int Position, string Param);
