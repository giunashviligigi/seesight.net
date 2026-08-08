using MediatR;

namespace SeeSight.Identity.Application.Users;

public sealed record ResetPasswordCommand(string Token, string NewPassword) : IRequest;
