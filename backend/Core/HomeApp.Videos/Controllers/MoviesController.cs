using Microsoft.AspNetCore.Mvc;
using HomeApp.Videos.DTOs;
using HomeApp.Videos.Services;
using Microsoft.Extensions.Logging;

namespace HomeApp.Videos.Controllers;

/// <summary>
/// API controller for movies
/// </summary>
[ApiController]
[Route("api/v2/movies")]
[Produces("application/json")]
public class MoviesController : ControllerBase
{
    private readonly IMovieRepository _movieRepository;
    private readonly IMediaSyncService _syncService;
    private readonly ILogger<MoviesController> _logger;

    public MoviesController(
        IMovieRepository movieRepository,
        IMediaSyncService syncService,
        ILogger<MoviesController> logger)
    {
        _movieRepository = movieRepository;
        _syncService = syncService;
        _logger = logger;
    }

    /// <summary>
    /// Get movies grouped by genre
    /// </summary>
    /// <param name="sample">Optional: limit number of movies per genre</param>
    /// <returns>Dictionary of genre to movie list</returns>
    [HttpGet]
    [ResponseCache(Duration = 300, VaryByQueryKeys = new[] { "sample" })]
    public async Task<ActionResult<ApiResponse<Dictionary<string, List<MovieCardDto>>>>> GetByGenre(
        [FromQuery] int? sample = null,
        CancellationToken ct = default)
    {
        try
        {
            var movies = await _movieRepository.GetByGenreAsync(sample, ct);
            
            var result = movies.ToDictionary(
                kv => kv.Key,
                kv => kv.Value.Select(m => new MovieCardDto
                {
                    Id = m.Id,
                    Title = m.Title,
                    Year = m.Year,
                    Genre = m.Genre ?? "Unknown",
                    ThumbnailUrl = m.Thumbnail,
                    VideoUrl = m.VideoUrl,
                    FilePath = m.FilePath,
                    Watched = m.Watched
                }).ToList()
            );

            return Ok(ApiResponse<Dictionary<string, List<MovieCardDto>>>.Ok(result));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting movies by genre");
            return StatusCode(500, ApiResponse<Dictionary<string, List<MovieCardDto>>>.Fail("Failed to retrieve movies"));
        }
    }

    /// <summary>
    /// Get movies for a specific genre
    /// </summary>
    [HttpGet("genre/{genre}")]
    [ResponseCache(Duration = 300)]
    public async Task<ActionResult<ApiResponse<List<MovieCardDto>>>> GetByGenre(
        string genre,
        [FromQuery] PaginationRequest? pagination = null,
        CancellationToken ct = default)
    {
        try
        {
            var movies = await _movieRepository.GetByGenreAsync(genre, pagination, ct);
            
            var result = movies.Select(m => new MovieCardDto
            {
                Id = m.Id,
                Title = m.Title,
                Year = m.Year,
                Genre = m.Genre ?? "Unknown",
                ThumbnailUrl = m.Thumbnail,
                VideoUrl = m.VideoUrl,
                FilePath = m.FilePath,
                Watched = m.Watched
            }).ToList();

            return Ok(ApiResponse<List<MovieCardDto>>.Ok(result));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting movies for genre {Genre}", genre);
            return StatusCode(500, ApiResponse<List<MovieCardDto>>.Fail("Failed to retrieve movies"));
        }
    }

    /// <summary>
    /// Get all movies as a flat list (for table view)
    /// </summary>
    [HttpGet("list")]
    [ResponseCache(Duration = 300, VaryByQueryKeys = new[] { "page", "pageSize", "sortBy", "sortDescending", "search" })]
    public async Task<ActionResult<ApiResponse<List<MovieListItemDto>>>> GetList(
        [FromQuery] PaginationRequest? pagination = null,
        CancellationToken ct = default)
    {
        try
        {
            var (movies, totalCount) = await _movieRepository.GetAllAsync(pagination, ct);
            
            var result = movies.Select(m => new MovieListItemDto
            {
                Id = m.Id,
                Title = m.Title,
                Year = m.Year,
                Genre = m.Genre ?? "Unknown",
                FilePath = m.FilePath,
                VideoUrl = m.VideoUrl,
                Watched = m.Watched
            }).ToList();

            var paginationInfo = pagination != null ? new PaginationInfo
            {
                Page = pagination.Page,
                PageSize = pagination.PageSize,
                TotalItems = totalCount
            } : null;

            return Ok(ApiResponse<List<MovieListItemDto>>.Ok(result, paginationInfo!));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting movie list");
            return StatusCode(500, ApiResponse<List<MovieListItemDto>>.Fail("Failed to retrieve movies"));
        }
    }

    /// <summary>
    /// Get a specific movie by ID
    /// </summary>
    [HttpGet("{id:int}")]
    public async Task<ActionResult<ApiResponse<MovieDto>>> GetById(int id, CancellationToken ct = default)
    {
        try
        {
            var movie = await _movieRepository.GetByIdAsync(id, ct);
            
            if (movie == null)
                return NotFound(ApiResponse<MovieDto>.Fail($"Movie with ID {id} not found"));

            var result = new MovieDto
            {
                Id = movie.Id,
                Name = movie.Name,
                Title = movie.Title,
                Year = movie.Year,
                Genre = movie.Genre ?? "Unknown",
                Plot = movie.Plot,
                FilePath = movie.FilePath,
                VideoUrl = movie.VideoUrl,
                ThumbnailUrl = movie.Thumbnail,
                Watched = movie.Watched
            };

            return Ok(ApiResponse<MovieDto>.Ok(result));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting movie {Id}", id);
            return StatusCode(500, ApiResponse<MovieDto>.Fail("Failed to retrieve movie"));
        }
    }

    /// <summary>
    /// Search movies by title
    /// </summary>
    [HttpGet("search")]
    public async Task<ActionResult<ApiResponse<List<MovieCardDto>>>> Search(
        [FromQuery] string query,
        [FromQuery] int maxResults = 50,
        CancellationToken ct = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(query))
                return BadRequest(ApiResponse<List<MovieCardDto>>.Fail("Search query is required"));

            var movies = await _movieRepository.SearchAsync(query, maxResults, ct);
            
            var result = movies.Select(m => new MovieCardDto
            {
                Id = m.Id,
                Title = m.Title,
                Year = m.Year,
                Genre = m.Genre ?? "Unknown",
                ThumbnailUrl = m.Thumbnail,
                VideoUrl = m.VideoUrl,
                FilePath = m.FilePath,
                Watched = m.Watched
            }).ToList();

            return Ok(ApiResponse<List<MovieCardDto>>.Ok(result));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching movies for {Query}", query);
            return StatusCode(500, ApiResponse<List<MovieCardDto>>.Fail("Failed to search movies"));
        }
    }

    /// <summary>
    /// Get all available genres
    /// </summary>
    [HttpGet("genres")]
    [ResponseCache(Duration = 600)]
    public async Task<ActionResult<ApiResponse<List<string>>>> GetGenres(CancellationToken ct = default)
    {
        try
        {
            var genres = await _movieRepository.GetGenresAsync(ct);
            return Ok(ApiResponse<List<string>>.Ok(genres));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting genres");
            return StatusCode(500, ApiResponse<List<string>>.Fail("Failed to retrieve genres"));
        }
    }

    /// <summary>
    /// Sync movies from filesystem with OMDB (batched to respect API limits)
    /// </summary>
    [HttpPost("sync")]
    public async Task<ActionResult<ApiResponse<SyncResultDto>>> Sync(
        [FromBody] SyncRequestDto? request = null,
        CancellationToken ct = default)
    {
        try
        {
            if (_syncService.IsSyncInProgress)
            {
                return Conflict(ApiResponse<SyncResultDto>.Fail("A sync operation is already in progress", 
                    new List<string> { "Please wait for the current sync to complete" }));
            }

            request ??= new SyncRequestDto { BatchSize = 100 };
            
            _logger.LogInformation("Starting movie sync with batch size {BatchSize}", request.BatchSize);
            var result = await _syncService.SyncMoviesAsync(request, ct);

            if (result.IsComplete)
            {
                return Ok(ApiResponse<SyncResultDto>.Ok(result, "Sync completed successfully"));
            }
            else
            {
                return Ok(ApiResponse<SyncResultDto>.Ok(result, 
                    "Sync partially completed - API limit reached. Continue later to sync remaining items."));
            }
        }
        catch (OperationCanceledException)
        {
            return Ok(ApiResponse<SyncResultDto>.Fail("Sync was cancelled"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during movie sync");
            return StatusCode(500, ApiResponse<SyncResultDto>.Fail($"Sync failed: {ex.Message}"));
        }
    }

    /// <summary>
    /// Get current sync progress
    /// </summary>
    [HttpGet("sync/progress")]
    public ActionResult<ApiResponse<SyncProgressDto>> GetSyncProgress()
    {
        var progress = _syncService.GetCurrentProgress();
        
        if (progress == null)
        {
            return Ok(ApiResponse<SyncProgressDto>.Ok(new SyncProgressDto { Status = "No sync in progress" }));
        }

        return Ok(ApiResponse<SyncProgressDto>.Ok(progress));
    }

    /// <summary>
    /// Set the watched status for a movie
    /// </summary>
    [HttpPatch("{id:int}/watched")]
    public async Task<ActionResult<ApiResponse<string>>> SetWatched(
        int id,
        [FromBody] SetWatchedRequest request,
        CancellationToken ct = default)
    {
        try
        {
            var success = await _movieRepository.SetWatchedAsync(id, request.Watched, ct);
            
            if (!success)
                return NotFound(ApiResponse<string>.Fail($"Movie with ID {id} not found"));

            return Ok(ApiResponse<string>.Ok($"Movie {id} marked as {(request.Watched ? "watched" : "unwatched")}"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting watched status for movie {Id}", id);
            return StatusCode(500, ApiResponse<string>.Fail("Failed to update watched status"));
        }
    }
}

/// <summary>
/// Request body for setting watched status
/// </summary>
public record SetWatchedRequest
{
    public bool Watched { get; init; }
}
