namespace SeeSight.Identity.Application.Exceptions;

/// <summary>Maps to 404 Not Found — thrown by the internal admin-facing user operations.</summary>
public sealed class UserNotFoundException() : Exception("User not found.");
