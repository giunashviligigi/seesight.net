using MediatR;
using Microsoft.EntityFrameworkCore;
using SeeSight.SharedKernel.Http;
using SeeSight.Tenant.Application.Abstractions;
using SeeSight.Tenant.Application.Exceptions;

namespace SeeSight.Tenant.Application.Employees;

public sealed class GetMyEmployeeQueryHandler(ITenantDbContext dbContext, ICurrentUserContext currentUser)
    : IRequestHandler<GetMyEmployeeQuery, EmployeeDto>
{
    public async Task<EmployeeDto> Handle(GetMyEmployeeQuery request, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId)
        {
            throw new EmployeeNotFoundException();
        }

        var employee = await dbContext.Employees
            .AsNoTracking()
            .SingleOrDefaultAsync(e => e.UserId == userId, cancellationToken)
            .ConfigureAwait(false);

        if (employee is null)
        {
            throw new EmployeeNotFoundException();
        }

        return EmployeeDto.FromDomain(employee);
    }
}
