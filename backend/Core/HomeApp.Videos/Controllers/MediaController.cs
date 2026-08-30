using HomeApp.Videos.Logic;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;
using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace HomeApp.Videos.Controllers;

[ApiController]
[EnableCors("MediaStreaming")]
[Route("media/{*filePath}")]
public class MediaController : ControllerBase
{
    private readonly VideoManager _videoManager;
    private static readonly FileExtensionContentTypeProvider ContentTypeProvider = new();

    public MediaController(VideoManager videoManager)
    {
        _videoManager = videoManager;
    }

    [AcceptVerbs("GET", "HEAD")]
    public async Task<IActionResult> GetMedia([FromRoute] string filePath, [FromQuery] bool browser = false)
    {
        if (!_videoManager.TryResolveMediaPath(filePath, out var fullPath) || !System.IO.File.Exists(fullPath))
        {
            return NotFound("File not found.");
        }

        if (browser && TryGetBrowserCompatiblePath(fullPath, out var browserPath))
        {
            fullPath = browserPath;
        }

        var fileSize = new FileInfo(fullPath).Length;
        var contentType = GetContentType(fullPath);
        Response.Headers["Accept-Ranges"] = "bytes";

        if (Request.Headers.TryGetValue("Range", out var rangeHeaderValue))
        {
            if (!TryParseRangeHeader(rangeHeaderValue.ToString(), fileSize, out var start, out var end))
            {
                return StatusCode(StatusCodes.Status416RangeNotSatisfiable);
            }

            Response.StatusCode = StatusCodes.Status206PartialContent;
            Response.ContentType = contentType;
            Response.Headers["Content-Range"] = $"bytes {start}-{end}/{fileSize}";
            Response.Headers["Content-Length"] = (end - start + 1).ToString();

            if (HttpMethods.IsHead(Request.Method))
            {
                return new EmptyResult();
            }

            using var fs = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            fs.Seek(start, SeekOrigin.Begin);
            var buffer = new byte[8192];
            long remaining = end - start + 1;
            while (remaining > 0)
            {
                var read = await fs.ReadAsync(buffer, 0, (int)Math.Min(buffer.Length, remaining));
                if (read == 0)
                {
                    break;
                }

                await Response.Body.WriteAsync(buffer, 0, read);
                remaining -= read;
            }

            return new EmptyResult();
        }

        if (HttpMethods.IsHead(Request.Method))
        {
            Response.ContentType = contentType;
            Response.ContentLength = fileSize;
            return new EmptyResult();
        }

        return PhysicalFile(fullPath, contentType, enableRangeProcessing: true);
    }

    private static bool TryParseRangeHeader(string rangeHeader, long fileSize, out long start, out long end)
    {
        start = 0;
        end = 0;

        if (string.IsNullOrWhiteSpace(rangeHeader) ||
            !rangeHeader.StartsWith("bytes=", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var rangeValue = rangeHeader["bytes=".Length..].Trim();
        if (string.IsNullOrWhiteSpace(rangeValue) || rangeValue.Contains(','))
        {
            return false;
        }

        var parts = rangeValue.Split('-', 2);
        if (parts.Length != 2)
        {
            return false;
        }

        var startText = parts[0].Trim();
        var endText = parts[1].Trim();

        if (string.IsNullOrEmpty(startText))
        {
            if (!long.TryParse(endText, out var suffixLength) || suffixLength <= 0)
            {
                return false;
            }

            suffixLength = Math.Min(suffixLength, fileSize);
            start = fileSize - suffixLength;
            end = fileSize - 1;
            return true;
        }

        if (!long.TryParse(startText, out start) || start < 0 || start >= fileSize)
        {
            return false;
        }

        if (string.IsNullOrEmpty(endText))
        {
            end = fileSize - 1;
            return true;
        }

        if (!long.TryParse(endText, out end) || end < start)
        {
            return false;
        }

        end = Math.Min(end, fileSize - 1);
        return true;
    }

    private static bool TryGetBrowserCompatiblePath(string fullPath, out string browserPath)
    {
        browserPath = string.Empty;

        var (videoCodec, audioCodec) = ProbePrimaryCodecs(fullPath);
        if (!IsBrowserFriendlyVideoCodec(videoCodec))
        {
            return false;
        }

        var extension = Path.GetExtension(fullPath);
        var hasAudio = !string.IsNullOrWhiteSpace(audioCodec);
        var browserFriendlyAudio = !hasAudio || IsBrowserFriendlyAudioCodec(audioCodec);
        var browserFriendlyContainer = IsBrowserFriendlyContainer(extension);

        // If both container and codecs are already browser-safe, stream the source file directly.
        if (browserFriendlyContainer && browserFriendlyAudio)
        {
            return false;
        }

        browserPath = Path.ChangeExtension(fullPath, ".browser.mp4");
        if (string.IsNullOrWhiteSpace(browserPath))
        {
            browserPath = string.Empty;
            return false;
        }

        var failurePath = browserPath + ".failed.json";

        var sourceInfo = new FileInfo(fullPath);
        if (System.IO.File.Exists(browserPath))
        {
            var browserInfo = new FileInfo(browserPath);
            if (browserInfo.LastWriteTimeUtc >= sourceInfo.LastWriteTimeUtc && browserInfo.Length > 0)
            {
                return true;
            }
        }

        if (TryReadFailureMarker(failurePath, sourceInfo, out var failure))
        {
            Console.WriteLine($"Browser audio-fix skipped for '{fullPath}' due to cached failure. Video={failure.VideoCodec}, Audio={failure.AudioCodec}, Error={failure.Error}");
            browserPath = string.Empty;
            return false;
        }

        const string ffmpegPath = "/usr/bin/ffmpeg";
        if (!System.IO.File.Exists(ffmpegPath))
        {
            WriteFailureMarker(failurePath, sourceInfo, videoCodec, audioCodec, "ffmpeg_missing");
            browserPath = string.Empty;
            return false;
        }

        var tempPath = browserPath + ".tmp.mp4";
        if (System.IO.File.Exists(tempPath))
        {
            System.IO.File.Delete(tempPath);
        }

        var ffmpeg = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = ffmpegPath,
                Arguments = BuildBrowserConversionArguments(fullPath, tempPath, browserFriendlyAudio, hasAudio),
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        ffmpeg.Start();
        _ = ffmpeg.StandardOutput.ReadToEnd();
        var ffmpegError = ffmpeg.StandardError.ReadToEnd();
        ffmpeg.WaitForExit();

        if (ffmpeg.ExitCode != 0 || !System.IO.File.Exists(tempPath))
        {
            if (System.IO.File.Exists(tempPath))
            {
                System.IO.File.Delete(tempPath);
            }

            Console.WriteLine($"Browser audio-fix conversion failed for '{fullPath}': {ffmpegError}");
            WriteFailureMarker(failurePath, sourceInfo, videoCodec, audioCodec, ffmpegError);
            browserPath = string.Empty;
            return false;
        }

        if (System.IO.File.Exists(browserPath))
        {
            System.IO.File.Delete(browserPath);
        }

        System.IO.File.Move(tempPath, browserPath);
        if (System.IO.File.Exists(failurePath))
        {
            System.IO.File.Delete(failurePath);
        }
        return true;
    }

    private static (string? VideoCodec, string? AudioCodec) ProbePrimaryCodecs(string filePath)
    {
        try
        {
            const string ffprobePath = "/usr/bin/ffprobe";
            if (!System.IO.File.Exists(ffprobePath))
            {
                return (null, null);
            }

            var ffprobe = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = ffprobePath,
                    Arguments = $"-v error -show_entries stream=codec_type,codec_name -of json \"{filePath}\"",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            ffprobe.Start();
            var output = ffprobe.StandardOutput.ReadToEnd();
            ffprobe.WaitForExit();

            using var document = JsonDocument.Parse(output);
            string? videoCodec = null;
            string? audioCodec = null;
            if (!document.RootElement.TryGetProperty("streams", out var streams) || streams.ValueKind != JsonValueKind.Array)
            {
                return (null, null);
            }

            foreach (var stream in streams.EnumerateArray())
            {
                var codecType = stream.TryGetProperty("codec_type", out var codecTypeProperty)
                    ? codecTypeProperty.GetString()
                    : null;
                var codecName = stream.TryGetProperty("codec_name", out var codecNameProperty)
                    ? codecNameProperty.GetString()
                    : null;

                if (videoCodec == null && string.Equals(codecType, "video", StringComparison.OrdinalIgnoreCase))
                {
                    videoCodec = codecName;
                }
                else if (audioCodec == null && string.Equals(codecType, "audio", StringComparison.OrdinalIgnoreCase))
                {
                    audioCodec = codecName;
                }
            }

            return (videoCodec, audioCodec);
        }
        catch
        {
            return (null, null);
        }
    }

    private static bool IsBrowserFriendlyVideoCodec(string? codec)
    {
        return string.Equals(codec, "h264", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsBrowserFriendlyAudioCodec(string? codec)
    {
        return string.Equals(codec, "aac", StringComparison.OrdinalIgnoreCase)
            || string.Equals(codec, "mp3", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsBrowserFriendlyContainer(string extension)
    {
        return string.Equals(extension, ".mp4", StringComparison.OrdinalIgnoreCase)
            || string.Equals(extension, ".m4v", StringComparison.OrdinalIgnoreCase);
    }

    private static string BuildBrowserConversionArguments(string inputPath, string outputPath, bool browserFriendlyAudio, bool hasAudio)
    {
        var arguments = $"-y -i \"{inputPath}\" -map 0:v:0?";

        if (hasAudio)
        {
            arguments += " -map 0:a:0?";
        }

        arguments += browserFriendlyAudio
            ? " -c:v copy -c:a copy"
            : " -c:v copy -c:a aac -b:a 192k";

        arguments += $" -movflags +faststart \"{outputPath}\"";
        return arguments;
    }

    private static string GetContentType(string fullPath)
    {
        return ContentTypeProvider.TryGetContentType(fullPath, out var contentType)
            ? contentType
            : "application/octet-stream";
    }

    private static bool TryReadFailureMarker(string failurePath, FileInfo sourceInfo, out BrowserConversionFailure failure)
    {
        failure = default!;

        if (!System.IO.File.Exists(failurePath))
        {
            return false;
        }

        try
        {
            var json = System.IO.File.ReadAllText(failurePath);
            var data = JsonSerializer.Deserialize<BrowserConversionFailure>(json);
            if (data == null || data.SourceLastWriteUtc != sourceInfo.LastWriteTimeUtc)
            {
                System.IO.File.Delete(failurePath);
                return false;
            }

            failure = data;
            return true;
        }
        catch
        {
            try
            {
                System.IO.File.Delete(failurePath);
            }
            catch
            {
            }

            return false;
        }
    }

    private static void WriteFailureMarker(string failurePath, FileInfo sourceInfo, string? videoCodec, string? audioCodec, string error)
    {
        try
        {
            var payload = new BrowserConversionFailure
            {
                SourceLastWriteUtc = sourceInfo.LastWriteTimeUtc,
                VideoCodec = videoCodec,
                AudioCodec = audioCodec,
                Error = error
            };

            var json = JsonSerializer.Serialize(payload);
            System.IO.File.WriteAllText(failurePath, json);
        }
        catch
        {
        }
    }

    private sealed class BrowserConversionFailure
    {
        public DateTime SourceLastWriteUtc { get; set; }
        public string? VideoCodec { get; set; }
        public string? AudioCodec { get; set; }
        public string Error { get; set; } = string.Empty;
    }
}
