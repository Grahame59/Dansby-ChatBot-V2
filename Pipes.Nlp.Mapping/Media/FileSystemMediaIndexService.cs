using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text.RegularExpressions;

namespace Pipes.Nlp.Mapping.Media;

/// <summary>
/// Reads configured movie and TV-show folders from the filesystem
/// and converts them into MediaItem records.
///
/// This service merges every configured media path into one logical library.
/// </summary>
public sealed class FileSystemMediaIndexService : IMediaIndexService
{
    private readonly MediaLibraryOptions _options;
    private readonly ILogger<FileSystemMediaIndexService> _log;

    private static readonly Regex TitleYearPattern = new(
        @"^(?<title>.+?)\s*\((?<year>\d{4})\)$",
        RegexOptions.Compiled);

    public FileSystemMediaIndexService(
        IOptions<MediaLibraryOptions> options,
        ILogger<FileSystemMediaIndexService> log)
    {
        _options = options.Value;
        _log = log;
    }

    public Task<IReadOnlyList<MediaItem>> GetAllAsync(
        CancellationToken ct)
    {
        var items = new List<MediaItem>();

        ScanMoviePaths(items, ct);
        ScanTvShowPaths(items, ct);

        var orderedItems = items
            .OrderBy(item => item.Type)
            .ThenBy(item => item.Title)
            .ToList();

        return Task.FromResult<IReadOnlyList<MediaItem>>(orderedItems);
    }

    public async Task<IReadOnlyList<MediaItem>> SearchAsync(
        string searchQuery,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(searchQuery))
        {
            return Array.Empty<MediaItem>();
        }

        var allItems = await GetAllAsync(ct);

        var normalizedQuery = searchQuery.Trim();

        return allItems
            .Where(item =>
                item.Title.Contains(
                    normalizedQuery,
                    StringComparison.OrdinalIgnoreCase) ||
                item.DisplayTitle.Contains(
                    normalizedQuery,
                    StringComparison.OrdinalIgnoreCase))
            .OrderBy(item => item.Title)
            .ToList();
    }

    private void ScanMoviePaths(
        List<MediaItem> items,
        CancellationToken ct)
    {
        foreach (var rootPath in _options.MoviePaths)
        {
            ct.ThrowIfCancellationRequested();

            if (!Directory.Exists(rootPath))
            {
                _log.LogWarning(
                    "Movie path does not exist: {Path}",
                    rootPath);

                continue;
            }

            foreach (var directory in Directory.EnumerateDirectories(rootPath))
            {
                ct.ThrowIfCancellationRequested();

                var folderName = Path.GetFileName(directory);
                var (title, year) = ParseTitleAndYear(folderName);

                items.Add(new MediaItem(
                    Title: title,
                    Year: year,
                    Type: MediaType.Movie,
                    Path: directory,
                    SeasonCount: 0,
                    EpisodeCount: 0));
            }
        }
    }

    private void ScanTvShowPaths(
        List<MediaItem> items,
        CancellationToken ct)
    {
        foreach (var rootPath in _options.TvShowPaths)
        {
            ct.ThrowIfCancellationRequested();

            if (!Directory.Exists(rootPath))
            {
                _log.LogWarning(
                    "TV-show path does not exist: {Path}",
                    rootPath);

                continue;
            }

            foreach (var showDirectory in Directory.EnumerateDirectories(rootPath))
            {
                ct.ThrowIfCancellationRequested();

                var folderName = Path.GetFileName(showDirectory);
                var (title, year) = ParseTitleAndYear(folderName);

                var seasonCount = CountSeasonFolders(showDirectory);
                var episodeCount = CountEpisodeFiles(showDirectory);

                items.Add(new MediaItem(
                    Title: title,
                    Year: year,
                    Type: MediaType.TvShow,
                    Path: showDirectory,
                    SeasonCount: seasonCount,
                    EpisodeCount: episodeCount));
            }
        }
    }

    private static (string Title, string? Year) ParseTitleAndYear(
        string folderName)
    {
        var match = TitleYearPattern.Match(folderName);

        if (!match.Success)
        {
            return (folderName.Trim(), null);
        }

        var title = match.Groups["title"].Value.Trim();
        var year = match.Groups["year"].Value;

        return (title, year);
    }

    private static int CountSeasonFolders(string showDirectory)
    {
        return Directory
            .EnumerateDirectories(showDirectory)
            .Count(directory =>
            {
                var name = Path.GetFileName(directory);

                return name.StartsWith(
                    "Season",
                    StringComparison.OrdinalIgnoreCase);
            });
    }

    private static int CountEpisodeFiles(string showDirectory)
    {
        string[] videoExtensions =
        [
            ".mkv",
            ".mp4",
            ".avi",
            ".mov",
            ".m4v"
        ];

        return Directory
            .EnumerateFiles(
                showDirectory,
                "*",
                SearchOption.AllDirectories)
            .Count(file =>
                videoExtensions.Contains(
                    Path.GetExtension(file),
                    StringComparer.OrdinalIgnoreCase));
    }
}