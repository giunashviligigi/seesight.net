namespace SeeSight.Identity.Application.Exceptions;

/// <summary>
/// Maps to 401 Unauthorized at the Api layer. Deliberately generic — thrown for
/// both "no such user" and "wrong password" so the response never reveals which
/// (no user-enumeration), per docs/Authentication.md §4.
/// </summary>
public sealed class InvalidCredentialsException() : Exception("Invalid email or password.");
