using MediatR;
using Microsoft.EntityFrameworkCore;
using SeeSight.SharedKernel.Tenancy;
using SeeSight.Tenant.Application.Abstractions;
using SeeSight.Tenant.Application.Exceptions;

namespace SeeSight.Tenant.Application.Companies;

public sealed class GetCompanyByIdQueryHandler(ITenantDbContext dbContext, ITenantContext tenantContext)
    : IRequestHandler<GetCompanyByIdQuery, CompanyDto>
{
    public async Task<CompanyDto> Handle(GetCompanyByIdQuery request, CancellationToken cancellationToken)
    {
        var company = await dbContext.Companies
            .AsNoTracking()
            .SingleOrDefaultAsync(c => c.Id == request.Id, cancellationToken)
            .ConfigureAwait(false);

        // Cross-tenant access is deliberately indistinguishable from
        // "doesn't exist" — no information leak about another company's
        // existence, per docs/TenantArchitecture.md §5.
        if (company is null || (!tenantContext.IsSuperAdmin && tenantContext.CompanyId?.Value != company.Id))
        {
            throw new CompanyNotFoundException();
        }

        return CompanyDto.FromDomain(company);
    }
}
