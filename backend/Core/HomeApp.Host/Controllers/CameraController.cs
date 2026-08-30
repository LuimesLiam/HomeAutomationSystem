using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using System.Threading;
using HomeApp.SecuritySystem;
using NetMQ;
using NetMQ.Sockets;
using System.Text.Json;

namespace HomeApp.Controllers;

[ApiController]
[Route("api/camera")]
public class CameraController : ControllerBase
{
    private static volatile bool _feedEnabled = true;
    private readonly SecuritySystemOptions _options;

    public CameraController(SecuritySystemOptions options)
    {
        _options = options;
    }

    private string SendCamFeedControl(string command)
    {
        using var req = new RequestSocket();
        req.Connect(_options.CameraControlEndpoint);
        req.SendFrame(command);

        if (!req.TryReceiveFrameString(TimeSpan.FromSeconds(2), out var reply))
        {
            throw new TimeoutException($"No response from camera control endpoint {_options.CameraControlEndpoint}");
        }

        return reply;
    }

    [HttpPost("enable")]
    public IActionResult EnableFeed()
    {
        var reply = SendCamFeedControl("enable");
        _feedEnabled = true;
        return Ok(new { feedEnabled = true, cameraService = reply });
    }

    [HttpPost("disable")]
    public IActionResult DisableFeed()
    {
        // Stop serving frames immediately, even if the physical camera service is
        // temporarily unavailable. This also causes existing MJPEG requests to end.
        _feedEnabled = false;
        var reply = SendCamFeedControl("disable");
        return Ok(new { feedEnabled = false, cameraService = reply });
    }

    [HttpGet("status")]
    public IActionResult GetStatus()
    {
        try
        {
            var cameraStatus = SendCamFeedControl("status");
            using var statusJson = JsonDocument.Parse(cameraStatus);

            return Ok(new
            {
                feedEnabled = _feedEnabled,
                cameraService = statusJson.RootElement.Clone()
            });
        }
        catch (Exception ex)
        {
            return StatusCode(503, new
            {
                feedEnabled = _feedEnabled,
                cameraService = new
                {
                    available = false,
                    error = ex.Message
                }
            });
        }
    }

    [HttpGet("stream")]
    //[Authorize] // Uncomment to require authentication
    public async Task GetStream(CancellationToken cancellationToken = default)
    {
        Response.ContentType = "multipart/x-mixed-replace; boundary=frame";
        while (!cancellationToken.IsCancellationRequested)
        {
            if (!_feedEnabled)
            {
                break;
            }
            var frame = ZmqMotionListener.LatestFrame;
            if (frame != null)
            {
                await Response.Body.WriteAsync(System.Text.Encoding.ASCII.GetBytes("--frame\r\n"));
                await Response.Body.WriteAsync(System.Text.Encoding.ASCII.GetBytes("Content-Type: image/jpeg\r\n\r\n"));
                await Response.Body.WriteAsync(frame, 0, frame.Length);
                await Response.Body.WriteAsync(System.Text.Encoding.ASCII.GetBytes("\r\n"));
                await Response.Body.FlushAsync();
            }
            try
            {
                await Task.Delay(50, cancellationToken); // ~20 FPS
            }
            catch (TaskCanceledException)
            {
                // Ignore cancellation exceptions
            }
            catch (Exception ex)
            {
                // Log or handle other exceptions as needed
                break; // Exit loop on error
            }
            
        }
    }
}
