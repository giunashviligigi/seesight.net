using MediatR;
using Microsoft.EntityFrameworkCore;
using SeeSight.Identity.Application.Abstractions;
using SeeSight.Identity.Application.Exceptions;

namespace SeeSight.Identity.Application.Users;

public sealed class LoginCommandHandler(
    IIdentityDbContext dbContext,
    IPasswordHasher passwordHasher,
    IJwtIssuer jwtIssuer) : IRequestHandler<LoginCommand, AuthResult>
{
    public async Task<AuthResult> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();

        var user = await dbContext.Users
            .SingleOrDefaultAsync(u => u.Email == normalizedEmail, cancellationToken)
            .ConfigureAwait(false);

        // Generic failure for both "no such user" and "wrong password" — never
        // reveal which, per docs/Authentication.md §4 (no user-enumeration).
        if (user is null || !user.CanAuthenticate || !passwordHasher.Verify(request.Password, user.PasswordHash))
        {
            throw new InvalidCredentialsException();
        }

        var accessToken = jwtIssuer.IssueAccessToken(user);

        return new AuthResult(accessToken.Value, accessToken.ExpiresAt, UserDto.FromDomain(user));
    }
}
