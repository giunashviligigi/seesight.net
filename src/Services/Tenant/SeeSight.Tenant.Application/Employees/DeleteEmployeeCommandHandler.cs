using MediatR;
using Microsoft.EntityFrameworkCore;
using SeeSight.Tenant.Application.Abstractions;
using SeeSight.Tenant.Application.Exceptions;

namespace SeeSight.Tenant.Application.Employees;

/// <summary>
/// Tombstones the local record only — docs/APIContracts.md's DELETE row says
/// "Tombstones email, unlinks userId," not "also deletes/deactivates the
/// Identity Service user." Severing the link, not touching the other side, is
/// the literal documented behavior.
/// </summary>
public sealed class DeleteEmployeeCommandHandler(ITenantDbContext dbContext, TimeProvider timeProvider)
    : IRequestHandler<DeleteEmployeeCommand>
{
    public async Task Handle(DeleteEmployeeCommand request, CancellationToken cancellationToken)
    {
        var employee = await dbContext.Employees
            .SingleOrDefaultAsync(e => e.Id == request.Id, cancellationToken)
            .ConfigureAwait(false);

        if (employee is null)
        {
            throw new EmployeeNotFoundException();
        }

        employee.Tombstone(timeProvider.GetUtcNow());
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
