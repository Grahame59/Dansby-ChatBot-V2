using System.Text.Json;
using Dansby.Shared;
using Microsoft.Extensions.Logging;
using Pipes.Nlp.Mapping.Responses;

namespace Pipes.Nlp.Mapping;

[ManualRegistration]  //ManualRegistrationAttribute.cs (Need this so it doesn't get scanned by AddAllIntentHandlersFrom(...), 
                      // Since ReplyHandler needs a string handledIntent in its ctor, it would throw an error.)
public sealed class ReplyHandler : IIntentHandler
{
    public IntentMetadata Metadata { get; } 
    private readonly IResponseMap _responses;
    private readonly IIntentQueue _queue;
    private readonly ILogger<ReplyHandler> _log;

    public ReplyHandler(
        string handledIntent, 
        IResponseMap responses, 
        IIntentQueue queue, 
        ILogger<ReplyHandler> log)
    {
        if (string.IsNullOrWhiteSpace(handledIntent))
        {
            throw new ArgumentException(
                "A handled intent is required.",
                nameof(handledIntent));
        }

        Metadata = new IntentMetadata(
            Name: handledIntent,
            Summary: "Provides a reply to the user based on their input.");

        _responses = responses;
        _queue = queue;
        _log = log;
    }

    public Task<HandlerResult> HandleAsync(
        JsonElement payload,
        string corr,
        CancellationToken ct)
    {
        string userText =
            payload.TryGetProperty("text", out var textElement) &&
            textElement.ValueKind == JsonValueKind.String
                ? textElement.GetString() ?? string.Empty
                : string.Empty;

        string intentName = Metadata.Name;

        // 1) Try configured static responses.
        string? reply = null;

        foreach (var key in ResponseKeyResolver.CandidatesFor(intentName))
        {
            reply = _responses.Pick(key);

            if (!string.IsNullOrWhiteSpace(reply))
            {
                break;
            }
        }

        // 2) Use a dynamic fallback when no configured response exists.
        if (string.IsNullOrWhiteSpace(reply))
        {
            reply = intentName.ToLowerInvariant() switch
            {
                "sys.time.now" =>
                    $"The time is {DateTime.Now:h:mm tt}.",

                "sys.time.date" =>
                    $"Today is {DateTime.Now:yyyy-MM-dd}.",

                "sys.time.dayofweek" =>
                    $"It's {DateTime.Now:dddd}.",

                _ =>
                    "I'm not sure how to respond to that."
            };
        }

        // 3) Deliver the response to the UI/voice output.
        var deliver = new Envelope(
            Id: Guid.NewGuid().ToString(),
            Ts: DateTimeOffset.UtcNow,
            Intent: "ui.out.say",
            Priority: 5,
            CorrelationId: corr,
            Payload: JsonSerializer.SerializeToElement(new
            {
                text = reply
            }));

        _queue.Enqueue(deliver);

        _log.LogInformation(
            "Reply intent={Intent} corr={Corr} → {Reply}",
            intentName,
            corr,
            reply);

        return Task.FromResult(
            HandlerResult.Success(new
            {
                intent = intentName,
                reply
            }));
    }
}