using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Formats.Jpeg;
using Microsoft.Extensions.Logging;

namespace HomeApp.Videos.Services;

/// <summary>
/// Service for downloading and optimizing images
/// </summary>
public class ImageService : IImageService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ImageService> _logger;
    private readonly SemaphoreSlim _downloadLimiter = new(5); // Limit concurrent downloads

    public ImageService(HttpClient httpClient, ILogger<ImageService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<string?> DownloadAndOptimizeAsync(string url, int maxWidth = 200, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(url))
            return null;

        await _downloadLimiter.WaitAsync(ct);
        try
        {
            var bytes = await _httpClient.GetByteArrayAsync(url, ct);
            var optimized = Optimize(bytes, maxWidth);
            return ToDataUrl(optimized);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to download image from {Url}", url);
            return null;
        }
        finally
        {
            _downloadLimiter.Release();
        }
    }

    public byte[] Optimize(byte[] imageData, int maxWidth = 200)
    {
        try
        {
            using var image = Image.Load(imageData);
            
            // Calculate new dimensions maintaining aspect ratio
            var ratio = (double)maxWidth / image.Width;
            var newWidth = maxWidth;
            var newHeight = (int)(image.Height * ratio);

            // Don't upscale
            if (ratio > 1)
            {
                newWidth = image.Width;
                newHeight = image.Height;
            }

            image.Mutate(x => x.Resize(newWidth, newHeight));

            using var output = new MemoryStream();
            image.Save(output, new JpegEncoder { Quality = 80 });
            return output.ToArray();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to optimize image");
            return imageData; // Return original if optimization fails
        }
    }

    public string ToDataUrl(byte[] imageData, string mimeType = "image/jpeg")
    {
        return $"data:{mimeType};base64,{Convert.ToBase64String(imageData)}";
    }
}
