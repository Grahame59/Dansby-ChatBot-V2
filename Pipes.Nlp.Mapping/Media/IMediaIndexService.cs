namespace Pipes.Nlp.Mapping.Media;

public interface IMediaIndexService
{
    Task<IReadOnlyList<MediaItem>> GetAllAsync(
        CancellationToken ct);

    Task<IReadOnlyList<MediaItem>> SearchAsync(
        string searchQuery,
        CancellationToken ct);
}