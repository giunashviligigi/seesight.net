using FluentAssertions;
using SeeSight.Identity.Application.Exceptions;
using SeeSight.Identity.Application.Users;
using SeeSight.Identity.Domain;
using SeeSight.Identity.UnitTests.TestSupport;

namespace SeeSight.Identity.UnitTests.Application;

public sealed class DeactivateUserCommandHandlerTests : IDisposable
{
    private readonly FakeIdentityDbContext _dbContext = new();
    private readonly TimeProvider _timeProvider = TimeProvider.System;

    public void Dispose() => _dbContext.Dispose();

    private DeactivateUserCommandHandler CreateHandler() => new(_dbContext, _timeProvider);

    [Fact]
    public async Task Handle_deactivates_the_user()
    {
        var user = User.Register("someone@example.com", "hash", null, null, DateTimeOffset.UtcNow);
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        var handler = CreateHandler();
        await handler.Handle(new DeactivateUserCommand(user.Id), CancellationToken.None);

        var storedUser = _dbContext.Users.Single(u => u.Id == user.Id);
        storedUser.Status.Should().Be(UserStatus.Inactive);
    }

    [Fact]
    public async Task Handle_throws_for_an_unknown_user()
    {
        var handler = CreateHandler();
        var act = () => handler.Handle(new DeactivateUserCommand(Guid.CreateVersion7()), CancellationToken.None);

        await act.Should().ThrowAsync<UserNotFoundException>();
    }
}
