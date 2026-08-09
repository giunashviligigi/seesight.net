using MediatR;
using Microsoft.EntityFrameworkCore;
using SeeSight.Tenant.Application.Abstractions;
using SeeSight.Tenant.Application.Exceptions;

namespace SeeSight.Tenant.Application.Departments;

public sealed class DeleteDepartmentCommandHandler(ITenantDbContext dbContext, TimeProvider timeProvider)
    : IRequestHandler<DeleteDepartmentCommand>
{
    public async Task Handle(DeleteDepartmentCommand request, CancellationToken cancellationToken)
    {
        var department = await dbContext.Departments
            .SingleOrDefaultAsync(d => d.Id == request.Id, cancellationToken)
            .ConfigureAwait(false);

        if (department is null)
        {
            throw new DepartmentNotFoundException();
        }

        var now = timeProvider.GetUtcNow();

        // Members are unassigned, not cascade-deleted, per docs/APIContracts.md.
        var members = await dbContext.Employees
            .Where(e => e.DepartmentId == department.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        foreach (var member in members)
        {
            member.ClearDepartment(now);
        }

        department.Delete(now);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
