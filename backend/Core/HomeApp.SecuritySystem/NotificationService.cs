using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace HomeApp.SecuritySystem;

public class NotificationService
{
    private readonly string _webhookUrl;
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(SecuritySystemOptions options, ILogger<NotificationService> logger)
    {
        _webhookUrl = options.DiscordWebhookUrl;
        _logger = logger;
    }

    /// <summary>Send a plain-text ping.</summary>
    public Task NotifyUserAsync(string message)
        => SendMessageAsync(message);

    /// <summary>Send a ping AND upload a file.</summary>
    public Task NotifyUserWithVideoAsync(string message, string filePath)
        => SendFileAsync(message, filePath);


    private async Task SendMessageAsync(string message)
    {
        if (string.IsNullOrWhiteSpace(_webhookUrl))
        {
            _logger.LogWarning("Discord webhook is not configured. Skipping notification.");
            return;
        }

        using var client = new HttpClient();
        var payload = new { content = message };
        using var json = new StringContent(
            JsonSerializer.Serialize(payload),
            Encoding.UTF8, 
            "application/json"
        );

        // add ?wait=true if you ever need Discord's response payload
        var url = _webhookUrl.Contains("?") 
            ? _webhookUrl + "&wait=true" 
            : _webhookUrl + "?wait=true";

        var resp = await client.PostAsync(url, json);
        resp.EnsureSuccessStatusCode();
    }

    private async Task SendFileAsync(string message, string filePath)
    {
        if (string.IsNullOrWhiteSpace(_webhookUrl))
        {
            _logger.LogWarning("Discord webhook is not configured. Skipping notification upload.");
            return;
        }

        if (!File.Exists(filePath))
            throw new FileNotFoundException("Attachment not found", filePath);

        var ext = Path.GetExtension(filePath).ToLowerInvariant();
        var mediaType = ext switch
        {
            ".mp4" => "video/mp4",
            ".mov" => "video/quicktime",
            ".webm" => "video/webm",
            _      => "application/octet-stream"
        };

        using var client = new HttpClient();
        using var form   = new MultipartFormDataContent();

        form.Add(new StringContent(message), "content");

        using var fs = File.OpenRead(filePath);
        var streamContent = new StreamContent(fs);
        streamContent.Headers.ContentType = new MediaTypeHeaderValue(mediaType);
        form.Add(streamContent, "file", Path.GetFileName(filePath));

        var url = _webhookUrl.Contains("?") 
            ? _webhookUrl + "&wait=true" 
            : _webhookUrl + "?wait=true";

        var resp = await client.PostAsync(url, form);
        resp.EnsureSuccessStatusCode();
    }
}
