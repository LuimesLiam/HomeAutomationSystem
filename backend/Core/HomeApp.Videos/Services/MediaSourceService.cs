using HomeApp.Videos.Data;
using HomeApp.Videos.Configuration;
using Microsoft.EntityFrameworkCore;

namespace HomeApp.Videos.Services;

public sealed class MediaSourceService
{
    public const string MovieType = "movie";
    public const string TvType = "tv";

    private readonly HomeAppVideosDbContext _dbContext;
    private readonly VideoModuleOptions _options;

    public MediaSourceService(HomeAppVideosDbContext dbContext, VideoModuleOptions options)
    {
        _dbContext = dbContext;
        _options = options;
    }

    public async Task EnsureSeededAsync(CancellationToken ct = default)
    {
        if (await _dbContext.MediaSources.AnyAsync(ct))
        {
            return;
        }

        var seededSources = BuildNormalizedSources(_options.MovieSourcePaths, MovieType)
            .Concat(BuildNormalizedSources(_options.TvSourcePaths, TvType))
            .ToList();

        if (seededSources.Count == 0)
        {
            return;
        }

        _dbContext.MediaSources.AddRange(seededSources);
        await _dbContext.SaveChangesAsync(ct);
    }

    public Task<List<MediaSource>> GetAllAsync(CancellationToken ct = default)
    {
        return _dbContext.MediaSources
            .AsNoTracking()
            .OrderBy(source => source.SourceType)
            .ThenBy(source => source.DisplayOrder)
            .ThenBy(source => source.Id)
            .ToListAsync(ct);
    }

    public string[] GetMovieSourcePaths()
    {
        return GetPaths(MovieType, _options.MovieSourcePaths);
    }

    public string[] GetTvSourcePaths()
    {
        return GetPaths(TvType, _options.TvSourcePaths);
    }

    public async Task<List<MediaSource>> SaveAsync(IEnumerable<MediaSource> requestedSources, CancellationToken ct = default)
    {
        var normalizedSources = NormalizeRequestedSources(requestedSources).ToList();
        var existingSources = await _dbContext.MediaSources
            .OrderBy(source => source.Id)
            .ToListAsync(ct);
        var existingById = existingSources.ToDictionary(source => source.Id);
        var retainedIds = new HashSet<int>();

        foreach (var source in normalizedSources)
        {
            if (source.Id > 0 && existingById.TryGetValue(source.Id, out var existing))
            {
                existing.SourceType = source.SourceType;
                existing.Path = source.Path;
                existing.DisplayOrder = source.DisplayOrder;
                retainedIds.Add(existing.Id);
            }
            else
            {
                _dbContext.MediaSources.Add(new MediaSource
                {
                    SourceType = source.SourceType,
                    Path = source.Path,
                    DisplayOrder = source.DisplayOrder
                });
            }
        }

        foreach (var existing in existingSources.Where(source => !retainedIds.Contains(source.Id)))
        {
            _dbContext.MediaSources.Remove(existing);
        }

        await _dbContext.SaveChangesAsync(ct);
        return await GetAllAsync(ct);
    }

    private string[] GetPaths(string sourceType, IEnumerable<string> fallbackPaths)
    {
        var configuredPaths = _dbContext.MediaSources
            .AsNoTracking()
            .Where(source => source.SourceType == sourceType)
            .OrderBy(source => source.DisplayOrder)
            .ThenBy(source => source.Id)
            .Select(source => source.Path)
            .ToArray();

        return configuredPaths.Length > 0
            ? configuredPaths
            : NormalizePaths(fallbackPaths).ToArray();
    }

    private static IEnumerable<MediaSource> NormalizeRequestedSources(IEnumerable<MediaSource> sources)
    {
        return NormalizeRequestedSources(sources, MovieType)
            .Concat(NormalizeRequestedSources(sources, TvType));
    }

    private static IEnumerable<MediaSource> NormalizeRequestedSources(IEnumerable<MediaSource> sources, string sourceType)
    {
        var seenPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var displayOrder = 0;

        foreach (var source in sources.Where(source => source.SourceType == sourceType))
        {
            var path = source.Path?.Trim();
            if (string.IsNullOrWhiteSpace(path))
            {
                continue;
            }

            var fullPath = Path.GetFullPath(path);
            if (!seenPaths.Add(fullPath))
            {
                continue;
            }

            yield return new MediaSource
            {
                Id = source.Id,
                SourceType = sourceType,
                Path = fullPath,
                DisplayOrder = displayOrder++
            };
        }
    }

    private static IEnumerable<MediaSource> BuildNormalizedSources(IEnumerable<string> paths, string sourceType)
    {
        return NormalizePaths(paths)
            .Select((path, index) => new MediaSource
            {
                SourceType = sourceType,
                Path = path,
                DisplayOrder = index
            });
    }

    private static IEnumerable<string> NormalizePaths(IEnumerable<string> paths)
    {
        return paths
            .Select(path => path?.Trim())
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(path => Path.GetFullPath(path!))
            .Distinct(StringComparer.OrdinalIgnoreCase);
    }
}
