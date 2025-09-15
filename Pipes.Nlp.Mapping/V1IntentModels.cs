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
}

public sealed class ExampleDef
{
    [JsonPropertyName("utterance")]
    public string Utterance { get; set; } = "";

    [JsonPropertyName("tokens")]
    public List<string>? Tokens { get; set; }
}
