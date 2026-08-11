using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Pipes.Nlp.Mapping.Media;

public sealed class JsonMediaSettingsService : IMediaSettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    private readonly string _settingsPath;
    private readonly ILogger<JsonMediaSettingsService> _log;
    private readonly SemaphoreSlim _fileLock = new(1, 1);

    public JsonMediaSettingsService(
        string settingsPath,
        ILogger<JsonMediaSettingsService> log)
    {
        if (string.IsNullOrWhiteSpace(settingsPath))
        {
            throw new ArgumentException(
                "A media-settings path is required.",
                nameof(settingsPath));
        }

        _settingsPath = settingsPath;
        _log = log;
    }

    public async Task<MediaLibraryOptions> GetAsync(
        CancellationToken ct = default)
    {
        await _fileLock.WaitAsync(ct);

        try
        {
            if (!File.Exists(_settingsPath))
            {
                var initialSettings = new MediaLibraryOptions();
                await SaveCoreAsync(initialSettings, ct);
                return initialSettings;
            }

            await using var stream = File.OpenRead(_settingsPath);

            var settings =
                await JsonSerializer.DeserializeAsync<MediaLibraryOptions>(
                    stream,
                    JsonOptions,
                    ct);

            return Normalize(settings ?? new MediaLibraryOptions());
        }
        catch (JsonException ex)
        {
            _log.LogError(
                ex,
                "Media settings file contains invalid JSON: {Path}",
                _settingsPath);

            throw;
        }
        catch (IOException ex)
        {
            _log.LogError(
                ex,
                "Unable to read media settings from {Path}",
                _settingsPath);

            throw;
        }
        finally
        {
            _fileLock.Release();
        }
    }

    public async Task SaveAsync(
        MediaLibraryOptions settings,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(settings);

        await _fileLock.WaitAsync(ct);

        try
        {
            await SaveCoreAsync(Normalize(settings), ct);

            _log.LogInformation(
                "Saved media settings to {Path}",
                _settingsPath);
        }
        finally
        {
            _fileLock.Release();
        }
    }

    private async Task SaveCoreAsync(
        MediaLibraryOptions settings,
        CancellationToken ct)
    {
        var directory = Path.GetDirectoryName(_settingsPath);

        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var temporaryPath = _settingsPath + ".tmp";

        await using (var stream = File.Create(temporaryPath))
        {
            await JsonSerializer.SerializeAsync(
                stream,
                settings,
                JsonOptions,
                ct);
        }

        File.Move(
            temporaryPath,
            _settingsPath,
            overwrite: true);
    }

    private static MediaLibraryOptions Normalize(
        MediaLibraryOptions settings)
    {
        return new MediaLibraryOptions
        {
            MoviePaths = NormalizePaths(settings.MoviePaths),
            TvShowPaths = NormalizePaths(settings.TvShowPaths)
        };
    }

    private static List<string> NormalizePaths(
        IEnumerable<string>? paths)
    {
        return paths?
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(path => path.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList()
            ?? [];
    }
}