namespace SeeSight.Identity.Domain;

/// <summary>
/// Authentication identity. Self-signup (<see cref="Register"/>) always creates a
/// <see cref="UserRole.CompanyAdmin"/> with no company assigned yet — a company is
/// created/assigned afterward (Tenant Service, M3). SuperAdmin/Employee accounts are
/// created through other paths not yet built in M1 — see docs/Authentication.md §4.
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

    private User(Guid id, string email, string passwordHash, string? firstName, string? lastName, DateTimeOffset now)
    {
        Id = id;
        Email = email;
        PasswordHash = passwordHash;
        FirstName = firstName;
        LastName = lastName;
        Role = UserRole.CompanyAdmin;
        Status = UserStatus.Active;
        MustChangePassword = false;
        CompanyId = null;
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
        return new User(Guid.CreateVersion7(), normalizedEmail, passwordHash, firstName, lastName, now);
    }

    /// <summary>
    /// Whether this user is permitted to authenticate — mirrors the original
    /// system's login check (missing/inactive user -> generic "invalid credentials",
    /// never a distinguishable error; see docs/Authentication.md §4).
    /// </summary>
    public bool CanAuthenticate => Status == UserStatus.Active;
}
