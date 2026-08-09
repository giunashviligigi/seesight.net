using MediatR;
using Microsoft.EntityFrameworkCore;
using SeeSight.Identity.Application.Abstractions;
using SeeSight.Identity.Application.Exceptions;

namespace SeeSight.Identity.Application.Users;

public sealed class UpdateInternalUserCommandHandler(
    IIdentityDbContext dbContext,
    TimeProvider timeProvider) : IRequestHandler<UpdateInternalUserCommand>
{
    public async Task Handle(UpdateInternalUserCommand request, CancellationToken cancellationToken)
    {
        var user = await dbContext.Users
            .SingleOrDefaultAsync(u => u.Id == request.UserId, cancellationToken)
            .ConfigureAwait(false);

        if (user is null)
        {
            throw new UserNotFoundException();
        }

        var now = timeProvider.GetUtcNow();

        if (request.FirstName is not null || request.LastName is not null)
        {
            user.UpdateProfile(request.FirstName, request.LastName, now);
        }

        if (request.ClearCompanyId)
        {
            user.AssignToCompany(null, now);
        }
        else if (request.CompanyId is not null)
        {
            user.AssignToCompany(request.CompanyId, now);
        }

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
