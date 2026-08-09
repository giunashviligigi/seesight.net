using MediatR;
using Microsoft.EntityFrameworkCore;
using SeeSight.SharedKernel.Tenancy;
using SeeSight.Tenant.Application.Abstractions;
using SeeSight.Tenant.Application.Exceptions;

namespace SeeSight.Tenant.Application.Companies;

public sealed class GetMyCompanyQueryHandler(ITenantDbContext dbContext, ITenantContext tenantContext)
    : IRequestHandler<GetMyCompanyQuery, CompanyDto>
{
    public async Task<CompanyDto> Handle(GetMyCompanyQuery request, CancellationToken cancellationToken)
    {
        if (tenantContext.CompanyId is not { } companyId)
        {
            throw new NoCompanyAssignedException();
        }

        var company = await dbContext.Companies
            .AsNoTracking()
            .SingleOrDefaultAsync(c => c.Id == companyId.Value, cancellationToken)
            .ConfigureAwait(false);

        if (company is null)
        {
            throw new CompanyNotFoundException();
        }

        return CompanyDto.FromDomain(company);
    }
}
