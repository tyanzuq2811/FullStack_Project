namespace IPM.Application.DTOs.Common;

public record ApiResponse<T>(
    bool Success,
    string? Message,
    T? Data,
    List<string>? Errors = null
)
{
    public static ApiResponse<T> SuccessResponse(T data, string? message = null)
        => new(true, message, data);

    public static ApiResponse<T> FailResponse(string message, List<string>? errors = null)
        => new(false, message, default, errors);
}

public record PagedResult<T>(
    List<T> Items,
    int TotalCount,
    int Page,
    int PageSize,
    int TotalPages
);

public record LeaderboardEntry(
    int Rank,
    Guid MemberId,
    string MemberName,
    double EloScore
);
