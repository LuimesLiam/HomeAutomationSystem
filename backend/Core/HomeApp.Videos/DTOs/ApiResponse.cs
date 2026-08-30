namespace HomeApp.Videos.DTOs;

/// <summary>
/// Standard API response wrapper
/// </summary>
public record ApiResponse<T>
{
    public bool Success { get; init; }
    public T? Data { get; init; }
    public string? Message { get; init; }
    public List<string> Errors { get; init; } = new();
    public PaginationInfo? Pagination { get; init; }

    public static ApiResponse<T> Ok(T data, string? message = null) => new()
    {
        Success = true,
        Data = data,
        Message = message
    };

    public static ApiResponse<T> Ok(T data, PaginationInfo pagination, string? message = null) => new()
    {
        Success = true,
        Data = data,
        Message = message,
        Pagination = pagination
    };

    public static ApiResponse<T> Fail(string message, List<string>? errors = null) => new()
    {
        Success = false,
        Message = message,
        Errors = errors ?? new()
    };
}

/// <summary>
/// Pagination information for list responses
/// </summary>
public record PaginationInfo
{
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalItems { get; init; }
    public int TotalPages => (int)Math.Ceiling((double)TotalItems / PageSize);
    public bool HasPrevious => Page > 1;
    public bool HasNext => Page < TotalPages;
}

/// <summary>
/// Request parameters for paginated lists
/// </summary>
public record PaginationRequest
{
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
    public string? SortBy { get; init; }
    public bool SortDescending { get; init; } = false;
    public string? Search { get; init; }
}
