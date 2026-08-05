using FluentAssertions;
using SeeSight.Identity.Application.Exceptions;
using SeeSight.Identity.Application.Users;
using SeeSight.Identity.Domain;
using SeeSight.Identity.UnitTests.TestSupport;

namespace SeeSight.Identity.UnitTests.Application;

public sealed class GetCurrentUserQueryHandlerTests : IDisposable
{
    private readonly FakeIdentityDbContext _dbContext = new();

    public void Dispose() => _dbContext.Dispose();

    [Fact]
    public async Task Handle_returns_the_user_for_a_known_id()
    {
        var user = User.Register("someone@example.com", "hash", "First", "Last", DateTimeOffset.UtcNow);
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        var handler = new GetCurrentUserQueryHandler(_dbContext);
        var result = await handler.Handle(new GetCurrentUserQuery(user.Id), CancellationToken.None);

        result.Id.Should().Be(user.Id);
        result.Email.Should().Be("someone@example.com");
    }

    [Fact]
    public async Task Handle_throws_for_an_unknown_id()
    {
        var handler = new GetCurrentUserQueryHandler(_dbContext);
        var act = () => handler.Handle(new GetCurrentUserQuery(Guid.CreateVersion7()), CancellationToken.None);

        await act.Should().ThrowAsync<UserSessionInvalidException>();
    }

    [Fact]
    public async Task Handle_throws_for_an_inactive_user()
    {
        var user = User.Register("inactive@example.com", "hash", null, null, DateTimeOffset.UtcNow);
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();
        _dbContext.Entry(user).Property("Status").CurrentValue = UserStatus.Inactive;
        await _dbContext.SaveChangesAsync();

        var handler = new GetCurrentUserQueryHandler(_dbContext);
        var act = () => handler.Handle(new GetCurrentUserQuery(user.Id), CancellationToken.None);

        await act.Should().ThrowAsync<UserSessionInvalidException>();
    }
}
