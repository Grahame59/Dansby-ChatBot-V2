using System.Text.Json;
using Dansby.Shared;
using Microsoft.Extensions.Logging;
using Pipes.Nlp.Mapping.Media;

namespace Pipes.Nlp.Mapping.Handlers.Media;

public sealed class MediaSearchHandler : IIntentHandler
{
    public string Name => "media.search";

    private readonly IMediaIndexService _mediaIndex;
    private readonly IIntentQueue _queue;
    private readonly ILogger<MediaSearchHandler> _log;

    public MediaSearchHandler(
        IMediaIndexService mediaIndex,
        IIntentQueue queue,
        ILogger<MediaSearchHandler> log)
    {
        _mediaIndex = mediaIndex;
        _queue = queue;
        _log = log;
    }

    public async Task<HandlerResult> HandleAsync(
        JsonElement payload,
        string corr,
        CancellationToken ct)
    {
        if (!TryReadSearchQuery(payload, out var searchQuery))
        {
            return HandlerResult.Fail(
                "BAD_INPUT",
                "payload.SearchQuery (string) required.");
        }

        var matches = await _mediaIndex.SearchAsync(searchQuery, ct);

        var reply = BuildReply(searchQuery, matches);

        EnqueueReply(reply, corr);

        _log.LogInformation(
            "Media search corr={Corr} query={Query} matches={MatchCount}",
            corr,
            searchQuery,
            matches.Count);

        return HandlerResult.Success(new
        {
            reply,
            query = searchQuery,
            count = matches.Count,
            results = matches
        });
    }

    private static bool TryReadSearchQuery(
        JsonElement payload,
        out string searchQuery)
    {
        searchQuery = string.Empty;

        if (payload.TryGetProperty("SearchQuery", out var upperCaseQuery) &&
            upperCaseQuery.ValueKind == JsonValueKind.String)
        {
            searchQuery = upperCaseQuery.GetString()?.Trim() ?? string.Empty;
        }
        else if (payload.TryGetProperty("searchQuery", out var lowerCaseQuery) &&
                 lowerCaseQuery.ValueKind == JsonValueKind.String)
        {
            searchQuery = lowerCaseQuery.GetString()?.Trim() ?? string.Empty;
        }

        return !string.IsNullOrWhiteSpace(searchQuery);
    }

    private static string BuildReply(
        string searchQuery,
        IReadOnlyCollection<MediaItem> matches)
    {
        if (matches.Count == 0)
        {
            return $"I couldn't find anything matching {searchQuery}.";
        }

        var lines = new List<string>
        {
            $"I found {matches.Count} result{(matches.Count == 1 ? "" : "s")} for {searchQuery}:"
        };

        foreach (var item in matches)
        {
            if (item.Type == MediaType.TvShow)
            {
                lines.Add(
                    $"- {item.DisplayTitle}; " +
                    $"{item.SeasonCount} Seasons; " +
                    $"{item.EpisodeCount} Episodes");
            }
            else
            {
                lines.Add($"- {item.DisplayTitle}; Movie");
            }
        }

        return string.Join(Environment.NewLine, lines);
    }

    private void EnqueueReply(string reply, string corr)
    {
        var payload = JsonSerializer.SerializeToElement(new
        {
            text = reply
        });

        var envelope = new Envelope(
            Id: Guid.NewGuid().ToString(),
            Ts: DateTimeOffset.UtcNow,
            Intent: "ui.out.say",
            Priority: 5,
            CorrelationId: corr,
            Payload: payload);

        _queue.Enqueue(envelope);
    }
}