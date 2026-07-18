using System.Text.Json;
using Dansby.Core.Api.Contracts;
using Dansby.Core.Api.Infrastructure;
using Dansby.Shared;
using Microsoft.AspNetCore.RateLimiting;
using Pipes.Nlp.Mapping;
using Pipes.Nlp.Mapping.Media; 

internal class Program
{
    private static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        ConfigureLogging(builder);
        ConfigureServices(builder);
        ConfigureRateLimiting(builder);

        var app = builder.Build();

        ConfigureMiddleware(app);
        MapEndpoints(app);

        app.Run();
    }

    private static void ConfigureLogging(WebApplicationBuilder builder)
    {
        builder.Logging.ClearProviders();
        builder.Logging.AddSimpleConsole(o =>
        {
            o.SingleLine = true;
            o.TimestampFormat = "HH:mm:ss ";
        });
    }

    private static void ConfigureServices(WebApplicationBuilder builder)
    {
        builder.Services.AddSingleton<IIntentQueue, InMemoryPriorityQueue>();
        builder.Services.AddSingleton<IHandlerRegistry, HandlerRegistry>();

        builder.Services.AddSingleton<ITokenizer, V1Tokenizer>();
        builder.Services.AddSingleton<V1RecognizerEngine>();
        builder.Services.AddSingleton<ITextRecognizer, V1RecognizerAdapter>();
        builder.Services.AddSingleton<Pipes.Nlp.Mapping.Responses.IResponseMap>(sp =>
        {
            var env = sp.GetRequiredService<IHostEnvironment>();
            var path = Path.Combine(env.ContentRootPath, "response_mappings.json");
            return new Pipes.Nlp.Mapping.Responses.ResponseMap(path);
        });

        builder.Services.AddAllIntentHandlersFrom(
            typeof(Pipes.Nlp.Mapping.NlpRecognizeHandler).Assembly);

        builder.Services.AddHostedService<DispatcherWorker>();

        builder.Services.AddCors(o => o.AddPolicy("ui", p => p
            .AllowAnyOrigin()
            .AllowAnyHeader()
            .AllowAnyMethod()));

        builder.Services.AddSingleton<IIntentHandler, UiSayLogHandler>();

        // Media library services
        builder.Services.Configure<MediaLibraryOptions>(builder.Configuration.GetSection("MediaLibrary"));
        builder.Services.AddSingleton<IMediaIndexService,FileSystemMediaIndexService>();

        // Zebra printer services
        builder.Services.AddSingleton<IIntentHandler, Pipes.Devices.ZebraPrinter.ZebraPrintSimpleHandler>();
        builder.Services.AddSingleton<IIntentHandler, Pipes.Devices.ZebraPrinter.ZebraPrintMailerPreviewHandler>();
        builder.Services.AddSingleton<IIntentHandler, Pipes.Devices.ZebraPrinter.ZebraPrintMailerFromCsvHandler>();

        RegisterReplyHandlers(builder.Services);
    }

    private static void RegisterReplyHandlers(IServiceCollection services)
    {
        string[] replyIntents =
        {
            "chat.greet", "chat.farewell", "chat.help", "chat.howareyou", "sys.status.current",
            "sys.meta.creator", "sys.meta.favoritecolor", "chat.thanks.reply",
            "chat.compliment", "chat.love", "chat.missedyou.reply",
            "chat.name.confirm", "chat.name.spelling", "chat.name.asked", "fun.easteregg.steven",
            "weather.forecast", "weather.temperature",
            "sys.time.now", "sys.time.date", "sys.time.dayofweek"
        };

        foreach (var intent in replyIntents)
        {
            services.AddSingleton<IIntentHandler>(sp =>
                new ReplyHandler(
                    handledIntent: intent,
                    responses: sp.GetRequiredService<Pipes.Nlp.Mapping.Responses.IResponseMap>(),
                    queue: sp.GetRequiredService<IIntentQueue>(),
                    log: sp.GetRequiredService<ILogger<ReplyHandler>>()));
        }
    }

    private static void ConfigureRateLimiting(WebApplicationBuilder builder)
    {
        builder.Services.AddRateLimiter(_ => _
            .AddFixedWindowLimiter("intents", o =>
            {
                o.PermitLimit = 20;
                o.Window = TimeSpan.FromSeconds(10);
            }));
    }

    private static void ConfigureMiddleware(WebApplication app)
    {
        app.UseCors("ui");
        app.UseDefaultFiles();
        app.UseStaticFiles();
        app.UseRateLimiter();
    }

    private static void MapEndpoints(WebApplication app)
    {
        var protectedApi = app.MapGroup("")
            .AddEndpointFilter(ApiKeyEndpointFilter.RequireApiKey);

        protectedApi.MapPost("/debug/handle", HandleDebugIntent)
            .RequireRateLimiting("intents");

        protectedApi.MapPost("/debug/respond", HandleDebugRespond)
            .RequireRateLimiting("intents");

        protectedApi.MapPost("/responses/reload", ReloadResponses)
            .RequireRateLimiting("intents");

        protectedApi.MapPost("/debug/recognize", RecognizeText)
            .RequireRateLimiting("intents");

        protectedApi.MapPost("/intents", EnqueueIntent)
            .RequireRateLimiting("intents");

        app.MapGet("/health", () => Results.Json(new { status = "ok" }));
    }

    private static async Task<IResult> HandleDebugIntent(
        IHandlerRegistry registry,
        JsonElement body,
        CancellationToken ct)
    {
        if (!body.TryGetProperty("intent", out var intentElement) ||
            intentElement.ValueKind != JsonValueKind.String)
        {
            return Results.BadRequest(new { error = "body.intent (string) required" });
        }

        var intent = intentElement.GetString() ?? "";
        if (string.IsNullOrWhiteSpace(intent))
        {
            return Results.BadRequest(new { error = "body.intent cannot be empty" });
        }

        var payload = body.TryGetProperty("payload", out var payloadElement) &&
                      payloadElement.ValueKind != JsonValueKind.Undefined
            ? payloadElement
            : JsonDocument.Parse("{}").RootElement;

        var handler = registry.Resolve(intent);
        if (handler is null)
        {
            return Results.BadRequest(new { error = $"no handler registered for intent '{intent}'" });
        }

        var correlationId = Guid.NewGuid().ToString("n");
        var result = await handler.HandleAsync(payload, correlationId, ct);

        if (!result.Ok)
        {
            return Results.BadRequest(new
            {
                error = result.ErrorCode,
                message = result.Message,
                intent,
                corr = correlationId
            });
        }

        return Results.Json(new { intent, corr = correlationId, result = result.Data });
    }

    private static async Task<IResult> HandleDebugRespond(ITextRecognizer recognizer, IHandlerRegistry registry, JsonElement body, CancellationToken ct)
    {
        if (!body.TryGetProperty("text", out var textElement) ||
            textElement.ValueKind != JsonValueKind.String)
        {
            return Results.BadRequest(new
            {
                error = "body.text (string) required"
            });
        }

        var text = textElement.GetString() ?? "";

        // Keep the extracted slots instead of discarding them.
        var (intent, _, slots, _) = recognizer.Recognize(text);

        var handler = registry.Resolve(intent);

        if (handler is null)
        {
            return Results.BadRequest(new
            {
                error = $"no handler for recognized intent '{intent}'"
            });
        }

        JsonElement payload;

        if (intent.Equals(
            "media.search",
            StringComparison.OrdinalIgnoreCase))
        {
            if (!slots.TryGetValue("SearchQuery", out var searchQuery) ||
                string.IsNullOrWhiteSpace(searchQuery))
            {
                return Results.BadRequest(new
                {
                    error = "media.search did not extract a SearchQuery slot",
                    intent
                });
            }

            payload = JsonSerializer.SerializeToElement(new
            {
                SearchQuery = searchQuery
            });
        }
        else if (intent.Equals(
            "zebra.print.simple",
            StringComparison.OrdinalIgnoreCase))
        {
            payload = JsonSerializer.SerializeToElement(new
            {
                labelText = ExtractLabelText(text)
            });
        }
        else
        {
            payload = JsonSerializer.SerializeToElement(new
            {
                text
            });
        }

        var correlationId = Guid.NewGuid().ToString();

        var result = await handler.HandleAsync(
            payload,
            correlationId,
            ct);

        if (!result.Ok)
        {
            return Results.BadRequest(new
            {
                error = result.ErrorCode,
                message = result.Message,
                intent,
                corr = correlationId
            });
        }

        return Results.Json(new
        {
            intent,
            corr = correlationId,
            result = result.Data
        });
    }
    private static async Task<IResult> ReloadResponses(Pipes.Nlp.Mapping.Responses.IResponseMap map)
    {
        await map.ReloadAsync();
        return Results.Json(new { reloaded = true });
    }

    private static IResult RecognizeText(ITextRecognizer recognizer, JsonElement body)
    {
        if (!body.TryGetProperty("text", out var textElement) ||
            textElement.ValueKind != JsonValueKind.String)
        {
            return Results.BadRequest(new { error = "body.text (string) required" });
        }

        var (intent, score, slots, domain) = recognizer.Recognize(textElement.GetString() ?? "");
        return Results.Json(new { intent, score, domain, slots });
    }

    private static IResult EnqueueIntent(
        IntentRequest request,
        IIntentQueue queue,
        IHandlerRegistry registry)
    {
        if (string.IsNullOrWhiteSpace(request.Intent))
        {
            return Results.BadRequest(new { error = "intent required" });
        }

        var intent = request.Intent.Trim();
        if (registry.Resolve(intent) is null)
        {
            return Results.BadRequest(new { error = $"unknown intent '{request.Intent}'" });
        }

        var env = new Envelope(
            Id: Guid.NewGuid().ToString(),
            Ts: DateTimeOffset.UtcNow,
            Intent: intent,
            Priority: Math.Clamp(request.Priority ?? 5, 0, 9),
            CorrelationId: string.IsNullOrWhiteSpace(request.CorrelationId)
                ? Guid.NewGuid().ToString()
                : request.CorrelationId!,
            Payload: request.Payload.ValueKind == JsonValueKind.Undefined
                ? JsonDocument.Parse("{}").RootElement
                : request.Payload);

        queue.Enqueue(env);
        return Results.Json(new
        {
            accepted = true,
            id = env.Id,
            correlationId = env.CorrelationId
        });
    }

    private static string ExtractLabelText(string text)
    {
        string[] separators = [":", "-"];

        var position = -1;
        string? usedSeparator = null;

        foreach (var separator in separators)
        {
            var index = text.IndexOf(separator, StringComparison.OrdinalIgnoreCase);
            if (index >= 0 && (position == -1 || index < position))
            {
                position = index;
                usedSeparator = separator;
            }
        }

        return position >= 0 && usedSeparator is not null
            ? text[(position + usedSeparator.Length)..].Trim()
            : string.Empty;
    }
}
