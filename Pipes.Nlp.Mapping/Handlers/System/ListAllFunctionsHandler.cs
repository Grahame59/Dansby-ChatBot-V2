// Pipes.Nlp.Mapping/Handlers/System/ListAllFunctionsHandler.cs
using System.Text;
using System.Text.Json;
using Dansby.Shared;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection; // GetServices<T>()

namespace Pipes.Nlp.Mapping.System;

public sealed class ListAllFunctionsHandler : IIntentHandler
{
    public string Name => "sys.status.listallfunctions";

    private readonly IServiceProvider _sp;             // <-- defer resolution
    private readonly IIntentQueue _queue;
    private readonly ILogger<ListAllFunctionsHandler> _log;

    public ListAllFunctionsHandler(
        IServiceProvider sp,
        IIntentQueue queue,
        ILogger<ListAllFunctionsHandler> log)
    {
        _sp = sp;
        _queue = queue;
        _log = log;
    }

    public Task<HandlerResult> HandleAsync(JsonElement payload, string corr, CancellationToken ct)
    {
        // Resolving the handler list **at call time**, not constructor time
        var handlers = _sp.GetServices<IIntentHandler>();

        // Collect names, excluding self
        var allNames = handlers
            .Where(h => !string.IsNullOrWhiteSpace(h.Name) && !string.Equals(h.Name, Name, StringComparison.OrdinalIgnoreCase))
            .Select(h => h.Name)
            .Distinct(StringComparer.OrdinalIgnoreCase);

        // Filters + pagination
        string? domain = TryGetString(payload, "domain");
        string? starts = TryGetString(payload, "startsWith");
        int page = Math.Max(1, TryGetInt(payload, "page") ?? 1);
        int pageSize = Math.Clamp(TryGetInt(payload, "pageSize") ?? 100, 1, 500);

        IEnumerable<string> q = allNames;
        if (!string.IsNullOrWhiteSpace(domain))
            q = q.Where(n => n.StartsWith(domain + ".", StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(starts))
            q = q.Where(n => n.StartsWith(starts, StringComparison.OrdinalIgnoreCase));

        var filtered = q.OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ToList();

        int total = filtered.Count;
        int skip = (page - 1) * pageSize;
        var items = filtered.Skip(skip).Take(pageSize).ToArray();

        // Pretty text
        var sb = new StringBuilder();
        sb.AppendLine(!string.IsNullOrWhiteSpace(domain)
            ? $"Functions (domain: {domain}) — {items.Length}/{total}:"
            : $"Functions — {items.Length}/{total}:");
        foreach (var name in items) sb.AppendLine($"• {name}");
        if (skip + items.Length < total)
            sb.AppendLine($"…and {total - (skip + items.Length)} more (page {page + 1})");

        // Say
        var sayPayload = JsonSerializer.Deserialize<JsonElement>(
            JsonSerializer.Serialize(new { text = sb.ToString().TrimEnd() })
        );
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

        _log.LogInformation("listallfunctions returned {Returned}/{Total} (page {Page})", items.Length, total, page);
        return Task.FromResult(HandlerResult.Success(result));
    }

    private static string? TryGetString(JsonElement el, string name)
        => el.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    private static int? TryGetInt(JsonElement el, string name)
        => el.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out var i) ? i : null;
}
