using System.Text.RegularExpressions;
using System.Text.Json;
using Dansby.Shared;

namespace Pipes.Nlp.Mapping;

public interface IIntentHandler
{
    string Name { get; } // intent name this handler consumes, e.g. "nlp.match"
    Task<HandlerResult> HandleAsync(JsonElement payload, string correlationId, CancellationToken ct);
}

/// <summary>
/// Minimal tokenizer + matcher. Replace with your v1.1 Tokenizer/IntentRecognizer later.
/// </summary>
public sealed class NlpMatchHandler : IIntentHandler
{
    public string Name => "nlp.match";

    // toy example bank; replace with your intent_mappings.json later
    private readonly Dictionary<string, string[]> _examples = new(StringComparer.OrdinalIgnoreCase)
    {
        ["lights.on"]   = new[] { "turn on the light", "lights on", "switch on desk light" },
        ["lights.off"]  = new[] { "turn off the light", "lights off", "switch off desk light" },
        ["music.play"]  = new[] { "play some music", "start music", "play a song" },
        ["time.now"]    = new[] { "what time is it", "tell me the time", "current time" },
    };

    public Task<HandlerResult> HandleAsync(JsonElement payload, string correlationId, CancellationToken ct)
    {
        if (!payload.TryGetProperty("text", out var textEl) || textEl.ValueKind != JsonValueKind.String)
            return Task.FromResult(HandlerResult.Fail("BAD_INPUT", "payload.text (string) required"));

        var text = textEl.GetString() ?? string.Empty;
        var q = Tokenize(text);

        string bestIntent = "unknown";
        double bestScore = 0;

        foreach (var kv in _examples)
        {
            foreach (var ex in kv.Value)
            {
                var s = Jaccard(q, Tokenize(ex));
                if (s > bestScore) { bestScore = s; bestIntent = kv.Key; }
            }
        }

        var data = new { detectedIntent = bestIntent, score = Math.Round(bestScore, 3) };
        return Task.FromResult(HandlerResult.Success(data));
    }

    private static string[] Tokenize(string s)
        => Regex.Split(s.ToLowerInvariant(), @"[^a-z0-9]+", RegexOptions.Compiled)
                .Where(t => t.Length > 0).ToArray();

    private static double Jaccard(string[] a, string[] b)
    {
        var A = a.ToHashSet(); var B = b.ToHashSet();
        var inter = A.Intersect(B).Count();
        var union = A.Union(B).Count();
        return union == 0 ? 0 : (double)inter / union;
    }
}
