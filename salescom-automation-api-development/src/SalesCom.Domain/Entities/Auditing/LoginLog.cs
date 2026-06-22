namespace SalesCom.Domain.Entities.Auditing;

using SalesCom.Domain.Common;
using SalesCom.Domain.Enums;

/// <summary>Audit trail of login attempts — the <c>login</c> table.</summary>
public sealed class LoginLog : EntityBase<long>
{
    public string UserName { get; set; } = string.Empty;

    public string FullName { get; set; } = string.Empty;

    public DateTimeOffset LoginTime { get; set; }

    public LoginStatus LoginStatus { get; set; }

    public string Remarks { get; set; } = string.Empty;
}
