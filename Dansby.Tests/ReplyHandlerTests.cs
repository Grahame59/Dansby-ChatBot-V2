using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Pipes.Nlp.Mapping;
using Xunit;

namespace Dansby.Tests;

public sealed class ReplyHandlerTests
{
    [Fact]
    public async Task HandleAsync_SysTimeDate_UsesDateFallback()
    {
        var queue = new TestIntentQueue();
        var handler = new ReplyHandler(
            handledIntent: "sys.time.date",
            responses: new NullResponseMap(),
            queue: queue,
            log: NullLogger<ReplyHandler>.Instance);

        var result = await handler.HandleAsync(
            JsonSerializer.SerializeToElement(new { }),
            corr: "test-correlation",
            ct: CancellationToken.None);

        Assert.True(result.Ok);

        var deliver = Assert.Single(queue.Items);
        Assert.Equal("ui.out.say", deliver.Intent);
        Assert.Equal("test-correlation", deliver.CorrelationId);
        Assert.Equal($"Today is {DateTime.Now:yyyy-MM-dd}.", deliver.Payload.GetProperty("text").GetString());
    }
}
