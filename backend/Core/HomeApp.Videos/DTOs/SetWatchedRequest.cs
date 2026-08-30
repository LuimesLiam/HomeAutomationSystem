namespace HomeApp.Videos.DTOs;

public record SetWatchedRequest
{
    public bool Watched { get; init; }
}
