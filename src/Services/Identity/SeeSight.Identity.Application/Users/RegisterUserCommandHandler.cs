using MediatR;
using Microsoft.EntityFrameworkCore;
using SeeSight.Identity.Application.Abstractions;
using SeeSight.Identity.Application.Exceptions;
using SeeSight.Identity.Domain;

namespace SeeSight.Identity.Application.Users;

public sealed class RegisterUserCommandHandler(
    IIdentityDbContext dbContext,
    IPasswordHasher passwordHasher,
    IJwtIssuer jwtIssuer,
    IOpaqueTokenGenerator tokenGenerator,
    ITokenHasher tokenHasher,
    TimeProvider timeProvider) : IRequestHandler<RegisterUserCommand, AuthResult>
{
    public async Task<AuthResult> Handle(RegisterUserCommand request, CancellationToken cancellationToken)
    {
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();

        var emailAlreadyExists = await dbContext.Users
            .AsNoTracking()
            .AnyAsync(u => u.Email == normalizedEmail, cancellationToken)
            .ConfigureAwait(false);

        if (emailAlreadyExists)
        {
            throw new EmailAlreadyInUseException();
        }

        var now = timeProvider.GetUtcNow();
        var passwordHash = passwordHasher.Hash(request.Password);
        var user = User.Register(request.Email, passwordHash, request.FirstName, request.LastName, now);

        dbContext.Users.Add(user);

        // Self-signup logs the user in immediately, same as login — the original
        // system sets the session cookie on both register and login (docs/Authentication.md §4).
        var accessToken = jwtIssuer.IssueAccessToken(user);
        var (refreshTokenEntity, rawRefreshToken) = RefreshTokenIssuance.IssueAndTrack(
            dbContext, tokenGenerator, tokenHasher, jwtIssuer, user.Id, request.IpAddress, now);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateException)
        {
            // The AnyAsync check above is not race-free under concurrent registration
            // attempts for the same email — the unique index on Users.Email is the
            // real guarantee; a violation here means we lost that race.
            throw new EmailAlreadyInUseException();
        }

        return new AuthResult(
            accessToken.Value,
            accessToken.ExpiresAt,
            rawRefreshToken,
            refreshTokenEntity.ExpiresAt,
            UserDto.FromDomain(user));
    }
}
