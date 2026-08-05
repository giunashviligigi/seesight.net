using MediatR;

namespace SeeSight.Identity.Application.Users;

public sealed record LoginCommand(string Email, string Password) : IRequest<AuthResult>;
