using HomeApp.Videos.Data;
using HomeApp.Videos.DTOs;

namespace HomeApp.Videos.Services;

/// <summary>
/// Interface for movie data access with caching support
/// </summary>
public interface IMovieRepository
{
    /// <summary>
    /// Get all movies with optional pagination
    /// </summary>
    Task<(List<Movie> Movies, int TotalCount)> GetAllAsync(PaginationRequest? pagination = null, CancellationToken ct = default);
    
    /// <summary>
    /// Get movies grouped by genre
    /// </summary>
    Task<Dictionary<string, List<Movie>>> GetByGenreAsync(int? sampleSize = null, CancellationToken ct = default);
    
    /// <summary>
    /// Get movies for a specific genre
    /// </summary>
    Task<List<Movie>> GetByGenreAsync(string genre, PaginationRequest? pagination = null, CancellationToken ct = default);
    
    /// <summary>
    /// Get a movie by ID
    /// </summary>
    Task<Movie?> GetByIdAsync(int id, CancellationToken ct = default);
    
    /// <summary>
    /// Search movies by title
    /// </summary>
    Task<List<Movie>> SearchAsync(string query, int maxResults = 50, CancellationToken ct = default);
    
    /// <summary>
    /// Check if a movie exists by file path
    /// </summary>
    Task<bool> ExistsAsync(string filePath, CancellationToken ct = default);
    
    /// <summary>
    /// Get all existing file paths (for sync optimization)
    /// </summary>
    Task<HashSet<string>> GetExistingPathsAsync(CancellationToken ct = default);
    
    /// <summary>
    /// Add a movie
    /// </summary>
    Task AddAsync(Movie movie, CancellationToken ct = default);
    
    /// <summary>
    /// Add multiple movies
    /// </summary>
    Task AddRangeAsync(IEnumerable<Movie> movies, CancellationToken ct = default);
    
    /// <summary>
    /// Update a movie
    /// </summary>
    Task UpdateAsync(Movie movie, CancellationToken ct = default);
    
    /// <summary>
    /// Set the watched status for a movie
    /// </summary>
    Task<bool> SetWatchedAsync(int id, bool watched, CancellationToken ct = default);
    
    /// <summary>
    /// Delete a movie
    /// </summary>
    Task DeleteAsync(int id, CancellationToken ct = default);
    
    /// <summary>
    /// Get all unique genres
    /// </summary>
    Task<List<string>> GetGenresAsync(CancellationToken ct = default);
    
    /// <summary>
    /// Invalidate cache
    /// </summary>
    void InvalidateCache();
}
