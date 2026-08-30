using Microsoft.Extensions.Configuration;

namespace HomeApp.SecuritySystem;

public sealed class SecuritySystemOptions
{
    public string DiscordWebhookUrl { get; set; } = string.Empty;
    public string CameraPublisherEndpoint { get; set; } = string.Empty;
    public string CameraControlEndpoint { get; set; } = string.Empty;
    public string RecordedVideoOutputFolder { get; set; } = string.Empty;

    public static SecuritySystemOptions FromConfiguration(IConfiguration configuration)
    {
        return new SecuritySystemOptions
        {
            DiscordWebhookUrl = configuration["DISCORD_WEBHOOK_URL"]?.Trim() ?? string.Empty,
            CameraPublisherEndpoint = GetRequiredValue(configuration, "CAMERA_PUBLISHER_ENDPOINT"),
            CameraControlEndpoint = GetRequiredValue(configuration, "CAMERA_CONTROL_ENDPOINT"),
            RecordedVideoOutputFolder = GetRequiredValue(configuration, "RECORDED_VIDEO_OUTPUT_FOLDER")
        };
    }

    private static string GetRequiredValue(IConfiguration configuration, params string[] keys)
    {
        foreach (var key in keys)
        {
            var value = configuration[key]?.Trim();
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        throw new InvalidOperationException(
            $"Missing required security system configuration value. Expected one of: {string.Join(", ", keys)}");
    }
}
