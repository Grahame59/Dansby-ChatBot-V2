using System.Text.Json;
using Dansby.Shared;

namespace Pipes.Nlp.Mapping;

// Returns current time; accepts optional IANA timezone (e.g., "America/New_York")
public sealed class SysTimeNowHandler : IIntentHandler
{
    public string Name => "sys.time.now";

    public Task<HandlerResult> HandleAsync(JsonElement payload, string corr, CancellationToken ct)
    {
        string? tz = null;
        if (payload.TryGetProperty("timezone", out var z) && z.ValueKind == JsonValueKind.String)
            tz = z.GetString();

        DateTimeOffset now;
        string tzUsed;

        try
        {
            if (string.IsNullOrWhiteSpace(tz))
            {
                now = DateTimeOffset.Now;
                tzUsed = "local";
            }
            else
            {
                var tzi = TimeZoneInfo.FindSystemTimeZoneById(tz!); // Linux uses IANA, e.g., "America/New_York"
                now = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, tzi);
                tzUsed = tz!;
            }
        }
        catch (TimeZoneNotFoundException)
        {
            // Graceful fallback if invalid tz was provided
            now = DateTimeOffset.Now;
            tzUsed = "local";
        }

        return Task.FromResult(HandlerResult.Success(new { now = now.ToString("O"), tz = tzUsed }));
    }
}
