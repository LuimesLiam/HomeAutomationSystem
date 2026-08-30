namespace HomeApp.Videos.DTOs;

/// <summary>
/// Data transfer object for movie information
/// </summary>
public record MovieDto
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string? Year { get; init; }
    public string Genre { get; init; } = "Unknown";
    public string? Plot { get; init; }
    public string FilePath { get; init; } = string.Empty;
    public string VideoUrl { get; init; } = string.Empty;
    public string? ThumbnailUrl { get; init; }
    public bool Watched { get; init; }
}

/// <summary>
/// DTO for movie list with minimal data for performance
/// </summary>
public record MovieListItemDto
{
    public int Id { get; init; }
    public string Title { get; init; } = string.Empty;
    public string? Year { get; init; }
    public string Genre { get; init; } = "Unknown";
    public string FilePath { get; init; } = string.Empty;
    public string VideoUrl { get; init; } = string.Empty;
    public bool Watched { get; init; }
}

/// <summary>
/// DTO for movie card display (includes thumbnail)
/// </summary>
public record MovieCardDto
{
    public int Id { get; init; }
    public string Title { get; init; } = string.Empty;
    public string? Year { get; init; }
    public string Genre { get; init; } = "Unknown";
    public string? ThumbnailUrl { get; init; }
    public string VideoUrl { get; init; } = string.Empty;
    public string FilePath { get; init; } = string.Empty;
    public bool Watched { get; init; }
}
