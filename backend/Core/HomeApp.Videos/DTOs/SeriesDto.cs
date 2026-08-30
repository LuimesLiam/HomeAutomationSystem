namespace HomeApp.Videos.DTOs;

/// <summary>
/// Data transfer object for TV series information
/// </summary>
public record SeriesDto
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string Genre { get; init; } = "Unknown";
    public string? ThumbnailUrl { get; init; }
    public List<EpisodeDto> Episodes { get; init; } = new();
}

/// <summary>
/// DTO for series card display
/// </summary>
public record SeriesCardDto
{
    public int Id { get; init; }
    public string Title { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string Genre { get; init; } = "Unknown";
    public string? ThumbnailUrl { get; init; }
    public int EpisodeCount { get; init; }
    public int SeasonCount { get; init; }
}

/// <summary>
/// Data transfer object for episode information
/// </summary>
public record EpisodeDto
{
    public int Id { get; init; }
    public int SeriesId { get; init; }
    public string Title { get; init; } = string.Empty;
    public string? Description { get; init; }
    public int Season { get; init; }
    public int EpisodeNumber { get; init; }
    public string FilePath { get; init; } = string.Empty;
    public string VideoUrl { get; init; } = string.Empty;
    public string? ThumbnailUrl { get; init; }
    public bool Watched { get; init; }
}

/// <summary>
/// DTO for episode list item (minimal data)
/// </summary>
public record EpisodeListItemDto
{
    public int Id { get; init; }
    public string Title { get; init; } = string.Empty;
    public string SeriesTitle { get; init; } = string.Empty;
    public int Season { get; init; }
    public int EpisodeNumber { get; init; }
    public string FilePath { get; init; } = string.Empty;
    public string VideoUrl { get; init; } = string.Empty;
    public bool Watched { get; init; }
}
