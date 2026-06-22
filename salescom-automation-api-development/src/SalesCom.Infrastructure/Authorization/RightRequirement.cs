namespace SalesCom.Infrastructure.Authorization;

using Microsoft.AspNetCore.Authorization;

/// <summary>
/// Authorization requirement: the caller must have the given integer right id in their granted set.
/// The policy name is the integer rendered as a string — see <see cref="RightPolicyProvider"/>.
/// </summary>
public sealed record RightRequirement(int Right) : IAuthorizationRequirement;
