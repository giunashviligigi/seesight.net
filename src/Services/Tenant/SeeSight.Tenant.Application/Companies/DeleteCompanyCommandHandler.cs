using MediatR;
using Microsoft.EntityFrameworkCore;
using SeeSight.Tenant.Application.Abstractions;
using SeeSight.Tenant.Application.Exceptions;

namespace SeeSight.Tenant.Application.Companies;

public sealed class DeleteCompanyCommandHandler(ITenantDbContext dbContext, TimeProvider timeProvider)
    : IRequestHandler<DeleteCompanyCommand>
{
    public async Task Handle(DeleteCompanyCommand request, CancellationToken cancellationToken)
    {
        var company = await dbContext.Companies
            .SingleOrDefaultAsync(c => c.Id == request.Id, cancellationToken)
            .ConfigureAwait(false);

        if (company is null)
        {
            throw new CompanyNotFoundException();
        }

        company.Delete(timeProvider.GetUtcNow());
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
