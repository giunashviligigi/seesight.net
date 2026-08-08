using SeeSight.Identity.Application.Users;

namespace SeeSight.Identity.Api.Contracts;

/// <summary>Shared response shape for register, login, and refresh — see <see cref="AuthResult"/>.</summary>
public sealed record AuthResponse(
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAt,
    string RefreshToken,
    DateTimeOffset RefreshTokenExpiresAt,
    UserDto User)
{
    public static AuthResponse FromResult(AuthResult result) => new(
        result.AccessToken,
        result.AccessTokenExpiresAt,
        result.RefreshToken,
        result.RefreshTokenExpiresAt,
        result.User);
}
