using Microsoft.AspNetCore.Mvc;
using HomeApp.Videos.Data;
using HomeApp.Videos.DTOs;
using HomeApp.Videos.Services;
using Microsoft.Extensions.Logging;

namespace HomeApp.Videos.Controllers;

/// <summary>
/// API controller for TV series and episodes
/// </summary>
[ApiController]
[Route("api/v2/series")]
[Produces("application/json")]
public class SeriesController : ControllerBase
{
    private readonly ISeriesRepository _seriesRepository;
    private readonly IMediaSyncService _syncService;
    private readonly ILogger<SeriesController> _logger;

    public SeriesController(
        ISeriesRepository seriesRepository,
        IMediaSyncService syncService,
        ILogger<SeriesController> logger)
    {
        _seriesRepository = seriesRepository;
        _syncService = syncService;
        _logger = logger;
    }

    /// <summary>
    /// Get series grouped by genre
    /// </summary>
    [HttpGet]
    [ResponseCache(Duration = 300, VaryByQueryKeys = new[] { "sample" })]
    public async Task<ActionResult<ApiResponse<Dictionary<string, List<SeriesDto>>>>> GetByGenre(
        [FromQuery] int? sample = null,
        CancellationToken ct = default)
    {
        try
        {
            var seriesDict = await _seriesRepository.GetByGenreAsync(sample, true, ct);
            
            var result = seriesDict.ToDictionary(
                kv => kv.Key,
                kv => kv.Value.Select(s => MapToSeriesDto(s)).ToList()
            );

            return Ok(ApiResponse<Dictionary<string, List<SeriesDto>>>.Ok(result));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting series by genre");
            return StatusCode(500, ApiResponse<Dictionary<string, List<SeriesDto>>>.Fail("Failed to retrieve series"));
        }
    }

    /// <summary>
    /// Get series for a specific genre
    /// </summary>
    [HttpGet("genre/{genre}")]
    [ResponseCache(Duration = 300)]
    public async Task<ActionResult<ApiResponse<List<SeriesDto>>>> GetByGenre(
        string genre,
        CancellationToken ct = default)
    {
        try
        {
            var series = await _seriesRepository.GetByGenreAsync(genre, true, ct);
            var result = series.Select(s => MapToSeriesDto(s)).ToList();
            return Ok(ApiResponse<List<SeriesDto>>.Ok(result));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting series for genre {Genre}", genre);
            return StatusCode(500, ApiResponse<List<SeriesDto>>.Fail("Failed to retrieve series"));
        }
    }

    /// <summary>
    /// Get all series as cards (for browsing)
    /// </summary>
    [HttpGet("cards")]
    [ResponseCache(Duration = 300, VaryByQueryKeys = new[] { "page", "pageSize" })]
    public async Task<ActionResult<ApiResponse<List<SeriesCardDto>>>> GetCards(
        [FromQuery] PaginationRequest? pagination = null,
        CancellationToken ct = default)
    {
        try
        {
            var (series, totalCount) = await _seriesRepository.GetAllAsync(pagination, ct);
            
            var result = series.Select(s => new SeriesCardDto
            {
                Id = s.Id,
                Title = s.Title,
                Description = s.Description,
                Genre = s.Genre ?? "Unknown",
                ThumbnailUrl = s.Thumbnail,
                EpisodeCount = s.Episodes?.Count ?? 0,
                SeasonCount = s.Episodes?.Select(e => e.Season).Distinct().Count() ?? 0
            }).ToList();

            var paginationInfo = pagination != null ? new PaginationInfo
            {
                Page = pagination.Page,
                PageSize = pagination.PageSize,
                TotalItems = totalCount
            } : null;

            return Ok(ApiResponse<List<SeriesCardDto>>.Ok(result, paginationInfo!));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting series cards");
            return StatusCode(500, ApiResponse<List<SeriesCardDto>>.Fail("Failed to retrieve series"));
        }
    }

    /// <summary>
    /// Get a specific series by ID with all episodes
    /// </summary>
    [HttpGet("{id:int}")]
    public async Task<ActionResult<ApiResponse<SeriesDto>>> GetById(int id, CancellationToken ct = default)
    {
        try
        {
            var series = await _seriesRepository.GetByIdAsync(id, true, ct);
            
            if (series == null)
                return NotFound(ApiResponse<SeriesDto>.Fail($"Series with ID {id} not found"));

            var result = MapToSeriesDto(series);
            return Ok(ApiResponse<SeriesDto>.Ok(result));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting series {Id}", id);
            return StatusCode(500, ApiResponse<SeriesDto>.Fail("Failed to retrieve series"));
        }
    }

    /// <summary>
    /// Get episodes for a series (optionally filtered by season)
    /// </summary>
    [HttpGet("{id:int}/episodes")]
    public async Task<ActionResult<ApiResponse<List<EpisodeDto>>>> GetEpisodes(
        int id,
        [FromQuery] int? season = null,
        CancellationToken ct = default)
    {
        try
        {
            var episodes = await _seriesRepository.GetEpisodesAsync(id, season, ct);
            
            var result = episodes.Select(e => new EpisodeDto
            {
                Id = e.Id,
                SeriesId = e.SeriesId,
                Title = e.Title,
                Description = e.Description,
                Season = e.Season,
                EpisodeNumber = e.EpisodeNumber,
                FilePath = e.FilePath,
                VideoUrl = e.VideoUrl,
                ThumbnailUrl = e.Thumbnail,
                Watched = e.Watched
            }).ToList();

            return Ok(ApiResponse<List<EpisodeDto>>.Ok(result));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting episodes for series {Id}", id);
            return StatusCode(500, ApiResponse<List<EpisodeDto>>.Fail("Failed to retrieve episodes"));
        }
    }

    /// <summary>
    /// Get all episodes as a flat list
    /// </summary>
    [HttpGet("episodes/list")]
    [ResponseCache(Duration = 300)]
    public async Task<ActionResult<ApiResponse<List<EpisodeListItemDto>>>> GetAllEpisodesList(CancellationToken ct = default)
    {
        try
        {
            var episodes = await _seriesRepository.GetAllEpisodesAsync(ct);
            
            var result = episodes.Select(e => new EpisodeListItemDto
            {
                Id = e.Id,
                Title = e.Title,
                SeriesTitle = e.Series?.Title ?? "",
                Season = e.Season,
                EpisodeNumber = e.EpisodeNumber,
                FilePath = e.FilePath,
                VideoUrl = e.VideoUrl,
                Watched = e.Watched
            }).ToList();

            return Ok(ApiResponse<List<EpisodeListItemDto>>.Ok(result));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting all episodes list");
            return StatusCode(500, ApiResponse<List<EpisodeListItemDto>>.Fail("Failed to retrieve episodes"));
        }
    }

    /// <summary>
    /// Sync TV shows from filesystem with OMDB
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
                return Conflict(ApiResponse<SyncResultDto>.Fail("A sync operation is already in progress"));
            }

            request ??= new SyncRequestDto { BatchSize = 100 };
            
            _logger.LogInformation("Starting TV sync with batch size {BatchSize}", request.BatchSize);
            var result = await _syncService.SyncTvShowsAsync(request, ct);

            if (result.IsComplete)
            {
                return Ok(ApiResponse<SyncResultDto>.Ok(result, "Sync completed successfully"));
            }
            else
            {
                return Ok(ApiResponse<SyncResultDto>.Ok(result, 
                    "Sync partially completed - API limit reached"));
            }
        }
        catch (OperationCanceledException)
        {
            return Ok(ApiResponse<SyncResultDto>.Fail("Sync was cancelled"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during TV sync");
            return StatusCode(500, ApiResponse<SyncResultDto>.Fail($"Sync failed: {ex.Message}"));
        }
    }

    /// <summary>
    /// Delete a series and all its episodes
    /// </summary>
    [HttpDelete("{id:int}")]
    public async Task<ActionResult<ApiResponse<string>>> Delete(int id, CancellationToken ct = default)
    {
        try
        {
            var deleted = await _seriesRepository.DeleteSeriesAsync(id, ct);
            if (!deleted)
                return NotFound(ApiResponse<string>.Fail($"Series with ID {id} not found"));
            
            return Ok(ApiResponse<string>.Ok($"Series {id} deleted successfully"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting series {Id}", id);
            return StatusCode(500, ApiResponse<string>.Fail($"Failed to delete series: {ex.Message}"));
        }
    }

    /// <summary>
    /// Delete all series and episodes (for re-sync)
    /// </summary>
    [HttpDelete("all")]
    public async Task<ActionResult<ApiResponse<string>>> DeleteAll(CancellationToken ct = default)
    {
        try
        {
            if (_syncService.IsSyncInProgress)
            {
                return Conflict(ApiResponse<string>.Fail("Cannot delete while sync is in progress"));
            }

            var count = await _seriesRepository.DeleteAllSeriesAsync(ct);
            return Ok(ApiResponse<string>.Ok($"Deleted {count} series and their episodes"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting all series");
            return StatusCode(500, ApiResponse<string>.Fail($"Failed to delete series: {ex.Message}"));
        }
    }

    /// <summary>
    /// Set the watched status for an episode
    /// </summary>
    [HttpPatch("episodes/{episodeId:int}/watched")]
    public async Task<ActionResult<ApiResponse<string>>> SetEpisodeWatched(
        int episodeId,
        [FromBody] SetWatchedRequest request,
        CancellationToken ct = default)
    {
        try
        {
            var success = await _seriesRepository.SetEpisodeWatchedAsync(episodeId, request.Watched, ct);
            
            if (!success)
                return NotFound(ApiResponse<string>.Fail($"Episode with ID {episodeId} not found"));

            return Ok(ApiResponse<string>.Ok($"Episode {episodeId} marked as {(request.Watched ? "watched" : "unwatched")}"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting watched status for episode {EpisodeId}", episodeId);
            return StatusCode(500, ApiResponse<string>.Fail("Failed to update watched status"));
        }
    }

    private static SeriesDto MapToSeriesDto(Series series)
    {
        return new SeriesDto
        {
            Id = series.Id,
            Name = series.Name,
            Title = series.Title,
            Description = series.Description,
            Genre = series.Genre ?? "Unknown",
            ThumbnailUrl = series.Thumbnail,
            Episodes = series.Episodes?.Select(e => new EpisodeDto
            {
                Id = e.Id,
                SeriesId = e.SeriesId,
                Title = e.Title,
                Description = e.Description,
                Season = e.Season,
                EpisodeNumber = e.EpisodeNumber,
                FilePath = e.FilePath,
                VideoUrl = e.VideoUrl,
                ThumbnailUrl = e.Thumbnail,
                Watched = e.Watched
            }).ToList() ?? new()
        };
    }
}
