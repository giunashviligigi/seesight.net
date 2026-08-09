using MediatR;
using Microsoft.EntityFrameworkCore;
using SeeSight.Tenant.Application.Abstractions;
using SeeSight.Tenant.Application.Exceptions;

namespace SeeSight.Tenant.Application.Employees;

public sealed class ActivateEmployeeCommandHandler(
    ITenantDbContext dbContext,
    IIdentityServiceClient identityServiceClient,
    TimeProvider timeProvider) : IRequestHandler<ActivateEmployeeCommand>
{
    public async Task Handle(ActivateEmployeeCommand request, CancellationToken cancellationToken)
    {
        var employee = await dbContext.Employees
            .SingleOrDefaultAsync(e => e.Id == request.Id, cancellationToken)
            .ConfigureAwait(false);

        if (employee is null)
        {
            throw new EmployeeNotFoundException();
        }

        employee.Activate(timeProvider.GetUtcNow());
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        if (employee.UserId is { } userId)
        {
            await identityServiceClient.ActivateUserAsync(userId, cancellationToken).ConfigureAwait(false);
        }
    }
}
