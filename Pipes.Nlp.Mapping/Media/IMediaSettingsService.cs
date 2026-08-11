namespace Pipes.Nlp.Mapping.Media;

/// <summary>
/// Loads and saves the filesystem locations used by the media library.
/// </summary>
public interface IMediaSettingsService
{
    Task<MediaLibraryOptions> GetAsync(
        CancellationToken ct = default);

    Task SaveAsync(
        MediaLibraryOptions settings,
        CancellationToken ct = default);
}