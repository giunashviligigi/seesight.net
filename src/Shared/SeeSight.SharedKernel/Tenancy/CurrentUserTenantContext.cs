using SeeSight.SharedKernel.Http;

namespace SeeSight.SharedKernel.Tenancy;

/// <summary>
/// Maps the already-resolved <see cref="ICurrentUserContext"/> (Gateway-forwarded
/// identity headers) onto <see cref="ITenantContext"/> — pure mapping, no
/// resolution logic of its own, per docs/adr/0009-hand-rolled-tenant-context.md.
/// Registered scoped, alongside <see cref="ICurrentUserContext"/>.
/// </summary>
internal sealed class CurrentUserTenantContext : ITenantContext
{
    public CurrentUserTenantContext(ICurrentUserContext currentUser)
    {
        CompanyId = currentUser.CompanyId is { } companyId ? new TenantId(companyId) : null;
        IsSuperAdmin = currentUser.Role == SeeSightRoles.SuperAdmin;
    }

    public TenantId? CompanyId { get; }
    public bool IsSuperAdmin { get; }
}
