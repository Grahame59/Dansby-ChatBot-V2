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

    public (string intent, double score, Dictionary<string, string> slots, string domain) Recognize(string text)
    {
        var (intent, score) = _engine.RecognizeBest(text);
        
        // normalizing old names → canonical names 
        if (Aliases.TryGetValue(intent, out var canonical))
            intent = canonical;

        // then do slots + domain so they use the canonical name
        // tiny slotting so IoT examples feel real right away
        var slots = ExtractSlots(text);

        string domain =
            intent.StartsWith("iot.", StringComparison.OrdinalIgnoreCase) ? "iot" :
            intent.StartsWith("chat.", StringComparison.OrdinalIgnoreCase) ? "chat" :
            slots.ContainsKey("action") ? "iot" : "other";

        _log.LogDebug("Recognized {Intent} score={Score} domain={Domain}", intent, score, domain);
        return (intent, score, slots, domain);
    }

    private static Dictionary<string, string> ExtractSlots(string text)
    {
        var t = text.ToLowerInvariant();
        string action =
            t.Contains("toggle") ? "toggle" :
            (t.Contains(" on ") || t.StartsWith("on ") || t.EndsWith(" on")) ? "on" :
            (t.Contains(" off ") || t.StartsWith("off ") || t.EndsWith(" off")) ? "off" : "";

        var locations = new[] { "living room", "livingroom", "kitchen", "office", "bedroom", "desk" };
        string loc = locations.FirstOrDefault(l => t.Contains(l)) ?? "";
        string device = t.Contains("lamp") ? "lamp" : (t.Contains("light") ? "light" : "");

        var slots = new Dictionary<string, string>();
        if (!string.IsNullOrEmpty(action)) slots["action"] = action;
        if (!string.IsNullOrEmpty(loc)) slots["location"] = loc.Replace(" ", "");
        if (!string.IsNullOrEmpty(device)) slots["device"] = device;
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

}
