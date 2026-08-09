namespace SeeSight.Identity.Domain;

/// <summary>
/// Authentication identity. Self-signup (<see cref="Register"/>) always creates a
/// <see cref="UserRole.CompanyAdmin"/> with no company assigned yet — a company is
/// created/assigned afterward. Admin-provisioned employee logins
/// (<see cref="ProvisionForEmployee"/>) are created by Tenant Service's
/// <c>createLogin: true</c> flow via the internal API — see
/// docs/TenantArchitecture.md §6. SuperAdmin accounts are created through a path
/// not yet built (no milestone requires it yet) — see docs/Authentication.md §4.
/// </summary>
public sealed class User
{
    public Guid Id { get; private set; }
    public string Email { get; private set; } = null!;
    public string PasswordHash { get; private set; } = null!;
    public string? FirstName { get; private set; }
    public string? LastName { get; private set; }
    public UserRole Role { get; private set; }
    public UserStatus Status { get; private set; }
    public bool MustChangePassword { get; private set; }
    public Guid? CompanyId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    // EF Core materialization only.
    private User()
    {
    }

    private User(
        Guid id,
        string email,
        string passwordHash,
        string? firstName,
        string? lastName,
        UserRole role,
        Guid? companyId,
        bool mustChangePassword,
        DateTimeOffset now)
    {
        Id = id;
        Email = email;
        PasswordHash = passwordHash;
        FirstName = firstName;
        LastName = lastName;
        Role = role;
        Status = UserStatus.Active;
        MustChangePassword = mustChangePassword;
        CompanyId = companyId;
        CreatedAt = now;
        UpdatedAt = now;
    }

    /// <summary>
    /// Self-signup: always a <see cref="UserRole.CompanyAdmin"/> with no company
    /// assigned. <paramref name="passwordHash"/> must already be hashed — the
    /// Domain layer never sees a plaintext password (hashing is an
    /// Infrastructure/<c>IPasswordHasher</c> concern, per docs/Authentication.md §4).
    /// </summary>
    public static User Register(string email, string passwordHash, string? firstName, string? lastName, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        ArgumentException.ThrowIfNullOrWhiteSpace(passwordHash);

        var normalizedEmail = email.Trim().ToLowerInvariant();
        return new User(Guid.CreateVersion7(), normalizedEmail, passwordHash, firstName, lastName, UserRole.CompanyAdmin, null, false, now);
    }

    /// <summary>
    /// Admin-provisioned employee login. Always <see cref="UserRole.Employee"/>,
    /// always tied to a company, and always <see cref="MustChangePassword"/> —
    /// the caller (Tenant Service, via the internal API) supplies a one-time
    /// temporary password whose forced-change-on-first-login is enforced by the
    /// Gateway's MustChangePassword gate, per docs/TenantArchitecture.md §6.
    /// <paramref name="passwordHash"/> must already be hashed.
    /// </summary>
    public static User ProvisionForEmployee(string email, string passwordHash, string? firstName, string? lastName, Guid companyId, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        ArgumentException.ThrowIfNullOrWhiteSpace(passwordHash);

        var normalizedEmail = email.Trim().ToLowerInvariant();
        return new User(Guid.CreateVersion7(), normalizedEmail, passwordHash, firstName, lastName, UserRole.Employee, companyId, true, now);
    }

    /// <summary>
    /// Whether this user is permitted to authenticate — mirrors the original
    /// system's login check (missing/inactive user -> generic "invalid credentials",
    /// never a distinguishable error; see docs/Authentication.md §4).
    /// </summary>
    public bool CanAuthenticate => Status == UserStatus.Active;

    /// <summary>
    /// Shared by both the reset-password and change-password flows: replaces the
    /// password hash and clears <see cref="MustChangePassword"/> — the only two
    /// paths that lift the forced-change flag, per docs/Authentication.md §4.
    /// <paramref name="newPasswordHash"/> must already be hashed — whether it
    /// differs from the current password, and whether the caller is even allowed
    /// to make this change, are Application-layer concerns (they need
    /// <c>IPasswordHasher</c>/token validation this entity doesn't have).
    /// </summary>
    public void SetPasswordHash(string newPasswordHash, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(newPasswordHash);

        PasswordHash = newPasswordHash;
        MustChangePassword = false;
        UpdatedAt = now;
    }

    /// <summary>Idempotent — mirrors the linked Employee's deactivation (docs/TenantArchitecture.md §6/APIContracts.md).</summary>
    public void Deactivate(DateTimeOffset now)
    {
        if (Status == UserStatus.Inactive)
        {
            return;
        }

        Status = UserStatus.Inactive;
        UpdatedAt = now;
    }

    /// <summary>Idempotent — mirrors the linked Employee's activation.</summary>
    public void Activate(DateTimeOffset now)
    {
        if (Status == UserStatus.Active)
        {
            return;
        }

        Status = UserStatus.Active;
        UpdatedAt = now;
    }

    /// <summary>
    /// Syncs the display name from the linked Employee record — a <see langword="null"/>
    /// argument means "leave this field unchanged," not "clear it" (Employee names
    /// are required fields, so callers never legitimately need to null them out).
    /// </summary>
    public void UpdateProfile(string? firstName, string? lastName, DateTimeOffset now)
    {
        FirstName = firstName ?? FirstName;
        LastName = lastName ?? LastName;
        UpdatedAt = now;
    }

    /// <summary>
    /// Assigns or clears the company link — used by Company Service's
    /// assign-admin/unassign-admin flow. <paramref name="companyId"/> of
    /// <see langword="null"/> explicitly clears it (unlike <see cref="UpdateProfile"/>,
    /// there is a real "no company" state to return to).
    /// </summary>
    public void AssignToCompany(Guid? companyId, DateTimeOffset now)
    {
        CompanyId = companyId;
        UpdatedAt = now;
    }
}
