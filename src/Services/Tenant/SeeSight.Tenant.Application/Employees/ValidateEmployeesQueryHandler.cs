using MediatR;
using Microsoft.EntityFrameworkCore;
using SeeSight.Tenant.Application.Abstractions;
using SeeSight.Tenant.Domain;

namespace SeeSight.Tenant.Application.Employees;

public sealed class ValidateEmployeesQueryHandler(ITenantDbContext dbContext)
    : IRequestHandler<ValidateEmployeesQuery, ValidateEmployeesResult>
{
    public async Task<ValidateEmployeesResult> Handle(ValidateEmployeesQuery request, CancellationToken cancellationToken)
    {
        // No forwarded user identity exists for a service-to-service call, so
        // the tenant query filter can't be relied on (and would exclude
        // everything) — bypass it and filter explicitly by the caller-supplied,
        // internal-token-authenticated CompanyId instead.
        var validIds = await dbContext.Employees
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(e => e.CompanyId == request.CompanyId
                && e.DeletedAt == null
                && e.Status == EmployeeStatus.Active
                && request.EmployeeIds.Contains(e.Id))
            .Select(e => e.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var invalidIds = request.EmployeeIds.Except(validIds).ToList();

        return new ValidateEmployeesResult(validIds, invalidIds);
    }
}
