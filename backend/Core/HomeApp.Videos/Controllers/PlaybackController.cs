using Microsoft.AspNetCore.Mvc;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using HomeApp.Videos.Configuration;
using Microsoft.Extensions.Logging;

namespace HomeApp.Videos.Controllers;

/// <summary>
/// Request for playback control
/// </summary>
public record PlaybackRequest
{
    public string? Path { get; init; }
    public string? VideoUrl { get; init; }
}

/// <summary>
/// Request for video control commands
/// </summary>
public record VideoControlRequest
{
    public string Command { get; init; } = string.Empty;
    public string? Path { get; init; }
    public int? Seconds { get; init; }
}

/// <summary>
/// Controller for video playback control
/// </summary>
[ApiController]
[Route("api/v2/playback")]
[Produces("application/json")]
public class PlaybackController : ControllerBase
{
    private readonly string _vlcControlUrl;
    private readonly string _vlcControlToken;
    private readonly ILogger<PlaybackController> _logger;
    private static readonly HttpClient _httpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(10)
    };

    public PlaybackController(VideoModuleOptions options, ILogger<PlaybackController> logger)
    {
        _vlcControlUrl = options.VlcControlUrl;
        _vlcControlToken = options.VlcControlToken;
        _logger = logger;
    }

    /// <summary>
    /// Start playing a video
    /// </summary>
    [HttpPost("play")]
    public async Task<IActionResult> Play([FromBody] PlaybackRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(request.Path))
            return BadRequest(new { success = false, error = "No file path provided" });

        if (!System.IO.File.Exists(request.Path))
            return NotFound(new { success = false, error = "File does not exist" });

        try
        {
            var payload = JsonSerializer.Serialize(new { command = "play", path = request.Path });
            var response = await SendVlcCommandAsync(payload, ct);
            
            var content = await response.Content.ReadAsStringAsync(ct);
            
            if (response.IsSuccessStatusCode)
            {
                return Ok(new { success = true, data = JsonSerializer.Deserialize<object>(content) });
            }
            
            _logger.LogWarning("VLC control returned error: {Status} - {Content}", response.StatusCode, content);
            return StatusCode((int)response.StatusCode, new { success = false, error = content });
        }
        catch (TaskCanceledException)
        {
            return StatusCode(504, new { success = false, error = "VLC control service timeout" });
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Failed to connect to VLC control service");
            return StatusCode(503, new { success = false, error = "VLC control service unavailable" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error playing video");
            return StatusCode(500, new { success = false, error = $"Failed to play video: {ex.Message}" });
        }
    }

    /// <summary>
    /// Send a control command (pause, stop, resume, seek, etc.)
    /// </summary>
    [HttpPost("control")]
    public async Task<IActionResult> Control([FromBody] VideoControlRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(request.Command))
            return BadRequest(new { success = false, error = "Missing command" });

        var validCommands = new[] { "play", "pause", "stop", "resume", "seek", "get_time", "set_time", "volume" };
        if (!validCommands.Contains(request.Command.ToLower()))
            return BadRequest(new { success = false, error = $"Invalid command. Valid commands: {string.Join(", ", validCommands)}" });

        try
        {
            var payload = new
            {
                command = request.Command,
                path = request.Path,
                seconds = request.Seconds
            };
            
            var json = JsonSerializer.Serialize(payload);
            var response = await SendVlcCommandAsync(json, ct);
            
            var content = await response.Content.ReadAsStringAsync(ct);
            
            if (response.IsSuccessStatusCode)
            {
                try
                {
                    var data = JsonSerializer.Deserialize<object>(content);
                    return Ok(new { success = true, data });
                }
                catch
                {
                    return Ok(new { success = true, message = content });
                }
            }
            
            _logger.LogWarning("VLC control command '{Command}' failed: {Status}", request.Command, response.StatusCode);
            return StatusCode((int)response.StatusCode, new { success = false, error = content });
        }
        catch (TaskCanceledException)
        {
            return StatusCode(504, new { success = false, error = "VLC control service timeout" });
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Failed to connect to VLC control service");
            return StatusCode(503, new { success = false, error = "VLC control service unavailable" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing control command {Command}", request.Command);
            return StatusCode(500, new { success = false, error = $"Failed to control video: {ex.Message}" });
        }
    }

    /// <summary>
    /// Get current playback status
    /// </summary>
    [HttpGet("status")]
    public async Task<IActionResult> GetStatus(CancellationToken ct = default)
    {
        try
        {
            var payload = JsonSerializer.Serialize(new { command = "get_time" });
            var response = await SendVlcCommandAsync(payload, ct);
            
            var content = await response.Content.ReadAsStringAsync(ct);
            
            if (response.IsSuccessStatusCode)
            {
                var data = JsonSerializer.Deserialize<object>(content);
                return Ok(new { success = true, data });
            }
            
            return Ok(new { success = false, playing = false });
        }
        catch
        {
            return Ok(new { success = false, playing = false, error = "Unable to get playback status" });
        }
    }

    private async Task<HttpResponseMessage> SendVlcCommandAsync(string json, CancellationToken ct)
    {
        using var message = new HttpRequestMessage(HttpMethod.Post, _vlcControlUrl)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
        if (!string.IsNullOrWhiteSpace(_vlcControlToken))
        {
            message.Headers.Add("X-HomeApp-Token", _vlcControlToken);
        }

        return await _httpClient.SendAsync(message, ct);
    }
}
