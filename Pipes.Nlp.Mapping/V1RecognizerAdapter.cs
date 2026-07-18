using Microsoft.Extensions.Logging;

namespace Pipes.Nlp.Mapping;

public sealed class V1RecognizerAdapter : ITextRecognizer
{
    private readonly V1RecognizerEngine _engine;
    private readonly ILogger<V1RecognizerAdapter> _log;

    public V1RecognizerAdapter(V1RecognizerEngine engine, ILogger<V1RecognizerAdapter> log)
    {
        _engine = engine;
        _log = log;
        _engine.Load(); // load intents at startup
    }

    // Simple Domain Extraction Logic - Determines the domain of the recognized intent based on the canonical intent name.
    private static string GetDomain(string intent)
    {
        if (string.IsNullOrWhiteSpace(intent) ||
            intent.Equals(
                "unknown",
                StringComparison.OrdinalIgnoreCase))
        {
            return "other";
        }

        var separatorIndex = intent.IndexOf('.');

        return separatorIndex > 0
            ? intent[..separatorIndex]
            : "other";
    }

    public (string intent, double score, Dictionary<string, string> slots, string domain) Recognize(string text)
    {
        var (intent, score) = _engine.RecognizeBest(text);

        // Normalize old names into current canonical intent names.
        if (Aliases.TryGetValue(intent, out var canonical))
        {
            intent = canonical;
        }

        // Extract only the slots relevant to the recognized intent.
        var slots = ExtractSlots(intent, text);

        // Determine domain from the canonical intent name.
        var domain = GetDomain(intent);

        _log.LogDebug(
            "Recognized {Intent} score={Score} domain={Domain}",
            intent,
            score,
            domain);

        return (intent, score, slots, domain);
    }

    private static Dictionary<string, string> ExtractSlots( string intent, string text)
    {
        if (intent.Equals(
            "media.search",
            StringComparison.OrdinalIgnoreCase))
        {
            return ExtractMediaSearchSlots(text);
        }

        if (intent.StartsWith(
            "iot.",
            StringComparison.OrdinalIgnoreCase))
        {
            return ExtractIotSlots(text);
        }

        return new Dictionary<string, string>();
    }

    //Media Slot Extraction Logic - Used for Searching Media Library (Movies and TV Shows on Plex)
    private static Dictionary<string, string> ExtractMediaSearchSlots(string text)
    {
        var searchQuery = ExtractMediaSearchQuery(text);

        var slots = new Dictionary<string, string>();

        if (!string.IsNullOrWhiteSpace(searchQuery))
        {
            slots["SearchQuery"] = searchQuery;
        }

        return slots;
    }

    //IoT Slot Extraction Logic - Placeholder for future expansion, currently only handles simple action/location/device extraction.
    private static Dictionary<string, string> ExtractIotSlots(string text)
    {
        var t = text.ToLowerInvariant();

        string action =
            t.Contains("toggle") ? "toggle" :
            (t.Contains(" on ") ||
            t.StartsWith("on ") ||
            t.EndsWith(" on")) ? "on" :
            (t.Contains(" off ") ||
            t.StartsWith("off ") ||
            t.EndsWith(" off")) ? "off" :
            string.Empty;

        string[] locations =
        [
            "living room", "kitchen", "office", "bedroom", "desk"
        ];

        var location =
            locations.FirstOrDefault(l => t.Contains(l))
            ?? string.Empty;

        var device =
            t.Contains("lamp") ? "lamp" :
            t.Contains("light") ? "light" :
            string.Empty;

        var slots = new Dictionary<string, string>();

        if (!string.IsNullOrEmpty(action))
        {
            slots["action"] = action;
        }

        if (!string.IsNullOrEmpty(location))
        {
            slots["location"] = location.Replace(" ", "");
        }

        if (!string.IsNullOrEmpty(device))
        {
            slots["device"] = device;
        }

        return slots;
    }

    // Runtime Normalization for the old intent_mappings.json name conventions from V1.1
    private static readonly Dictionary<string, string> Aliases = new(StringComparer.OrdinalIgnoreCase)
    {
        // Canonical pass-throughs
        { "chat.greet", "chat.greet" },
        { "chat.farewell", "chat.farewell" },
        { "chat.help", "chat.help" },
        { "chat.howareyou", "chat.howareyou" },
        { "chat.apology", "chat.apology" },
        { "chat.love", "chat.love" },
        { "chat.compliment", "chat.compliment" },
        { "chat.thanks.reply", "chat.thanks.reply" },
        { "chat.missedyou.reply", "chat.missedyou.reply" },
        { "chat.name.asked", "chat.name.asked" },
        { "chat.name.confirm", "chat.name.confirm" },
        { "chat.name.spelling", "chat.name.spelling" },

        { "sys.status.current", "sys.status.current" },
        { "sys.status.listallfunctions", "sys.status.listallfunctions" },
        { "sys.meta.creator", "sys.meta.creator" },
        { "sys.meta.favoritecolor", "sys.meta.favoritecolor" },
        { "sys.time.now", "sys.time.now" },
        { "sys.time.date", "sys.time.date" },
        { "sys.time.dayofweek", "sys.time.dayofweek" },
        { "weather.forecast", "weather.forecast" },
        { "weather.temperature", "weather.temperature" },
        { "fun.easteregg.steven", "fun.easteregg.steven" },

        // Legacy → Canonical
        { "greetings", "chat.greet" },
        { "goodbye", "chat.farewell" },
        { "howareyou", "chat.howareyou" },
        { "currenttask", "sys.status.current" },
        { "usercurrenttaskcodingondansby", "sys.status.current" },
        { "whoiscreatorname", "sys.meta.creator" },
        { "creatorname", "sys.meta.creator" },
        { "dansbyfavcolor", "sys.meta.favoritecolor" },
        { "userthankyou", "chat.thanks.reply" },
        { "complimentaffection", "chat.compliment" },
        { "loveaffection", "chat.love" },
        { "usermissedyou", "chat.missedyou.reply" },
        { "calledname", "chat.name.confirm" },
        { "callednamespeltwrong", "chat.name.spelling" },
        { "name", "chat.name.asked" },

        { "weather", "weather.forecast" },
        { "temperature", "weather.temperature" },

        { "time", "sys.time.now" },
        { "date", "sys.time.date" },
        { "dayofweek", "sys.time.dayofweek" },

        { "performexitdansby", "app.exit" },              // deprecated; map if you still allow it
        { "openerrorlog", "ops.errorlog.open" },          // deprecated
        { "forcesavelorehaven", "ops.autosave.force" },   // deprecated
        { "pauseautosavetimer", "ops.autosave.pause" },   // deprecated
        { "resumeautosavetimer", "ops.autosave.resume" }, // deprecated
        { "listallfunctions", "sys.status.listallfunctions" }, // legacy

        { "handlevolumeintent", "media.volume.set" },    
        { "summonslime", "ui.sprite.summon" }           
    };

    private static string ExtractMediaSearchQuery(string text)
    {
        var query = text.Trim();

        // Longer Prefixes should be listed first to avoid premature matches (e.g., "search for the movie titled" before "search for the movie")
        string[] prefixes = 
        [
            "search for the movie titled ",
            "search for the tv show titled ",
            "search for the movie ",
            "search for the film ",
            "search for the tv show ",
            "search my movie library for ",
            "search my tv show library for ",
            "search my movies for ",
            "search my shows for ",
            "search my collection for ",
            "search plex for ",
            "search for ",
            "find the movie titled ",
            "find the tv show titled ",
            "find the movie ",
            "find the film ",
            "find the tv show ",
            "find ",
            "look up the movie titled ",
            "look up the tv show titled ",
            "look up the movie ",
            "look up the film ",
            "look up the tv show ",
            "look up ",
            "do i have the movie ",
            "do i have the film ",
            "do i have the tv show ",
            "do i have ",
            "is ",
            "do I own the movie ",
            "do I own the film ",
            "do I own the tv show ",
            "do I own "

        ];

        foreach (var prefix in prefixes)
        {
            if (!query.StartsWith(
                prefix,
                StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            query = query[prefix.Length..];
            break;
        }

        string[] suffixes =
        [
            " on plex",
            " in my movie collection",
            " in my tv collection",
            " in my collection",
            " on my server",
            " in my library",
            " in my movie library",
            " in my tv library"
        ];

        foreach (var suffix in suffixes)
        {
            if (!query.EndsWith(
                suffix,
                StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            query = query[..^suffix.Length];
            break;
        }

        return query
            .Trim()
            .TrimEnd('?', '!', '.');
    }

}
