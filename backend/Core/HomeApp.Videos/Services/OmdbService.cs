using System.Text.Json;
using HomeApp.Videos.Configuration;
using Microsoft.Extensions.Logging;

namespace HomeApp.Videos.Services;

/// <summary>
/// Service for integrating with the OMDB API with rate limiting and caching
/// </summary>
public class OmdbService : IOmdbService
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly ILogger<OmdbService> _logger;
    private readonly int _dailyLimit;
    private int _callsToday;
    private DateTime _lastResetDate;
    private readonly SemaphoreSlim _rateLimiter = new(1, 1);
    private readonly Dictionary<string, (object Result, DateTime CachedAt)> _cache = new();
    private readonly TimeSpan _cacheDuration = TimeSpan.FromHours(24);

    public OmdbService(HttpClient httpClient, VideoModuleOptions options, ILogger<OmdbService> logger)
    {
        _httpClient = httpClient;
        _apiKey = options.OmdbApiKey;
        _dailyLimit = options.OmdbDailyLimit;
        _logger = logger;
        _lastResetDate = DateTime.UtcNow.Date;
    }

    public int GetRemainingApiCalls()
    {
        ResetDailyCounterIfNeeded();
        return Math.Max(0, _dailyLimit - _callsToday);
    }

    public bool HasApiCallsAvailable()
    {
        return GetRemainingApiCalls() > 0;
    }

    public async Task<OmdbMovieResult?> SearchMovieAsync(string title, string? year = null, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(_apiKey))
        {
            _logger.LogWarning("OMDB API key not configured");
            return null;
        }

        var cacheKey = $"movie:{title}:{year}";
        if (TryGetCached<OmdbMovieResult>(cacheKey, out var cached))
            return cached;

        if (!HasApiCallsAvailable())
        {
            _logger.LogWarning("OMDB daily API limit reached");
            return null;
        }

        await _rateLimiter.WaitAsync(ct);
        try
        {
            var url = $"http://www.omdbapi.com/?apikey={_apiKey}&t={Uri.EscapeDataString(title)}&type=movie";
            if (!string.IsNullOrEmpty(year))
                url += $"&y={Uri.EscapeDataString(year)}";

            var response = await _httpClient.GetStringAsync(url, ct);
            IncrementCallCount();

            var doc = JsonDocument.Parse(response);
            var root = doc.RootElement;

            if (root.TryGetProperty("Response", out var resp) && resp.GetString() == "True")
            {
                var result = new OmdbMovieResult
                {
                    Found = true,
                    Title = GetStringProperty(root, "Title") ?? title,
                    Year = GetStringProperty(root, "Year") ?? "",
                    Genre = GetStringProperty(root, "Genre") ?? "",
                    Plot = GetStringProperty(root, "Plot") ?? "",
                    PosterUrl = GetPosterUrl(root),
                    ImdbId = GetStringProperty(root, "imdbID"),
                    Rating = GetStringProperty(root, "imdbRating")
                };

                CacheResult(cacheKey, result);
                return result;
            }

            return new OmdbMovieResult { Found = false, Title = title };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching OMDB for movie: {Title}", title);
            return null;
        }
        finally
        {
            _rateLimiter.Release();
        }
    }

    public async Task<OmdbSeriesResult?> SearchSeriesAsync(string title, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(_apiKey))
            return null;

        var cacheKey = $"series:{title}";
        if (TryGetCached<OmdbSeriesResult>(cacheKey, out var cached))
            return cached;

        if (!HasApiCallsAvailable())
        {
            _logger.LogWarning("OMDB daily API limit reached");
            return null;
        }

        await _rateLimiter.WaitAsync(ct);
        try
        {
            var url = $"http://www.omdbapi.com/?apikey={_apiKey}&t={Uri.EscapeDataString(title)}&type=series";
            var response = await _httpClient.GetStringAsync(url, ct);
            IncrementCallCount();

            var doc = JsonDocument.Parse(response);
            var root = doc.RootElement;

            if (root.TryGetProperty("Response", out var resp) && resp.GetString() == "True")
            {
                var result = new OmdbSeriesResult
                {
                    Found = true,
                    Title = GetStringProperty(root, "Title") ?? title,
                    Year = GetStringProperty(root, "Year") ?? "",
                    Genre = GetStringProperty(root, "Genre") ?? "",
                    Plot = GetStringProperty(root, "Plot") ?? "",
                    PosterUrl = GetPosterUrl(root),
                    ImdbId = GetStringProperty(root, "imdbID"),
                    TotalSeasons = int.TryParse(GetStringProperty(root, "totalSeasons"), out var s) ? s : null
                };

                CacheResult(cacheKey, result);
                return result;
            }

            return new OmdbSeriesResult { Found = false, Title = title };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching OMDB for series: {Title}", title);
            return null;
        }
        finally
        {
            _rateLimiter.Release();
        }
    }

    public async Task<OmdbEpisodeResult?> GetEpisodeAsync(string seriesTitle, int season, int episode, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(_apiKey))
            return null;

        var cacheKey = $"episode:{seriesTitle}:S{season}E{episode}";
        if (TryGetCached<OmdbEpisodeResult>(cacheKey, out var cached))
            return cached;

        if (!HasApiCallsAvailable())
        {
            _logger.LogWarning("OMDB daily API limit reached");
            return null;
        }

        await _rateLimiter.WaitAsync(ct);
        try
        {
            var url = $"http://www.omdbapi.com/?apikey={_apiKey}&t={Uri.EscapeDataString(seriesTitle)}&Season={season}&Episode={episode}";
            var response = await _httpClient.GetStringAsync(url, ct);
            IncrementCallCount();

            var doc = JsonDocument.Parse(response);
            var root = doc.RootElement;

            if (root.TryGetProperty("Response", out var resp) && resp.GetString() == "True")
            {
                var result = new OmdbEpisodeResult
                {
                    Found = true,
                    Title = GetStringProperty(root, "Title") ?? $"Episode {episode}",
                    Plot = GetStringProperty(root, "Plot") ?? "",
                    PosterUrl = GetPosterUrl(root),
                    ImdbRating = GetStringProperty(root, "imdbRating"),
                    Season = season,
                    Episode = episode
                };

                CacheResult(cacheKey, result);
                return result;
            }

            return new OmdbEpisodeResult { Found = false, Season = season, Episode = episode };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching OMDB episode: {Series} S{Season}E{Episode}", seriesTitle, season, episode);
            return null;
        }
        finally
        {
            _rateLimiter.Release();
        }
    }

    private void ResetDailyCounterIfNeeded()
    {
        var today = DateTime.UtcNow.Date;
        if (_lastResetDate < today)
        {
            _callsToday = 0;
            _lastResetDate = today;
        }
    }

    private void IncrementCallCount()
    {
        ResetDailyCounterIfNeeded();
        _callsToday++;
    }

    private static string? GetStringProperty(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var prop) ? prop.GetString() : null;
    }

    private static string? GetPosterUrl(JsonElement root)
    {
        var poster = GetStringProperty(root, "Poster");
        return !string.IsNullOrEmpty(poster) && poster != "N/A" ? poster : null;
    }

    private bool TryGetCached<T>(string key, out T? result) where T : class
    {
        if (_cache.TryGetValue(key, out var entry) && DateTime.UtcNow - entry.CachedAt < _cacheDuration)
        {
            result = entry.Result as T;
            return result != null;
        }
        result = null;
        return false;
    }

    private void CacheResult(string key, object result)
    {
        _cache[key] = (result, DateTime.UtcNow);
        
        // Clean old cache entries periodically
        if (_cache.Count > 1000)
        {
            var keysToRemove = _cache
                .Where(kv => DateTime.UtcNow - kv.Value.CachedAt > _cacheDuration)
                .Select(kv => kv.Key)
                .ToList();
            
            foreach (var k in keysToRemove)
                _cache.Remove(k);
        }
    }
}
