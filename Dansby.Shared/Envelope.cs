using System.Text.Json;

namespace Dansby.Shared;

public sealed record Envelope(
    string Id,
    DateTimeOffset Ts,
    string Intent,
    int Priority,
    string CorrelationId,
    JsonElement Payload);

public sealed record HandlerResult(
    bool Ok,
    string? ErrorCode = null,
    string? Message = null,
    object? Data = null)
{
    public static HandlerResult Success(object? data = null, string? message = null)
        => new(true, null, message, data);

    public static HandlerResult Fail(string code, string message, bool log = true, object? data = null)
        => new(false, code, message, data);
}
