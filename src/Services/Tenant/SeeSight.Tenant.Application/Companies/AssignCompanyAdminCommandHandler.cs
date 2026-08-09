using MediatR;
using Microsoft.EntityFrameworkCore;
using SeeSight.Tenant.Application.Abstractions;
using SeeSight.Tenant.Application.Exceptions;

namespace SeeSight.Tenant.Application.Companies;

public sealed class AssignCompanyAdminCommandHandler(
    ITenantDbContext dbContext,
    IIdentityServiceClient identityServiceClient) : IRequestHandler<AssignCompanyAdminCommand>
{
    public async Task Handle(AssignCompanyAdminCommand request, CancellationToken cancellationToken)
    {
        var companyExists = await dbContext.Companies
            .AsNoTracking()
            .AnyAsync(c => c.Id == request.CompanyId, cancellationToken)
            .ConfigureAwait(false);

        if (!companyExists)
        {
            throw new CompanyNotFoundException();
        }

        // ReplaceExisting (unassigning any prior admins of this company) is not
        // implemented in M3: it requires listing Identity Service users by
        // companyId, which needs the GET /users endpoint — explicitly deferred
        // out of M3's scope. The primary operation (assigning the requested
        // user) works correctly; only the "unassign others" side effect is
        // deferred, tracked as known technical debt (docs/validation/M3/README.md).
        await identityServiceClient.UpdateUserAsync(request.UserId, null, null, false, request.CompanyId, cancellationToken).ConfigureAwait(false);
    }
}
