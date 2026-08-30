using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using HomeApp.Videos.Configuration;
using HomeApp.Videos.Services;

namespace HomeApp.Videos.Logic;

public class VideoItem
{
    public string Type { get; set; } = "video";
    public required string Name { get; set; }
    public required string Path { get; set; }
    public string? FileExtension { get; set; }
    public string? VideoUrl { get; set; }
    public string? Thumbnail { get; set; }
    public string? MountPath { get; set; }
}

public class VideoManager
{
    public const string BrowserAddressMode = "browser";
    public const string HomeAddressMode = "home";

    private readonly VideoModuleOptions _options;
    private readonly MediaSourceService _mediaSourceService;
    private readonly string _mediaBaseUrl;
    private readonly string? _homeMediaBaseUrl;
    private readonly string[] _videoExtensions = [".mp4", ".avi", ".mkv"];
    private readonly string _ffmpegPath;
    private readonly string _ffprobePath;
    private readonly string _vlcPath;

    public VideoManager(VideoModuleOptions options, MediaSourceService mediaSourceService, string ffmpegPath = "/usr/bin/ffmpeg", string ffprobePath = "/usr/bin/ffprobe")
    {
        _options = options;
        _mediaSourceService = mediaSourceService;
        _mediaBaseUrl = options.MediaBaseUrl.TrimEnd('/');
        _homeMediaBaseUrl = options.HomeMediaBaseUrl?.TrimEnd('/');
        _vlcPath = options.VlcPath;
        _ffmpegPath = ffmpegPath;
        _ffprobePath = ffprobePath;
    }

    public string[] GetMovieSourceDirectories() => _mediaSourceService.GetMovieSourcePaths();

    public string[] GetTvSourceDirectories() => _mediaSourceService.GetTvSourcePaths();

    public IEnumerable<string> GetAllSourceDirectories() => GetMovieSourceDirectories().Concat(GetTvSourceDirectories());

    private IEnumerable<string> SafeEnumerateFiles(string path, string searchPattern, SearchOption searchOption)
    {
        var files = new List<string>();
        SafeEnumerateFilesRecursive(path, searchPattern, searchOption, files);
        return files;
    }

    private void SafeEnumerateFilesRecursive(string path, string searchPattern, SearchOption searchOption, List<string> files)
    {
        try
        {
            foreach (var file in Directory.EnumerateFiles(path, searchPattern))
            {
                files.Add(file);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error enumerating files in {path}: {ex.Message}");
        }

        if (searchOption == SearchOption.AllDirectories)
        {
            try
            {
                foreach (var dir in Directory.EnumerateDirectories(path))
                {
                    SafeEnumerateFilesRecursive(dir, searchPattern, searchOption, files);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error enumerating directories in {path}: {ex.Message}");
            }
        }
    }

    private IEnumerable<string> SafeEnumerateDirectories(string path, string searchPattern, SearchOption searchOption)
    {
        var dirs = new List<string>();
        SafeEnumerateDirectoriesRecursive(path, searchPattern, searchOption, dirs);
        return dirs;
    }

    private void SafeEnumerateDirectoriesRecursive(string path, string searchPattern, SearchOption searchOption, List<string> dirs)
    {
        try
        {
            foreach (var dir in Directory.EnumerateDirectories(path, searchPattern))
            {
                dirs.Add(dir);
                if (searchOption == SearchOption.AllDirectories)
                {
                    SafeEnumerateDirectoriesRecursive(dir, searchPattern, searchOption, dirs);
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error enumerating directories in {path}: {ex.Message}");
        }
    }

    public List<VideoItem> ListAllMovies()
    {
        var items = new List<VideoItem>();

        foreach (var moviesDir in GetMovieSourceDirectories().Where(Directory.Exists))
        {
            foreach (var file in SafeEnumerateFiles(moviesDir, "*.*", SearchOption.AllDirectories)
                .Where(f => _videoExtensions.Contains(Path.GetExtension(f).ToLowerInvariant())))
            {
                try
                {
                    var movieName = Path.GetFileNameWithoutExtension(file);
                    items.Add(CreateVideoItem(file, movieName));
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error processing movie file {file}: {ex.Message}");
                }
            }
        }

        return items;
    }

    public class TvEpisodeItem : VideoItem
    {
        public string Series { get; set; } = string.Empty;
        public int Season { get; set; }
        public int Episode { get; set; }
    }

    public List<TvEpisodeItem> ListAllEpisodes()
    {
        var items = new List<TvEpisodeItem>();

        foreach (var tvDir in GetTvSourceDirectories().Where(Directory.Exists))
        {
            foreach (var file in SafeEnumerateFiles(tvDir, "*.*", SearchOption.AllDirectories)
                .Where(f => _videoExtensions.Contains(Path.GetExtension(f).ToLowerInvariant())))
            {
                try
                {
                    var epName = Path.GetFileNameWithoutExtension(file);

                    var seasonEpisodeMatch = System.Text.RegularExpressions.Regex.Match(
                        epName,
                        @"[._\s-]?S(\d+)[._\s-]?E(\d+)",
                        System.Text.RegularExpressions.RegexOptions.IgnoreCase);

                    if (!seasonEpisodeMatch.Success)
                    {
                        continue;
                    }

                    var seasonNumber = int.Parse(seasonEpisodeMatch.Groups[1].Value);
                    var episodeNumber = int.Parse(seasonEpisodeMatch.Groups[2].Value);

                    var seriesNameFromFile = epName.Substring(0, seasonEpisodeMatch.Index).Trim();
                    seriesNameFromFile = System.Text.RegularExpressions.Regex.Replace(seriesNameFromFile, @"[._]+", " ").Trim();

                    if (string.IsNullOrWhiteSpace(seriesNameFromFile) || seriesNameFromFile.Length < 2)
                    {
                        var relative = Path.GetRelativePath(tvDir, file);
                        var parts = relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                        seriesNameFromFile = FindSeriesNameFromPath(parts);
                    }

                    if (string.IsNullOrWhiteSpace(seriesNameFromFile))
                    {
                        continue;
                    }

                    var baseItem = CreateVideoItem(file, epName);
                    items.Add(new TvEpisodeItem
                    {
                        Series = seriesNameFromFile,
                        Season = seasonNumber,
                        Episode = episodeNumber,
                        Type = baseItem.Type,
                        Name = baseItem.Name,
                        Path = baseItem.Path,
                        FileExtension = baseItem.FileExtension,
                        VideoUrl = baseItem.VideoUrl,
                        Thumbnail = baseItem.Thumbnail,
                        MountPath = baseItem.MountPath
                    });
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error processing TV episode file {file}: {ex.Message}");
                }
            }
        }

        return items;
    }

    private string FindSeriesNameFromPath(string[] pathParts)
    {
        var categoryFolders = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "new", "old", "temp", "complete", "incomplete", "downloading", "watch", "archive"
        };

        foreach (var part in pathParts.Take(pathParts.Length - 1))
        {
            if (categoryFolders.Contains(part))
            {
                continue;
            }

            if (System.Text.RegularExpressions.Regex.IsMatch(part, @"^S(eason)?\s*\d+", System.Text.RegularExpressions.RegexOptions.IgnoreCase))
            {
                continue;
            }

            if (System.Text.RegularExpressions.Regex.IsMatch(part, @"\.S\d+\.", System.Text.RegularExpressions.RegexOptions.IgnoreCase))
            {
                var match = System.Text.RegularExpressions.Regex.Match(part, @"^(.+?)\.S\d+", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                if (match.Success)
                {
                    return match.Groups[1].Value.Replace(".", " ").Trim();
                }
            }

            if (part.Length > 2)
            {
                return part.Replace(".", " ").Replace("_", " ").Trim();
            }
        }

        return string.Empty;
    }

    public List<VideoItem> ListVideos(string? path, string? search)
    {
        var dir = string.IsNullOrEmpty(path) ? GetAllSourceDirectories().FirstOrDefault() ?? string.Empty : path;
        if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir))
        {
            throw new DirectoryNotFoundException("Directory does not exist");
        }

        var items = new List<VideoItem>();
        var searchQuery = (search ?? string.Empty).Trim().ToLowerInvariant();

        if (!string.IsNullOrEmpty(searchQuery))
        {
            foreach (var folder in SafeEnumerateDirectories(dir, "*", SearchOption.AllDirectories))
            {
                try
                {
                    var folderName = Path.GetFileName(folder);
                    if (folderName.ToLowerInvariant().Contains(searchQuery))
                    {
                        items.Add(new VideoItem
                        {
                            Type = "folder",
                            Name = folderName,
                            Path = folder
                        });
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error processing folder {folder}: {ex.Message}");
                }
            }

            foreach (var file in SafeEnumerateFiles(dir, "*.*", SearchOption.AllDirectories)
                .Where(f => _videoExtensions.Contains(Path.GetExtension(f).ToLowerInvariant())))
            {
                try
                {
                    var movieName = Path.GetFileNameWithoutExtension(file);
                    if (movieName.ToLowerInvariant().Contains(searchQuery))
                    {
                        items.Add(CreateVideoItem(file, movieName));
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error processing video file {file}: {ex.Message}");
                }
            }
        }
        else
        {
            foreach (var folder in Directory.GetDirectories(dir))
            {
                try
                {
                    items.Add(new VideoItem
                    {
                        Type = "folder",
                        Name = Path.GetFileName(folder),
                        Path = folder
                    });
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error processing folder {folder}: {ex.Message}");
                }
            }

            foreach (var file in Directory.GetFiles(dir)
                .Where(f => _videoExtensions.Contains(Path.GetExtension(f).ToLowerInvariant())))
            {
                try
                {
                    var movieName = Path.GetFileNameWithoutExtension(file);
                    items.Add(CreateVideoItem(file, movieName));
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error processing video file {file}: {ex.Message}");
                }
            }
        }

        return items;
    }

    private VideoItem CreateVideoItem(string filePath, string movieName)
    {
        var ext = Path.GetExtension(filePath);
        return new VideoItem
        {
            Type = "video",
            Name = movieName,
            FileExtension = ext,
            MountPath = filePath,
            VideoUrl = BuildMediaUrl(filePath),
            Thumbnail = null,
            Path = BuildPlaybackPath(filePath)
        };
    }

    public string BuildMediaUrl(string filePath)
    {
        return BuildMediaUrl(filePath, _mediaBaseUrl);
    }

    public string BuildMediaUrl(string filePath, string baseUrl)
    {
        var relativePath = GetRelativeMediaPath(filePath);
        return $"{baseUrl.TrimEnd('/')}/{relativePath}";
    }

    public string BuildMediaUrlForBrowser(string filePath, string requestOrigin)
    {
        return BuildMediaUrl(filePath, $"{requestOrigin.TrimEnd('/')}/media");
    }

    public string? BuildMediaUrlForHome(string filePath)
    {
        return string.IsNullOrWhiteSpace(_homeMediaBaseUrl)
            ? null
            : BuildMediaUrl(filePath, _homeMediaBaseUrl);
    }

    public bool TryResolveMediaPath(string requestPath, out string fullPath)
    {
        fullPath = string.Empty;

        if (string.IsNullOrWhiteSpace(requestPath))
        {
            return false;
        }

        var normalized = requestPath.Replace('\\', '/').Trim('/');
        var segments = normalized.Split('/', 3, StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length < 3 || !int.TryParse(segments[1], out var sourceIndex))
        {
            return false;
        }

        var root = segments[0].ToLowerInvariant() switch
        {
            "movies" => GetSourceByIndex(GetMovieSourceDirectories(), sourceIndex),
            "tv" => GetSourceByIndex(GetTvSourceDirectories(), sourceIndex),
            _ => null
        };

        if (string.IsNullOrWhiteSpace(root))
        {
            return false;
        }

        var relativePath = segments[2].Replace('/', Path.DirectorySeparatorChar);
        var candidate = Path.GetFullPath(Path.Combine(root, relativePath));
        var normalizedRoot = EnsureTrailingSeparator(Path.GetFullPath(root));
        if (!candidate.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(candidate, root, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        fullPath = candidate;
        return true;
    }

    private string BuildPlaybackPath(string filePath)
    {
        if (string.IsNullOrWhiteSpace(_vlcPath))
        {
            return filePath;
        }

        var legacyRoot = _options.MoviePathRoot?.Trim();
        if (string.IsNullOrWhiteSpace(legacyRoot))
        {
            return filePath;
        }

        var normalizedLegacyRoot = EnsureTrailingSeparator(Path.GetFullPath(legacyRoot));
        var normalizedFilePath = Path.GetFullPath(filePath);
        if (!normalizedFilePath.StartsWith(normalizedLegacyRoot, StringComparison.OrdinalIgnoreCase))
        {
            return filePath;
        }

        var relative = normalizedFilePath[normalizedLegacyRoot.Length..];
        return Path.Combine(_vlcPath, relative);
    }

    private string GetRelativeMediaPath(string filePath)
    {
        if (TryBuildRelativePath("movies", GetMovieSourceDirectories(), filePath, out var movieRelativePath))
        {
            return movieRelativePath;
        }

        if (TryBuildRelativePath("tv", GetTvSourceDirectories(), filePath, out var tvRelativePath))
        {
            return tvRelativePath;
        }

        throw new InvalidOperationException($"File path '{filePath}' is not inside a configured media source.");
    }

    private static bool TryBuildRelativePath(string kind, string[] roots, string filePath, out string relativePath)
    {
        var normalizedFilePath = Path.GetFullPath(filePath);

        for (var index = 0; index < roots.Length; index++)
        {
            var root = roots[index];
            var normalizedRoot = EnsureTrailingSeparator(Path.GetFullPath(root));
            if (!normalizedFilePath.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var relative = normalizedFilePath[normalizedRoot.Length..]
                .Replace(Path.DirectorySeparatorChar, '/')
                .Replace(Path.AltDirectorySeparatorChar, '/');
            relativePath = $"{kind}/{index}/{relative}";
            return true;
        }

        relativePath = string.Empty;
        return false;
    }

    private static string? GetSourceByIndex(string[] sources, int index)
    {
        return index >= 0 && index < sources.Length ? sources[index] : null;
    }

    private static string EnsureTrailingSeparator(string path)
    {
        return path.EndsWith(Path.DirectorySeparatorChar) || path.EndsWith(Path.AltDirectorySeparatorChar)
            ? path
            : path + Path.DirectorySeparatorChar;
    }

    private string GenerateThumbnail(string videoPath, string thumbnailPath)
    {
        try
        {
            if (!File.Exists(_ffprobePath) || !File.Exists(_ffmpegPath))
            {
                var black = new byte[320 * 180 * 3];
                return Convert.ToBase64String(black);
            }

            var ffprobe = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = _ffprobePath,
                    Arguments = $"-v error -show_entries format=duration -of default=noprint_wrappers=1:nokey=1 \"{videoPath}\"",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };
            ffprobe.Start();
            var durationStr = ffprobe.StandardOutput.ReadLine();
            ffprobe.WaitForExit();
            double.TryParse(durationStr, out var duration);
            var middle = duration > 0 ? duration / 2 : 0;

            var ffmpeg = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = _ffmpegPath,
                    Arguments = $"-ss {middle} -i \"{videoPath}\" -frames:v 1 -vf scale=320:180 \"{thumbnailPath}\" -y",
                    RedirectStandardOutput = false,
                    RedirectStandardError = false,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };
            ffmpeg.Start();
            ffmpeg.WaitForExit();

            if (File.Exists(thumbnailPath))
            {
                return Convert.ToBase64String(File.ReadAllBytes(thumbnailPath));
            }
        }
        catch (Exception)
        {
        }

        var blackFallback = new byte[320 * 180 * 3];
        return Convert.ToBase64String(blackFallback);
    }
}
