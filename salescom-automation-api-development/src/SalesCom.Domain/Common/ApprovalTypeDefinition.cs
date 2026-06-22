using SalesCom.Domain.Enums;

namespace SalesCom.Domain.Common;


/// <summary>One catalog entry: the type, its display name, its fixed phase, sort order, and description.</summary>
public sealed record ApprovalTypeDefinition(
    ApprovalType Type,
    string Name,
    ApprovalPhase Phase,
    int SortOrder);

/// <summary>
/// The canonical, code-owned catalog of <see cref="ApprovalType"/>s — the single source of truth for
/// each type's <see cref="ApprovalPhase"/>. Replaces the former <c>approval_type</c> table: a level
/// stores only the <see cref="ApprovalType"/> id and resolves the rest here. Mirrors the
/// <c>Permissions</c> catalog pattern.
/// </summary>
public static class ApprovalTypeCatalog
{
    private static readonly ApprovalTypeDefinition[] _all =
    [
        new(ApprovalType.SetupReview,"Setup Review",ApprovalPhase.PreRun,1),
        new(ApprovalType.ReportRun,"Report Run",ApprovalPhase.PostRun,2),
    ];

    /// <summary>Every defined type, in sort order.</summary>
    public static IReadOnlyList<ApprovalTypeDefinition> All { get; } = [.. _all.OrderBy(d => d.SortOrder)];

    /// <summary>The definition for a type. Throws if the value is not a defined catalog member.</summary>
    public static ApprovalTypeDefinition Get(ApprovalType type) =>
        _all.SingleOrDefault(d => d.Type == type)
        ?? throw new ArgumentOutOfRangeException(nameof(type), type, "Undefined approval type.");

    /// <summary>The fixed phase of a type — the value run/payout gating depends on.</summary>
    public static ApprovalPhase PhaseOf(ApprovalType type) => Get(type).Phase;
}
