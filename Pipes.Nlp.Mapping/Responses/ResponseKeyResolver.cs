namespace Pipes.Nlp.Mapping.Responses;

// Maps canonical intents → JSON keys in response_mappings.json
public static class ResponseKeyResolver
{
    private static readonly Dictionary<string, string[]> Map = new(StringComparer.OrdinalIgnoreCase)
    {
        // chat
        ["chat.greet"]                  = new[] { "greetings" },                // #1
        ["chat.farewell"]               = new[] { "goodbye" },                  // #2
        ["chat.help"]                   = new[] { "help" },                     // #3
        ["chat.howareyou"]              = new[] { "howareyou" },                // #4
        ["sys.status.current"]          = new[] { "currenttask" },              // #5
        ["sys.meta.creator"]            = new[] { "whoiscreatorname" },         // #6
        ["sys.meta.favoritecolor"]      = new[] { "dansbyfavcolor" },           // #7
        ["chat.thanks.reply"]           = new[] { "userthankyou" },             // #8
        ["chat.compliment"]             = new[] { "complimentaffection" },      // #9
        ["chat.love"]                   = new[] { "loveaffection" },            // #10
        ["chat.missedyou.reply"]        = new[] { "usermissedyou" },            // #11
        ["chat.name.asked"]             = new[] { "calledname" },               // #12
        ["chat.name.confirmed"]         = new[] { "useraskedname"}              // #13
        ["chat.name.spelling"]          = new[] { "callednamespeltwrong" },     // #14
        ["fun.easteregg.steven"]        = new[] { "steveneasteregg" },          // #15
        ["chat.apology"]                = new[] { "apology" },                  // #16

        // weather/system (static) [NOT IMPLEMENTED]
        ["weather.forecast"]            = new[] { "weather" },                  // #17
        ["weather.temperature"]         = new[] { "temperature" },              // #18
        ["sys.status.listallfunctions"] = new[] { "listallfunctions" },         // #19

        // dynamic (no JSON needed)
        ["sys.time.now"]                = Array.Empty<string>(),                // #20
        ["sys.date.today"]              = Array.Empty<string>(),                // #21
        ["sys.time.dayofweek"]          = Array.Empty<string>(),                // #22

        // There are 29 total Intent groups in (intent_mappings.json)
        // 7 of the calls are depreciated and were migrated from V1.1
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
        => Map.TryGetValue(canonical, out var arr) && arr.Length > 0 ? arr : new[] { canonical };
}
