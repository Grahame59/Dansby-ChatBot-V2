using System.Collections.Concurrent;
using Dansby.Shared;

namespace Dansby.Core.Api.Infrastructure;

internal sealed class HandlerRegistry : IHandlerRegistry
{
    private readonly ConcurrentDictionary<string, IIntentHandler> _handlers =
        new(StringComparer.OrdinalIgnoreCase);

    public HandlerRegistry(IEnumerable<IIntentHandler> handlers)
    {
        foreach (var handler in handlers)
        {
            Register(handler);
        }
    }

    public void Register(IIntentHandler handler) => _handlers[handler.Name] = handler;

    public IIntentHandler? Resolve(string intent) =>
        _handlers.TryGetValue(intent, out var handler) ? handler : null;
}
