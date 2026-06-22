namespace SalesCom.Application.Common;

/// <summary>
/// Envelope for paged read endpoints. Carries the slice plus the total count so clients can
/// render pagination controls without a second round-trip. Page/pageSize are echoed back so
/// clients can detect server-side clamping (max page size enforcement). Total pages is
/// intentionally omitted — clients compute it as <c>ceil(totalCount / pageSize)</c>.
/// </summary>
public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount);