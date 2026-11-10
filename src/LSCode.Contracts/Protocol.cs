using System.Text.Json;
namespace LSCode.Contracts;

public sealed record Request(int ProtocolVersion, string RequestId, string Method, JsonElement Payload);
public sealed record Failure(string Code, string Message, object? Details = null);
public sealed record Response(int ProtocolVersion, string RequestId, object? Result, Failure? Error = null);
public static class Protocol
{
    public const int Version = 1;
    public const int MaximumMessageLength = 40 * 1024 * 1024;
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
}
