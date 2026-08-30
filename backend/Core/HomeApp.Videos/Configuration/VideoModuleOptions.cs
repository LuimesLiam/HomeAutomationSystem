using Microsoft.Extensions.Configuration;

namespace HomeApp.Videos.Configuration;

public sealed class VideoModuleOptions
{
    public string PostgresConnection { get; set; } = string.Empty;
    public string MoviePathRoot { get; set; } = string.Empty;
    public string[] MovieSourcePaths { get; set; } = [];
    public string[] TvSourcePaths { get; set; } = [];
    public string MediaBaseUrl { get; set; } = "/media";
    public string? HomeMediaBaseUrl { get; set; }
    public string VlcControlUrl { get; set; } = string.Empty;
    public string VlcControlToken { get; set; } = string.Empty;
    public string VlcPath { get; set; } = string.Empty;
    public string OmdbApiKey { get; set; } = string.Empty;
    public int OmdbDailyLimit { get; set; } = 1000;

    public static VideoModuleOptions FromConfiguration(IConfiguration configuration)
    {
        var legacyMediaRoot = GetRequiredValue(configuration, "MOVIE_PATH_ROOT");

        return new VideoModuleOptions
        {
            PostgresConnection = GetRequiredValue(
                configuration,
                "POSTGRES_CONNECTION",
                "ConnectionStrings:DefaultConnection"),
            MoviePathRoot = legacyMediaRoot,
            MovieSourcePaths = GetMediaSourcePaths(configuration, "MOVIE_SOURCE_PATHS", "Movies", legacyMediaRoot),
            TvSourcePaths = GetMediaSourcePaths(configuration, "TV_SOURCE_PATHS", "TV", legacyMediaRoot),
            MediaBaseUrl = NormalizeBaseUrl(GetRequiredValue(configuration, "MEDIA_BASE_URL")),
            HomeMediaBaseUrl = NormalizeOptionalBaseUrl(configuration["HOME_MEDIA_BASE_URL"]),
            VlcControlUrl = GetRequiredValue(configuration, "VLC_CONTROL_URL"),
            VlcControlToken = configuration["VLC_CONTROL_TOKEN"]?.Trim() ?? string.Empty,
            VlcPath = GetRequiredValue(configuration, "VLC_PATH"),
            OmdbApiKey = configuration["OMDB_API_KEY"]?.Trim() ?? string.Empty,
            OmdbDailyLimit = GetIntValue(configuration, "OMDB_DAILY_LIMIT", 1000)
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
            $"Missing required video configuration value. Expected one of: {string.Join(", ", keys)}");
    }

    private static int GetIntValue(IConfiguration configuration, string key, int fallback)
    {
        return int.TryParse(configuration[key], out var value) ? value : fallback;
    }

    private static string[] GetMediaSourcePaths(
        IConfiguration configuration,
        string explicitKey,
        string legacyChildFolder,
        string legacyRoot)
    {
        var explicitPaths = SplitPathCsv(configuration[explicitKey]);
        if (explicitPaths.Length > 0)
        {
            return explicitPaths;
        }

        return [Path.GetFullPath(Path.Combine(legacyRoot, legacyChildFolder))];
    }

    private static string NormalizeBaseUrl(string value)
    {
        var trimmed = value.Trim().TrimEnd('/');
        if (trimmed.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            trimmed.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
            trimmed.StartsWith("/", StringComparison.Ordinal))
        {
            return trimmed;
        }

        return $"http://{trimmed}";
    }

    private static string? NormalizeOptionalBaseUrl(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : NormalizeBaseUrl(value);
    }

    private static string[] SplitPathCsv(string? value)
    {
        return SplitRawCsv(value)
            .Select(Path.GetFullPath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string[] SplitRawCsv(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return [];
        }

        return value
            .Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
}
