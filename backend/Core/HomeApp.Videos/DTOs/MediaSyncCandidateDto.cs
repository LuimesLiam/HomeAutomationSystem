namespace HomeApp.Videos.DTOs;

public sealed record MediaSyncCandidateDto
{
    public required string Path { get; init; }
    public required string Name { get; init; }
    public required string MediaType { get; init; }
    public string? Title { get; init; }
    public string? Year { get; init; }
    public string? Series { get; init; }
    public int? Season { get; init; }
    public int? Episode { get; init; }
}

public sealed record SelectedMediaSyncRequest
{
    /// <summary>
    /// Null preserves the legacy "sync the first N" behavior. An empty list
    /// deliberately syncs nothing.
    /// </summary>
    public IReadOnlyList<string>? SelectedPaths { get; init; }
}
