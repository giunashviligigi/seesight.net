using SeeSight.Identity.Domain;

namespace SeeSight.Identity.Application.Abstractions;

public sealed record AccessToken(string Value, DateTimeOffset ExpiresAt);

/// <summary>
/// Issues RS256-signed access tokens and knows the configured refresh-token
/// lifetime. Implemented in Infrastructure — see docs/Authentication.md §2.
/// The refresh token's raw value/hash are generated separately (see
/// <see cref="IOpaqueTokenGenerator"/>/<see cref="ITokenHasher"/>) — this
/// interface only owns "how long," the one piece of that decision that's
/// Infrastructure-configured (<c>JwtOptions.RefreshTokenLifetimeDays</c>), so
/// Application never hardcodes or re-derives the configured lifetime.
/// </summary>
public interface IJwtIssuer
{
    AccessToken IssueAccessToken(User user);

    DateTimeOffset ComputeRefreshTokenExpiry(DateTimeOffset now);
}
