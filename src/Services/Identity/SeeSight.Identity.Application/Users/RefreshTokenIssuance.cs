using SeeSight.Identity.Application.Abstractions;
using SeeSight.Identity.Domain;

namespace SeeSight.Identity.Application.Users;

/// <summary>
/// The generate-hash-persist sequence for issuing a new refresh token — shared
/// by register, login, and refresh (all three issue one), so it isn't
/// duplicated three times. Not exposed as a MediatR request; it's a plain
/// helper the three handlers call directly.
/// </summary>
internal static class RefreshTokenIssuance
{
    public static (RefreshToken Entity, string RawToken) IssueAndTrack(
        IIdentityDbContext dbContext,
        IOpaqueTokenGenerator tokenGenerator,
        ITokenHasher tokenHasher,
        IJwtIssuer jwtIssuer,
        Guid userId,
        string? ipAddress,
        DateTimeOffset now)
    {
        var rawToken = tokenGenerator.Generate();
        var tokenHash = tokenHasher.Hash(rawToken);
        var expiresAt = jwtIssuer.ComputeRefreshTokenExpiry(now);

        var entity = RefreshToken.Issue(userId, tokenHash, expiresAt, ipAddress, now);
        dbContext.RefreshTokens.Add(entity);

        return (entity, rawToken);
    }
}
