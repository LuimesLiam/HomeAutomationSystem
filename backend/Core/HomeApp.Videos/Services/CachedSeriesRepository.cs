using HomeApp.Videos.Data;
using HomeApp.Videos.Services;
using HomeApp.Videos.DTOs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace HomeApp.Videos.Services;

/// <summary>
/// Enhanced series repository with caching
/// </summary>
public class CachedSeriesRepository : ISeriesRepository
{
    private readonly HomeAppVideosDbContext _db;
    private readonly IMemoryCache _cache;
    private readonly ILogger<CachedSeriesRepository> _logger;
    
    private const string CacheKeyByGenre = "series:byGenre";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(15);

    public CachedSeriesRepository(HomeAppVideosDbContext db, IMemoryCache cache, ILogger<CachedSeriesRepository> logger)
    {
        _db = db;
        _cache = cache;
        _logger = logger;
    }

    public async Task<(List<Series> Series, int TotalCount)> GetAllAsync(PaginationRequest? pagination = null, CancellationToken ct = default)
    {
        var query = _db.Series
            .Where(s => !s.Hidden)
            .Include(s => s.Episodes.Where(e => !e.Hidden))
            .AsNoTracking();

        var totalCount = await query.CountAsync(ct);

        if (pagination != null)
        {
            query = pagination.SortBy?.ToLowerInvariant() switch
            {
                "title" => pagination.SortDescending 
                    ? query.OrderByDescending(s => s.Title) 
                    : query.OrderBy(s => s.Title),
                "name" => pagination.SortDescending 
                    ? query.OrderByDescending(s => s.Name) 
                    : query.OrderBy(s => s.Name),
                _ => query.OrderBy(s => s.Title)
            };

            if (!string.IsNullOrWhiteSpace(pagination.Search))
            {
                var search = pagination.Search.ToLower();
                query = query.Where(s => 
                    s.Title.ToLower().Contains(search) ||
                    s.Name.ToLower().Contains(search) ||
                    (s.Genre != null && s.Genre.ToLower().Contains(search)));
            }

            query = query
                .Skip((pagination.Page - 1) * pagination.PageSize)
                .Take(pagination.PageSize);
        }
        else
        {
            query = query.OrderBy(s => s.Title);
        }

        var series = await query.ToListAsync(ct);
        return (series, totalCount);
    }

    public async Task<Dictionary<string, List<Series>>> GetByGenreAsync(int? sampleSize = null, bool includeEpisodes = true, CancellationToken ct = default)
    {
        var cacheKey = $"{CacheKeyByGenre}:{sampleSize}:{includeEpisodes}";
        
        if (_cache.TryGetValue(cacheKey, out Dictionary<string, List<Series>>? cached) && cached != null)
            return cached;

        var query = _db.Series.Where(s => !s.Hidden);
        
        if (includeEpisodes)
        {
            query = query.Include(s => s.Episodes.Where(e => !e.Hidden).OrderBy(e => e.Season).ThenBy(e => e.EpisodeNumber));
        }

        var seriesList = await query.AsNoTracking().ToListAsync(ct);
        var result = new Dictionary<string, List<Series>>();

        foreach (var series in seriesList)
        {
            var genres = (series.Genre ?? "Unknown")
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            
            if (genres.Length == 0) 
                genres = new[] { "Unknown" };

            foreach (var genre in genres)
            {
                if (!result.ContainsKey(genre))
                    result[genre] = new List<Series>();
                
                if (!sampleSize.HasValue || result[genre].Count < sampleSize.Value)
                    result[genre].Add(series);
            }
        }

        var sortedResult = result
            .OrderBy(kv => kv.Key)
            .ToDictionary(
                kv => kv.Key,
                kv => kv.Value.OrderBy(s => s.Title).ToList()
            );

        _cache.Set(cacheKey, sortedResult, CacheDuration);
        return sortedResult;
    }

    public async Task<List<Series>> GetByGenreAsync(string genre, bool includeEpisodes = true, CancellationToken ct = default)
    {
        var query = _db.Series.Where(s => !s.Hidden);
        
        if (includeEpisodes)
        {
            query = query.Include(s => s.Episodes.Where(e => !e.Hidden).OrderBy(e => e.Season).ThenBy(e => e.EpisodeNumber));
        }

        var seriesList = await query.AsNoTracking().ToListAsync(ct);

        return seriesList
            .Where(s => (s.Genre ?? "Unknown")
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Contains(genre, StringComparer.OrdinalIgnoreCase))
            .OrderBy(s => s.Title)
            .ToList();
    }

    public async Task<Series?> GetByIdAsync(int id, bool includeEpisodes = true, CancellationToken ct = default)
    {
        var query = _db.Series.AsNoTracking();
        
        if (includeEpisodes)
        {
            query = query.Include(s => s.Episodes.Where(e => !e.Hidden).OrderBy(e => e.Season).ThenBy(e => e.EpisodeNumber));
        }

        return await query.FirstOrDefaultAsync(s => s.Id == id, ct);
    }

    public async Task<Series?> GetByNameAsync(string name, CancellationToken ct = default)
    {
        return await _db.Series.FirstOrDefaultAsync(s => s.Name == name, ct);
    }

    public async Task<Series> GetOrCreateAsync(string name, CancellationToken ct = default)
    {
        var series = await _db.Series.FirstOrDefaultAsync(s => s.Name == name, ct);
        
        if (series == null)
        {
            series = new Series
            {
                Name = name,
                Title = name,
                Hidden = false
            };
            _db.Series.Add(series);
            await _db.SaveChangesAsync(ct);
            InvalidateCache();
        }

        return series;
    }

    public async Task AddAsync(Series series, CancellationToken ct = default)
    {
        _db.Series.Add(series);
        await _db.SaveChangesAsync(ct);
        InvalidateCache();
    }

    public async Task UpdateAsync(Series series, CancellationToken ct = default)
    {
        _db.Series.Update(series);
        await _db.SaveChangesAsync(ct);
        InvalidateCache();
    }

    public async Task<List<Episode>> GetAllEpisodesAsync(CancellationToken ct = default)
    {
        return await _db.Episodes
            .Include(e => e.Series)
            .Where(e => !e.Hidden && e.Series != null && !e.Series.Hidden)
            .OrderBy(e => e.Series!.Title)
            .ThenBy(e => e.Season)
            .ThenBy(e => e.EpisodeNumber)
            .AsNoTracking()
            .ToListAsync(ct);
    }

    public async Task<List<Episode>> GetEpisodesAsync(int seriesId, int? season = null, CancellationToken ct = default)
    {
        var query = _db.Episodes
            .Where(e => e.SeriesId == seriesId && !e.Hidden);

        if (season.HasValue)
        {
            query = query.Where(e => e.Season == season.Value);
        }

        return await query
            .OrderBy(e => e.Season)
            .ThenBy(e => e.EpisodeNumber)
            .AsNoTracking()
            .ToListAsync(ct);
    }

    public async Task<HashSet<string>> GetExistingEpisodePathsAsync(CancellationToken ct = default)
    {
        var paths = await _db.Episodes.Select(e => e.FilePath).ToListAsync(ct);
        return paths.ToHashSet();
    }

    public async Task AddEpisodesAsync(IEnumerable<Episode> episodes, CancellationToken ct = default)
    {
        _db.Episodes.AddRange(episodes);
        await _db.SaveChangesAsync(ct);
        InvalidateCache();
    }

    public async Task<bool> SetEpisodeWatchedAsync(int episodeId, bool watched, CancellationToken ct = default)
    {
        var episode = await _db.Episodes.FindAsync(new object[] { episodeId }, ct);
        if (episode == null)
            return false;
        
        episode.Watched = watched;
        await _db.SaveChangesAsync(ct);
        InvalidateCache();
        return true;
    }

    public async Task<bool> DeleteSeriesAsync(int id, CancellationToken ct = default)
    {
        var series = await _db.Series.FirstOrDefaultAsync(s => s.Id == id, ct);
        if (series == null)
            return false;

        _db.Series.Remove(series);
        await _db.SaveChangesAsync(ct);
        InvalidateCache();
        return true;
    }

    public async Task<int> DeleteAllSeriesAsync(CancellationToken ct = default)
    {
        var count = await _db.Series.CountAsync(ct);
        
        // Delete all episodes first (due to FK relationship)
        await EntityFrameworkQueryableExtensions.ExecuteDeleteAsync(_db.Episodes, ct);
        // Then delete all series
        await EntityFrameworkQueryableExtensions.ExecuteDeleteAsync(_db.Series, ct);
        
        InvalidateCache();
        return count;
    }

    public void InvalidateCache()
    {
        // Clear all series-related cache entries
        for (int i = 0; i <= 50; i++)
        {
            _cache.Remove($"{CacheKeyByGenre}:{i}:True");
            _cache.Remove($"{CacheKeyByGenre}:{i}:False");
        }
        _cache.Remove($"{CacheKeyByGenre}::True");
        _cache.Remove($"{CacheKeyByGenre}::False");
    }
}
