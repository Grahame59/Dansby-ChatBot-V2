using System.Text.Json;
using Dansby.Shared;
using Microsoft.Extensions.Logging;
using Pipes.Nlp.Mapping.Responses;

namespace Pipes.Nlp.Mapping;

[ManualRegistration]  //ManualRegistrationAttribute.cs (Need this so it doesn't get scanned by AddAllIntentHandlersFrom(...), 
                      // Since ReplyHandler needs a string handledIntent in its ctor, it would throw an error.)
public sealed class ReplyHandler : IIntentHandler
{
    public string Name { get; }

    private readonly IResponseMap _responses;
    private readonly IIntentQueue _queue;
    private readonly ILogger<ReplyHandler> _log;

    public ReplyHandler(string handledIntent, IResponseMap responses, IIntentQueue queue, ILogger<ReplyHandler> log)
    {
        Name = handledIntent;
        _responses = responses;
        _queue = queue;
        _log = log;
    }

    public Task<HandlerResult> HandleAsync(JsonElement payload, string corr, CancellationToken ct)
    {
        // optional: read user text (future personalization)
        string userText = payload.TryGetProperty("text", out var t) && t.ValueKind == JsonValueKind.String
            ? (t.GetString() ?? "")
            : "";

        // 1) Try static JSON responses
        string? reply = null;
        foreach (var key in Responses.ResponseKeyResolver.CandidatesFor(Name))
        {
            reply = _responses.Pick(key);
            if (!string.IsNullOrWhiteSpace(reply)) break;
        }

        // 2) Dynamic fallbacks
        if (string.IsNullOrWhiteSpace(reply))
        {
            reply = Name.ToLowerInvariant() switch
            {
                "sys.time.now" => $"The time is {DateTime.Now:h:mm tt}.",
                "sys.date.today" => $"Today is {DateTime.Now:yyyy-MM-dd}.",
                "sys.time.dayofweek" => $"It's {DateTime.Now:dddd}.",
                _ => "I'm not sure how to respond to that."
            };
        }

        // 3) Optional delivery envelope for UI/voice
        var deliver = new Envelope(
            Id: Guid.NewGuid().ToString(),
            Ts: DateTimeOffset.UtcNow,
            Intent: "ui.out.say",
            Priority: 5,
            CorrelationId: corr,
            Payload: JsonSerializer.Deserialize<JsonElement>(JsonSerializer.Serialize(new { text = reply }))
        );
        _queue.Enqueue(deliver);

        _log.LogInformation("Reply intent={Intent} corr={Corr} → {Reply}", Name, corr, reply);

        return Task.FromResult(HandlerResult.Success(new { intent = Name, reply }));
    }
}
