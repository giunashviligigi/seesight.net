namespace SeeSight.Identity.Application.Exceptions;

/// <summary>
/// Maps to 400 Bad Request — the caller is already authenticated (this is only
/// reachable via the change-password endpoint), so this is a validation failure
/// on the submitted current password, not an authentication failure.
/// </summary>
public sealed class CurrentPasswordIncorrectException() : Exception("The current password is incorrect.");
