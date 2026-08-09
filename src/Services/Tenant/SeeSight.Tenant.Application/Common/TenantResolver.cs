using SeeSight.SharedKernel.Tenancy;
using SeeSight.Tenant.Application.Exceptions;

namespace SeeSight.Tenant.Application.Common;

public sealed class TenantResolver : ITenantResolver
{
    public Guid Resolve(ITenantContext tenantContext, Guid? requestedCompanyId)
    {
        if (tenantContext.IsSuperAdmin)
        {
            return requestedCompanyId ?? throw new CompanyIdRequiredException();
        }

        if (tenantContext.CompanyId is not { } ownCompanyId)
        {
            throw new NoCompanyAssignedException();
        }

        if (requestedCompanyId is { } requested && requested != ownCompanyId.Value)
        {
            throw new CrossTenantAccessException();
        }

        return ownCompanyId.Value;
    }
}
