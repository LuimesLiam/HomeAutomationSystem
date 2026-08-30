namespace HomeApp.Videos.DTOs;

/// <summary>
/// Result of a sync operation
/// </summary>
public record SyncResultDto
{
    public int TotalFound { get; init; }
    public int Added { get; init; }
    public int Skipped { get; init; }
    public int Failed { get; init; }
    public int ApiCallsUsed { get; init; }
    public int ApiCallsRemaining { get; init; }
    public List<string> Errors { get; init; } = new();
    public TimeSpan Duration { get; init; }
    public bool IsComplete { get; init; }
    public string? ContinuationToken { get; init; }
}

/// <summary>
/// Progress update during sync
/// </summary>
public record SyncProgressDto
{
    public int Processed { get; init; }
    public int Total { get; init; }
    public int CurrentBatch { get; init; }
    public int TotalBatches { get; init; }
    public string Status { get; init; } = "Processing";
    public double ProgressPercentage => Total > 0 ? (double)Processed / Total * 100 : 0;
}

/// <summary>
/// Request to start a sync operation
/// </summary>
public record SyncRequestDto
{
    public int BatchSize { get; init; } = 100;
    public bool ForceRefresh { get; init; } = false;
    public string? ContinuationToken { get; init; }
}
