using MediatR;
using Microsoft.EntityFrameworkCore;
using SeeSight.Tenant.Application.Abstractions;
using SeeSight.Tenant.Application.Exceptions;

namespace SeeSight.Tenant.Application.Employees;

/// <summary>Also deactivates the linked Identity Service user — docs/APIContracts.md.</summary>
public sealed class DeactivateEmployeeCommandHandler(
    ITenantDbContext dbContext,
    IIdentityServiceClient identityServiceClient,
    TimeProvider timeProvider) : IRequestHandler<DeactivateEmployeeCommand>
{
    public async Task Handle(DeactivateEmployeeCommand request, CancellationToken cancellationToken)
    {
        var employee = await dbContext.Employees
            .SingleOrDefaultAsync(e => e.Id == request.Id, cancellationToken)
            .ConfigureAwait(false);

        if (employee is null)
        {
            throw new EmployeeNotFoundException();
        }

        employee.Deactivate(timeProvider.GetUtcNow());
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        if (employee.UserId is { } userId)
        {
            await identityServiceClient.DeactivateUserAsync(userId, cancellationToken).ConfigureAwait(false);
        }
    }
}
