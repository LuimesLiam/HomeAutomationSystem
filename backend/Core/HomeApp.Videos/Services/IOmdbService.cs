using HomeApp.Videos.DTOs;

namespace HomeApp.Videos.Services;

/// <summary>
/// Interface for OMDB API integration
/// </summary>
public interface IOmdbService
{
    /// <summary>
    /// Search for movie information by title
    /// </summary>
    Task<OmdbMovieResult?> SearchMovieAsync(string title, string? year = null, CancellationToken ct = default);
    
    /// <summary>
    /// Search for series information by title
    /// </summary>
    Task<OmdbSeriesResult?> SearchSeriesAsync(string title, CancellationToken ct = default);
    
    /// <summary>
    /// Get episode information for a series
    /// </summary>
    Task<OmdbEpisodeResult?> GetEpisodeAsync(string seriesTitle, int season, int episode, CancellationToken ct = default);
    
    /// <summary>
    /// Get the number of API calls remaining for the day
    /// </summary>
    int GetRemainingApiCalls();
    
    /// <summary>
    /// Check if API calls are available
    /// </summary>
    bool HasApiCallsAvailable();
}

/// <summary>
/// Result from OMDB movie search
/// </summary>
public record OmdbMovieResult
{
    public string Title { get; init; } = string.Empty;
    public string Year { get; init; } = string.Empty;
    public string Genre { get; init; } = string.Empty;
    public string Plot { get; init; } = string.Empty;
    public string? PosterUrl { get; init; }
    public string? ImdbId { get; init; }
    public string? Rating { get; init; }
    public bool Found { get; init; }
}

/// <summary>
/// Result from OMDB series search
/// </summary>
public record OmdbSeriesResult
{
    public string Title { get; init; } = string.Empty;
    public string Year { get; init; } = string.Empty;
    public string Genre { get; init; } = string.Empty;
    public string Plot { get; init; } = string.Empty;
    public string? PosterUrl { get; init; }
    public string? ImdbId { get; init; }
    public int? TotalSeasons { get; init; }
    public bool Found { get; init; }
}

/// <summary>
/// Result from OMDB episode search
/// </summary>
public record OmdbEpisodeResult
{
    public string Title { get; init; } = string.Empty;
    public string Plot { get; init; } = string.Empty;
    public string? PosterUrl { get; init; }
    public string? ImdbRating { get; init; }
    public int Season { get; init; }
    public int Episode { get; init; }
    public bool Found { get; init; }
}
