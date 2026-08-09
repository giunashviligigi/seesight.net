using MediatR;

namespace SeeSight.Identity.Application.Users;

public sealed record ActivateUserCommand(Guid UserId) : IRequest;
