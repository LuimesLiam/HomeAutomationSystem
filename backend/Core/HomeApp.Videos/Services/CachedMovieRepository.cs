using HomeApp.Videos.Data;
using HomeApp.Videos.Services;
using HomeApp.Videos.DTOs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace HomeApp.Videos.Services;

/// <summary>
/// Enhanced movie repository with caching and async operations
/// </summary>
public class CachedMovieRepository : IMovieRepository
{
    private readonly HomeAppVideosDbContext _db;
    private readonly IMemoryCache _cache;
    private readonly ILogger<CachedMovieRepository> _logger;
    
    private const string CacheKeyAllMovies = "movies:all";
    private const string CacheKeyByGenre = "movies:byGenre";
    private const string CacheKeyGenres = "movies:genres";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(15);

    public CachedMovieRepository(HomeAppVideosDbContext db, IMemoryCache cache, ILogger<CachedMovieRepository> logger)
    {
        _db = db;
        _cache = cache;
        _logger = logger;
    }

    public async Task<(List<Movie> Movies, int TotalCount)> GetAllAsync(PaginationRequest? pagination = null, CancellationToken ct = default)
    {
        var query = _db.Movies.Where(m => !m.Hidden).AsNoTracking();
        
        var totalCount = await query.CountAsync(ct);

        if (pagination != null)
        {
            // Apply sorting
            query = pagination.SortBy?.ToLowerInvariant() switch
            {
                "title" => pagination.SortDescending 
                    ? query.OrderByDescending(m => m.Title) 
                    : query.OrderBy(m => m.Title),
                "year" => pagination.SortDescending 
                    ? query.OrderByDescending(m => m.Year) 
                    : query.OrderBy(m => m.Year),
                "genre" => pagination.SortDescending 
                    ? query.OrderByDescending(m => m.Genre) 
                    : query.OrderBy(m => m.Genre),
                _ => query.OrderBy(m => m.Title)
            };

            // Apply search filter
            if (!string.IsNullOrWhiteSpace(pagination.Search))
            {
                var search = pagination.Search.ToLower();
                query = query.Where(m => 
                    m.Title.ToLower().Contains(search) ||
                    m.Name.ToLower().Contains(search) ||
                    (m.Genre != null && m.Genre.ToLower().Contains(search)));
            }

            // Apply pagination
            query = query
                .Skip((pagination.Page - 1) * pagination.PageSize)
                .Take(pagination.PageSize);
        }
        else
        {
            query = query.OrderBy(m => m.Title);
        }

        var movies = await query.ToListAsync(ct);
        return (movies, totalCount);
    }

    public async Task<Dictionary<string, List<Movie>>> GetByGenreAsync(int? sampleSize = null, CancellationToken ct = default)
    {
        var cacheKey = $"{CacheKeyByGenre}:{sampleSize}";
        
        if (_cache.TryGetValue(cacheKey, out Dictionary<string, List<Movie>>? cached) && cached != null)
            return cached;

        var movies = await _db.Movies
            .Where(m => !m.Hidden)
            .AsNoTracking()
            .ToListAsync(ct);

        var result = new Dictionary<string, List<Movie>>();
        
        foreach (var movie in movies)
        {
            var genres = (movie.Genre ?? "Unknown")
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            
            if (genres.Length == 0) 
                genres = new[] { "Unknown" };
            
            foreach (var genre in genres)
            {
                if (!result.ContainsKey(genre))
                    result[genre] = new List<Movie>();
                
                if (!sampleSize.HasValue || result[genre].Count < sampleSize.Value)
                    result[genre].Add(movie);
            }
        }

        // Sort genres alphabetically and movies by title
        var sortedResult = result
            .OrderBy(kv => kv.Key)
            .ToDictionary(
                kv => kv.Key,
                kv => kv.Value.OrderBy(m => m.Title).ToList()
            );

        _cache.Set(cacheKey, sortedResult, CacheDuration);
        return sortedResult;
    }

    public async Task<List<Movie>> GetByGenreAsync(string genre, PaginationRequest? pagination = null, CancellationToken ct = default)
    {
        var movies = await _db.Movies
            .Where(m => !m.Hidden)
            .AsNoTracking()
            .ToListAsync(ct);

        var filtered = movies
            .Where(m => (m.Genre ?? "Unknown")
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Contains(genre, StringComparer.OrdinalIgnoreCase))
            .OrderBy(m => m.Title)
            .ToList();

        if (pagination != null)
        {
            filtered = filtered
                .Skip((pagination.Page - 1) * pagination.PageSize)
                .Take(pagination.PageSize)
                .ToList();
        }

        return filtered;
    }

    public async Task<Movie?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        return await _db.Movies.AsNoTracking().FirstOrDefaultAsync(m => m.Id == id, ct);
    }

    public async Task<List<Movie>> SearchAsync(string query, int maxResults = 50, CancellationToken ct = default)
    {
        var search = query.ToLower();
        return await _db.Movies
            .Where(m => !m.Hidden && (
                m.Title.ToLower().Contains(search) ||
                m.Name.ToLower().Contains(search) ||
                (m.Year != null && m.Year.Contains(search)) ||
                (m.Genre != null && m.Genre.ToLower().Contains(search))
            ))
            .OrderBy(m => m.Title)
            .Take(maxResults)
            .AsNoTracking()
            .ToListAsync(ct);
    }

    public async Task<bool> ExistsAsync(string filePath, CancellationToken ct = default)
    {
        return await _db.Movies.AnyAsync(m => m.FilePath == filePath, ct);
    }

    public async Task<HashSet<string>> GetExistingPathsAsync(CancellationToken ct = default)
    {
        var paths = await _db.Movies.Select(m => m.FilePath).ToListAsync(ct);
        return paths.ToHashSet();
    }

    public async Task AddAsync(Movie movie, CancellationToken ct = default)
    {
        _db.Movies.Add(movie);
        await _db.SaveChangesAsync(ct);
        InvalidateCache();
    }

    public async Task AddRangeAsync(IEnumerable<Movie> movies, CancellationToken ct = default)
    {
        _db.Movies.AddRange(movies);
        await _db.SaveChangesAsync(ct);
        InvalidateCache();
    }

    public async Task UpdateAsync(Movie movie, CancellationToken ct = default)
    {
        _db.Movies.Update(movie);
        await _db.SaveChangesAsync(ct);
        InvalidateCache();
    }

    public async Task<bool> SetWatchedAsync(int id, bool watched, CancellationToken ct = default)
    {
        var movie = await _db.Movies.FindAsync(new object[] { id }, ct);
        if (movie == null)
            return false;
        
        movie.Watched = watched;
        await _db.SaveChangesAsync(ct);
        InvalidateCache();
        return true;
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        var movie = await _db.Movies.FindAsync(new object[] { id }, ct);
        if (movie != null)
        {
            _db.Movies.Remove(movie);
            await _db.SaveChangesAsync(ct);
            InvalidateCache();
        }
    }

    public async Task<List<string>> GetGenresAsync(CancellationToken ct = default)
    {
        if (_cache.TryGetValue(CacheKeyGenres, out List<string>? cached) && cached != null)
            return cached;

        var movies = await _db.Movies
            .Where(m => !m.Hidden)
            .Select(m => m.Genre)
            .ToListAsync(ct);

        var genres = movies
            .Where(g => !string.IsNullOrEmpty(g))
            .SelectMany(g => g!.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Distinct()
            .OrderBy(g => g)
            .ToList();

        _cache.Set(CacheKeyGenres, genres, CacheDuration);
        return genres;
    }

    public void InvalidateCache()
    {
        _cache.Remove(CacheKeyAllMovies);
        _cache.Remove(CacheKeyByGenre);
        _cache.Remove(CacheKeyGenres);
        
        // Also remove sample-specific cache keys
        for (int i = 1; i <= 50; i++)
        {
            _cache.Remove($"{CacheKeyByGenre}:{i}");
        }
        _cache.Remove($"{CacheKeyByGenre}:");
    }
}
