using MediatR;
using Microsoft.EntityFrameworkCore;
using SeeSight.SharedKernel.Tenancy;
using SeeSight.Shared.Common;
using SeeSight.Tenant.Application.Abstractions;
using SeeSight.Tenant.Application.Common;

namespace SeeSight.Tenant.Application.Employees;

public sealed class GetEmployeesQueryHandler(
    ITenantDbContext dbContext,
    ITenantContext tenantContext,
    ITenantResolver tenantResolver) : IRequestHandler<GetEmployeesQuery, PagedResult<EmployeeDto>>
{
    public async Task<PagedResult<EmployeeDto>> Handle(GetEmployeesQuery request, CancellationToken cancellationToken)
    {
        var companyId = tenantResolver.Resolve(tenantContext, request.CompanyId);

        var query = dbContext.Employees.AsNoTracking().Where(e => e.CompanyId == companyId);

        if (request.DepartmentId is { } departmentId)
        {
            query = query.Where(e => e.DepartmentId == departmentId);
        }

        if (request.Status is { } status)
        {
            query = query.Where(e => e.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
#pragma warning disable CA1304, CA1311
            var search = request.Search.Trim().ToLowerInvariant();
            query = query.Where(e =>
                EF.Functions.Like(e.FirstName.ToLower(), $"%{search}%") ||
                EF.Functions.Like(e.LastName.ToLower(), $"%{search}%") ||
                EF.Functions.Like(e.Email.ToLower(), $"%{search}%"));
#pragma warning restore CA1304, CA1311
        }

        var total = await query.CountAsync(cancellationToken).ConfigureAwait(false);

        var page = Math.Max(request.Page, 1);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);

        var entities = await query
            .OrderBy(e => e.LastName).ThenBy(e => e.FirstName)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var items = entities.Select(EmployeeDto.FromDomain).ToList();
        return new PagedResult<EmployeeDto>(items, total, page, pageSize);
    }
}
