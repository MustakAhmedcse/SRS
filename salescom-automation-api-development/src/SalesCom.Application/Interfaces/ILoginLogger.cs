namespace SalesCom.Application.Interfaces;

using SalesCom.Domain.Enums;

/// <summary>
/// Records a login attempt into the <c>login</c> audit table (<see cref="SalesCom.Domain.Entities.Auditing.LoginLog"/>).
/// Staged on the request's unit of work — the calling handler owns the single transactional save.
/// </summary>
public interface ILoginLogger
{
    Task LogAsync(string userName, string fullName, LoginStatus status, string remarks, CancellationToken cancellationToken);
}
