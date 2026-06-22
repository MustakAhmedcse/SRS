namespace SalesCom.Domain.Entities.Identity;

using SalesCom.Domain.Common;

/// <summary>A single right (capability id) granted to a <see cref="User"/>.</summary>
public sealed class UserRight : EntityBase<long>
{
    public long UserId { get; set; }

    public int RightsCode { get; set; }

    // Navigation properties
    public User? User { get; set; }
}
