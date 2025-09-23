namespace Dansby.Shared;

public interface IIntentQueue
{
    void Enqueue(Envelope env);
    bool TryDequeue(out Envelope? env);
}
