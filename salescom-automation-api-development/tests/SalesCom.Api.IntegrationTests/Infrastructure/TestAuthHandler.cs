namespace SalesCom.Api.IntegrationTests.Infrastructure;

using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

/// <summary>
/// Test-only authentication handler. Stamps the application's claim shape (UserId, UserName, Email)
/// for a fixed central user id, so [Authorize] endpoints and the claims-based current-user work
/// without a real token. The user's rights are seeded into the DB by <see cref="TestDataSeeder"/>,
/// since [HasRight] enforcement is DB-backed.
/// </summary>
internal sealed class TestAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "Test";

    /// <summary>The central user id of the seeded test user.</summary>
    public const string TestUserId = "1";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var claims = new List<Claim>
        {
            new("UserId", TestUserId),
            new("UserName", "test"),
            new("Email", "test@example.com"),
        };

        var identity = new ClaimsIdentity(claims, SchemeName);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, SchemeName);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
