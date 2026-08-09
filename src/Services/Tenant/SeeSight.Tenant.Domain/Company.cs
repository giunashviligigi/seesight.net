using System.Text;
using SeeSight.SharedKernel.Persistence;

namespace SeeSight.Tenant.Domain;

/// <summary>
/// The tenant root — see docs/TenantArchitecture.md §1. Unlike <see cref="Department"/>/
/// <see cref="Employee"/>, a <see cref="Company"/> is not itself tenant-scoped
/// data (it *is* the tenant, so it does not implement <c>IHasTenant</c>); its
/// own read access is checked directly against <c>Id</c> in the owning
/// Application-layer handler, not an EF Core tenant query filter. It still
/// implements <see cref="ISoftDelete"/> — the soft-delete filter and the
/// tenant filter are independent concerns.
/// </summary>
public sealed class Company : ISoftDelete
{
    public Guid Id { get; private set; }
    public string Name { get; private set; } = null!;
    public string? LegalName { get; private set; }
    public string Slug { get; private set; } = null!;
    public string? Country { get; private set; }
    public string? BillingEmail { get; private set; }
    public string Timezone { get; private set; } = null!;
    public CompanyStatus Status { get; private set; }
    public string? PolicyJson { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public DateTimeOffset? DeletedAt { get; private set; }

    // EF Core materialization only.
    private Company()
    {
    }

    private Company(
        Guid id,
        string name,
        string? legalName,
        string slug,
        string? country,
        string? billingEmail,
        string timezone,
        string? policyJson,
        DateTimeOffset now)
    {
        Id = id;
        Name = name;
        LegalName = legalName;
        Slug = slug;
        Country = country;
        BillingEmail = billingEmail;
        Timezone = timezone;
        Status = CompanyStatus.Active;
        PolicyJson = policyJson;
        CreatedAt = now;
        UpdatedAt = now;
    }

    public static Company Create(
        string name,
        string? legalName,
        string? country,
        string? billingEmail,
        string timezone,
        string? policyJson,
        DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(timezone);

        var slug = GenerateSlug(name);
        return new Company(Guid.CreateVersion7(), name, legalName, slug, country, billingEmail, timezone, policyJson, now);
    }

    public bool IsUsable => Status == CompanyStatus.Active && DeletedAt is null;

    public void UpdateProfile(
        string name,
        string? legalName,
        string? country,
        string? billingEmail,
        string timezone,
        string? policyJson,
        DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(timezone);

        Name = name;
        LegalName = legalName;
        Country = country;
        BillingEmail = billingEmail;
        Timezone = timezone;
        PolicyJson = policyJson;
        UpdatedAt = now;
    }

    public void Deactivate(DateTimeOffset now)
    {
        Status = CompanyStatus.Inactive;
        UpdatedAt = now;
    }

    /// <summary>Also clears a prior soft delete — see docs/APIContracts.md's Tenant Service table.</summary>
    public void Activate(DateTimeOffset now)
    {
        Status = CompanyStatus.Active;
        DeletedAt = null;
        UpdatedAt = now;
    }

    public void Delete(DateTimeOffset now)
    {
        DeletedAt = now;
        UpdatedAt = now;
    }

    // A short, human-forgettable identifier — the exact algorithm is not part
    // of any documented contract, so uniqueness (a random suffix) matters more
    // here than readability; collisions on the readable portion are expected
    // and fine. Deliberately Guid.NewGuid() (v4, fully random), not
    // Guid.CreateVersion7() — v7 GUIDs are time-ordered and share their
    // leading hex digits when created within the same timestamp tick, which
    // would defeat the point of this suffix for two companies created close
    // together in time.
    private static string GenerateSlug(string name)
    {
        var builder = new StringBuilder(name.Length);
        var previousWasHyphen = false;
        foreach (var c in name.Trim().ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(c))
            {
                builder.Append(c);
                previousWasHyphen = false;
            }
            else if (!previousWasHyphen && builder.Length > 0)
            {
                builder.Append('-');
                previousWasHyphen = true;
            }
        }

        if (builder.Length > 0 && builder[^1] == '-')
        {
            builder.Length--;
        }

        var slugBase = builder.Length > 0 ? builder.ToString() : "company";
        return $"{slugBase}-{Guid.NewGuid().ToString("N")[..8]}";
    }
}
