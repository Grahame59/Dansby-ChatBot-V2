using System.Text.Json;

namespace Dansby.Shared
{
    public interface IIntentHandler
    {
        IntentMetadata Metadata { get; }

    Task<HandlerResult> HandleAsync(
        JsonElement payload,
        string correlationId,
        CancellationToken ct);
    }
}
