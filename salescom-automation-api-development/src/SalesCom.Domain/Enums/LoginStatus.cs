namespace SalesCom.Domain.Enums;

/// <summary>Outcome of a login attempt recorded in <see cref="Entities.Auditing.LoginLog"/>.</summary>
public enum LoginStatus
{
    Success = 1,
    Failed = 2,
}
