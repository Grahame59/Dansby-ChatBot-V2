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

        // #2.5 Extract Raw text
        var rawText = t.GetString() ?? "";

        // #2.6 Unique Case - Zebra Printer Parser
        // Pattern for Parsing Below:
        // [utterance]  [separator]  [label data]
        
        if (intent == "zebra.print.simple")
        {
            // Allowed parsing symbols
            string[] separators = [":", "-"];

            // Find the FIRST symbol that appears after the utterance
            int pos = -1;
            string? usedSep = null;

            foreach (var sep in separators)
            {
                var i = rawText.IndexOf(sep, StringComparison.OrdinalIgnoreCase);
                if (i >= 0 && (pos == -1 || i < pos))
                {
                    pos = i;
                    usedSep = sep;
                }
            }

            if (pos >= 0 && usedSep != null)
            {
                // Extract everything AFTER the separator
                string label = rawText[(pos + usedSep.Length)..].Trim();

                if (!string.IsNullOrWhiteSpace(label))
                {
                    slots["labelText"] = label;
                }
            }
            else
            {
                // No separator found → user did not follow the format
                // This will propagate to printer handler as BAD_INPUT
                // (We let the printer handler respond with the standard error)
            }
        }

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
