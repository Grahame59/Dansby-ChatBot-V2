using System.Text.Json;
using Dansby.Shared;

namespace Pipes.Nlp.Mapping;

public sealed class NlpRecognizeHandler : IIntentHandler
{
    public string Name => "nlp.recognize";
    private readonly ITextRecognizer _rec;

    public NlpRecognizeHandler(ITextRecognizer rec) => _rec = rec;

    public Task<HandlerResult> HandleAsync(JsonElement payload, string corr, CancellationToken ct)
    {
        if (!payload.TryGetProperty("text", out var t) || t.ValueKind != JsonValueKind.String)
            return Task.FromResult(HandlerResult.Fail("BAD_INPUT", "payload.text (string) required"));

        var (intent, score, slots, domain) = _rec.Recognize(t.GetString() ?? "");
        return Task.FromResult(HandlerResult.Success(new { intent, score, domain, slots }));
    }
}
