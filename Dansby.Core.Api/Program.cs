using System.Collections.Concurrent;
using System.Text.Json;
using Dansby.Shared;
using Pipes.Nlp.Mapping;

internal class Program
{
    private static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        builder.Logging.ClearProviders();
        builder.Logging.AddSimpleConsole(o => { o.SingleLine = true; o.TimestampFormat = "HH:mm:ss "; });

        // Dependency Injection: queue, registry, worker, handlers
        builder.Services.AddSingleton<IIntentQueue, InMemoryPriorityQueue>();
        builder.Services.AddSingleton<IHandlerRegistry, HandlerRegistry>();

            // v1.1 recognizer wired into v2
            builder.Services.AddSingleton<Pipes.Nlp.Mapping.V1Tokenizer>();
            builder.Services.AddSingleton<Pipes.Nlp.Mapping.V1RecognizerEngine>();
            builder.Services.AddSingleton<Pipes.Nlp.Mapping.ITextRecognizer, Pipes.Nlp.Mapping.V1RecognizerAdapter>();

        // Auto-Registry for Handlers
        builder.Services.AddAllIntentHandlersFrom
        (
            typeof(Pipes.Nlp.Mapping.NlpRecognizeHandler).Assembly
            // add more assemblies as more modules are added
        );
        builder.Services.AddHostedService<DispatcherWorker>();

        var app = builder.Build();

        app.MapGet("/health", () => Results.Json(new { status = "ok" }));

        /// <summary>
        /// This /intents endpoint: 
        /// 
        ///     - Checks the API key (Currently: superlongrandomvalue123) 
        ///     - Validates the intent name exists
        ///     - Normalizes data and builds an Envelope
        ///     - Enqueues the Envelope onto the priority queue
        ///     - Returns immediately (202/200) to the client
        /// 
        /// </summary>
        
        app.MapPost("/intents", async (HttpRequest http, IntentRequest req, IIntentQueue queue, IHandlerRegistry reg) =>
        {
            // 1) API key check
            var configuredKey = app.Configuration["DANSBY_API_KEY"];
            if (string.IsNullOrEmpty(configuredKey) ||
                !http.Headers.TryGetValue("X-Api-Key", out var key) ||
                key != configuredKey)
            {
                return Results.Unauthorized();
            }

            // 2) Basic validation
            if (string.IsNullOrWhiteSpace(req.Intent))
                return Results.BadRequest(new { error = "intent required" });

            // (Optional) As of now, rejecting unknown intents early instead of dropping later
            if (reg.Resolve(req.Intent.Trim()) is null)
                return Results.BadRequest(new { error = $"unknown intent '{req.Intent}'" });

            // 3) Build envelope and enqueue
            var env = new Envelope(
                Id: Guid.NewGuid().ToString(),
                Ts: DateTimeOffset.UtcNow,
                Intent: req.Intent.Trim(),
                Priority: Math.Clamp(req.Priority ?? 5, 0, 9),
                CorrelationId: string.IsNullOrWhiteSpace(req.CorrelationId) ? Guid.NewGuid().ToString() : req.CorrelationId!,
                Payload: req.Payload.ValueKind == JsonValueKind.Undefined ? JsonDocument.Parse("{}").RootElement : req.Payload
            );

            queue.Enqueue(env);
            return Results.Json(new { accepted = true, id = env.Id, correlationId = env.CorrelationId });
        });

        app.Run();
    }
}

// ---- Models & Services ----
record IntentRequest(string Intent, int? Priority, string? CorrelationId, JsonElement Payload);

interface IIntentQueue
{
    void Enqueue(Envelope env);
    bool TryDequeue(out Envelope? env);
}

sealed class InMemoryPriorityQueue : IIntentQueue
{
    private readonly ConcurrentQueue<Envelope>[] _qs = Enumerable.Range(0, 10).Select(_ => new ConcurrentQueue<Envelope>()).ToArray();

    public void Enqueue(Envelope env) => _qs[env.Priority].Enqueue(env);

    public bool TryDequeue(out Envelope? env)
    {
        // scan from highest priority (0) to lowest (9)
        for (int p = 0; p < _qs.Length; p++)
            if (_qs[p].TryDequeue(out var e)) { env = e; return true; }
        env = null; return false;
    }
}

interface IHandlerRegistry
{
    void Register(IIntentHandler handler);
    IIntentHandler? Resolve(string intent);
}

sealed class HandlerRegistry : IHandlerRegistry
{
    private readonly ConcurrentDictionary<string, IIntentHandler> _map = new(StringComparer.OrdinalIgnoreCase);
    public HandlerRegistry(IEnumerable<IIntentHandler> handlers)
    {
        foreach (var h in handlers) Register(h);
    }
    public void Register(IIntentHandler handler) => _map[handler.Name] = handler;
    public IIntentHandler? Resolve(string intent) => _map.TryGetValue(intent, out var h) ? h : null;
}

/// <summary>
/// The DispatcherWorker Class:
/// 
///     - Loops, pops an Envelope off the queue
///     - Finds the right handler by the envelope’s Intent via HandlerRegistry
///     - Calls the handler’s HandleAsync(payload, correlationId, ct)
///     - Logs success or error
/// 
/// </summary>
sealed class DispatcherWorker : BackgroundService
{
    private readonly IIntentQueue _queue;
    private readonly IHandlerRegistry _registry;
    private readonly ILogger<DispatcherWorker> _log;

    public DispatcherWorker(IIntentQueue q, IHandlerRegistry reg, ILogger<DispatcherWorker> log)
    {
        _queue = q; _registry = reg; _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _log.LogInformation("Dispatcher started");
        while (!stoppingToken.IsCancellationRequested)
        {
            if (!_queue.TryDequeue(out var env) || env is null)
            {
                await Task.Delay(15, stoppingToken);
                continue;
            }

            var handler = _registry.Resolve(env.Intent);
            if (handler is null)
            {
                _log.LogWarning("No handler for intent {Intent} corr={Corr}", env.Intent, env.CorrelationId);
                continue;
            }

            try
            {
                var res = await handler.HandleAsync(env.Payload, env.CorrelationId, stoppingToken);
                if (res.Ok)
                    _log.LogInformation("OK intent={Intent} corr={Corr} data={Data}", env.Intent, env.CorrelationId, System.Text.Json.JsonSerializer.Serialize(res.Data));
                else
                    _log.LogWarning("ERR intent={Intent} corr={Corr} code={Code} msg={Msg}", env.Intent, env.CorrelationId, res.ErrorCode, res.Message);
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Unhandled handler error intent={Intent} corr={Corr}", env.Intent, env.CorrelationId);
            }
        }
        _log.LogInformation("Dispatcher stopped");
    }
}
