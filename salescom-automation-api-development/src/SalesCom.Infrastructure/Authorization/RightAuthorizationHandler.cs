namespace SalesCom.Infrastructure.Authorization;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using SalesCom.Application.Interfaces;
using SalesCom.Infrastructure.Configurations;

/// <summary>
/// Per-request authorization handler. Reads the caller's <c>UserId</c> claim (the central user id),
/// then queries the DB for the user's granted right ids via <see cref="IUserRightsQuery"/>. Rights are
/// not carried in the token — enforcement is DB-backed, so grant changes apply immediately without
/// re-issuing tokens.
/// </summary>
internal sealed class RightAuthorizationHandler(
    IUserRightsQuery rightsQuery,
    IHttpContextAccessor httpContextAccessor,
    IOptions<JwtConfiguration> jwtOptions) : AuthorizationHandler<RightRequirement>
{
    private readonly string _userIdClaim = jwtOptions.Value.UserIdClaim;

    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, RightRequirement requirement)
    {
        var userId = context.User.FindFirst(_userIdClaim)?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            context.Fail(new AuthorizationFailureReason(this, "Missing UserId claim."));
            return;
        }

        var cancellationToken = httpContextAccessor.HttpContext?.RequestAborted ?? CancellationToken.None;
        var rights = await rightsQuery.GetRightsAsync(userId, cancellationToken);

        if (rights.Contains(requirement.Right))
        {
            context.Succeed(requirement);
        }
    }
}
