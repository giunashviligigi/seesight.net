using MediatR;
using Microsoft.EntityFrameworkCore;
using SeeSight.SharedKernel.Tenancy;
using SeeSight.Shared.Common;
using SeeSight.Tenant.Application.Abstractions;
using SeeSight.Tenant.Application.Common;

namespace SeeSight.Tenant.Application.Departments;

public sealed class GetDepartmentsQueryHandler(
    ITenantDbContext dbContext,
    ITenantContext tenantContext,
    ITenantResolver tenantResolver) : IRequestHandler<GetDepartmentsQuery, PagedResult<DepartmentDto>>
{
    public async Task<PagedResult<DepartmentDto>> Handle(GetDepartmentsQuery request, CancellationToken cancellationToken)
    {
        var companyId = tenantResolver.Resolve(tenantContext, request.CompanyId);

        var entities = await dbContext.Departments
            .AsNoTracking()
            .Where(d => d.CompanyId == companyId)
            .OrderBy(d => d.Name)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var items = entities.Select(DepartmentDto.FromDomain).ToList();
        return new PagedResult<DepartmentDto>(items, items.Count, 1, Math.Max(items.Count, 1));
    }
}
