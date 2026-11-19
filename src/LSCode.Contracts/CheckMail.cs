namespace LSCode.Contracts;

public sealed class CheckMailRequest
{
    public string Host { get; init; } = "";
    public int Port { get; init; }
    public string Security { get; init; } = "";
    public string From { get; init; } = "";
    public string Alias { get; init; } = "";
    public string Account { get; init; } = "";
    public string Password { get; init; } = "";
    public string To { get; init; } = "";
    public string Cc { get; init; } = "";
    public string Bcc { get; init; } = "";
    public string Subject { get; init; } = "";
    public string Body { get; init; } = "";
    public bool Html { get; init; }
}

public sealed record CheckMailResult(bool Success, string Message, string? Field = null);
