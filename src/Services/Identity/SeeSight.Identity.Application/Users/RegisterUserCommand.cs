using MediatR;

namespace SeeSight.Identity.Application.Users;

public sealed record RegisterUserCommand(
    string Email,
    string Password,
    string? FirstName,
    string? LastName,
    string? IpAddress = null) : IRequest<AuthResult>;
