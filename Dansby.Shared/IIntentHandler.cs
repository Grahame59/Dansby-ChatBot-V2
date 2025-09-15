using System.Text.Json;

namespace Dansby.Shared
{
    public interface IIntentHandler
    {
        string Name { get; }
        Task<HandlerResult> HandleAsync(JsonElement payload, string correlationId, CancellationToken ct);
    }
}
