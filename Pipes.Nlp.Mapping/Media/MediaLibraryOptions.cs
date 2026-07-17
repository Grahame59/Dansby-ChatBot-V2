namespace Pipes.Nlp.Mapping.Media;

/// <summary>
/// Configuration settings for all filesystem locations that contain
/// movies and TV shows.
///
/// Multiple paths are supported so additional Plex drives can be added
/// without changing the media indexing code.
/// </summary>
public sealed class MediaLibraryOptions
{
    public List<string> MoviePaths { get; set; } = [];

    public List<string> TvShowPaths { get; set; } = [];
}