namespace SeeSight.Identity.Application.Exceptions;

/// <summary>Maps to 400 Bad Request. Thrown for a missing, expired, or already-used password reset token.</summary>
public sealed class InvalidPasswordResetTokenException() : Exception("Invalid or expired password reset token.");
