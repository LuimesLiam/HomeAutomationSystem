using System;

namespace HomeApp.Videos.Data;

public class Movie
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Year { get; set; }
    public string? Genre { get; set; }
    public string? Plot { get; set; }
    public string FilePath { get; set; } = string.Empty;
    public string VideoUrl { get; set; } = string.Empty;
    public string Thumbnail { get; set; } = string.Empty;
    public bool Hidden { get; set; }
    public bool Watched { get; set; }
}
