using MediatR;

namespace SeeSight.Identity.Application.Users;

public sealed record ChangePasswordCommand(Guid UserId, string CurrentPassword, string NewPassword) : IRequest;
