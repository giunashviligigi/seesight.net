using FluentAssertions;
using NSubstitute;
using SeeSight.Identity.Application.Abstractions;
using SeeSight.Identity.Application.Exceptions;
using SeeSight.Identity.Application.Users;
using SeeSight.Identity.Domain;
using SeeSight.Identity.UnitTests.TestSupport;

namespace SeeSight.Identity.UnitTests.Application;

public sealed class ChangePasswordCommandHandlerTests : IDisposable
{
    private readonly FakeIdentityDbContext _dbContext = new();
    private readonly IPasswordHasher _passwordHasher = Substitute.For<IPasswordHasher>();
    private readonly TimeProvider _timeProvider = TimeProvider.System;

    public void Dispose() => _dbContext.Dispose();

    private ChangePasswordCommandHandler CreateHandler() => new(_dbContext, _passwordHasher, _timeProvider);

    private async Task<User> SeedUserAsync(string passwordHash = "old-hash")
    {
        var user = User.Register("user@example.com", passwordHash, null, null, DateTimeOffset.UtcNow);
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();
        return user;
    }

    [Fact]
    public async Task Handle_changes_the_password_and_clears_MustChangePassword()
    {
        var user = await SeedUserAsync();
        _passwordHasher.Verify("CurrentPass123", "old-hash").Returns(true);
        _passwordHasher.Hash("NewSecurePass123").Returns("new-hash");

        var handler = CreateHandler();
        await handler.Handle(new ChangePasswordCommand(user.Id, "CurrentPass123", "NewSecurePass123"), CancellationToken.None);

        var storedUser = _dbContext.Users.Single(u => u.Id == user.Id);
        storedUser.PasswordHash.Should().Be("new-hash");
        storedUser.MustChangePassword.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_throws_for_an_unknown_user()
    {
        var handler = CreateHandler();
        var act = () => handler.Handle(new ChangePasswordCommand(Guid.CreateVersion7(), "CurrentPass123", "NewSecurePass123"), CancellationToken.None);

        await act.Should().ThrowAsync<UserSessionInvalidException>();
    }

    [Fact]
    public async Task Handle_throws_for_an_inactive_user()
    {
        var user = await SeedUserAsync();
        _dbContext.Entry(user).Property("Status").CurrentValue = UserStatus.Inactive;
        await _dbContext.SaveChangesAsync();

        var handler = CreateHandler();
        var act = () => handler.Handle(new ChangePasswordCommand(user.Id, "CurrentPass123", "NewSecurePass123"), CancellationToken.None);

        await act.Should().ThrowAsync<UserSessionInvalidException>();
    }

    [Fact]
    public async Task Handle_throws_when_the_current_password_is_wrong()
    {
        var user = await SeedUserAsync();
        _passwordHasher.Verify("WrongPass123", "old-hash").Returns(false);

        var handler = CreateHandler();
        var act = () => handler.Handle(new ChangePasswordCommand(user.Id, "WrongPass123", "NewSecurePass123"), CancellationToken.None);

        await act.Should().ThrowAsync<CurrentPasswordIncorrectException>();
    }

    [Fact]
    public async Task Handle_throws_when_the_new_password_matches_the_current_password()
    {
        var user = await SeedUserAsync();
        _passwordHasher.Verify("SamePass123", "old-hash").Returns(true);

        var handler = CreateHandler();
        var act = () => handler.Handle(new ChangePasswordCommand(user.Id, "SamePass123", "SamePass123"), CancellationToken.None);

        await act.Should().ThrowAsync<SamePasswordException>();
    }
}
