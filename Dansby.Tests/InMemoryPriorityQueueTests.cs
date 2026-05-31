using System.Text.Json;
using Dansby.Core.Api.Infrastructure;
using Dansby.Shared;
using Xunit;

namespace Dansby.Tests;

public sealed class InMemoryPriorityQueueTests
{
    [Fact]
    public void TryDequeue_WhenQueueIsEmpty_ReturnsFalse()
    {
        var queue = new InMemoryPriorityQueue();

        var hasEnvelope = queue.TryDequeue(out var envelope);

        Assert.False(hasEnvelope);
        Assert.Null(envelope);
        Assert.Equal(0, queue.TotalCount);
    }

    [Fact]
    public void TryDequeue_ReturnsLowerNumericPriorityFirst()
    {
        var queue = new InMemoryPriorityQueue();
        var lowPriority = CreateEnvelope("low", priority: 9);
        var highPriority = CreateEnvelope("high", priority: 0);
        var normalPriority = CreateEnvelope("normal", priority: 5);

        queue.Enqueue(lowPriority);
        queue.Enqueue(highPriority);
        queue.Enqueue(normalPriority);

        Assert.True(queue.TryDequeue(out var first));
        Assert.Equal("high", first?.Intent);

        Assert.True(queue.TryDequeue(out var second));
        Assert.Equal("normal", second?.Intent);

        Assert.True(queue.TryDequeue(out var third));
        Assert.Equal("low", third?.Intent);
    }

    [Fact]
    public void TryDequeue_PreservesFifoOrderWithinSamePriority()
    {
        var queue = new InMemoryPriorityQueue();

        queue.Enqueue(CreateEnvelope("first", priority: 4));
        queue.Enqueue(CreateEnvelope("second", priority: 4));

        Assert.True(queue.TryDequeue(out var first));
        Assert.Equal("first", first?.Intent);

        Assert.True(queue.TryDequeue(out var second));
        Assert.Equal("second", second?.Intent);
    }

    [Fact]
    public void CountByPriority_ReportsQueuedItemsPerPriority()
    {
        var queue = new InMemoryPriorityQueue();

        queue.Enqueue(CreateEnvelope("urgent", priority: 0));
        queue.Enqueue(CreateEnvelope("normal-one", priority: 5));
        queue.Enqueue(CreateEnvelope("normal-two", priority: 5));

        var counts = queue.CountByPriority();

        Assert.Equal(10, counts.Length);
        Assert.Equal(1, counts[0]);
        Assert.Equal(2, counts[5]);
        Assert.Equal(3, queue.TotalCount);
    }

    private static Envelope CreateEnvelope(string intent, int priority) =>
        new(
            Id: Guid.NewGuid().ToString(),
            Ts: DateTimeOffset.UtcNow,
            Intent: intent,
            Priority: priority,
            CorrelationId: Guid.NewGuid().ToString(),
            Payload: JsonSerializer.SerializeToElement(new { }));
}
