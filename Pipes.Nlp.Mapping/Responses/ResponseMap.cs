using System.Text.Json;

namespace Pipes.Nlp.Mapping.Responses;

public interface IResponseMap
{
    string? Pick(string key);
    Task ReloadAsync(CancellationToken ct = default);
}

public sealed class ResponseMap : IResponseMap
{
    private readonly string _path;
    private readonly Random _rng = new();
    private Dictionary<string, List<string>> _map = new(StringComparer.OrdinalIgnoreCase);

    public ResponseMap(string? path = null)
    {
        _path = path ?? Path.Combine(AppContext.BaseDirectory, "response_mappings.json");
        if (!File.Exists(_path)) File.WriteAllText(_path, "{}");
        _ = ReloadAsync();
    }

    public async Task ReloadAsync(CancellationToken ct = default)
    {
        await using var fs = File.OpenRead(_path);
        var doc = await JsonSerializer.DeserializeAsync<Dictionary<string, List<string>>>(fs, cancellationToken: ct)
                  ?? new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        _map = doc.ToDictionary(kv => kv.Key, kv => kv.Value ?? new List<string>(), StringComparer.OrdinalIgnoreCase);
    }

    public string? Pick(string key)
    {
        if (!_map.TryGetValue(key, out var list) || list.Count == 0) return null;
        return list.Count == 1 ? list[0] : list[_rng.Next(list.Count)];
    }
}
