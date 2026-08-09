namespace SeeSight.SharedKernel.Tenancy;

/// <summary>
/// A small, stable wrapper around the tenant (<c>Company</c>) identifier — see
/// docs/adr/0009-hand-rolled-tenant-context.md. Deliberately has no implicit
/// conversion to/from <see cref="Guid"/>, so a raw <see cref="Guid"/> can never
/// be passed where a tenant id is expected without an explicit, readable
/// construction site.
/// </summary>
public readonly record struct TenantId(Guid Value)
{
    public override string ToString() => Value.ToString();
}
