using HomeApp.Videos.Data;
using HomeApp.Videos.DTOs;

namespace HomeApp.Videos.Services;

/// <summary>
/// Interface for TV series data access
/// </summary>
public interface ISeriesRepository
{
    /// <summary>
    /// Get all series with optional pagination
    /// </summary>
    Task<(List<Series> Series, int TotalCount)> GetAllAsync(PaginationRequest? pagination = null, CancellationToken ct = default);
    
    /// <summary>
    /// Get series grouped by genre
    /// </summary>
    Task<Dictionary<string, List<Series>>> GetByGenreAsync(int? sampleSize = null, bool includeEpisodes = true, CancellationToken ct = default);
    
    /// <summary>
    /// Get series for a specific genre
    /// </summary>
    Task<List<Series>> GetByGenreAsync(string genre, bool includeEpisodes = true, CancellationToken ct = default);
    
    /// <summary>
    /// Get a series by ID with episodes
    /// </summary>
    Task<Series?> GetByIdAsync(int id, bool includeEpisodes = true, CancellationToken ct = default);
    
    /// <summary>
    /// Get a series by name
    /// </summary>
    Task<Series?> GetByNameAsync(string name, CancellationToken ct = default);
    
    /// <summary>
    /// Get or create a series by name
    /// </summary>
    Task<Series> GetOrCreateAsync(string name, CancellationToken ct = default);
    
    /// <summary>
    /// Add a series
    /// </summary>
    Task AddAsync(Series series, CancellationToken ct = default);
    
    /// <summary>
    /// Update a series
    /// </summary>
    Task UpdateAsync(Series series, CancellationToken ct = default);
    
    /// <summary>
    /// Get all episodes
    /// </summary>
    Task<List<Episode>> GetAllEpisodesAsync(CancellationToken ct = default);
    
    /// <summary>
    /// Get episodes for a series
    /// </summary>
    Task<List<Episode>> GetEpisodesAsync(int seriesId, int? season = null, CancellationToken ct = default);
    
    /// <summary>
    /// Get all existing episode file paths
    /// </summary>
    Task<HashSet<string>> GetExistingEpisodePathsAsync(CancellationToken ct = default);
    
    /// <summary>
    /// Add episodes
    /// </summary>
    Task AddEpisodesAsync(IEnumerable<Episode> episodes, CancellationToken ct = default);
    
    /// <summary>
    /// Set the watched status for an episode
    /// </summary>
    Task<bool> SetEpisodeWatchedAsync(int episodeId, bool watched, CancellationToken ct = default);
    
    /// <summary>
    /// Delete a series and all its episodes
    /// </summary>
    Task<bool> DeleteSeriesAsync(int id, CancellationToken ct = default);
    
    /// <summary>
    /// Delete all series and episodes
    /// </summary>
    Task<int> DeleteAllSeriesAsync(CancellationToken ct = default);
    
    /// <summary>
    /// Invalidate cache
    /// </summary>
    void InvalidateCache();
}
