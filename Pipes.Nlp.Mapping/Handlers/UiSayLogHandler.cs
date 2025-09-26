using System.Text.Json;
using Dansby.Shared;
using Microsoft.Extensions.Logging;

namespace Pipes.Nlp.Mapping;

public sealed class UiSayLogHandler : IIntentHandler
{
    public string Name => "ui.out.say";
    private readonly ILogger<UiSayLogHandler> _log;

    public UiSayLogHandler(ILogger<UiSayLogHandler> log) => _log = log;

    public Task<HandlerResult> HandleAsync(JsonElement payload, string corr, CancellationToken ct)
    {
        var text = payload.TryGetProperty("text", out var t) && t.ValueKind == JsonValueKind.String ? t.GetString() : "(no text)";
        _log.LogInformation("UI OUT corr={Corr}: {Text}", corr, text);
        return Task.FromResult(HandlerResult.Success(new { text }));
    }
}
