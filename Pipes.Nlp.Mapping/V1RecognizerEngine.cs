using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Pipes.Nlp.Mapping;

public sealed class V1RecognizerEngine
{
    private readonly ILogger<V1RecognizerEngine> _log;
    private readonly V1Tokenizer _tokenizer;
    private List<IntentDef> _intents = new();
    private readonly double _threshold = 0.65; // same as I had v1.1 default for Jaccard 

    public V1RecognizerEngine(ILogger<V1RecognizerEngine> log, V1Tokenizer tokenizer)
    {
        _log = log;
        _tokenizer = tokenizer;
    }

    public void Load(string? path = null)
    {
        path ??= Path.Combine(AppContext.BaseDirectory, "intent_mappings.json");
        if (!File.Exists(path))
        {
            _log.LogWarning("Intent file not found at {Path}. Using empty set.", path);
            _intents = new(); return;
        }

        try
        {
            var json = File.ReadAllText(path);
            var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var intents = JsonSerializer.Deserialize<List<IntentDef>>(json, opts) ?? new();

            foreach (var intent in intents)
            foreach (var ex in intent.Examples)
                if (ex.Tokens is null || ex.Tokens.Count == 0)
                    ex.Tokens = _tokenizer.Tokenize(ex.Utterance);

            _intents = intents;
            _log.LogInformation("Loaded {Count} intents from {Path}", _intents.Count, path);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Failed to load intents at {Path}. Using empty set.", path);
            _intents = new();
        }
    }

    public (string intent, double score) RecognizeBest(string userInput)
    {
        if (string.IsNullOrWhiteSpace(userInput)) return ("unknown", 0.0);

        var userTokens = _tokenizer.Tokenize(userInput);
        var userSet = userTokens.ToHashSet(StringComparer.OrdinalIgnoreCase);

        string best = "unknown";
        double bestScore = 0.0;

        foreach (var intent in _intents)
        {
            foreach (var ex in intent.Examples)
            {
                var exSet = (ex.Tokens ?? new()).ToHashSet(StringComparer.OrdinalIgnoreCase);
                var inter = exSet.Intersect(userSet).Count();
                var union = exSet.Union(userSet).Count();
                double j = union == 0 ? 0 : (double)inter / union;
                if (j > bestScore) { bestScore = j; best = intent.Name; }
            }
        }

        if (bestScore >= _threshold) return (best, Math.Round(bestScore, 3));
        return ("unknown", Math.Round(bestScore, 3));
    }
}
