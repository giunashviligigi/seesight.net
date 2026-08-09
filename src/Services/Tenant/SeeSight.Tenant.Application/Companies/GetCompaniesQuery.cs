using MediatR;
using SeeSight.Shared.Common;

namespace SeeSight.Tenant.Application.Companies;

/// <summary>SUPER_ADMIN only — paginated, searchable (docs/APIContracts.md).</summary>
public sealed record GetCompaniesQuery(string? Search, int Page, int PageSize) : IRequest<PagedResult<CompanyDto>>;
