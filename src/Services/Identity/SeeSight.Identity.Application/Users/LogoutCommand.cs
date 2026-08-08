using MediatR;

namespace SeeSight.Identity.Application.Users;

/// <summary>
/// Revokes the presented refresh token — real server-side revocation, not just
/// "clear the cookie" (docs/Authentication.md §2). Idempotent and deliberately
/// forgiving: a missing/unknown/already-revoked token is not an error, since
/// the caller's goal ("I should no longer be logged in") is already satisfied.
/// </summary>
public sealed record LogoutCommand(string? RefreshToken) : IRequest;
