namespace SalesCom.Api.IntegrationTests.Infrastructure;

using SalesCom.Application.Interfaces;

/// <summary>
/// Test double for the Central Login service. Rejects every credential and auth token, which is
/// the behavior the anonymous-endpoint integration tests exercise.
/// </summary>
internal sealed class StubCentralLoginClient : ICentralLoginClient
{
    public Task<CentralLoginResult> LoginAsync(string userName, string password, bool rememberMe, CancellationToken cancellationToken) =>
        Task.FromResult(new CentralLoginResult(CentralLoginStatus.Rejected, null, null, null));

    public Task<CentralLoginResult> VerifyAuthTokenAsync(string authToken, CancellationToken cancellationToken) =>
        Task.FromResult(new CentralLoginResult(CentralLoginStatus.Rejected, null, null, null));
}
