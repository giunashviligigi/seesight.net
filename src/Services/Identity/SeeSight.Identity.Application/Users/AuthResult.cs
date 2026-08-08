namespace SeeSight.Identity.Application.Users;

/// <summary>
/// Shared shape for register, login, and refresh — the original system logs a
/// user in immediately on self-signup ("Login/register set the cookie
/// server-side"), per docs/Authentication.md §4. Carries both tokens: the
/// Gateway sets both as httpOnly cookies, per docs/Authentication.md §3.
/// </summary>
public sealed record AuthResult(
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAt,
    string RefreshToken,
    DateTimeOffset RefreshTokenExpiresAt,
    UserDto User);
