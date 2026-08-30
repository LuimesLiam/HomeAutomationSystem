using HomeApp.Videos.DTOs;

namespace HomeApp.Videos.Services;

/// <summary>
/// Interface for media synchronization service
/// </summary>
public interface IMediaSyncService
{
    /// <summary>
    /// Sync movies from filesystem to database in batches
    /// </summary>
    Task<SyncResultDto> SyncMoviesAsync(SyncRequestDto request, CancellationToken ct = default);
    
    /// <summary>
    /// Sync TV series and episodes from filesystem to database in batches
    /// </summary>
    Task<SyncResultDto> SyncTvShowsAsync(SyncRequestDto request, CancellationToken ct = default);
    
    /// <summary>
    /// Get the current sync status
    /// </summary>
    SyncProgressDto? GetCurrentProgress();
    
    /// <summary>
    /// Check if a sync operation is currently running
    /// </summary>
    bool IsSyncInProgress { get; }
}
