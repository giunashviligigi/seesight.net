using MediatR;

namespace SeeSight.Identity.Application.Users;

public sealed record ForgotPasswordCommand(string Email) : IRequest<ForgotPasswordResult>;

/// <summary>
/// <see cref="DebugToken"/>/<see cref="DebugExpiresAt"/> are always populated by
/// the handler when a matching user exists — whether the Api layer actually
/// exposes them in the HTTP response is a hosting-environment decision
/// (Development only, never Staging/Production), not a business rule, so it's
/// made by the controller via <c>IHostEnvironment</c>, not here.
/// </summary>
public sealed record ForgotPasswordResult(string? DebugToken, DateTimeOffset? DebugExpiresAt);
