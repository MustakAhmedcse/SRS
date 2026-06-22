namespace SalesCom.Infrastructure.Authorization;

using System.Globalization;
using Microsoft.AspNetCore.Authorization;

/// <summary>
/// Declarative right check on controllers/actions. The integer flows through the dynamic
/// <see cref="RightPolicyProvider"/>, which materializes a <see cref="RightRequirement"/> for the
/// handler. Usage: <c>[HasRight(Rights.DataSources.Manage)]</c>.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
public sealed class HasRightAttribute : AuthorizeAttribute
{
    public HasRightAttribute(int right)
        : base(policy: right.ToString(CultureInfo.InvariantCulture))
    {
    }
}
