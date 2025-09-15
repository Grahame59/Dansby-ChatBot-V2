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
    // (Optional): I should rename them all to these for cleanliness later on in the json
    private static readonly Dictionary<string, string> Aliases = new(StringComparer.OrdinalIgnoreCase)
    {
        // Chat
        { "chat.greet", "chat.greet" }, // already canonical
        { "greeting", "chat.greet" },
        { "goodbye", "chat.goodbye" },
        { "help", "chat.help" },
        { "howareyou", "chat.howareyou" },
        { "currenttask", "chat.currenttask" },
        { "usercurrenttaskcodingondansby", "chat.currenttask" },
        { "whoiscreatorname", "chat.creator.name" },
        { "creatorname", "chat.creator.name" },
        { "dansbyfavcolor", "chat.favorites.color" },
        { "userthankyou", "chat.thanks" },
        { "complimentaffection", "chat.affection.compliment" },
        { "loveaffection", "chat.affection.love" },
        { "usermissedyou", "chat.affection.missyou" },
        { "calledname", "chat.name.called" },
        { "callednamespeltwrong", "chat.name.misspelling" },
        { "apology", "chat.apology" },
        { "steveneasteregg", "fun.easteregg.steven" },

        // Weather
        { "weather", "weather.current" },
        { "temperature", "weather.current.temp" },

        // System / Time
        { "time", "sys.time.now" },
        { "date", "sys.date.today" },
        { "dayofweek", "sys.time.dayofweek" },

        // Operations / App control
        { "performexitdansby", "app.exit" },
        { "openerrorlog", "ops.errorlog.open" },
        { "forcesavelorehaven", "ops.autosave.force" },
        { "pauseautosavetimer", "ops.autosave.pause" },
        { "resumeautosavetimer", "ops.autosave.resume" },
        { "listallfunctions", "sys.capabilities.list" },

        // Media
        { "handlevolumeintent", "media.volume.set" },

        // UI / Toys
        { "summonslime", "ui.sprite.summon" },

        // Legacy lights (if any examples still use these)
        { "lights.on",  "iot.lights.set" },
        { "lights.off", "iot.lights.set" }
    };

}
