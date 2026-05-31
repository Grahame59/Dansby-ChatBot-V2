using Dansby.Shared;

namespace Dansby.Tests;

internal sealed class TestIntentQueue : IIntentQueue
{
    private readonly Queue<Envelope> _items = new();

    public IReadOnlyCollection<Envelope> Items => _items;

    public void Enqueue(Envelope env) => _items.Enqueue(env);

    public bool TryDequeue(out Envelope? env)
    {
        if (_items.TryDequeue(out var item))
        {
            env = item;
            return true;
        }

        env = null;
        return false;
    }
}
