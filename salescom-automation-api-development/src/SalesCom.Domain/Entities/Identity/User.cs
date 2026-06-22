namespace SalesCom.Domain.Entities.Identity;

using SalesCom.Domain.Common;
using SalesCom.Domain.Entities.Approvals;

/// <summary>
/// Local user record. <see cref="UserId"/> is the external/central identifier (text); the numeric
/// <see cref="EntityBase{TId}.Id"/> is this application's own surrogate key.
/// </summary>
public sealed class User : EntityBase<long>
{
    public string UserName { get; set; } = string.Empty;

    /// <summary>External/central user identifier.</summary>
    public string UserId { get; set; } = string.Empty;

    public string FullName { get; set; } = string.Empty;

    public string MobileNo { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string Department { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? UpdatedAt { get; set; }

    public string CreatedBy { get; set; } = string.Empty;

    public string? UpdatedBy { get; set; }

    //Navigation Properties

    public List<UserRight> UserRights { get; set; } = [];

    public List<ApprovalFlowLevelUser> ApprovalFlowLevelUsers { get; set; } = [];
}
