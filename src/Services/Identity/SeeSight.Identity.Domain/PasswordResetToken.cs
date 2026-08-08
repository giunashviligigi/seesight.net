namespace SeeSight.Identity.Domain;

/// <summary>
/// A one-time password reset token. Only the SHA-256 hash is persisted — see
/// docs/Authentication.md §4.
/// </summary>
public sealed class PasswordResetToken
{
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public string TokenHash { get; private set; } = null!;
    public DateTimeOffset ExpiresAt { get; private set; }
    public DateTimeOffset? UsedAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    // EF Core materialization only.
    private PasswordResetToken()
    {
    }

    private PasswordResetToken(Guid id, Guid userId, string tokenHash, DateTimeOffset expiresAt, DateTimeOffset now)
    {
        Id = id;
        UserId = userId;
        TokenHash = tokenHash;
        ExpiresAt = expiresAt;
        CreatedAt = now;
    }

    public static PasswordResetToken Issue(Guid userId, string tokenHash, DateTimeOffset expiresAt, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tokenHash);
        if (expiresAt <= now)
        {
            throw new ArgumentOutOfRangeException(nameof(expiresAt), "Password reset token expiry must be in the future.");
        }

        return new PasswordResetToken(Guid.CreateVersion7(), userId, tokenHash, expiresAt, now);
    }

    public bool IsValid(DateTimeOffset now) => UsedAt is null && now < ExpiresAt;

    /// <summary>
    /// Callers must check <see cref="IsValid"/> first — this throws rather than
    /// silently no-opping on a double-use, since that represents a genuine logic
    /// error (or a race the caller should have prevented), unlike
    /// <see cref="RefreshToken.Revoke"/>'s deliberately idempotent design.
    /// </summary>
    public void MarkUsed(DateTimeOffset now)
    {
        if (UsedAt is not null)
        {
            throw new InvalidOperationException("This password reset token has already been used.");
        }

        UsedAt = now;
    }
}
