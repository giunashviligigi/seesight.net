using MediatR;
using Microsoft.EntityFrameworkCore;
using SeeSight.SharedKernel.Http;
using SeeSight.Tenant.Application.Abstractions;
using SeeSight.Tenant.Application.Exceptions;

namespace SeeSight.Tenant.Application.Employees;

public sealed class GetEmployeeByIdQueryHandler(ITenantDbContext dbContext, ICurrentUserContext currentUser)
    : IRequestHandler<GetEmployeeByIdQuery, EmployeeDto>
{
    public async Task<EmployeeDto> Handle(GetEmployeeByIdQuery request, CancellationToken cancellationToken)
    {
        // Cross-tenant reads already 404 via the EF Core tenant query filter —
        // the only extra check needed here is EMPLOYEE's self-scoping rule.
        var employee = await dbContext.Employees
            .AsNoTracking()
            .SingleOrDefaultAsync(e => e.Id == request.Id, cancellationToken)
            .ConfigureAwait(false);

        if (employee is null)
        {
            throw new EmployeeNotFoundException();
        }

        if (currentUser.Role == SeeSightRoles.Employee && employee.UserId != currentUser.UserId)
        {
            throw new EmployeeNotFoundException();
        }

        return EmployeeDto.FromDomain(employee);
    }
}
