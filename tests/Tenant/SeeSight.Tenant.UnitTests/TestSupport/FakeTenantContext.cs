using SeeSight.SharedKernel.Tenancy;

namespace SeeSight.Tenant.UnitTests.TestSupport;

internal sealed class FakeTenantContext(TenantId? companyId, bool isSuperAdmin) : ITenantContext
{
    public TenantId? CompanyId { get; } = companyId;

    public bool IsSuperAdmin { get; } = isSuperAdmin;
}
