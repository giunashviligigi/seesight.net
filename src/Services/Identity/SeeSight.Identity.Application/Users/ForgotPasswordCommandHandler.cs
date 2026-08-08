using MediatR;
using Microsoft.EntityFrameworkCore;
using SeeSight.Identity.Application.Abstractions;
using SeeSight.Identity.Domain;

namespace SeeSight.Identity.Application.Users;

public sealed class ForgotPasswordCommandHandler(
    IIdentityDbContext dbContext,
    IOpaqueTokenGenerator tokenGenerator,
    ITokenHasher tokenHasher,
    TimeProvider timeProvider) : IRequestHandler<ForgotPasswordCommand, ForgotPasswordResult>
{
    // Fixed business rule, not deployment config — docs/Authentication.md §4
    // states "1-hour expiry" as a fact, unlike the access/refresh token
    // lifetimes which are explicitly documented as configurable.
    private static readonly TimeSpan TokenLifetime = TimeSpan.FromHours(1);

    public async Task<ForgotPasswordResult> Handle(ForgotPasswordCommand request, CancellationToken cancellationToken)
    {
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();

        var userId = await dbContext.Users
            .AsNoTracking()
            .Where(u => u.Email == normalizedEmail)
            .Select(u => (Guid?)u.Id)
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        // Always a generic success — never reveal whether the email exists
        // (docs/Authentication.md §4, no user-enumeration).
        if (userId is null)
        {
            return new ForgotPasswordResult(null, null);
        }

        var now = timeProvider.GetUtcNow();
        var rawToken = tokenGenerator.Generate();
        var tokenHash = tokenHasher.Hash(rawToken);
        var expiresAt = now.Add(TokenLifetime);

        var resetToken = PasswordResetToken.Issue(userId.Value, tokenHash, expiresAt, now);
        dbContext.PasswordResetTokens.Add(resetToken);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return new ForgotPasswordResult(rawToken, expiresAt);
    }
}
