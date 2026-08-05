using MediatR;
using Microsoft.EntityFrameworkCore;
using SeeSight.Identity.Application.Abstractions;
using SeeSight.Identity.Application.Exceptions;

namespace SeeSight.Identity.Application.Users;

public sealed class GetCurrentUserQueryHandler(IIdentityDbContext dbContext)
    : IRequestHandler<GetCurrentUserQuery, UserDto>
{
    public async Task<UserDto> Handle(GetCurrentUserQuery request, CancellationToken cancellationToken)
    {
        var user = await dbContext.Users
            .AsNoTracking()
            .SingleOrDefaultAsync(u => u.Id == request.UserId, cancellationToken)
            .ConfigureAwait(false);

        if (user is null || !user.CanAuthenticate)
        {
            throw new UserSessionInvalidException();
        }

        return UserDto.FromDomain(user);
    }
}
