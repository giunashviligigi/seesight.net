namespace SeeSight.Identity.Application.Exceptions;

/// <summary>Maps to 409 Conflict at the Api layer.</summary>
public sealed class EmailAlreadyInUseException() : Exception("A user with this email already exists.");
