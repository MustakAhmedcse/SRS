namespace SalesCom.Infrastructure.Gateways;

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SalesCom.Application.Interfaces;
using SalesCom.Infrastructure.Configurations;

/// <summary>
/// HTTP gateway to the Central Login service. Wire contract per "API Integration Documentation for
/// Central Login": <c>POST {_config.AuthLoginEndpoint}</c> answers <c>authType "Normal"</c> (immediate
/// <c>userInfo</c> + central tokens) or <c>"SSO"</c> (redirect to the central OTP page); <c>POST
/// {_config.VerifyAuthTokenEndpoint}</c> exchanges the post-OTP auth token for <c>userInfo</c> + tokens.
/// A 400/401 maps to <see cref="CentralLoginStatus.Rejected"/> with the central message (invalid
/// credentials, OTP lockout); transport failures and out-of-contract responses map to
/// <see cref="CentralLoginStatus.Unavailable"/> — this client never throws for a down dependency.
/// </summary>
internal sealed class CentralLoginClient(
    HttpClient httpClient,
    IOptions<CentralLoginConfiguration> options,
    ILogger<CentralLoginClient> logger) : ICentralLoginClient
{
    private const string UnavailableMessage = "Central login service is unavailable. Try again later.";

    private readonly CentralLoginConfiguration _config = options.Value;

    public async Task<CentralLoginResult> LoginAsync(string userName, string password, bool rememberMe, CancellationToken cancellationToken)
    {
        var request = new LoginWireRequest(_config.ApplicationName, _config.ApplicationKey, userName, password, rememberMe);

        try
        {
            using var response = await httpClient.PostAsJsonAsync(_config.AuthLoginEndpoint, request, cancellationToken);
            var body = await ReadBodyAsync<LoginWireData>(response, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return MapFailure(response.StatusCode, body?.Message);
            }

            var data = body?.Data;
            if (data is not null && string.Equals(data.AuthType, "SSO", StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(data.RedirectUrl))
            {
                return new CentralLoginResult(CentralLoginStatus.SsoRedirect, null, data.RedirectUrl, body?.Message);
            }

            if (data is not null && string.Equals(data.AuthType, "Normal", StringComparison.OrdinalIgnoreCase)
                && data.AuthInfo?.UserInfo is { } userInfo)
            {
                return new CentralLoginResult(CentralLoginStatus.Success, Map(userInfo), null, body?.Message);
            }

            logger.LogWarning("Central login returned 200 with an unexpected payload (authType: {AuthType}).", data?.AuthType);
            return new CentralLoginResult(CentralLoginStatus.Unavailable, null, null, UnavailableMessage);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            logger.LogWarning(ex, "Central login call failed for {Endpoint}.", _config.AuthLoginEndpoint);
            return new CentralLoginResult(CentralLoginStatus.Unavailable, null, null, UnavailableMessage);
        }
    }

    public async Task<CentralLoginResult> VerifyAuthTokenAsync(string authToken, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await httpClient.PostAsJsonAsync(
                _config.VerifyAuthTokenEndpoint, new VerifyWireRequest(authToken), cancellationToken);
            var body = await ReadBodyAsync<AuthInfoWire>(response, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return MapFailure(response.StatusCode, body?.Message);
            }

            if (body?.Data?.UserInfo is { } userInfo)
            {
                return new CentralLoginResult(CentralLoginStatus.Success, Map(userInfo), null, body.Message);
            }

            logger.LogWarning("Central verify-auth-token returned 200 without userInfo.");
            return new CentralLoginResult(CentralLoginStatus.Unavailable, null, null, UnavailableMessage);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            logger.LogWarning(ex, "Central login call failed for {Endpoint}.", _config.VerifyAuthTokenEndpoint);
            return new CentralLoginResult(CentralLoginStatus.Unavailable, null, null, UnavailableMessage);
        }
    }

    private static CentralLoginResult MapFailure(HttpStatusCode statusCode, string? message) =>
        statusCode is HttpStatusCode.BadRequest or HttpStatusCode.Unauthorized
            ? new CentralLoginResult(CentralLoginStatus.Rejected, null, null, message)
            : new CentralLoginResult(CentralLoginStatus.Unavailable, null, null, message ?? UnavailableMessage);

    private static async Task<WireEnvelope<T>?> ReadBodyAsync<T>(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            return await response.Content.ReadFromJsonAsync<WireEnvelope<T>>(cancellationToken);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static CentralUserInfo Map(UserInfoWire u) => new(
        UserId: u.UserId,
        LoginName: u.LoginName ?? string.Empty,
        FullName: u.FullName ?? string.Empty,
        Email: NullIfEmpty(u.Email),
        MobileNumber: NullIfEmpty(u.MobileNumber?.Trim()),
        IsInternal: IsYes(u.IsInternal),
        IsLocked: IsYes(u.IsLocked),
        IsActive: IsYes(u.UserStatus),
        CenterId: u.CurrentCenterId > 0 ? u.CurrentCenterId : null,
        Department: NullIfEmpty(u.Department),
        UserGroupId: u.UserGroupId);

    private static bool IsYes(string? flag) => string.Equals(flag?.Trim(), "Y", StringComparison.OrdinalIgnoreCase);

    private static string? NullIfEmpty(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;

    // Request bodies the Central Login API expects, per "API Integration Documentation for Central
    // Login". Property names are pinned with [JsonPropertyName] so the emitted JSON keys are exact and
    // independent of any serializer naming policy. Values come from CentralLoginConfiguration (appsettings).
    private sealed record LoginWireRequest(
        [property: JsonPropertyName("applicationName")] string ApplicationName,
        [property: JsonPropertyName("applicationKey")] string ApplicationKey,
        [property: JsonPropertyName("userName")] string UserName,
        [property: JsonPropertyName("password")] string Password,
        [property: JsonPropertyName("rememberMe")] bool RememberMe);

    private sealed record VerifyWireRequest(
        [property: JsonPropertyName("authToken")] string AuthToken);

    private sealed record WireEnvelope<T>(string? Message, int Status, T? Data);

    private sealed record LoginWireData(string? AuthType, string? RedirectUrl, string? RedirectToken, AuthInfoWire? AuthInfo);

    private sealed record AuthInfoWire(string? AccessToken, string? RefreshToken, int? AccessTokenExpireInMinutes, UserInfoWire? UserInfo);

    private sealed record UserInfoWire(
        int UserId,
        string? FullName,
        string? LoginName,
        string? IsLocked,
        string? IsInternal,
        string? MobileNumber,
        int CurrentCenterId,
        string? Department,
        string? Email,
        int? UserGroupId,
        string? UserStatus);
}
