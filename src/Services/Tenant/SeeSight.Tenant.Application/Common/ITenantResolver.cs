using SeeSight.SharedKernel.Tenancy;

namespace SeeSight.Tenant.Application.Common;

/// <summary>
/// The "which company does this list/create request target" rule —
/// docs/TenantArchitecture.md §4. Request *validation*, not query scoping (it
/// runs before a query exists), so it lives here in Application rather than as
/// an EF Core query filter — see docs/adr/0009-hand-rolled-tenant-context.md.
/// </summary>
public interface ITenantResolver
{
    /// <summary>
    /// A super admin must pass an explicit <paramref name="requestedCompanyId"/>
    /// (no default tenant). A non-super-admin either omits it (their own company
    /// is used implicitly) or must pass exactly their own — anything else is
    /// rejected.
    /// </summary>
    Guid Resolve(ITenantContext tenantContext, Guid? requestedCompanyId);
}
