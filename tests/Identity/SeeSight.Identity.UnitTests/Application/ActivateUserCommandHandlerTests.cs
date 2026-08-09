using FluentAssertions;
using SeeSight.Identity.Application.Exceptions;
using SeeSight.Identity.Application.Users;
using SeeSight.Identity.Domain;
using SeeSight.Identity.UnitTests.TestSupport;

namespace SeeSight.Identity.UnitTests.Application;

public sealed class ActivateUserCommandHandlerTests : IDisposable
{
    private readonly FakeIdentityDbContext _dbContext = new();
    private readonly TimeProvider _timeProvider = TimeProvider.System;

    public void Dispose() => _dbContext.Dispose();

    private ActivateUserCommandHandler CreateHandler() => new(_dbContext, _timeProvider);

    [Fact]
    public async Task Handle_activates_the_user()
    {
        var user = User.Register("someone@example.com", "hash", null, null, DateTimeOffset.UtcNow);
        user.Deactivate(DateTimeOffset.UtcNow);
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        var handler = CreateHandler();
        await handler.Handle(new ActivateUserCommand(user.Id), CancellationToken.None);

        var storedUser = _dbContext.Users.Single(u => u.Id == user.Id);
        storedUser.Status.Should().Be(UserStatus.Active);
    }

    [Fact]
    public async Task Handle_throws_for_an_unknown_user()
    {
        var handler = CreateHandler();
        var act = () => handler.Handle(new ActivateUserCommand(Guid.CreateVersion7()), CancellationToken.None);

        await act.Should().ThrowAsync<UserNotFoundException>();
    }
}
