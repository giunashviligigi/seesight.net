namespace SeeSight.Identity.Application.Exceptions;

/// <summary>
/// Maps to 401 Unauthorized — same externally-visible outcome as
/// <see cref="InvalidRefreshTokenException"/>, but a distinct type so this
/// specific case (an already-revoked refresh token presented again — a
/// possible-theft signal per docs/Authentication.md §2) is distinguishable in
/// logs and tests from an ordinary expired/unknown token.
/// </summary>
public sealed class RefreshTokenReuseDetectedException() : Exception("Invalid or expired refresh token.");
