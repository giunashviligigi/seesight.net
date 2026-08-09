using SeeSight.SharedKernel.Persistence;
using SeeSight.SharedKernel.Tenancy;

namespace SeeSight.Tenant.Domain;

public sealed class Department : IHasTenant, ISoftDelete
{
    public Guid Id { get; private set; }
    public Guid CompanyId { get; private set; }
    public string Name { get; private set; } = null!;
    public string? Code { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public DateTimeOffset? DeletedAt { get; private set; }

    // EF Core materialization only.
    private Department()
    {
    }

    private Department(Guid id, Guid companyId, string name, string? code, DateTimeOffset now)
    {
        Id = id;
        CompanyId = companyId;
        Name = name;
        Code = code;
        CreatedAt = now;
        UpdatedAt = now;
    }

    public static Department Create(Guid companyId, string name, string? code, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        return new Department(Guid.CreateVersion7(), companyId, name, code, now);
    }

    public void UpdateProfile(string name, string? code, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        Name = name;
        Code = code;
        UpdatedAt = now;
    }

    /// <summary>
    /// Soft delete — members are unassigned (their <c>DepartmentId</c> cleared),
    /// not cascade-deleted; that reassignment is an Application-layer concern
    /// across the Employee aggregate, not this entity (docs/APIContracts.md).
    /// </summary>
    public void Delete(DateTimeOffset now)
    {
        DeletedAt = now;
        UpdatedAt = now;
    }
}
