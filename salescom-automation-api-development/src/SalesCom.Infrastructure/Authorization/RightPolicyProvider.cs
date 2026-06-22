namespace SalesCom.Infrastructure.Authorization;

using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

/// <summary>
/// Dynamic policy provider: any policy name that parses as an integer is materialized into a
/// <see cref="RightRequirement"/>-backed policy on demand, so controllers can declare
/// <c>[HasRight(1001)]</c> without per-right AddPolicy registrations.
/// </summary>
internal sealed class RightPolicyProvider(IOptions<AuthorizationOptions> options)
    : DefaultAuthorizationPolicyProvider(options)
{
    public override async Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        var existing = await base.GetPolicyAsync(policyName);
        if (existing is not null)
        {
            return existing;
        }

        if (!int.TryParse(policyName, NumberStyles.Integer, CultureInfo.InvariantCulture, out var right))
        {
            return null;
        }

        return new AuthorizationPolicyBuilder()
            .RequireAuthenticatedUser()
            .AddRequirements(new RightRequirement(right))
            .Build();
    }
}
