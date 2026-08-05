namespace SeeSight.Identity.Application.Exceptions;

/// <summary>
/// Maps to 401 Unauthorized at the Api layer. Thrown when a token's subject no
/// longer resolves to an active user (deleted/deactivated since the token was
/// issued) — mirrors the original system re-validating against the database on
/// every request rather than trusting a decoded token blindly.
/// </summary>
public sealed class UserSessionInvalidException() : Exception("The current session is no longer valid.");
