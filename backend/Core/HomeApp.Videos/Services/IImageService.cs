namespace HomeApp.Videos.Services;

/// <summary>
/// Interface for image processing service
/// </summary>
public interface IImageService
{
    /// <summary>
    /// Download an image from URL and optimize it (resize, compress)
    /// </summary>
    /// <param name="url">Source image URL</param>
    /// <param name="maxWidth">Maximum width in pixels</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Base64 data URL or null if failed</returns>
    Task<string?> DownloadAndOptimizeAsync(string url, int maxWidth = 200, CancellationToken ct = default);
    
    /// <summary>
    /// Optimize an existing image
    /// </summary>
    byte[] Optimize(byte[] imageData, int maxWidth = 200);
    
    /// <summary>
    /// Convert image bytes to a data URL
    /// </summary>
    string ToDataUrl(byte[] imageData, string mimeType = "image/jpeg");
}
