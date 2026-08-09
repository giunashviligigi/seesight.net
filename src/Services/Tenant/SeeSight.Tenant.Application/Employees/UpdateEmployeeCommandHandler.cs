using MediatR;
using Microsoft.EntityFrameworkCore;
using SeeSight.Tenant.Application.Abstractions;
using SeeSight.Tenant.Application.Exceptions;

namespace SeeSight.Tenant.Application.Employees;

/// <summary>Syncs the linked Identity Service user's name — docs/APIContracts.md ("Syncs linked User.firstName/lastName via REST").</summary>
public sealed class UpdateEmployeeCommandHandler(
    ITenantDbContext dbContext,
    IIdentityServiceClient identityServiceClient,
    TimeProvider timeProvider) : IRequestHandler<UpdateEmployeeCommand, EmployeeDto>
{
    public async Task<EmployeeDto> Handle(UpdateEmployeeCommand request, CancellationToken cancellationToken)
    {
        var employee = await dbContext.Employees
            .SingleOrDefaultAsync(e => e.Id == request.Id, cancellationToken)
            .ConfigureAwait(false);

        if (employee is null)
        {
            throw new EmployeeNotFoundException();
        }

        employee.UpdateProfile(
            request.FirstName, request.LastName, request.DepartmentId, request.JobTitle,
            request.Phone, request.Nationality, request.PassportNumber, request.PreferredAirport, timeProvider.GetUtcNow());

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        if (employee.UserId is { } userId)
        {
            await identityServiceClient.UpdateUserAsync(userId, request.FirstName, request.LastName, false, null, cancellationToken).ConfigureAwait(false);
        }

        return EmployeeDto.FromDomain(employee);
    }
}
