namespace SalesCom.Domain.Entities.Auditing;

using SalesCom.Domain.Common;
using SalesCom.Domain.Enums;
using System.Text.Json;

/// <summary>
/// Immutable change-audit row — one per entity insert, update or delete. Records who made the
/// change and against which entity, with the before/after delta captured as jsonb.
/// </summary>
public sealed class AuditLog : EntityBase<long>
{
    /// <summary>Originating fleet component, e.g. "SalesCom" — lets components share one audit store.</summary>
    public string ApplicationName { get; set; } = string.Empty;

    /// <summary>CLR type name of the changed entity, e.g. "DataSource".</summary>
    public string EntityName { get; set; } = string.Empty;

    /// <summary>Primary-key value(s) of the changed entity.</summary>
    public string EntityId { get; set; } = string.Empty;

    public AuditAction ActionType { get; set; }

    /// <summary><c>UserId</c> of the caller; <c>null</c> for system / unauthenticated changes.</summary>
    public Guid? ChangedByUserId { get; set; }

    /// <summary>Login name of the caller, or "system".</summary>
    public string ChangedBy { get; set; } = string.Empty;

    public DateTimeOffset ChangedAt { get; set; }

    /// <summary>Comma-separated names of the modified properties; <c>null</c> for inserts and deletes.</summary>
    public string? ChangedColumns { get; set; }

    /// <summary>Full previous state; <c>null</c> for inserts.</summary>
    public JsonDocument? OldValues { get; set; }

    /// <summary>Full new state; <c>null</c> for deletes.</summary>
    public JsonDocument? NewValues { get; set; }
}
