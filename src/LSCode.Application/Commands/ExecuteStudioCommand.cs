using System.Text.Json;
using MediatR;

namespace LSCode.Application.Commands;

/// <summary>Application boundary for every request received from a presentation adapter.</summary>
public sealed record ExecuteStudioCommand(string Method, JsonElement Payload) : IRequest<object?>;
