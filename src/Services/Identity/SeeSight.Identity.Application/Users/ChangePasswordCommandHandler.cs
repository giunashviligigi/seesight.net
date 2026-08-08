using MediatR;
using Microsoft.EntityFrameworkCore;
using SeeSight.Identity.Application.Abstractions;
using SeeSight.Identity.Application.Exceptions;

namespace SeeSight.Identity.Application.Users;

public sealed class ChangePasswordCommandHandler(
    IIdentityDbContext dbContext,
    IPasswordHasher passwordHasher,
    TimeProvider timeProvider) : IRequestHandler<ChangePasswordCommand>
{
    public async Task Handle(ChangePasswordCommand request, CancellationToken cancellationToken)
    {
        var user = await dbContext.Users
            .SingleOrDefaultAsync(u => u.Id == request.UserId, cancellationToken)
            .ConfigureAwait(false);

        if (user is null || !user.CanAuthenticate)
        {
            throw new UserSessionInvalidException();
        }

        if (!passwordHasher.Verify(request.CurrentPassword, user.PasswordHash))
        {
            throw new CurrentPasswordIncorrectException();
        }

        if (request.NewPassword == request.CurrentPassword)
        {
            throw new SamePasswordException();
        }

        var newPasswordHash = passwordHasher.Hash(request.NewPassword);
        user.SetPasswordHash(newPasswordHash, timeProvider.GetUtcNow());

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
