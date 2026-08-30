using System.Text.Json;
using HomeApp.Videos.Configuration;

namespace HomeApp.Host.Settings;

public sealed class MediaLibrarySettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    private readonly string _filePath;
    private readonly VideoModuleOptions _videoOptions;

    public MediaLibrarySettingsStore(IWebHostEnvironment environment, VideoModuleOptions videoOptions)
    {
        _filePath = Path.Combine(environment.ContentRootPath, "media-library-settings.json");
        _videoOptions = videoOptions;
    }

    public MediaLibrarySettingsDto GetCurrent()
    {
        return new MediaLibrarySettingsDto
        {
            MovieSourcePaths = [.. _videoOptions.MovieSourcePaths],
            TvSourcePaths = [.. _videoOptions.TvSourcePaths]
        };
    }

    public async Task<MediaLibrarySettingsDto> SaveAsync(MediaLibrarySettingsDto settings, CancellationToken ct = default)
    {
        var normalized = new MediaLibrarySettingsDto
        {
            MovieSourcePaths = NormalizePaths(settings.MovieSourcePaths),
            TvSourcePaths = NormalizePaths(settings.TvSourcePaths)
        };

        var directoryPath = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrWhiteSpace(directoryPath))
        {
            Directory.CreateDirectory(directoryPath);
        }

        await using var stream = File.Create(_filePath);
        await JsonSerializer.SerializeAsync(stream, normalized, JsonOptions, ct);

        _videoOptions.MovieSourcePaths = normalized.MovieSourcePaths;
        _videoOptions.TvSourcePaths = normalized.TvSourcePaths;

        return normalized;
    }

    public static void ApplyPersistedSettings(string contentRootPath, VideoModuleOptions videoOptions)
    {
        var filePath = Path.Combine(contentRootPath, "media-library-settings.json");
        if (!File.Exists(filePath))
        {
            return;
        }

        try
        {
            var content = File.ReadAllText(filePath);
            var persisted = JsonSerializer.Deserialize<MediaLibrarySettingsDto>(content, JsonOptions);
            if (persisted == null)
            {
                return;
            }

            videoOptions.MovieSourcePaths = NormalizePaths(persisted.MovieSourcePaths);
            videoOptions.TvSourcePaths = NormalizePaths(persisted.TvSourcePaths);
        }
        catch
        {
            // Ignore malformed persisted settings and continue with environment defaults.
        }
    }

    private static string[] NormalizePaths(IEnumerable<string>? paths)
    {
        if (paths == null)
        {
            return [];
        }

        return paths
            .Select(path => path?.Trim())
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(path => Path.GetFullPath(path!))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
}

public sealed class MediaLibrarySettingsDto
{
    public string[] MovieSourcePaths { get; set; } = [];
    public string[] TvSourcePaths { get; set; } = [];
}
