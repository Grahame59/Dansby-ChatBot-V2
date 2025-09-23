using System.Text.Json;
using Dansby.Shared;

namespace Pipes.Nlp.Mapping;

public sealed class ChatGreetHandler : IIntentHandler
{
    public string Name => "chat.greet";

    public Task<HandlerResult> HandleAsync(JsonElement payload, string corr, CancellationToken ct)
        => Task.FromResult(HandlerResult.Success(new { text = "Hey! What’s up?" }));
}
