using System.Diagnostics;
using System.Text.RegularExpressions;
using HomeApp.Videos.Data;
using HomeApp.Videos.Services;
using HomeApp.Videos.DTOs;
using HomeApp.Videos.Logic;
using Microsoft.Extensions.Logging;

namespace HomeApp.Videos.Services;

public class MediaSyncService : IMediaSyncService
{
    private readonly VideoManager _videoManager;
    private readonly HomeAppVideosDbContext _dbContext;
    private readonly IOmdbService _omdbService;
    private readonly IImageService _imageService;
    private readonly ILogger<MediaSyncService> _logger;

    private SyncProgressDto? _currentProgress;
    private readonly SemaphoreSlim _syncLock = new(1, 1);

    public bool IsSyncInProgress => _currentProgress != null;

    public MediaSyncService(
        VideoManager videoManager,
        HomeAppVideosDbContext dbContext,
        IOmdbService omdbService,
        IImageService imageService,
        ILogger<MediaSyncService> logger)
    {
        _videoManager = videoManager;
        _dbContext = dbContext;
        _omdbService = omdbService;
        _imageService = imageService;
        _logger = logger;
    }

    public SyncProgressDto? GetCurrentProgress() => _currentProgress;

    public async Task<SyncResultDto> SyncMoviesAsync(SyncRequestDto request, CancellationToken ct = default)
    {
        if (!await _syncLock.WaitAsync(TimeSpan.Zero, ct))
        {
            return new SyncResultDto
            {
                IsComplete = false,
                Errors = new List<string> { "A sync operation is already in progress" }
            };
        }

        var stopwatch = Stopwatch.StartNew();
        var errors = new List<string>();
        var added = 0;
        var skipped = 0;
        var failed = 0;
        var apiCallsUsed = 0;

        try
        {
            var existingPaths = _dbContext.Movies.Select(m => m.FilePath).ToHashSet();
            var allVideos = _videoManager.ListAllMovies();
            var videosToProcess = allVideos
                .Where(v => !string.IsNullOrEmpty(v.MountPath ?? v.Path) &&
                           !existingPaths.Contains(v.MountPath ?? v.Path))
                .ToList();

            var totalToProcess = videosToProcess.Count;
            var batchSize = Math.Min(request.BatchSize, 100);
            var totalBatches = (int)Math.Ceiling((double)totalToProcess / batchSize);

            _logger.LogInformation("Starting movie sync: {Total} new movies to process in {Batches} batches",
                totalToProcess, totalBatches);

            for (var batchIndex = 0; batchIndex < totalBatches && !ct.IsCancellationRequested; batchIndex++)
            {
                var batch = videosToProcess
                    .Skip(batchIndex * batchSize)
                    .Take(batchSize)
                    .ToList();

                _currentProgress = new SyncProgressDto
                {
                    Processed = batchIndex * batchSize,
                    Total = totalToProcess,
                    CurrentBatch = batchIndex + 1,
                    TotalBatches = totalBatches,
                    Status = $"Processing batch {batchIndex + 1} of {totalBatches}"
                };

                var moviesToAdd = new List<Movie>();

                foreach (var video in batch)
                {
                    try
                    {
                        if (!_omdbService.HasApiCallsAvailable())
                        {
                            _logger.LogWarning("API limit reached, saving progress and stopping");
                            break;
                        }

                        var movie = await CreateMovieFromVideoAsync(video, ct);
                        if (movie != null)
                        {
                            moviesToAdd.Add(movie);
                            added++;
                            apiCallsUsed++;
                        }
                        else
                        {
                            skipped++;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error processing video: {Name}", video.Name);
                        errors.Add($"Failed to process {video.Name}: {ex.Message}");
                        failed++;
                    }
                }

                if (moviesToAdd.Any())
                {
                    _dbContext.Movies.AddRange(moviesToAdd);
                    await _dbContext.SaveChangesAsync(ct);
                    _logger.LogInformation("Saved batch {Batch}/{Total}: {Count} movies",
                        batchIndex + 1, totalBatches, moviesToAdd.Count);
                }

                if (!_omdbService.HasApiCallsAvailable())
                {
                    return new SyncResultDto
                    {
                        TotalFound = totalToProcess,
                        Added = added,
                        Skipped = skipped,
                        Failed = failed,
                        ApiCallsUsed = apiCallsUsed,
                        ApiCallsRemaining = 0,
                        Errors = errors,
                        Duration = stopwatch.Elapsed,
                        IsComplete = false,
                        ContinuationToken = $"batch:{batchIndex + 1}"
                    };
                }
            }

            stopwatch.Stop();
            return new SyncResultDto
            {
                TotalFound = totalToProcess,
                Added = added,
                Skipped = skipped,
                Failed = failed,
                ApiCallsUsed = apiCallsUsed,
                ApiCallsRemaining = _omdbService.GetRemainingApiCalls(),
                Errors = errors,
                Duration = stopwatch.Elapsed,
                IsComplete = true
            };
        }
        finally
        {
            _currentProgress = null;
            _syncLock.Release();
        }
    }

    public async Task<SyncResultDto> SyncTvShowsAsync(SyncRequestDto request, CancellationToken ct = default)
    {
        if (!await _syncLock.WaitAsync(TimeSpan.Zero, ct))
        {
            return new SyncResultDto
            {
                IsComplete = false,
                Errors = new List<string> { "A sync operation is already in progress" }
            };
        }

        var stopwatch = Stopwatch.StartNew();
        var errors = new List<string>();
        var added = 0;
        var skipped = 0;
        var failed = 0;
        var apiCallsUsed = 0;

        try
        {
            var existingPaths = _dbContext.Episodes.Select(e => e.FilePath).ToHashSet();
            var allEpisodes = _videoManager.ListAllEpisodes();
            var episodesToProcess = allEpisodes
                .Where(e => !string.IsNullOrEmpty(e.MountPath ?? e.Path) &&
                           !existingPaths.Contains(e.MountPath ?? e.Path))
                .ToList();

            var totalToProcess = episodesToProcess.Count;
            var batchSize = Math.Min(request.BatchSize, 100);
            var totalBatches = (int)Math.Ceiling((double)totalToProcess / batchSize);
            var seriesCache = new Dictionary<string, Series>();
            var seriesOmdbCache = new Dictionary<string, OmdbSeriesResult?>();

            _logger.LogInformation("Starting TV sync: {Total} new episodes to process in {Batches} batches",
                totalToProcess, totalBatches);

            for (var batchIndex = 0; batchIndex < totalBatches && !ct.IsCancellationRequested; batchIndex++)
            {
                var batch = episodesToProcess
                    .Skip(batchIndex * batchSize)
                    .Take(batchSize)
                    .ToList();

                _currentProgress = new SyncProgressDto
                {
                    Processed = batchIndex * batchSize,
                    Total = totalToProcess,
                    CurrentBatch = batchIndex + 1,
                    TotalBatches = totalBatches,
                    Status = $"Processing batch {batchIndex + 1} of {totalBatches}"
                };

                var episodesToAdd = new List<Episode>();

                foreach (var video in batch)
                {
                    try
                    {
                        if (!_omdbService.HasApiCallsAvailable())
                        {
                            _logger.LogWarning("API limit reached, saving progress");
                            break;
                        }

                        if (!seriesCache.TryGetValue(video.Series, out var series))
                        {
                            series = _dbContext.Series.FirstOrDefault(s => s.Name == video.Series);
                            if (series == null)
                            {
                                if (!seriesOmdbCache.TryGetValue(video.Series, out var omdbSeries))
                                {
                                    omdbSeries = await _omdbService.SearchSeriesAsync(video.Series, ct);
                                    seriesOmdbCache[video.Series] = omdbSeries;
                                    apiCallsUsed++;
                                }

                                string? thumbnail = null;
                                if (omdbSeries?.PosterUrl != null)
                                {
                                    thumbnail = await _imageService.DownloadAndOptimizeAsync(omdbSeries.PosterUrl, 200, ct);
                                }

                                series = new Series
                                {
                                    Name = video.Series,
                                    Title = omdbSeries?.Title ?? video.Series,
                                    Description = omdbSeries?.Plot,
                                    Genre = omdbSeries?.Genre ?? "Unknown",
                                    Thumbnail = thumbnail,
                                    Hidden = false
                                };
                                _dbContext.Series.Add(series);
                                await _dbContext.SaveChangesAsync(ct);
                            }

                            seriesCache[video.Series] = series;
                        }

                        var episode = await CreateEpisodeFromVideoAsync(video, series, ct);
                        if (episode != null)
                        {
                            episodesToAdd.Add(episode);
                            added++;
                            apiCallsUsed++;
                        }
                        else
                        {
                            skipped++;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error processing episode: {Name}", video.Name);
                        errors.Add($"Failed to process {video.Name}: {ex.Message}");
                        failed++;
                    }
                }

                if (episodesToAdd.Any())
                {
                    _dbContext.Episodes.AddRange(episodesToAdd);
                    await _dbContext.SaveChangesAsync(ct);
                    _logger.LogInformation("Saved batch {Batch}/{Total}: {Count} episodes",
                        batchIndex + 1, totalBatches, episodesToAdd.Count);
                }

                if (!_omdbService.HasApiCallsAvailable())
                {
                    break;
                }
            }

            stopwatch.Stop();
            return new SyncResultDto
            {
                TotalFound = totalToProcess,
                Added = added,
                Skipped = skipped,
                Failed = failed,
                ApiCallsUsed = apiCallsUsed,
                ApiCallsRemaining = _omdbService.GetRemainingApiCalls(),
                Errors = errors,
                Duration = stopwatch.Elapsed,
                IsComplete = _omdbService.HasApiCallsAvailable()
            };
        }
        finally
        {
            _currentProgress = null;
            _syncLock.Release();
        }
    }

    private async Task<Movie?> CreateMovieFromVideoAsync(VideoItem video, CancellationToken ct)
    {
        var filePath = video.MountPath ?? video.Path;
        if (string.IsNullOrEmpty(filePath))
        {
            return null;
        }

        var (parsedTitle, parsedYear) = ParseTitleAndYear(video.Name);
        var omdbResult = await _omdbService.SearchMovieAsync(parsedTitle, parsedYear, ct);

        string? thumbnail = null;
        if (omdbResult?.PosterUrl != null)
        {
            thumbnail = await _imageService.DownloadAndOptimizeAsync(omdbResult.PosterUrl, 200, ct);
        }
        else if (!string.IsNullOrEmpty(video.Thumbnail))
        {
            thumbnail = video.Thumbnail;
        }

        return new Movie
        {
            Name = video.Name,
            FilePath = filePath,
            VideoUrl = _videoManager.BuildMediaUrl(filePath),
            Thumbnail = thumbnail ?? string.Empty,
            Title = omdbResult?.Title ?? parsedTitle,
            Year = omdbResult?.Year ?? parsedYear,
            Genre = omdbResult?.Genre ?? "Unknown",
            Plot = omdbResult?.Plot ?? "No plot available.",
            Hidden = false
        };
    }

    private async Task<Episode?> CreateEpisodeFromVideoAsync(VideoManager.TvEpisodeItem video, Series series, CancellationToken ct)
    {
        var filePath = video.MountPath ?? video.Path;
        if (string.IsNullOrEmpty(filePath))
        {
            return null;
        }

        var omdbResult = await _omdbService.GetEpisodeAsync(video.Series, video.Season, video.Episode, ct);

        string? thumbnail = null;
        if (omdbResult?.PosterUrl != null)
        {
            thumbnail = await _imageService.DownloadAndOptimizeAsync(omdbResult.PosterUrl, 200, ct);
        }
        else if (!string.IsNullOrEmpty(video.Thumbnail))
        {
            thumbnail = video.Thumbnail;
        }
        else if (!string.IsNullOrEmpty(series.Thumbnail))
        {
            thumbnail = series.Thumbnail;
        }

        return new Episode
        {
            SeriesId = series.Id,
            Title = omdbResult?.Title ?? video.Name,
            Description = omdbResult?.Plot,
            Season = video.Season,
            EpisodeNumber = video.Episode,
            FilePath = filePath,
            VideoUrl = _videoManager.BuildMediaUrl(filePath),
            Thumbnail = thumbnail,
            Hidden = false
        };
    }

    private static (string title, string year) ParseTitleAndYear(string filename)
    {
        var name = Path.GetFileNameWithoutExtension(filename);
        name = name.Replace('.', ' ').Replace('_', ' ').Replace('-', ' ');

        var yearMatch = Regex.Match(name, @"(?:19|20)\d{2}");
        var year = yearMatch.Success ? yearMatch.Value : string.Empty;
        var title = yearMatch.Success ? name[..yearMatch.Index].Trim() : name.Trim();
        title = Regex.Replace(title, @"\s+", " ");

        return (title, year);
    }
}
