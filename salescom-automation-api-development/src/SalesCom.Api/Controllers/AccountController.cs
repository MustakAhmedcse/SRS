namespace SalesCom.Api.Controllers;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SalesCom.Api.Extensions;
using SalesCom.Application.Commands.Account.Login;
using SalesCom.Application.Commands.Account.VerifyAuthToken;
using SalesCom.Application.Common;
using SalesCom.Application.Messaging;
using SalesCom.Application.Queries.Account.GetMe;

[ApiController]
[Route("api/account")]
[Produces("application/json")]
public sealed class AccountController(
    ICommandDispatcher commandDispatcher,
    IQueryDispatcher queryDispatcher) : ControllerBase
{
    /// <summary>
    /// Authenticate via the Central Login service. An external user receives
    /// <c>{ authType: "Normal", session }</c> (our access token + granted rights); an internal user
    /// receives <c>{ authType: "SSO", redirectUrl }</c> — redirect the browser there for OTP
    /// verification, then exchange the returned auth token via
    /// <c>POST /api/account/verify-auth-token</c> for a session.
    /// </summary>
    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<LoginResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> LoginAsync([FromBody] LoginCommand command, CancellationToken cancellationToken)
    {
        var result = await commandDispatcher.DispatchAsync(command, cancellationToken);
        return result.ToApiResponse(this, successMessage: "Login successful.");
    }

    /// <summary>
    /// Complete an SSO/2FA login: exchanges the auth token the central OTP page appended to the
    /// redirect-back URL for a session (our access token + granted rights).
    /// </summary>
    [HttpPost("verify-auth-token")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<AuthSession>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> VerifyAuthTokenAsync([FromBody] VerifyAuthTokenCommand command, CancellationToken cancellationToken)
    {
        var result = await commandDispatcher.DispatchAsync(command, cancellationToken);
        return result.ToApiResponse(this, successMessage: "Login successful.");
    }

    /// <summary>Return the authenticated user's profile and granted rights.</summary>
    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<MeResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> MeAsync(CancellationToken cancellationToken)
    {
        var result = await queryDispatcher.DispatchAsync(new GetMeQuery(), cancellationToken);
        return result.ToApiResponse(this);
    }
}
