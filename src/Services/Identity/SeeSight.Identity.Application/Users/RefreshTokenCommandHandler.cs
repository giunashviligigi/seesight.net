using MediatR;
using Microsoft.EntityFrameworkCore;
using SeeSight.Identity.Application.Abstractions;
using SeeSight.Identity.Application.Exceptions;

namespace SeeSight.Identity.Application.Users;

public sealed class RefreshTokenCommandHandler(
    IIdentityDbContext dbContext,
    IJwtIssuer jwtIssuer,
    IOpaqueTokenGenerator tokenGenerator,
    ITokenHasher tokenHasher,
    TimeProvider timeProvider) : IRequestHandler<RefreshTokenCommand, AuthResult>
{
    public async Task<AuthResult> Handle(RefreshTokenCommand request, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var presentedHash = tokenHasher.Hash(request.RefreshToken);

        var presentedToken = await dbContext.RefreshTokens
            .SingleOrDefaultAsync(t => t.TokenHash == presentedHash, cancellationToken)
            .ConfigureAwait(false);

        if (presentedToken is null)
        {
            throw new InvalidRefreshTokenException();
        }

        if (presentedToken.RevokedAt is not null)
        {
            // Reuse of an already-rotated-away token — the legitimate client's copy
            // was revoked when it was exchanged; this presentation is either a stale
            // retry or a stolen copy. Treat as a compromise signal and revoke every
            // other active token for this user, per docs/Authentication.md §2.
            var activeTokensForUser = await dbContext.RefreshTokens
                .Where(t => t.UserId == presentedToken.UserId && t.RevokedAt == null)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            foreach (var token in activeTokensForUser)
            {
                token.Revoke(now);
            }

            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            throw new RefreshTokenReuseDetectedException();
        }

        if (!presentedToken.IsActive(now))
        {
            throw new InvalidRefreshTokenException();
        }

        var user = await dbContext.Users
            .SingleOrDefaultAsync(u => u.Id == presentedToken.UserId, cancellationToken)
            .ConfigureAwait(false);

        if (user is null || !user.CanAuthenticate)
        {
            throw new InvalidRefreshTokenException();
        }

        var accessToken = jwtIssuer.IssueAccessToken(user);
        var (newRefreshToken, rawRefreshToken) = RefreshTokenIssuance.IssueAndTrack(
            dbContext, tokenGenerator, tokenHasher, jwtIssuer, user.Id, request.IpAddress, now);

        presentedToken.Revoke(now, newRefreshToken.Id);

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return new AuthResult(
            accessToken.Value,
            accessToken.ExpiresAt,
            rawRefreshToken,
            newRefreshToken.ExpiresAt,
            UserDto.FromDomain(user));
    }
}
