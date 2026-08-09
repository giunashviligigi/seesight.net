using MediatR;
using Microsoft.EntityFrameworkCore;
using SeeSight.Tenant.Application.Abstractions;
using SeeSight.Tenant.Application.Exceptions;

namespace SeeSight.Tenant.Application.Companies;

public sealed class ActivateCompanyCommandHandler(ITenantDbContext dbContext, TimeProvider timeProvider)
    : IRequestHandler<ActivateCompanyCommand>
{
    public async Task Handle(ActivateCompanyCommand request, CancellationToken cancellationToken)
    {
        // Ignores the soft-delete filter deliberately — Activate is the one
        // operation whose entire purpose is recovering an already-deleted row.
        var company = await dbContext.Companies
            .IgnoreQueryFilters()
            .SingleOrDefaultAsync(c => c.Id == request.Id, cancellationToken)
            .ConfigureAwait(false);

        if (company is null)
        {
            throw new CompanyNotFoundException();
        }

        company.Activate(timeProvider.GetUtcNow());
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
