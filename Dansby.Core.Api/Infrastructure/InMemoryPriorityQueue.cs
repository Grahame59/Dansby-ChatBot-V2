using System.Collections.Concurrent;
using Dansby.Shared;

namespace Dansby.Core.Api.Infrastructure;

internal sealed class InMemoryPriorityQueue : IIntentQueue
{
    private readonly ConcurrentQueue<Envelope>[] _queues =
        Enumerable.Range(0, 10).Select(_ => new ConcurrentQueue<Envelope>()).ToArray();

    public void Enqueue(Envelope env) => _queues[env.Priority].Enqueue(env);

    public bool TryDequeue(out Envelope? env)
    {
        // Lower numeric priority wins: 0 is highest, 9 is lowest.
        for (var priority = 0; priority < _queues.Length; priority++)
        {
            if (_queues[priority].TryDequeue(out var candidate))
            {
                env = candidate;
                return true;
            }
        }

        env = null;
        return false;
    }

    public int[] CountByPriority() => _queues.Select(q => q.Count).ToArray();

    public int TotalCount => _queues.Sum(q => q.Count);
}
