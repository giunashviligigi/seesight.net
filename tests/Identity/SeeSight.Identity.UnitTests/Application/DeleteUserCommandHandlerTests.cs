using FluentAssertions;
using SeeSight.Identity.Application.Users;
using SeeSight.Identity.Domain;
using SeeSight.Identity.UnitTests.TestSupport;

namespace SeeSight.Identity.UnitTests.Application;

public sealed class DeleteUserCommandHandlerTests : IDisposable
{
    private readonly FakeIdentityDbContext _dbContext = new();

    public void Dispose() => _dbContext.Dispose();

    private DeleteUserCommandHandler CreateHandler() => new(_dbContext);

    [Fact]
    public async Task Handle_hard_deletes_the_user()
    {
        var user = User.Register("someone@example.com", "hash", null, null, DateTimeOffset.UtcNow);
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        var handler = CreateHandler();
        await handler.Handle(new DeleteUserCommand(user.Id), CancellationToken.None);

        _dbContext.Users.Any(u => u.Id == user.Id).Should().BeFalse();
    }

    [Fact]
    public async Task Handle_is_idempotent_for_an_unknown_user()
    {
        var handler = CreateHandler();
        var act = () => handler.Handle(new DeleteUserCommand(Guid.CreateVersion7()), CancellationToken.None);

        await act.Should().NotThrowAsync();
    }
}
