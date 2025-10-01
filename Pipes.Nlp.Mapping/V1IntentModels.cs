using System.Text.Json.Serialization;

namespace Pipes.Nlp.Mapping;

public sealed class IntentDef
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("examples")]
    public List<ExampleDef> Examples { get; set; } = new();

    [JsonPropertyName("tags")]
    public List<string>? Tags { get; set; }

    [JsonPropertyName("aliases")]
    public List<string> Aliases { get; set; } = new();     

    [JsonPropertyName("deprecated")]
    public bool Deprecated { get; set; }                   
}

public sealed class ExampleDef
{
    [JsonPropertyName("utterance")]
    public string Utterance { get; set; } = "";

    // Keep for backward-compat, but we’ll recompute anyway
    [JsonPropertyName("tokens")]
    public IReadOnlyList<string>? Tokens { get; set; }
}
