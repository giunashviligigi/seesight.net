namespace SeeSight.Identity.Domain;

/// <summary>
/// A rotating refresh token. Only the SHA-256 hash of the opaque token value is
/// ever persisted — the Domain layer never sees the plaintext token (hashing is
/// an Infrastructure/<c>ITokenHasher</c> concern, mirroring how <see cref="User"/>
/// never sees a plaintext password). Rotation is modeled explicitly:
/// <see cref="Revoke"/> with a <c>replacedByTokenId</c> links the old token to
/// its successor, per docs/Authentication.md §2.
/// </summary>
public sealed class RefreshToken
{
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public string TokenHash { get; private set; } = null!;
    public DateTimeOffset ExpiresAt { get; private set; }
    public DateTimeOffset? RevokedAt { get; private set; }
    public Guid? ReplacedByTokenId { get; private set; }
    public string? CreatedByIp { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    // EF Core materialization only.
    private RefreshToken()
    {
    }

    private RefreshToken(Guid id, Guid userId, string tokenHash, DateTimeOffset expiresAt, string? createdByIp, DateTimeOffset now)
    {
        Id = id;
        UserId = userId;
        TokenHash = tokenHash;
        ExpiresAt = expiresAt;
        CreatedByIp = createdByIp;
        CreatedAt = now;
    }

    public static RefreshToken Issue(Guid userId, string tokenHash, DateTimeOffset expiresAt, string? createdByIp, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tokenHash);
        if (expiresAt <= now)
        {
            throw new ArgumentOutOfRangeException(nameof(expiresAt), "Refresh token expiry must be in the future.");
        }

        return new RefreshToken(Guid.CreateVersion7(), userId, tokenHash, expiresAt, createdByIp, now);
    }

    public bool IsActive(DateTimeOffset now) => RevokedAt is null && now < ExpiresAt;

    /// <summary>
    /// Idempotent — a no-op if already revoked. The reuse-detection path (see
    /// docs/Authentication.md §2) revokes an entire token chain in one pass and
    /// shouldn't need to special-case tokens that are already revoked.
    /// </summary>
    public void Revoke(DateTimeOffset now, Guid? replacedByTokenId = null)
    {
        if (RevokedAt is not null)
        {
            return;
        }

        RevokedAt = now;
        ReplacedByTokenId = replacedByTokenId;
    }
}
