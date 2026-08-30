namespace HomeApp.Videos.Data;

public class MediaSource
{
    public int Id { get; set; }
    public string SourceType { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public int DisplayOrder { get; set; }
}
