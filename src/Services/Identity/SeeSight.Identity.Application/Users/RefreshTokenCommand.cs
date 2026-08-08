using MediatR;

namespace SeeSight.Identity.Application.Users;

public sealed record RefreshTokenCommand(string RefreshToken, string? IpAddress = null) : IRequest<AuthResult>;
