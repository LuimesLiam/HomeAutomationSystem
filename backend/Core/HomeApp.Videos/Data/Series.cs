using System.Collections.Generic;

namespace HomeApp.Videos.Data;

public class Series
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Genre { get; set; }
    public string? Thumbnail { get; set; }
    public ICollection<Episode> Episodes { get; set; } = new List<Episode>();
    public bool Hidden { get; set; }
}
