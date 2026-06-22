namespace SalesCom.Application.Common;

/// <summary>
/// Unified API response envelope for endpoints that return a payload.
/// Minimal by design: <see cref="Success"/> tells the client which branch to take,
/// <see cref="Message"/> carries the human-readable description (success message or full error
/// detail, including multi-field validation joined with "; "), <see cref="Data"/> holds the
/// payload on success, <see cref="ErrorCode"/> the stable machine code on failure.
/// </summary>
public sealed record ApiResponse<T>
{
    public required bool Success { get; init; }

    public required string Message { get; init; }

    /// <summary>Payload on success. Serialized as omitted when null.</summary>
    public T? Data { get; init; }

    /// <summary>Stable machine code, e.g. <c>User.InvalidCredentials</c>. Null on success.</summary>
    public string? ErrorCode { get; init; }

    public static ApiResponse<T> Ok(T data, string message = "Success") => new()
    {
        Success = true,
        Message = message,
        Data = data,
    };

    public static ApiResponse<T> Fail(string message, string? errorCode = null) => new()
    {
        Success = false,
        Message = message,
        ErrorCode = errorCode,
    };
}

/// <summary>
/// Unified API response envelope for endpoints that produce no payload (commands without a
/// return value, status updates, deletions). Same shape as <see cref="ApiResponse{T}"/> minus
/// the <c>Data</c> field.
/// </summary>
public sealed record ApiResponse
{
    public required bool Success { get; init; }

    public required string Message { get; init; }

    public string? ErrorCode { get; init; }

    public static ApiResponse Ok(string message = "Success") => new()
    {
        Success = true,
        Message = message,
    };

    public static ApiResponse Fail(string message, string? errorCode = null) => new()
    {
        Success = false,
        Message = message,
        ErrorCode = errorCode,
    };
}
