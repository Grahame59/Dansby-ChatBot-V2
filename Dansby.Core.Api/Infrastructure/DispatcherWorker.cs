using System.Text.Json;
using Dansby.Shared;

namespace Dansby.Core.Api.Infrastructure;

internal sealed class DispatcherWorker : BackgroundService
{
    private readonly IIntentQueue _queue;
    private readonly IHandlerRegistry _registry;
    private readonly ILogger<DispatcherWorker> _log;

    public DispatcherWorker(
        IIntentQueue queue,
        IHandlerRegistry registry,
        ILogger<DispatcherWorker> log)
    {
        _queue = queue;
        _registry = registry;
        _log = log;
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
                _log.LogWarning(
                    "No handler for intent {Intent} corr={Corr}",
                    env.Intent,
                    env.CorrelationId);
                continue;
            }

            try
            {
                var result = await handler.HandleAsync(env.Payload, env.CorrelationId, stoppingToken);
                if (result.Ok)
                {
                    _log.LogInformation(
                        "OK intent={Intent} corr={Corr} data={Data}",
                        env.Intent,
                        env.CorrelationId,
                        JsonSerializer.Serialize(result.Data));
                }
                else
                {
                    _log.LogWarning(
                        "ERR intent={Intent} corr={Corr} code={Code} msg={Msg}",
                        env.Intent,
                        env.CorrelationId,
                        result.ErrorCode,
                        result.Message);
                }
            }
            catch (Exception ex)
            {
                _log.LogError(
                    ex,
                    "Unhandled handler error intent={Intent} corr={Corr}",
                    env.Intent,
                    env.CorrelationId);
            }
        }

        _log.LogInformation("Dispatcher stopped");
    }
}
