using MediatR;
using SeeSight.SharedKernel.Http;
using SeeSight.SharedKernel.Tenancy;
using SeeSight.Tenant.Application.Abstractions;
using SeeSight.Tenant.Application.Exceptions;
using SeeSight.Tenant.Domain;

namespace SeeSight.Tenant.Application.Companies;

public sealed class CreateCompanyCommandHandler(
    ITenantDbContext dbContext,
    ITenantContext tenantContext,
    ICurrentUserContext currentUser,
    IIdentityServiceClient identityServiceClient,
    TimeProvider timeProvider) : IRequestHandler<CreateCompanyCommand, CompanyDto>
{
    public async Task<CompanyDto> Handle(CreateCompanyCommand request, CancellationToken cancellationToken)
    {
        // A SUPER_ADMIN may create any number of companies; a COMPANY_ADMIN may
        // only self-create while unassigned (docs/Authorization.md §5).
        if (!tenantContext.IsSuperAdmin && tenantContext.CompanyId is not null)
        {
            throw new CompanyAlreadyAssignedException();
        }

        var now = timeProvider.GetUtcNow();
        var company = Company.Create(request.Name, request.LegalName, request.Country, request.BillingEmail, request.Timezone, request.PolicyJson, now);
        dbContext.Companies.Add(company);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // Self-signup created this COMPANY_ADMIN with no company; creating one
        // now makes them its admin — otherwise the self-create flow would leave
        // them permanently unable to act on the company they just made.
        if (!tenantContext.IsSuperAdmin && currentUser.UserId is { } userId)
        {
            await identityServiceClient.UpdateUserAsync(userId, null, null, false, company.Id, cancellationToken).ConfigureAwait(false);
        }

        return CompanyDto.FromDomain(company);
    }
}
