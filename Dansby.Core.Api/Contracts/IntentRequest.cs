using System.Text.Json;

namespace Dansby.Core.Api.Contracts;

public sealed record IntentRequest(
    string Intent,
    int? Priority,
    string? CorrelationId,
    JsonElement Payload);
