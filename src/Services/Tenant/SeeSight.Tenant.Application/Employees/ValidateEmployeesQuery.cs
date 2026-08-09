using MediatR;

namespace SeeSight.Tenant.Application.Employees;

/// <summary>
/// Backs <c>POST /internal/employees/validate</c> — called by Trip Service to
/// confirm traveler ids belong to the claimed company and are active
/// (docs/TenantArchitecture.md §3 point 3, §5). Internal-service-token guarded,
/// not Gateway-routed, and not tied to any forwarded user identity — the caller
/// is another backend service, not a user, so this query takes
/// <see cref="CompanyId"/> explicitly rather than resolving it from
/// <c>ITenantContext</c> (which has no data for a service-to-service call).
/// </summary>
public sealed record ValidateEmployeesQuery(Guid CompanyId, IReadOnlyList<Guid> EmployeeIds)
    : IRequest<ValidateEmployeesResult>;

public sealed record ValidateEmployeesResult(IReadOnlyList<Guid> ValidEmployeeIds, IReadOnlyList<Guid> InvalidEmployeeIds);
