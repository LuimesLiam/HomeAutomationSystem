using HomeApp.Library.Imaging;
using ImageMagick;
using Microsoft.AspNetCore.Mvc;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using HomeApp.Videos.Logic;
using HomeApp.Videos.Data;
using HomeApp.Videos.Configuration;
using Microsoft.AspNetCore.Http;
using System.Diagnostics;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Collections.Generic;
using HomeApp.Videos.DTOs;

namespace HomeApp.Videos.Controllers;

[ApiController]
[Route("api/video")]
public class VideoController : ControllerBase
{
    private readonly VideoManager _videoManager;
    private readonly string _vlcControlUrl;
    private readonly string _vlcControlToken;
    private readonly MovieRepository _movieRepository;
    private readonly string _omdbApiKey;
    private static readonly HttpClient _httpClient = new();

    public VideoController(VideoManager videoManager, MovieRepository movieRepository, VideoModuleOptions options)
    {
        _videoManager = videoManager;
        _movieRepository = movieRepository;
        _vlcControlUrl = options.VlcControlUrl;
        _vlcControlToken = options.VlcControlToken;
        _omdbApiKey = options.OmdbApiKey;
    }

    [HttpGet]
    public IActionResult Get([FromQuery] string? genre = null, [FromQuery] int? sample = null)
    {
        if (!string.IsNullOrEmpty(genre))
        {
            var movies = _movieRepository.GetByGenre(genre);
            if (sample.HasValue)
                movies = movies.Take(sample.Value).ToList();
            return Ok(movies.Select(MapMovie).ToList());
        }

        var items = _movieRepository.GetByGenre();
        if (sample.HasValue)
        {
            var limited = new Dictionary<string, List<object>>();
            foreach (var kv in items)
            {
                limited[kv.Key] = kv.Value.Take(sample.Value).Select(MapMovie).ToList();
            }
            return Ok(limited);
        }
        return Ok(items.ToDictionary(kv => kv.Key, kv => kv.Value.Select(MapMovie).ToList()));
    }

    [HttpGet("genre")]
    public IActionResult GetByGenrePaged([FromQuery] string genre, [FromQuery] int page = 0, [FromQuery] int pageSize = 50, [FromQuery] string? search = null)
    {
        if (string.IsNullOrWhiteSpace(genre))
        {
            return BadRequest(new { error = "Missing genre" });
        }

        if (page < 0)
        {
            page = 0;
        }

        pageSize = Math.Clamp(pageSize, 1, 200);

        var (items, total) = _movieRepository.GetByGenrePaged(genre, page, pageSize, search);

        return Ok(new
        {
            items = items.Select(MapMovie).ToList(),
            total,
            page,
            pageSize
        });
    }

    [HttpGet("list")]
    public IActionResult List()
    {
        var movies = _movieRepository.GetAll()
            .Select(m => new
            {
                m.Id,
                m.Name,
                m.Title,
                m.Year,
                m.Genre,
                Description = m.Plot,
                m.FilePath,
                VideoUrl = BuildMediaUrl(m.FilePath, m.VideoUrl),
                m.Thumbnail,
                ThumbnailUrl = m.Thumbnail,
                m.Watched
            })
            .ToList();
        return Ok(movies);
    }

    [HttpGet("search")]
    public IActionResult Search([FromQuery] string query, [FromQuery] int maxResults = 100)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return BadRequest(new { error = "Missing query" });
        }

        maxResults = Math.Clamp(maxResults, 1, 200);

        var movies = _movieRepository.Search(query, maxResults);
        var grouped = new Dictionary<string, List<object>>();

        foreach (var movie in movies)
        {
            var genres = (movie.Genre ?? "Unknown")
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

                grouped[genre].Add(MapMovie(movie));
            }
        }

        return Ok(grouped);
    }

    [HttpGet("playback-options")]
    public IActionResult GetPlaybackOptions([FromQuery] string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return BadRequest(new { error = "Missing filePath" });
        }

        try
        {
            var requestOrigin = $"{Request.Scheme}://{Request.Host}{Request.PathBase}";
            var browserUrl = AppendBrowserQuery(_videoManager.BuildMediaUrlForBrowser(filePath, requestOrigin));
            var homeUrl = AppendBrowserQuery(_videoManager.BuildMediaUrlForHome(filePath));

            return Ok(new PlaybackOptionsResponse
            {
                BrowserUrl = browserUrl,
                HomeUrl = homeUrl,
                HomeAvailable = !string.IsNullOrWhiteSpace(homeUrl)
            });
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = $"Unable to build playback URLs: {ex.Message}" });
        }
    }

    [HttpPost("playback")]
    public async Task<IActionResult> Playback([FromBody] PlaybackRequest req)
    {
        if (string.IsNullOrEmpty(req.Path) || !System.IO.File.Exists(req.Path))
            return BadRequest(new { error = "No file path provided or file does not exist" });

        try
        {
            var payload = JsonSerializer.Serialize(new { command = "play", path = req.Path });
            var response = await SendVlcCommandAsync(payload);
            var content = await response.Content.ReadAsStringAsync();
            if (response.IsSuccessStatusCode)
                return Ok(JsonSerializer.Deserialize<object>(content));
            return StatusCode((int)response.StatusCode, JsonSerializer.Deserialize<object>(content));
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = $"Failed to play video: {ex.Message}" });
        }
    }

    [HttpPost("control")]
    public async Task<IActionResult> Control([FromBody] VideoControlRequest req)
    {
        if (req == null || string.IsNullOrEmpty(req.Command))
            return BadRequest(new { error = "Missing command" });

        var payload = new
        {
            command = req.Command,
            path = req.Path,
            seconds = req.Seconds
        };
        try
        {
            var json = JsonSerializer.Serialize(payload);
            var response = await SendVlcCommandAsync(json);
            var content = await response.Content.ReadAsStringAsync();
            if (response.IsSuccessStatusCode)
                return Ok(JsonSerializer.Deserialize<object>(content));
            return StatusCode((int)response.StatusCode, JsonSerializer.Deserialize<object>(content));
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = $"Failed to control video: {ex.Message}" });
        }
    }

    private async Task<HttpResponseMessage> SendVlcCommandAsync(string json)
    {
        using var message = new HttpRequestMessage(HttpMethod.Post, _vlcControlUrl)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
        if (!string.IsNullOrWhiteSpace(_vlcControlToken))
        {
            message.Headers.Add("X-HomeApp-Token", _vlcControlToken);
        }

        return await _httpClient.SendAsync(message);
    }

    [HttpGet("sync/candidates")]
    public IActionResult GetSyncCandidates()
    {
        var existing = new HashSet<string>(
            _movieRepository.GetAll().Select(m => m.FilePath),
            StringComparer.OrdinalIgnoreCase);

        var candidates = _videoManager.ListAllMovies()
            .Select(video =>
            {
                var path = video.MountPath ?? video.Path;
                var (title, year) = ParseTitleAndYear(video.Name);
                return new { video, path, title, year };
            })
            .Where(item => !string.IsNullOrWhiteSpace(item.path) && !existing.Contains(item.path!))
            .Select(item => new MediaSyncCandidateDto
            {
                Path = item.path!,
                Name = item.video.Name,
                MediaType = "movie",
                Title = item.title,
                Year = item.year
            })
            .OrderBy(item => item.Title, StringComparer.OrdinalIgnoreCase)
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

        var existing = new HashSet<string>(
            _movieRepository.GetAll().Select(m => m.FilePath),
            StringComparer.OrdinalIgnoreCase);
        var selectedPaths = request?.SelectedPaths == null
            ? null
            : new HashSet<string>(request.SelectedPaths, StringComparer.OrdinalIgnoreCase);
        var videos = _videoManager.ListAllMovies()
            .Where(v =>
            {
                var filePath = v.MountPath ?? v.Path;
                return !string.IsNullOrEmpty(filePath) &&
                    !existing.Contains(filePath) &&
                    (selectedPaths == null || selectedPaths.Contains(filePath));
            })
            .ToList();
        var totalAvailable = videos.Count;
        if (limit.HasValue && limit.Value > 0)
        {
            videos = videos.Take(limit.Value).ToList();
        }
        var toAdd = new List<Movie>();

        foreach (var video in videos)
        {
            var filePath = video.MountPath ?? video.Path;
            if (filePath == null)
                continue;

            // Parse title and year from filename
            var (parsedTitle, parsedYear) = ParseTitleAndYear(video.Name);
            string omdbTitle = parsedTitle;
            string omdbYear = parsedYear;

            string omdbTitleResult = omdbTitle;
            string omdbYearResult = omdbYear;
            string omdbGenre = "";
            string omdbPlot = "";
            JsonDocument? omdbDoc = null;

            try
            {
                var url = $"http://www.omdbapi.com/?apikey={_omdbApiKey}&t={Uri.EscapeDataString(omdbTitle)}" + (string.IsNullOrEmpty(omdbYear) ? "" : $"&y={Uri.EscapeDataString(omdbYear)}");
                var json = await _httpClient.GetStringAsync(url);
                var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("Response", out var resp) && resp.GetString() == "True")
                {
                    omdbDoc = doc;
                    omdbTitleResult = doc.RootElement.GetProperty("Title").GetString() ?? omdbTitle;
                    omdbYearResult = doc.RootElement.TryGetProperty("Year", out var y) ? y.GetString() ?? omdbYear : omdbYear;
                    omdbGenre = doc.RootElement.TryGetProperty("Genre", out var g) ? g.GetString() ?? "" : "";
                    omdbPlot = doc.RootElement.TryGetProperty("Plot", out var p) ? p.GetString() ?? "" : "";
                }
            }
            catch (Exception ex)
            {
                Console.Write(ex.Message);
                // ignore individual failures
            }

            // Try OMDB poster first, fallback to extracting from video
            string omdbThumb = await GetThumbnailFromOmdbOrVideo(omdbDoc, filePath);

            var videoUrl = _videoManager.BuildMediaUrl(filePath);

            // Add movie, using OMDB info if found, else placeholders
            var movie = new Movie
            {
                Name = video.Name,
                FilePath = filePath,
                VideoUrl = videoUrl,
                Thumbnail = omdbThumb,
                Title = omdbTitleResult,
                Year = string.IsNullOrWhiteSpace(omdbYearResult) ? null : omdbYearResult,
                Genre = string.IsNullOrWhiteSpace(omdbGenre) ? "Unknown" : omdbGenre,
                Plot = string.IsNullOrWhiteSpace(omdbPlot) ? "No plot available." : omdbPlot,
                Hidden = false
            };
            toAdd.Add(movie);
        }
        _movieRepository.AddRange(toAdd);

        var remaining = totalAvailable - toAdd.Count;
        return Ok(new
        {
            added = toAdd.Count,
            pendingMovies = remaining > 0 ? remaining : 0,
            isComplete = remaining <= 0
        });
    }

    [HttpPatch("{id:int}")]
    public IActionResult UpdateMovie(int id, [FromBody] UpdateMovieRequest request)
    {
        var movie = _movieRepository.GetById(id);
        if (movie == null)
        {
            return NotFound();
        }

        if (request.Title != null)
        {
            movie.Title = request.Title;
        }

        if (request.Year != null)
        {
            movie.Year = string.IsNullOrWhiteSpace(request.Year) ? null : request.Year;
        }

        if (request.Genre != null)
        {
            movie.Genre = request.Genre;
        }

        var nextPlot = request.Description ?? request.Plot;
        if (nextPlot != null)
        {
            movie.Plot = nextPlot;
        }

        _movieRepository.Update(movie);
        return Ok(MapMovie(movie));
    }

    [HttpPost("{id:int}/resync")]
    public async Task<IActionResult> ResyncMovie(int id)
    {
        if (string.IsNullOrEmpty(_omdbApiKey))
        {
            return StatusCode(500, new { error = "OMDB_API_KEY is not configured" });
        }

        var movie = _movieRepository.GetById(id);
        if (movie == null)
        {
            return NotFound();
        }

        var (parsedTitle, parsedYear) = ParseTitleAndYear(movie.Name);
        var title = string.IsNullOrWhiteSpace(movie.Title) ? parsedTitle : movie.Title;
        var year = string.IsNullOrWhiteSpace(movie.Year) ? parsedYear : movie.Year;

        var omdbResult = await TryFetchOmdbMovie(title, year);
        if (omdbResult == null)
        {
            return NotFound(new { error = "OMDB lookup failed" });
        }

        movie.Title = omdbResult.Value.Title ?? movie.Title;
        movie.Year = string.IsNullOrWhiteSpace(omdbResult.Value.Year) ? movie.Year : omdbResult.Value.Year;
        movie.Genre = string.IsNullOrWhiteSpace(omdbResult.Value.Genre) ? movie.Genre : omdbResult.Value.Genre;
        movie.Plot = string.IsNullOrWhiteSpace(omdbResult.Value.Plot) ? movie.Plot : omdbResult.Value.Plot;

        if (omdbResult.Value.Doc != null)
        {
            movie.Thumbnail = await GetThumbnailFromOmdbOrVideo(omdbResult.Value.Doc, movie.FilePath);
        }

        _movieRepository.Update(movie);
        return Ok(MapMovie(movie));
    }

    private async Task<string> GetThumbnailFromOmdbOrVideo(JsonDocument? doc, string videoPath)
    {
        // First, try to get poster from OMDB
        if (doc != null && doc.RootElement.TryGetProperty("Poster", out var posterEl))
        {
            var posterUrl = posterEl.GetString();
            if (!string.IsNullOrEmpty(posterUrl) && posterUrl != "N/A")
            {
                try
                {
                    var bytes = await _httpClient.GetByteArrayAsync(posterUrl);
                    return $"data:image/jpeg;base64,{Convert.ToBase64String(DownsampleImage(bytes, 200))}";
                }
                catch
                {
                    // Fall through to video extraction
                }
            }
        }

        // Fallback: extract thumbnail from video file using ffmpeg
        return ExtractThumbnailFromVideo(videoPath);
    }

    private string ExtractThumbnailFromVideo(string videoPath)
    {
        try
        {
            var ffprobePath = "/usr/bin/ffprobe";
            var ffmpegPath = "/usr/bin/ffmpeg";

            if (!System.IO.File.Exists(ffprobePath) || !System.IO.File.Exists(ffmpegPath))
            {
                return string.Empty;
            }

            // Get video duration using ffprobe
            var ffprobe = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = ffprobePath,
                    Arguments = $"-v error -show_entries format=duration -of default=noprint_wrappers=1:nokey=1 \"{videoPath}\"",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };
            ffprobe.Start();
            string? durationStr = ffprobe.StandardOutput.ReadLine();
            ffprobe.WaitForExit();
            double.TryParse(durationStr, out double duration);
            double middle = duration > 0 ? duration / 2 : 0;

            // Generate thumbnail to a temp file
            var tempThumbnail = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.jpg");
            var ffmpeg = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = ffmpegPath,
                    Arguments = $"-ss {middle} -i \"{videoPath}\" -frames:v 1 -vf scale=200:-1 \"{tempThumbnail}\" -y",
                    RedirectStandardOutput = false,
                    RedirectStandardError = false,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };
            ffmpeg.Start();
            ffmpeg.WaitForExit();

            if (System.IO.File.Exists(tempThumbnail))
            {
                var bytes = System.IO.File.ReadAllBytes(tempThumbnail);
                System.IO.File.Delete(tempThumbnail);
                return $"data:image/jpeg;base64,{Convert.ToBase64String(bytes)}";
            }
        }
        catch (Exception)
        {
            // Swallow and return empty
        }
        return string.Empty;
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

    // Helper to parse title and year from filename
    private (string title, string year) ParseTitleAndYear(string filename)
    {
        // Remove extension
        var name = Path.GetFileNameWithoutExtension(filename);
        // Replace dots, underscores, and dashes with spaces
        name = name.Replace('.', ' ').Replace('_', ' ').Replace('-', ' ');
        // Regex to find year (4 digits, 19xx or 20xx)
        var yearMatch = System.Text.RegularExpressions.Regex.Match(name, @"(?:19|20)\d{2}");
        string year = yearMatch.Success ? yearMatch.Value : string.Empty;
        // Remove everything after year
        string title = yearMatch.Success ? name.Substring(0, yearMatch.Index).Trim() : name.Trim();
        // Remove extra spaces
        title = System.Text.RegularExpressions.Regex.Replace(title, @"\s+", " ");
        return (title, year);
    }

    private async Task<(string? Title, string? Year, string? Genre, string? Plot, JsonDocument? Doc)?> TryFetchOmdbMovie(string title, string? year)
    {
        try
        {
            var url = $"http://www.omdbapi.com/?apikey={_omdbApiKey}&t={Uri.EscapeDataString(title)}" +
                      (string.IsNullOrEmpty(year) ? "" : $"&y={Uri.EscapeDataString(year)}");
            var json = await _httpClient.GetStringAsync(url);
            var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("Response", out var resp) && resp.GetString() == "True")
            {
                var resultTitle = doc.RootElement.TryGetProperty("Title", out var t) ? t.GetString() : null;
                var resultYear = doc.RootElement.TryGetProperty("Year", out var y) ? y.GetString() : null;
                var resultGenre = doc.RootElement.TryGetProperty("Genre", out var g) ? g.GetString() : null;
                var resultPlot = doc.RootElement.TryGetProperty("Plot", out var p) ? p.GetString() : null;
                return (resultTitle, resultYear, resultGenre, resultPlot, doc);
            }
        }
        catch
        {
            // ignore lookup failures
        }

        return null;
    }


    public class PlaybackRequest
    {
        public string? Path { get; set; }
    }

    public class VideoControlRequest
    {
        public string? Command { get; set; }
        public string? Path { get; set; }
        public int? Seconds { get; set; }
    }

    public class SetWatchedRequest
    {
        public bool Watched { get; set; }
    }

    public class UpdateMovieRequest
    {
        public string? Title { get; set; }
        public string? Year { get; set; }
        public string? Genre { get; set; }
        public string? Description { get; set; }
        public string? Plot { get; set; }
    }

    private object MapMovie(Movie movie)
    {
        return new
        {
            movie.Id,
            movie.Name,
            movie.Title,
            movie.Year,
            movie.Genre,
            Description = movie.Plot,
            Plot = movie.Plot,
            movie.FilePath,
            VideoUrl = BuildMediaUrl(movie.FilePath, movie.VideoUrl),
            movie.Thumbnail,
            ThumbnailUrl = movie.Thumbnail,
            movie.Hidden,
            movie.Watched
        };
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

    private static string? AppendBrowserQuery(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return url;
        }

        var separator = url.Contains('?', StringComparison.Ordinal) ? "&" : "?";
        return $"{url}{separator}browser=true";
    }

    public sealed class PlaybackOptionsResponse
    {
        public string BrowserUrl { get; set; } = string.Empty;
        public string? HomeUrl { get; set; }
        public bool HomeAvailable { get; set; }
    }
    
    [HttpPatch("{id}/watched")]
    public IActionResult SetWatched(int id, [FromBody] SetWatchedRequest request)
    {
        _movieRepository.SetWatched(id, request.Watched);
        return Ok();
    }
}
