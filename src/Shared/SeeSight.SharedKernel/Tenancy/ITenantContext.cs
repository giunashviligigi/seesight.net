namespace SeeSight.SharedKernel.Tenancy;

/// <summary>
/// The current request's tenant-scoping data — pure data, not a scoping
/// *decision* (the query filter predicate and the super-admin bypass are each
/// service's own Infrastructure concern; see docs/adr/0009-hand-rolled-tenant-context.md).
/// </summary>
public interface ITenantContext
{
    TenantId? CompanyId { get; }

    bool IsSuperAdmin { get; }
}
