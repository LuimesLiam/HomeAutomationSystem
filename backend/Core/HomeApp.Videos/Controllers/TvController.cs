using HomeApp.Library.Imaging;
using ImageMagick;
using Microsoft.AspNetCore.Mvc;
using HomeApp.Videos.Logic;
using HomeApp.Videos.Data;
using HomeApp.Videos.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.IO;
using HomeApp.Videos.DTOs;

namespace HomeApp.Videos.Controllers;

[ApiController]
[Route("api/tv")]
public class TvController : ControllerBase
{
    private readonly VideoManager _videoManager;
    private readonly TvRepository _tvRepository;
    private readonly string _omdbApiKey;
    private static readonly HttpClient _httpClient = new();

    public TvController(VideoManager videoManager, TvRepository tvRepository, VideoModuleOptions options)
    {
        _videoManager = videoManager;
        _tvRepository = tvRepository;
        _omdbApiKey = options.OmdbApiKey;
    }

    [HttpGet]
    public IActionResult Get([FromQuery] string? genre = null, [FromQuery] int? sample = null)
    {
        if (!string.IsNullOrEmpty(genre))
        {
            var seriesList = _tvRepository.GetSeriesByGenre(genre);
            if (sample.HasValue)
                seriesList = seriesList.Take(sample.Value).ToList();
            var list = seriesList.Select(MapSeriesSummary).ToList();
            return Ok(list);
        }

        var grouped = _tvRepository.GetSeriesByGenre();
        var dict = grouped.ToDictionary(
            kv => kv.Key,
            kv => kv.Value.Select(MapSeriesSummary).ToList()
        );

        if (sample.HasValue)
        {
            var limited = new Dictionary<string, List<object>>();
            foreach (var kv in dict)
            {
                limited[kv.Key] = kv.Value.Take(sample.Value).ToList();
            }
            return Ok(limited);
        }

        return Ok(dict);
    }

    [HttpGet("{seriesId:int}/episodes")]
    public IActionResult GetEpisodes(int seriesId)
    {
        var series = _tvRepository.GetSeriesById(seriesId);
        if (series == null)
        {
            return NotFound();
        }

        var episodes = _tvRepository.GetEpisodesForSeries(seriesId)
            .Select(MapEpisode)
            .ToList();
        return Ok(episodes);
    }

    [HttpGet("list")]
    public IActionResult List()
    {
        var series = _tvRepository.GetAllSeries()
            .Select(MapSeriesListItem)
            .ToList();

        return Ok(series);
    }

    [HttpGet("search")]
    public IActionResult Search([FromQuery] string query, [FromQuery] int maxResults = 100)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return BadRequest(new { error = "Missing query" });
        }

        maxResults = Math.Clamp(maxResults, 1, 200);

        var matches = _tvRepository.SearchSeries(query, maxResults);
        var grouped = new Dictionary<string, List<object>>();

        foreach (var series in matches)
        {
            var genres = (series.Genre ?? "Unknown")
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            if (genres.Length == 0)
            {
                genres = new[] { "Unknown" };
            }

            foreach (var genre in genres)
            {
                if (!grouped.ContainsKey(genre))
                {
                    grouped[genre] = new List<object>();
                }

                grouped[genre].Add(MapSeriesSummary(series));
            }
        }

        return Ok(grouped);
    }

    private object MapSeriesSummary(Series series)
    {
        return new
        {
            series.Id,
            series.Name,
            series.Title,
            series.Description,
            series.Genre,
            series.Thumbnail,
            ThumbnailUrl = series.Thumbnail,
            series.Hidden
        };
    }

    private object MapSeriesListItem(Series series)
    {
        var visibleEpisodes = (series.Episodes ?? [])
            .Where(e => !e.Hidden)
            .ToList();

        return new
        {
            series.Id,
            series.Name,
            series.Title,
            series.Description,
            series.Genre,
            series.Thumbnail,
            ThumbnailUrl = series.Thumbnail,
            SeasonCount = visibleEpisodes
                .Select(e => e.Season)
                .Distinct()
                .Count(),
            EpisodeCount = visibleEpisodes.Count,
            WatchedEpisodeCount = visibleEpisodes.Count(e => e.Watched),
            FullyWatched = visibleEpisodes.Count > 0 && visibleEpisodes.All(e => e.Watched)
        };
    }

    private object MapEpisode(Episode episode)
    {
        return new
        {
            episode.Id,
            episode.Title,
            episode.Description,
            episode.Season,
            episode.EpisodeNumber,
            episode.FilePath,
            VideoUrl = BuildMediaUrl(episode.FilePath, episode.VideoUrl),
            episode.Thumbnail,
            ThumbnailUrl = episode.Thumbnail,
            episode.Hidden,
            episode.Watched
        };
    }

    [HttpPatch("series/{id:int}")]
    public IActionResult UpdateSeries(int id, [FromBody] UpdateSeriesRequest request)
    {
        var series = _tvRepository.GetSeriesById(id);
        if (series == null)
        {
            return NotFound();
        }

        if (request.Title != null)
        {
            series.Title = request.Title;
        }

        if (request.Description != null)
        {
            series.Description = request.Description;
        }

        if (request.Genre != null)
        {
            series.Genre = request.Genre;
        }

        if (request.Thumbnail != null)
        {
            series.Thumbnail = request.Thumbnail;
        }

        _tvRepository.UpdateSeries(series);
        return Ok(MapSeriesSummary(series));
    }

    [HttpPost("series/{id:int}/resync")]
    public async Task<IActionResult> ResyncSeries(int id)
    {
        if (string.IsNullOrEmpty(_omdbApiKey))
        {
            return StatusCode(500, new { error = "OMDB_API_KEY is not configured" });
        }

        var series = _tvRepository.GetSeriesById(id);
        if (series == null)
        {
            return NotFound();
        }

        var queryTitle = string.IsNullOrWhiteSpace(series.Title) ? series.Name : series.Title;
        var omdb = await TryFetchOmdbSeries(queryTitle);
        if (omdb == null)
        {
            return NotFound(new { error = "OMDB lookup failed" });
        }

        series.Title = string.IsNullOrWhiteSpace(omdb.Value.Title) ? series.Title : omdb.Value.Title!;
        series.Description = string.IsNullOrWhiteSpace(omdb.Value.Description) ? series.Description : omdb.Value.Description;
        series.Genre = string.IsNullOrWhiteSpace(omdb.Value.Genre) ? series.Genre : omdb.Value.Genre;

        if (!string.IsNullOrWhiteSpace(omdb.Value.Thumbnail))
        {
            series.Thumbnail = omdb.Value.Thumbnail;
        }

        _tvRepository.UpdateSeries(series);
        return Ok(MapSeriesSummary(series));
    }

    [HttpPost("series/{id:int}/season/{season:int}/resync")]
    public async Task<IActionResult> ResyncSeason(int id, int season)
    {
        if (string.IsNullOrEmpty(_omdbApiKey))
        {
            return StatusCode(500, new { error = "OMDB_API_KEY is not configured" });
        }

        if (season <= 0)
        {
            return BadRequest(new { error = "Season must be greater than 0" });
        }

        var series = _tvRepository.GetSeriesById(id);
        if (series == null)
        {
            return NotFound();
        }

        var episodes = _tvRepository.GetEpisodesForSeriesSeason(id, season);
        if (episodes.Count == 0)
        {
            return NotFound(new { error = "No episodes found for season" });
        }

        var queryTitle = string.IsNullOrWhiteSpace(series.Title) ? series.Name : series.Title;
        var updated = 0;

        foreach (var episode in episodes)
        {
            var omdb = await TryFetchOmdbEpisode(queryTitle, season, episode.EpisodeNumber);
            if (omdb == null)
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(omdb.Value.Title))
            {
                episode.Title = omdb.Value.Title!;
            }

            if (!string.IsNullOrWhiteSpace(omdb.Value.Description))
            {
                episode.Description = omdb.Value.Description;
            }

            if (!string.IsNullOrWhiteSpace(omdb.Value.Thumbnail))
            {
                episode.Thumbnail = omdb.Value.Thumbnail;
            }
            else if (string.IsNullOrWhiteSpace(episode.Thumbnail) && !string.IsNullOrWhiteSpace(series.Thumbnail))
            {
                episode.Thumbnail = series.Thumbnail;
            }

            updated++;
        }

        if (updated > 0)
        {
            _tvRepository.UpdateEpisodes(episodes);
        }

        return Ok(new { season, updated, total = episodes.Count });
    }

    [HttpGet("sync/candidates")]
    public IActionResult GetSyncCandidates()
    {
        var existing = new HashSet<string>(
            _tvRepository.ExistingEpisodePaths(),
            StringComparer.OrdinalIgnoreCase);

        var candidates = _videoManager.ListAllEpisodes()
            .Select(video => new
            {
                Video = video,
                Path = video.MountPath ?? video.Path
            })
            .Where(item => !string.IsNullOrWhiteSpace(item.Path) && !existing.Contains(item.Path!))
            .Select(item => new MediaSyncCandidateDto
            {
                Path = item.Path!,
                Name = item.Video.Name,
                MediaType = "episode",
                Title = item.Video.Name,
                Series = item.Video.Series,
                Season = item.Video.Season,
                Episode = item.Video.Episode
            })
            .OrderBy(item => item.Series, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.Season)
            .ThenBy(item => item.Episode)
            .ThenBy(item => item.Path, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return Ok(candidates);
    }

    [HttpPost("sync")]
    public async Task<IActionResult> Sync(
        [FromQuery] int? limit = null,
        [FromBody] SelectedMediaSyncRequest? request = null)
    {
        if (string.IsNullOrEmpty(_omdbApiKey))
        {
            return StatusCode(500, new { error = "OMDB_API_KEY is not configured" });
        }

        const int batchSize = 100;
        const int dailyLimit = 1000;
        
        var existing = new HashSet<string>(
            _tvRepository.ExistingEpisodePaths(),
            StringComparer.OrdinalIgnoreCase);
        var selectedPaths = request?.SelectedPaths == null
            ? null
            : new HashSet<string>(request.SelectedPaths, StringComparer.OrdinalIgnoreCase);
        var videos = _videoManager.ListAllEpisodes()
            .Where(v =>
            {
                var path = v.MountPath ?? v.Path;
                return !string.IsNullOrEmpty(path) &&
                    !existing.Contains(path) &&
                    (selectedPaths == null || selectedPaths.Contains(path));
            })
            .ToList();
        var totalAvailable = videos.Count;
        if (limit.HasValue && limit.Value > 0)
        {
            videos = videos.Take(limit.Value).ToList();
        }
        
        var totalAdded = 0;
        var omdbCallsUsed = 0;
        var batchToAdd = new List<Episode>();
        var seriesCache = new Dictionary<string, Series>();
        var seriesOmdbCache = new Dictionary<string, (string? Title, string? Description, string? Genre, string? Thumbnail)>();
        
        foreach (var video in videos)
        {
            // Stop if we've hit the daily API limit
            if (omdbCallsUsed >= dailyLimit)
            {
                Console.WriteLine($"Stopping sync: daily OMDB limit of {dailyLimit} reached");
                break;
            }
            
            var filePath = video.MountPath ?? video.Path;
            if (filePath == null)
                continue;

            // Get or create series
            if (!seriesCache.TryGetValue(video.Series, out var series))
            {
                series = _tvRepository.GetOrCreateSeries(video.Series);
                seriesCache[video.Series] = series;

                // Fetch OMDb info for the series (only once per series)
                if (!seriesOmdbCache.ContainsKey(video.Series))
                {
                    try
                    {
                        var urlSeries = $"http://www.omdbapi.com/?apikey={_omdbApiKey}&t={Uri.EscapeDataString(video.Series)}";
                        var jsonSeries = await _httpClient.GetStringAsync(urlSeries);
                        omdbCallsUsed++;
                        var docSeries = JsonDocument.Parse(jsonSeries);
                        if (docSeries.RootElement.TryGetProperty("Response", out var respSeries) && respSeries.GetString() == "True")
                        {
                            var title = docSeries.RootElement.TryGetProperty("Title", out var st) ? st.GetString() : null;
                            var description = docSeries.RootElement.TryGetProperty("Plot", out var d) ? d.GetString() : null;
                            var genre = docSeries.RootElement.TryGetProperty("Genre", out var g) ? g.GetString() : null;
                            string? thumbnail = null;
                            if (docSeries.RootElement.TryGetProperty("Poster", out var posterEl))
                            {
                                var posterUrl = posterEl.GetString();
                                if (!string.IsNullOrEmpty(posterUrl) && posterUrl != "N/A")
                                {
                                    try
                                    {
                                        var bytes = await _httpClient.GetByteArrayAsync(posterUrl);
                                        thumbnail = $"data:image/jpeg;base64,{Convert.ToBase64String(DownsampleImage(bytes, 200))}";
                                    }
                                    catch { }
                                }
                            }
                            seriesOmdbCache[video.Series] = (title, description, genre, thumbnail);
                        }
                        else
                        {
                            seriesOmdbCache[video.Series] = (null, null, null, null);
                        }
                    }
                    catch { seriesOmdbCache[video.Series] = (null, null, null, null); }
                }

                // Only update series info if missing
                var omdbInfo = seriesOmdbCache[video.Series];
                if (string.IsNullOrWhiteSpace(series.Title) && !string.IsNullOrWhiteSpace(omdbInfo.Title))
                    series.Title = omdbInfo.Title;
                if (string.IsNullOrWhiteSpace(series.Description) && !string.IsNullOrWhiteSpace(omdbInfo.Description))
                    series.Description = omdbInfo.Description;
                if (string.IsNullOrWhiteSpace(series.Genre) && !string.IsNullOrWhiteSpace(omdbInfo.Genre))
                    series.Genre = omdbInfo.Genre;
                if (string.IsNullOrWhiteSpace(series.Thumbnail) && !string.IsNullOrWhiteSpace(omdbInfo.Thumbnail))
                    series.Thumbnail = omdbInfo.Thumbnail;
            }

            // Query OMDb for episode info
            string omdbTitle = video.Series;
            int season = video.Season;
            int episodeNum = video.Episode;
            string omdbEpisodeTitle = video.Name;
            string? omdbDescription = null;
            string? omdbThumb = null;

            try
            {
                var url = $"http://www.omdbapi.com/?apikey={_omdbApiKey}&t={Uri.EscapeDataString(omdbTitle)}&Season={season}&Episode={episodeNum}";
                var json = await _httpClient.GetStringAsync(url);
                omdbCallsUsed++;
                var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("Response", out var resp) && resp.GetString() == "True")
                {
                    omdbEpisodeTitle = doc.RootElement.TryGetProperty("Title", out var et) ? et.GetString() ?? video.Name : video.Name;
                    omdbDescription = doc.RootElement.TryGetProperty("Plot", out var p) ? p.GetString() : null;
                    if (doc.RootElement.TryGetProperty("Poster", out var posterElEp))
                    {
                        var posterUrlEp = posterElEp.GetString();
                        if (!string.IsNullOrEmpty(posterUrlEp) && posterUrlEp != "N/A")
                        {
                            try
                            {
                                var bytes = await _httpClient.GetByteArrayAsync(posterUrlEp);
                                omdbThumb = $"data:image/jpeg;base64,{Convert.ToBase64String(DownsampleImage(bytes, 200))}";
                            }
                            catch { }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"OMDB error for {video.Series} S{season}E{episodeNum}: {ex.Message}");
            }

            // Fallback: if omdbThumb is still null, use series thumbnail or video thumbnail
            if (string.IsNullOrEmpty(omdbThumb))
            {
                omdbThumb = series.Thumbnail ?? video.Thumbnail;
            }

            var videoUrl = _videoManager.BuildMediaUrl(filePath);

            var episode = new Episode
            {
                SeriesId = series.Id,
                Title = omdbEpisodeTitle,
                Description = omdbDescription,
                Season = video.Season,
                EpisodeNumber = video.Episode,
                FilePath = filePath,
                VideoUrl = videoUrl,
                Thumbnail = omdbThumb,
                Hidden = false
            };
            batchToAdd.Add(episode);
            
            // Save batch every 100 episodes to preserve progress
            if (batchToAdd.Count >= batchSize)
            {
                _tvRepository.AddEpisodes(batchToAdd);
                totalAdded += batchToAdd.Count;
                Console.WriteLine($"Saved batch: {batchToAdd.Count} episodes (total: {totalAdded}, OMDB calls: {omdbCallsUsed})");
                batchToAdd.Clear();
            }
        }
        
        // Save any remaining episodes
        if (batchToAdd.Count > 0)
        {
            _tvRepository.AddEpisodes(batchToAdd);
            totalAdded += batchToAdd.Count;
            Console.WriteLine($"Saved final batch: {batchToAdd.Count} episodes (total: {totalAdded}, OMDB calls: {omdbCallsUsed})");
        }
        
        var remaining = totalAvailable - totalAdded;
        return Ok(new { 
            added = totalAdded, 
            omdbCallsUsed, 
            omdbCallsRemaining = dailyLimit - omdbCallsUsed,
            pendingEpisodes = remaining > 0 ? remaining : 0,
            isComplete = remaining <= 0
        });
    }

    private byte[] DownsampleImage(byte[] original, int width)
    {
        using var image = RasterImage.Read(original);
        var ratio = (double)width / image.Width;
        var height = (int)(image.Height * ratio);
        
        image.Resize((uint)width, (uint)Math.Max(1, height));
        
        using var ms = new MemoryStream();
        image.Write(ms, MagickFormat.Jpeg);
        return ms.ToArray();
    }

    private async Task<(string? Title, string? Description, string? Genre, string? Thumbnail)?> TryFetchOmdbSeries(string title)
    {
        try
        {
            var url = $"http://www.omdbapi.com/?apikey={_omdbApiKey}&t={Uri.EscapeDataString(title)}";
            var json = await _httpClient.GetStringAsync(url);
            var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("Response", out var resp) && resp.GetString() == "True")
            {
                var seriesTitle = doc.RootElement.TryGetProperty("Title", out var t) ? t.GetString() : null;
                var description = doc.RootElement.TryGetProperty("Plot", out var d) ? d.GetString() : null;
                var genre = doc.RootElement.TryGetProperty("Genre", out var g) ? g.GetString() : null;
                string? thumbnail = null;
                if (doc.RootElement.TryGetProperty("Poster", out var posterEl))
                {
                    var posterUrl = posterEl.GetString();
                    if (!string.IsNullOrEmpty(posterUrl) && posterUrl != "N/A")
                    {
                        try
                        {
                            var bytes = await _httpClient.GetByteArrayAsync(posterUrl);
                            thumbnail = $"data:image/jpeg;base64,{Convert.ToBase64String(DownsampleImage(bytes, 200))}";
                        }
                        catch
                        {
                            // ignore poster failures
                        }
                    }
                }
                return (seriesTitle, description, genre, thumbnail);
            }
        }
        catch
        {
            // ignore lookup failures
        }

        return null;
    }

    private async Task<(string? Title, string? Description, string? Thumbnail)?> TryFetchOmdbEpisode(string seriesTitle, int season, int episodeNumber)
    {
        try
        {
            var url = $"http://www.omdbapi.com/?apikey={_omdbApiKey}&t={Uri.EscapeDataString(seriesTitle)}&Season={season}&Episode={episodeNumber}";
            var json = await _httpClient.GetStringAsync(url);
            var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("Response", out var resp) && resp.GetString() == "True")
            {
                var title = doc.RootElement.TryGetProperty("Title", out var et) ? et.GetString() : null;
                var description = doc.RootElement.TryGetProperty("Plot", out var p) ? p.GetString() : null;
                string? thumbnail = null;
                if (doc.RootElement.TryGetProperty("Poster", out var posterEl))
                {
                    var posterUrl = posterEl.GetString();
                    if (!string.IsNullOrEmpty(posterUrl) && posterUrl != "N/A")
                    {
                        try
                        {
                            var bytes = await _httpClient.GetByteArrayAsync(posterUrl);
                            thumbnail = $"data:image/jpeg;base64,{Convert.ToBase64String(DownsampleImage(bytes, 200))}";
                        }
                        catch
                        {
                            // ignore poster failures
                        }
                    }
                }

                return (title, description, thumbnail);
            }
        }
        catch
        {
            // ignore lookup failures
        }

        return null;
    }

    private string BuildMediaUrl(string? filePath, string fallbackUrl)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return fallbackUrl;
        }

        try
        {
            return _videoManager.BuildMediaUrl(filePath);
        }
        catch
        {
            return fallbackUrl;
        }
    }

    [HttpPatch("{id:int}/watched")]
    public async Task<IActionResult> SetWatched(int id, [FromBody] SetWatchedRequest request)
    {
        var success = await _tvRepository.SetWatchedAsync(id, request.Watched);
        if (!success) return NotFound();
        return Ok(new { message = $"Episode {id} marked as {(request.Watched ? "watched" : "unwatched")}" });
    }

    public class UpdateSeriesRequest
    {
        public string? Title { get; set; }
        public string? Description { get; set; }
        public string? Genre { get; set; }
        public string? Thumbnail { get; set; }
    }
}
