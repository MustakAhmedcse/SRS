namespace SalesCom.Application.Queries.Account.GetMe;

public sealed record MeResponse(
    string UserId,
    string UserName,
    string FullName,
    string? Email,
    string MobileNo,
    string Department,
    IReadOnlyList<int> Rights);
