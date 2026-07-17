using System.Text.Json;
using Dansby.Shared;
using Microsoft.Extensions.Logging;
using Pipes.Nlp.Mapping.Media;

namespace Pipes.Nlp.Mapping.Handlers.Media;

public sealed class MediaListHandler : IIntentHandler
{
    public string Name => "media.list";

    private readonly IMediaIndexService _mediaIndex;
    private readonly IIntentQueue _queue;
    private readonly ILogger<MediaListHandler> _log;

    public MediaListHandler(
        IMediaIndexService mediaIndex,
        IIntentQueue queue,
        ILogger<MediaListHandler> log)
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
        var items = await _mediaIndex.GetAllAsync(ct);

        var movies = items
            .Where(item => item.Type == MediaType.Movie)
            .OrderBy(item => item.Title)
            .ToList();

        var shows = items
            .Where(item => item.Type == MediaType.TvShow)
            .OrderBy(item => item.Title)
            .ToList();

        var reply = BuildReply(movies, shows);

        EnqueueReply(reply, corr);

        _log.LogInformation(
            "Listed media corr={Corr} movies={MovieCount} shows={ShowCount}",
            corr,
            movies.Count,
            shows.Count);

        return HandlerResult.Success(new
        {
            movies,
            shows,
            movieCount = movies.Count,
            showCount = shows.Count
        });
    }

    private static string BuildReply(
        IReadOnlyCollection<MediaItem> movies,
        IReadOnlyCollection<MediaItem> shows)
    {
        if (movies.Count == 0 && shows.Count == 0)
        {
            return "I couldn't find any indexed movies or TV shows.";
        }

        var lines = new List<string>();

        if (movies.Count > 0)
        {
            lines.Add($"Movies ({movies.Count}):");

            foreach (var movie in movies)
            {
                lines.Add($"- {movie.DisplayTitle}");
            }
        }

        if (shows.Count > 0)
        {
            if (lines.Count > 0)
            {
                lines.Add(string.Empty);
            }

            lines.Add($"TV Shows ({shows.Count}):");

            foreach (var show in shows)
            {
                lines.Add(
                    $"- {show.DisplayTitle}; " +
                    $"{show.SeasonCount} Seasons; " +
                    $"{show.EpisodeCount} Episodes");
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