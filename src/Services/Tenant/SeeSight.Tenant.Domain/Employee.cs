using SeeSight.SharedKernel.Persistence;
using SeeSight.SharedKernel.Tenancy;

namespace SeeSight.Tenant.Domain;

/// <summary>
/// <see cref="UserId"/> is a logical reference to Identity Service's <c>User</c> —
/// Tenant Service never queries Identity's database directly (docs/DatabaseDesign.md §4).
/// </summary>
public sealed class Employee : IHasTenant, ISoftDelete
{
    public Guid Id { get; private set; }
    public Guid CompanyId { get; private set; }
    public Guid? DepartmentId { get; private set; }
    public Guid? UserId { get; private set; }
    public string Email { get; private set; } = null!;
    public string FirstName { get; private set; } = null!;
    public string LastName { get; private set; } = null!;
    public string? JobTitle { get; private set; }
    public string? Phone { get; private set; }
    public string? Nationality { get; private set; }
    public string? PassportNumber { get; private set; }
    public string? PreferredAirport { get; private set; }
    public EmployeeStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public DateTimeOffset? DeletedAt { get; private set; }

    // EF Core materialization only.
    private Employee()
    {
    }

    private Employee(
        Guid id,
        Guid companyId,
        Guid? departmentId,
        Guid? userId,
        string email,
        string firstName,
        string lastName,
        string? jobTitle,
        string? phone,
        string? nationality,
        string? passportNumber,
        string? preferredAirport,
        DateTimeOffset now)
    {
        Id = id;
        CompanyId = companyId;
        DepartmentId = departmentId;
        UserId = userId;
        Email = email;
        FirstName = firstName;
        LastName = lastName;
        JobTitle = jobTitle;
        Phone = phone;
        Nationality = nationality;
        PassportNumber = passportNumber;
        PreferredAirport = preferredAirport;
        Status = EmployeeStatus.Active;
        CreatedAt = now;
        UpdatedAt = now;
    }

    public static Employee Create(
        Guid companyId,
        Guid? departmentId,
        Guid? userId,
        string email,
        string firstName,
        string lastName,
        string? jobTitle,
        string? phone,
        string? nationality,
        string? passportNumber,
        string? preferredAirport,
        DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        ArgumentException.ThrowIfNullOrWhiteSpace(firstName);
        ArgumentException.ThrowIfNullOrWhiteSpace(lastName);

        var normalizedEmail = email.Trim().ToLowerInvariant();
        return new Employee(
            Guid.CreateVersion7(), companyId, departmentId, userId, normalizedEmail,
            firstName, lastName, jobTitle, phone, nationality, passportNumber, preferredAirport, now);
    }

    public void UpdateProfile(
        string firstName,
        string lastName,
        Guid? departmentId,
        string? jobTitle,
        string? phone,
        string? nationality,
        string? passportNumber,
        string? preferredAirport,
        DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(firstName);
        ArgumentException.ThrowIfNullOrWhiteSpace(lastName);

        FirstName = firstName;
        LastName = lastName;
        DepartmentId = departmentId;
        JobTitle = jobTitle;
        Phone = phone;
        Nationality = nationality;
        PassportNumber = passportNumber;
        PreferredAirport = preferredAirport;
        UpdatedAt = now;
    }

    /// <summary>Used when the owning Department is deleted — members are unassigned, not removed.</summary>
    public void ClearDepartment(DateTimeOffset now)
    {
        DepartmentId = null;
        UpdatedAt = now;
    }

    public void Deactivate(DateTimeOffset now)
    {
        Status = EmployeeStatus.Inactive;
        UpdatedAt = now;
    }

    public void Activate(DateTimeOffset now)
    {
        Status = EmployeeStatus.Active;
        UpdatedAt = now;
    }

    /// <summary>
    /// Soft delete: tombstones the email (freeing it for reuse under the
    /// per-company unique-email constraint) and unlinks the Identity Service
    /// user — see docs/APIContracts.md's Tenant Service table
    /// ("Tombstones email, unlinks userId").
    /// </summary>
    public void Tombstone(DateTimeOffset now)
    {
        Email = $"deleted+{Id:N}@tombstoned.invalid";
        UserId = null;
        DeletedAt = now;
        UpdatedAt = now;
    }
}
