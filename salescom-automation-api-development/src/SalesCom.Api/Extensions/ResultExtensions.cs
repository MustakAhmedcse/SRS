namespace SalesCom.Api.Extensions;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SalesCom.Application.Common;
using SalesCom.Domain.Common;

/// <summary>
/// Maps <see cref="Result"/> / <see cref="Result{T}"/> to HTTP responses wrapped in the unified
/// <see cref="ApiResponse"/> / <see cref="ApiResponse{T}"/> envelope. HTTP status codes still
/// follow REST semantics — only the response body shape is unified.
/// </summary>
internal static class ResultExtensions
{
    public static IActionResult ToApiResponse(
        this Result result,
        ControllerBase controller,
        string successMessage = "Success",
        int successStatus = StatusCodes.Status200OK)
    {
        if (result.IsSuccess)
        {
            return controller.StatusCode(successStatus, ApiResponse.Ok(successMessage));
        }

        var status = MapStatus(result.Error.Type);
        return controller.StatusCode(status, ApiResponse.Fail(result.Error.Message, result.Error.Code));
    }

    public static IActionResult ToApiResponse<T>(
        this Result<T> result,
        ControllerBase controller,
        string successMessage = "Success",
        int successStatus = StatusCodes.Status200OK)
    {
        if (result.IsSuccess)
        {
            return controller.StatusCode(successStatus, ApiResponse<T>.Ok(result.Value, successMessage));
        }

        var status = MapStatus(result.Error.Type);
        return controller.StatusCode(status, ApiResponse<T>.Fail(result.Error.Message, result.Error.Code));
    }

    private static int MapStatus(ErrorType type) => type switch
    {
        ErrorType.Validation => StatusCodes.Status400BadRequest,
        ErrorType.NotFound => StatusCodes.Status404NotFound,
        ErrorType.Conflict => StatusCodes.Status409Conflict,
        ErrorType.Unauthorized => StatusCodes.Status401Unauthorized,
        ErrorType.Forbidden => StatusCodes.Status403Forbidden,
        _ => StatusCodes.Status500InternalServerError,
    };
}
