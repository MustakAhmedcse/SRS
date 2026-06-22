namespace SalesCom.Api.IntegrationTests.Infrastructure;

using System.Text.Json.Serialization;

/// <summary>Mirror of the API's <c>ApiResponse</c> envelope, for test-side deserialisation.</summary>
public sealed class ApiResponseDto<T>
{
    [JsonPropertyName("success")] public bool Success { get; set; }
    [JsonPropertyName("message")] public string? Message { get; set; }
    [JsonPropertyName("data")] public T? Data { get; set; }
    [JsonPropertyName("errorCode")] public string? ErrorCode { get; set; }
}

public sealed class ApiResponseDto
{
    [JsonPropertyName("success")] public bool Success { get; set; }
    [JsonPropertyName("message")] public string? Message { get; set; }
    [JsonPropertyName("errorCode")] public string? ErrorCode { get; set; }
}
