namespace HomeApp.Videos.Data;

public class Episode
{
    public int Id { get; set; }
    public int SeriesId { get; set; }
    public Series? Series { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int Season { get; set; }
    public int EpisodeNumber { get; set; }
    public string FilePath { get; set; } = string.Empty;
    public string VideoUrl { get; set; } = string.Empty;
    public string? Thumbnail { get; set; }
    public bool Hidden { get; set; }
    public bool Watched { get; set; }
}
