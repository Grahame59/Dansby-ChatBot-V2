using Pipes.Nlp.Mapping.Responses;

namespace Dansby.Tests;

internal sealed class NullResponseMap : IResponseMap
{
    public string? Pick(string key) => null;

    public Task ReloadAsync(CancellationToken ct = default) => Task.CompletedTask;
}
