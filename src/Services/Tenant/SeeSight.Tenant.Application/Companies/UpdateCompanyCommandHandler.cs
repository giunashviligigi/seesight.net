using MediatR;
using Microsoft.EntityFrameworkCore;
using SeeSight.SharedKernel.Tenancy;
using SeeSight.Tenant.Application.Abstractions;
using SeeSight.Tenant.Application.Exceptions;

namespace SeeSight.Tenant.Application.Companies;

public sealed class UpdateCompanyCommandHandler(
    ITenantDbContext dbContext,
    ITenantContext tenantContext,
    TimeProvider timeProvider) : IRequestHandler<UpdateCompanyCommand, CompanyDto>
{
    public async Task<CompanyDto> Handle(UpdateCompanyCommand request, CancellationToken cancellationToken)
    {
        var company = await dbContext.Companies
            .SingleOrDefaultAsync(c => c.Id == request.Id, cancellationToken)
            .ConfigureAwait(false);

        if (company is null || (!tenantContext.IsSuperAdmin && tenantContext.CompanyId?.Value != company.Id))
        {
            throw new CompanyNotFoundException();
        }

        company.UpdateProfile(request.Name, request.LegalName, request.Country, request.BillingEmail, request.Timezone, request.PolicyJson, timeProvider.GetUtcNow());
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return CompanyDto.FromDomain(company);
    }
}
