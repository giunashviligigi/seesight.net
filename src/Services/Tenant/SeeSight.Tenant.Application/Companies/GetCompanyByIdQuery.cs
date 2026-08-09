using MediatR;

namespace SeeSight.Tenant.Application.Companies;

/// <summary>
/// Tenant-scoped: a non-super-admin may only fetch their own company id —
/// enforced in the handler (this is a single explicit id, not a list/create
/// request, so <c>ITenantResolver</c> doesn't apply; see docs/TenantArchitecture.md §4).
/// </summary>
public sealed record GetCompanyByIdQuery(Guid Id) : IRequest<CompanyDto>;
