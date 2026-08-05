using SeeSight.Identity.Domain;

namespace SeeSight.Identity.Application.Abstractions;

public sealed record AccessToken(string Value, DateTimeOffset ExpiresAt);

/// <summary>
/// Issues RS256-signed access tokens. Implemented in Infrastructure — see
/// docs/Authentication.md §2. Refresh tokens are not issued yet (M2).
/// </summary>
public interface IJwtIssuer
{
    AccessToken IssueAccessToken(User user);
}
