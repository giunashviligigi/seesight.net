using MediatR;
using Microsoft.EntityFrameworkCore;
using SeeSight.Tenant.Application.Abstractions;
using SeeSight.Tenant.Application.Exceptions;

namespace SeeSight.Tenant.Application.Companies;

public sealed class DeactivateCompanyCommandHandler(ITenantDbContext dbContext, TimeProvider timeProvider)
    : IRequestHandler<DeactivateCompanyCommand>
{
    public async Task Handle(DeactivateCompanyCommand request, CancellationToken cancellationToken)
    {
        var company = await dbContext.Companies
            .SingleOrDefaultAsync(c => c.Id == request.Id, cancellationToken)
            .ConfigureAwait(false);

        if (company is null)
        {
            throw new CompanyNotFoundException();
        }

        company.Deactivate(timeProvider.GetUtcNow());
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
