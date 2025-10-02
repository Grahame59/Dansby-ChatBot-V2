using System.Text;
using System.Text.Json;
using Dansby.Shared;
using Microsoft.Extensions.Logging;

namespace Pipes.Nlp.Mapping.System;

public sealed class ListAllFunctionsHandler : IIntentHandler
{
    public string Name => "sys.status.listallfunctions";

    private readonly IHandlerRegistry _registry;
    private readonly IIntentQueue _queue;
    private readonly ILogger<ListAllFunctionsHandler> _log;

    public ListAllFunctionsHandler(IHandlerRegistry registry, IIntentQueue queue, ILogger<ListAllFunctionsHandler> log)
    {
        _registry = registry;
        _queue = queue;
        _log = log;
    }

    public Task<HandlerResult> HandleAsync(JsonElement payload, string corr, CancellationToken ct)
    {
        // optional filters
        string? domain = TryGetString(payload, "domain");         // e.g. "chat"
        string? starts = TryGetString(payload, "startsWith");     // e.g. "chat.name."
        int page = Math.Max(1, TryGetInt(payload, "page") ?? 1);
        int pageSize = Math.Clamp(TryGetInt(payload, "pageSize") ?? 100, 1, 500);

        var all = _registry.Intents();

        // filter
        IEnumerable<string> q = all;
        if (!string.IsNullOrWhiteSpace(domain))
            q = q.Where(n => n.StartsWith(domain + ".", StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(starts))
            q = q.Where(n => n.StartsWith(starts, StringComparison.OrdinalIgnoreCase));

        var filtered = q.OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ToList();

        // paginate
        int total = filtered.Count;
        int skip = (page - 1) * pageSize;
        var items = filtered.Skip(skip).Take(pageSize).ToArray();

        // pretty text for ui
        var sb = new StringBuilder();
        if (!string.IsNullOrWhiteSpace(domain))
            sb.AppendLine($"Functions (domain: {domain}) — {items.Length}/{total}:");
        else
            sb.AppendLine($"Functions — {items.Length}/{total}:");

        foreach (var name in items)
            sb.AppendLine($"• {name}");

        if (skip + items.Length < total)
            sb.AppendLine($"…and {total - (skip + items.Length)} more (page {page + 1})");

        // enqueue a UI message (to keep parity with ReplyHandler behavior)
        var sayPayload = JsonSerializer.Deserialize<JsonElement>(JsonSerializer.Serialize(new { text = sb.ToString().TrimEnd() }));
        _queue.Enqueue(new Envelope(
            Id: Guid.NewGuid().ToString(),
            Ts: DateTimeOffset.UtcNow,
            Intent: "ui.out.say",
            Priority: 5,
            CorrelationId: corr,
            Payload: sayPayload
        ));

        var result = new
        {
            total, page, pageSize,
            returned = items.Length,
            filters = new { domain, startsWith = starts },
            items
        };

        _log.LogInformation("Listed {Returned}/{Total} functions (page {Page})", items.Length, total, page);
        return Task.FromResult(HandlerResult.Success(result));
    }

    private static string? TryGetString(JsonElement el, string name)
        => el.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    private static int? TryGetInt(JsonElement el, string name)
        => el.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out var i) ? i : null;
}
