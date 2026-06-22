namespace SalesCom.Infrastructure.Configurations;

using System.ComponentModel.DataAnnotations;

/// <summary>
/// Central Login service connection. The application authenticates to it with an application
/// name + key pair carried in every request body; the base URL hosts the
/// <c>account/v1/login</c> and <c>account/v1/verify-auth-token</c> endpoints.
/// </summary>
public sealed record CentralLoginConfiguration
{
    public const string SectionName = "CentralLogin";

    [Required, Url]
    public string BaseUrl { get; init; } = string.Empty;
    [Required, MinLength(5)]
    public string AuthLoginEndpoint { get; init; } = string.Empty;
    [Required, MinLength(5)]
    public string VerifyAuthTokenEndpoint { get; init; } = string.Empty;

    [Required, MinLength(1)]
    public string ApplicationName { get; init; } = string.Empty;

    [Required, MinLength(1)]
    public string ApplicationKey { get; init; } = string.Empty;

    [Range(1, 60)]
    public int TimeoutSeconds { get; init; } = 10;

    /// <summary>
    /// Local-development only: when true the Central Login service is never called. A login is accepted
    /// on username alone (the password is ignored) provided the user exists in the local <c>users</c>
    /// table. Must remain false in every deployed environment.
    /// </summary>
    public bool BypassEnabled { get; init; }
}
