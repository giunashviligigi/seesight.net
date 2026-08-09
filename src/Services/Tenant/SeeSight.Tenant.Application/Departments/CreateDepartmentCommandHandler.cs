using MediatR;
using Microsoft.EntityFrameworkCore;
using SeeSight.SharedKernel.Tenancy;
using SeeSight.Tenant.Application.Abstractions;
using SeeSight.Tenant.Application.Common;
using SeeSight.Tenant.Application.Exceptions;
using SeeSight.Tenant.Domain;

namespace SeeSight.Tenant.Application.Departments;

public sealed class CreateDepartmentCommandHandler(
    ITenantDbContext dbContext,
    ITenantContext tenantContext,
    ITenantResolver tenantResolver,
    TimeProvider timeProvider) : IRequestHandler<CreateDepartmentCommand, DepartmentDto>
{
    public async Task<DepartmentDto> Handle(CreateDepartmentCommand request, CancellationToken cancellationToken)
    {
        var companyId = tenantResolver.Resolve(tenantContext, request.CompanyId);

        var nameTaken = await dbContext.Departments
            .AsNoTracking()
            .AnyAsync(d => d.CompanyId == companyId && d.Name == request.Name, cancellationToken)
            .ConfigureAwait(false);

        if (nameTaken)
        {
            throw new DuplicateDepartmentNameException();
        }

        var now = timeProvider.GetUtcNow();
        var department = Department.Create(companyId, request.Name, request.Code, now);
        dbContext.Departments.Add(department);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateException)
        {
            throw new DuplicateDepartmentNameException();
        }

        return DepartmentDto.FromDomain(department);
    }
}
