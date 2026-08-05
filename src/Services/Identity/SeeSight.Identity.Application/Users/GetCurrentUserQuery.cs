using MediatR;

namespace SeeSight.Identity.Application.Users;

public sealed record GetCurrentUserQuery(Guid UserId) : IRequest<UserDto>;
