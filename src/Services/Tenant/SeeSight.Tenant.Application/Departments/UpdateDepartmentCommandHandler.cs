using MediatR;
using Microsoft.EntityFrameworkCore;
using SeeSight.Tenant.Application.Abstractions;
using SeeSight.Tenant.Application.Exceptions;

namespace SeeSight.Tenant.Application.Departments;

public sealed class UpdateDepartmentCommandHandler(ITenantDbContext dbContext, TimeProvider timeProvider)
    : IRequestHandler<UpdateDepartmentCommand, DepartmentDto>
{
    public async Task<DepartmentDto> Handle(UpdateDepartmentCommand request, CancellationToken cancellationToken)
    {
        // The EF Core tenant query filter already excludes another company's
        // department for a non-super-admin — a cross-tenant id naturally 404s
        // here with no extra check, per docs/adr/0009-hand-rolled-tenant-context.md.
        var department = await dbContext.Departments
            .SingleOrDefaultAsync(d => d.Id == request.Id, cancellationToken)
            .ConfigureAwait(false);

        if (department is null)
        {
            throw new DepartmentNotFoundException();
        }

        var nameTaken = await dbContext.Departments
            .AsNoTracking()
            .AnyAsync(d => d.CompanyId == department.CompanyId && d.Id != department.Id && d.Name == request.Name, cancellationToken)
            .ConfigureAwait(false);

        if (nameTaken)
        {
            throw new DuplicateDepartmentNameException();
        }

        department.UpdateProfile(request.Name, request.Code, timeProvider.GetUtcNow());

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
