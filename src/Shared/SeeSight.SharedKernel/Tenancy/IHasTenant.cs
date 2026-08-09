namespace SeeSight.SharedKernel.Tenancy;

/// <summary>
/// Marks an entity as tenant-scoped — the owning service's <c>DbContext</c>
/// applies a <c>HasQueryFilter</c> keyed on <see cref="CompanyId"/> for every
/// entity implementing this, per docs/adr/0009-hand-rolled-tenant-context.md.
/// </summary>
public interface IHasTenant
{
    Guid CompanyId { get; }
}
