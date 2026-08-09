using MediatR;
using SeeSight.Shared.Common;
using SeeSight.Tenant.Domain;

namespace SeeSight.Tenant.Application.Employees;

/// <summary>SUPER_ADMIN/COMPANY_ADMIN only — search/filter/sort/paginate (docs/APIContracts.md).</summary>
public sealed record GetEmployeesQuery(
    Guid? CompanyId,
    string? Search,
    Guid? DepartmentId,
    EmployeeStatus? Status,
    int Page,
    int PageSize) : IRequest<PagedResult<EmployeeDto>>;
