namespace SeeSight.Identity.Application.Users;

/// <summary>
/// Shared shape for both register and login — the original system logs a user
/// in immediately on self-signup ("Login/register set the cookie server-side"),
/// per docs/Authentication.md §4.
/// </summary>
public sealed record AuthResult(string AccessToken, DateTimeOffset ExpiresAt, UserDto User);
