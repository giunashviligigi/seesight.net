namespace SeeSight.Shared.Common;

/// <summary>
/// The standard list-endpoint envelope — <c>{ items, total, page, pageSize }</c> —
/// per docs/APIContracts.md "Response Envelope & Error Conventions". Shared so
/// every service's list endpoints return the identical shape without
/// reimplementing it (docs/CodingStandards.md §2).
/// </summary>
public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Total, int Page, int PageSize);
