using MediatR;

namespace SeeSight.Identity.Application.Users;

public sealed record DeactivateUserCommand(Guid UserId) : IRequest;
