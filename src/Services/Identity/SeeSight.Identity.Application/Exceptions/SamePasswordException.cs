namespace SeeSight.Identity.Application.Exceptions;

/// <summary>Maps to 400 Bad Request. The new password must differ from the current one, per docs/Authentication.md §4.</summary>
public sealed class SamePasswordException() : Exception("The new password must be different from the current password.");
