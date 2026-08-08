namespace SeeSight.Identity.Api.Contracts;

/// <summary>
/// Always the same generic message regardless of whether the email exists (no
/// user-enumeration, docs/Authentication.md §4). <see cref="DebugToken"/>/
/// <see cref="DebugResetUrl"/> are populated only in the Development
/// environment, and only when a matching user actually exists.
/// </summary>
public sealed record ForgotPasswordResponse(string Message, string? DebugToken, string? DebugResetUrl);
