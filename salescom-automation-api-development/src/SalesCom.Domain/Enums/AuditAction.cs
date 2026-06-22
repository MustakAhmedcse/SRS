namespace SalesCom.Domain.Enums;

/// <summary>The kind of change captured by an <see cref="AuditLog"/> row.</summary>
public enum AuditAction
{
    Created = 0,
    Updated = 1,
    Deleted = 2,
}
