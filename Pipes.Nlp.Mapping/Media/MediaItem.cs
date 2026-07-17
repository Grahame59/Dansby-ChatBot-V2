namespace Pipes.Nlp.Mapping.Media;

public sealed record MediaItem(
    string Title,
    string? Year,
    MediaType Type,
    string Path,
    int SeasonCount,
    int EpisodeCount)
{
    public string DisplayTitle =>
        string.IsNullOrWhiteSpace(Year)
            ? Title
            : $"{Title} ({Year})";
}