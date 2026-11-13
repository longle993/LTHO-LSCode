using System.Text.Json;

namespace LSCode.Application;

public interface IDeveloperTools
{
    Task<object?> ExecuteAsync(string method, JsonElement payload, CancellationToken cancellationToken);
}
