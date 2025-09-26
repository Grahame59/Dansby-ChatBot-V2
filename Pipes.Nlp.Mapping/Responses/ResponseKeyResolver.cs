namespace Pipes.Nlp.Mapping.Responses;

// Maps canonical intents → JSON keys in response_mappings.json
public static class ResponseKeyResolver
{
    private static readonly Dictionary<string, string[]> Map = new(StringComparer.OrdinalIgnoreCase)
    {
        // chat
        ["chat.greet"]                 = new[] { "greetings" },
        ["chat.goodbye"]               = new[] { "goodbye" },
        ["chat.help"]                  = new[] { "help" },
        ["chat.howareyou"]             = new[] { "howareyou" },
        ["chat.currenttask"]           = new[] { "currenttask", "usercurrenttaskcodingondansby" },
        ["chat.creator.name"]          = new[] { "creatorname", "whoiscreatorname" },
        ["chat.favorites.color"]       = new[] { "dansbyfavcolor" },
        ["chat.thanks"]                = new[] { "userthankyou" },
        ["chat.affection.compliment"]  = new[] { "complimentaffection" },
        ["chat.affection.love"]        = new[] { "loveaffection" },
        ["chat.affection.missyou"]     = new[] { "usermissedyou" },
        ["chat.name.called"]           = new[] { "calledname" },
        ["chat.name.misspelling"]      = new[] { "callednamespeltwrong" },
        ["fun.easteregg.steven"]       = new[] { "steveneasteregg" },

        // weather/system (static)
        ["weather.current"]            = new[] { "weather" },
        ["weather.current.temp"]       = new[] { "temperature" },

        // dynamic (no JSON needed)
        ["sys.time.now"]               = Array.Empty<string>(),
        ["sys.date.today"]             = Array.Empty<string>(),
        ["sys.time.dayofweek"]         = Array.Empty<string>(),
    };

    public static IEnumerable<string> CandidatesFor(string canonical)
        => Map.TryGetValue(canonical, out var arr) && arr.Length > 0 ? arr : new[] { canonical };
}
