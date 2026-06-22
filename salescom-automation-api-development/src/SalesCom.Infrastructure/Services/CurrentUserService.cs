namespace SalesCom.Infrastructure.Services;

using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using SalesCom.Application.Interfaces;
using SalesCom.Infrastructure.Configurations;

/// <summary>
/// Reads the authenticated user from <see cref="HttpContext"/> using the claim names configured in
/// <see cref="JwtConfiguration"/> (UserId, UserName, Email).
/// </summary>
internal sealed class CurrentUserService(
    IHttpContextAccessor httpContextAccessor,
    IOptions<JwtConfiguration> jwtOptions) : ICurrentUser
{
    private readonly JwtConfiguration _jwt = jwtOptions.Value;

    public bool IsAuthenticated => User?.Identity?.IsAuthenticated == true;

    public string UserId => User?.FindFirstValue(_jwt.UserIdClaim) ?? string.Empty;

    public string UserName => User?.FindFirstValue(_jwt.UserNameClaim) ?? string.Empty;

    public string? Email
    {
        get
        {
            var raw = User?.FindFirstValue(_jwt.EmailClaim);
            return string.IsNullOrWhiteSpace(raw) ? null : raw;
        }
    }

    private ClaimsPrincipal? User => httpContextAccessor.HttpContext?.User;
}
