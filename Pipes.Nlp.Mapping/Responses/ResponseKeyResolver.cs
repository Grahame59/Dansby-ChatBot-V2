namespace Pipes.Nlp.Mapping.Responses;
public static class ResponseKeyResolver
{
    // Map canonical → legacy JSON keys (only when responses still use old names)
    private static readonly Dictionary<string, string[]> Map = new(StringComparer.OrdinalIgnoreCase)
    {
        // chat
        ["chat.greet"]             = new[] { "chat.greet", "greetings" },
        ["chat.farewell"]          = new[] { "chat.farewell", "goodbye" },
        ["chat.help"]              = new[] { "chat.help", "help" },
        ["chat.howareyou"]         = new[] { "chat.howareyou", "howareyou" },
        ["chat.apology"]           = new[] { "chat.apology", "apology" },
        ["chat.love"]              = new[] { "chat.love", "loveaffection" },
        ["chat.compliment"]        = new[] { "chat.compliment", "complimentaffection" },
        ["chat.thanks.reply"]      = new[] { "chat.thanks.reply", "userthankyou" },
        ["chat.missedyou.reply"]   = new[] { "chat.missedyou.reply", "usermissedyou" },
        ["chat.name.asked"]        = new[] { "chat.name.asked", "name" },
        ["chat.name.confirm"]      = new[] { "chat.name.confirm", "calledname" },
        ["chat.name.spelling"]     = new[] { "chat.name.spelling", "callednamespeltwrong" },

        // system/meta/status
        ["sys.meta.creator"]       = new[] { "sys.meta.creator", "whoiscreatorname", "creatorname" },
        ["sys.meta.favoritecolor"] = new[] { "sys.meta.favoritecolor", "dansbyfavcolor", "favcolor" },
        ["sys.status.current"]     = new[] { "sys.status.current", "currenttask" },
        ["sys.status.listallfunctions"] = new[] { "sys.status.listallfunctions", "listallfunctions" },

        // weather
        ["weather.forecast"]       = new[] { "weather.forecast", "weather" },
        ["weather.temperature"]    = new[] { "weather.temperature", "temperature" },

        // dynamic (no responses needed) – keep empty
        ["sys.time.now"]           = Array.Empty<string>(),
        ["sys.time.date"]          = Array.Empty<string>(),
        ["sys.time.dayofweek"]     = Array.Empty<string>(),

        // fun
        ["fun.easteregg.steven"]   = new[] { "steveneasteregg" }

        // There are 29 total Intent groups in (intent_mappings.json)
        // 7 of the calls are deprecated and were migrated from V1.1
        // They are as follows:

        // #1. handlevolumeintent,                                              // #23
        // #2. resumeautosavetimer,                                             // #24
        // #3. pauseautosavetimer,                                              // #25
        // #4. forcesavelorehaven                                               // #26
        // #5. openerrorlog                                                     // #27
        // #6. summonslime                                                      // #28
        // #7. performexitdansby                                                // #29
    };
    
    public static IEnumerable<string> CandidatesFor(string canonical)
        => Map.TryGetValue(canonical, out var arr) && arr.Length > 0
            ? arr
            : new[] { canonical };
}