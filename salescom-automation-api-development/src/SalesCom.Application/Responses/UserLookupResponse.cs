namespace SalesCom.Application.Responses;

/// <summary>
/// A user resolved by login name for the approver picker: the surrogate <c>UserId</c> the caller stores
/// on the approval-flow-level-user, plus the display fields that populate the form.
/// </summary>
public sealed record UserLookupResponse(
    long UserId,
    string FullName,
    string MobileNo,
    string Email);
