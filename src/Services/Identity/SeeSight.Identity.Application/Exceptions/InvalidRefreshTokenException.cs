namespace SeeSight.Identity.Application.Exceptions;

/// <summary>
/// Maps to 401 Unauthorized. Thrown for a refresh token that's missing, expired,
/// unknown, or whose user is no longer active — deliberately generic (mirrors
/// docs/Authentication.md §4's login no-user-enumeration rule).
/// </summary>
public sealed class InvalidRefreshTokenException() : Exception("Invalid or expired refresh token.");
