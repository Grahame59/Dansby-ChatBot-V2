using System.Text.Json;
using Dansby.Shared;

namespace Pipes.Nlp.Mapping;

    /// <summary>
    /// This Class <c>NlpRouteHandler</c> is essentially the bridge between raw text and 'actual intent handlers'.
    /// NlpRouteHandler never actually “does” the work like printing, telling time, etc...
    /// It delegates by dropping a new envelope into IIntentQueue
    /// </summary>
public sealed class NlpRouteHandler : IIntentHandler
{
    // The Intent name, ("nlp.route"), this handler consumes
    public string Name => "nlp.route";
    private readonly ITextRecognizer _recognizer; // This is the NLP 'Brain' (The Old Jaccard Recognizer)
    private readonly IIntentQueue _queue; // Shared queue to push follow-up work

    public NlpRouteHandler(ITextRecognizer r, IIntentQueue q) { _recognizer = r; _queue = q; }

    public Task<HandlerResult> HandleAsync(JsonElement payload, string corr, CancellationToken ct)
    {
        // #1. Validate inpute shape: need payload.text (string)
        if (!payload.TryGetProperty("text", out var t) || t.ValueKind != JsonValueKind.String)
            return Task.FromResult(HandlerResult.Fail("BAD_INPUT", "payload.text (string) required"));

        // #2. Run Recognizer (pure CPU work, no I/O needed here)
        var (intent, score, slots, domain) = _recognizer.Recognize(t.GetString() ?? "");

        // #3. Build the next payload from the recognized slots
        //     slots is Dictionary<string,string>; It then converts to JsonElement. 
        var nextPayload = JsonSerializer.Deserialize<JsonElement>(JsonSerializer.Serialize(slots));

        // 4) Create the *next* envelope that the dispatcher will handle
        var env = new Envelope
        (
            Id: Guid.NewGuid().ToString(),
            Ts: DateTimeOffset.UtcNow,
            Intent: intent,           // e.g., "sys.time.now", "chat.greet", ...
            Priority: 4,              // arbitrary mid priority
            CorrelationId: corr,      // keep same correlation id to trace the flow
            Payload: nextPayload
        );

        // 5) Enqueue follow-up work (async-ish, but queue.Enqueue is synchronous O(1))
        _queue.Enqueue(env);

        // 6) Immediately return a summary to the caller (the dispatcher logs this)
        return Task.FromResult(HandlerResult.Success(new
        {
            recognized = intent,
            score,
            domain,
            slots,
            routed = intent
        }));
    }
}
