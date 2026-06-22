namespace SalesCom.Application.Interfaces;

/// <summary>
/// Resolves the set of granted right ids for a user, looked up by their central user id. Used by the
/// per-request <c>[HasRight]</c> authorization check, so enforcement always reflects the current DB state.
/// </summary>
public interface IUserRightsQuery
{
    Task<IReadOnlyList<int>> GetRightsAsync(string userId, CancellationToken cancellationToken);
}
