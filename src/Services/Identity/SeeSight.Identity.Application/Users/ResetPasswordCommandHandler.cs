using MediatR;
using Microsoft.EntityFrameworkCore;
using SeeSight.Identity.Application.Abstractions;
using SeeSight.Identity.Application.Exceptions;

namespace SeeSight.Identity.Application.Users;

public sealed class ResetPasswordCommandHandler(
    IIdentityDbContext dbContext,
    IPasswordHasher passwordHasher,
    ITokenHasher tokenHasher,
    TimeProvider timeProvider) : IRequestHandler<ResetPasswordCommand>
{
    public async Task Handle(ResetPasswordCommand request, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var tokenHash = tokenHasher.Hash(request.Token);

        var resetToken = await dbContext.PasswordResetTokens
            .SingleOrDefaultAsync(t => t.TokenHash == tokenHash, cancellationToken)
            .ConfigureAwait(false);

        if (resetToken is null || !resetToken.IsValid(now))
        {
            throw new InvalidPasswordResetTokenException();
        }

        var user = await dbContext.Users
            .SingleOrDefaultAsync(u => u.Id == resetToken.UserId, cancellationToken)
            .ConfigureAwait(false);

        if (user is null || !user.CanAuthenticate)
        {
            throw new InvalidPasswordResetTokenException();
        }

        var newPasswordHash = passwordHasher.Hash(request.NewPassword);
        user.SetPasswordHash(newPasswordHash, now);
        resetToken.MarkUsed(now);

        // Single SaveChangesAsync — both entity changes commit in one transaction,
        // per docs/Authentication.md §4 ("inside one DB transaction").
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
