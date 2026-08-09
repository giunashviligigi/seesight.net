using MediatR;
using Microsoft.EntityFrameworkCore;
using SeeSight.Identity.Application.Abstractions;
using SeeSight.Identity.Application.Exceptions;
using SeeSight.Identity.Domain;

namespace SeeSight.Identity.Application.Users;

public sealed class ProvisionEmployeeUserCommandHandler(
    IIdentityDbContext dbContext,
    IPasswordHasher passwordHasher,
    IOpaqueTokenGenerator tokenGenerator,
    TimeProvider timeProvider) : IRequestHandler<ProvisionEmployeeUserCommand, ProvisionEmployeeUserResult>
{
    public async Task<ProvisionEmployeeUserResult> Handle(ProvisionEmployeeUserCommand request, CancellationToken cancellationToken)
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

        // The temp password only needs to be a securely random, one-time secret —
        // the caller must change it immediately (MustChangePassword=true), so it
        // never needs to be short/memorable. Reusing the existing opaque-token
        // generator avoids inventing a second random-secret abstraction.
        var tempPassword = tokenGenerator.Generate();
        var passwordHash = passwordHasher.Hash(tempPassword);

        var user = User.ProvisionForEmployee(request.Email, passwordHash, request.FirstName, request.LastName, request.CompanyId, now);
        dbContext.Users.Add(user);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateException)
        {
            throw new EmailAlreadyInUseException();
        }

        return new ProvisionEmployeeUserResult(user.Id, tempPassword);
    }
}
