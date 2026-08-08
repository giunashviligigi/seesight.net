using FluentAssertions;
using NSubstitute;
using SeeSight.Identity.Application.Abstractions;
using SeeSight.Identity.Application.Exceptions;
using SeeSight.Identity.Application.Users;
using SeeSight.Identity.Domain;
using SeeSight.Identity.UnitTests.TestSupport;

namespace SeeSight.Identity.UnitTests.Application;

public sealed class ResetPasswordCommandHandlerTests : IDisposable
{
    private readonly FakeIdentityDbContext _dbContext = new();
    private readonly IPasswordHasher _passwordHasher = Substitute.For<IPasswordHasher>();
    private readonly ITokenHasher _tokenHasher = Substitute.For<ITokenHasher>();
    private readonly TimeProvider _timeProvider = TimeProvider.System;

    public void Dispose() => _dbContext.Dispose();

    private ResetPasswordCommandHandler CreateHandler() =>
        new(_dbContext, _passwordHasher, _tokenHasher, _timeProvider);

    private async Task<(User User, PasswordResetToken Token)> SeedValidTokenAsync(string rawToken = "raw-reset-token", string tokenHash = "hashed-reset-token")
    {
        var user = User.Register("user@example.com", "old-hash", null, null, DateTimeOffset.UtcNow);
        _dbContext.Users.Add(user);
        var token = PasswordResetToken.Issue(user.Id, tokenHash, DateTimeOffset.UtcNow.AddHours(1), DateTimeOffset.UtcNow);
        _dbContext.PasswordResetTokens.Add(token);
        await _dbContext.SaveChangesAsync();
        _tokenHasher.Hash(rawToken).Returns(tokenHash);
        return (user, token);
    }

    [Fact]
    public async Task Handle_resets_the_password_and_marks_the_token_used()
    {
        var (user, token) = await SeedValidTokenAsync();
        _passwordHasher.Hash("NewSecurePass123").Returns("new-hash");

        var handler = CreateHandler();
        await handler.Handle(new ResetPasswordCommand("raw-reset-token", "NewSecurePass123"), CancellationToken.None);

        var storedUser = _dbContext.Users.Single(u => u.Id == user.Id);
        storedUser.PasswordHash.Should().Be("new-hash");
        storedUser.MustChangePassword.Should().BeFalse();

        var storedToken = _dbContext.PasswordResetTokens.Single(t => t.Id == token.Id);
        storedToken.UsedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Handle_throws_for_an_unknown_token()
    {
        _tokenHasher.Hash("unknown-token").Returns("unknown-hash");

        var handler = CreateHandler();
        var act = () => handler.Handle(new ResetPasswordCommand("unknown-token", "NewSecurePass123"), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidPasswordResetTokenException>();
    }

    [Fact]
    public async Task Handle_throws_for_an_already_used_token()
    {
        var (_, token) = await SeedValidTokenAsync();
        token.MarkUsed(DateTimeOffset.UtcNow.AddMinutes(-1));
        await _dbContext.SaveChangesAsync();

        var handler = CreateHandler();
        var act = () => handler.Handle(new ResetPasswordCommand("raw-reset-token", "NewSecurePass123"), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidPasswordResetTokenException>();
    }

    [Fact]
    public async Task Handle_throws_for_an_expired_token()
    {
        var user = User.Register("user@example.com", "old-hash", null, null, DateTimeOffset.UtcNow);
        _dbContext.Users.Add(user);
        var token = PasswordResetToken.Issue(user.Id, "hashed-reset-token", DateTimeOffset.UtcNow.AddMilliseconds(50), DateTimeOffset.UtcNow);
        _dbContext.PasswordResetTokens.Add(token);
        await _dbContext.SaveChangesAsync();
        _tokenHasher.Hash("raw-reset-token").Returns("hashed-reset-token");

        await Task.Delay(150);

        var handler = CreateHandler();
        var act = () => handler.Handle(new ResetPasswordCommand("raw-reset-token", "NewSecurePass123"), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidPasswordResetTokenException>();
    }

    [Fact]
    public async Task Handle_throws_for_an_inactive_user()
    {
        var user = User.Register("user@example.com", "old-hash", null, null, DateTimeOffset.UtcNow);
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();
        _dbContext.Entry(user).Property("Status").CurrentValue = UserStatus.Inactive;
        await _dbContext.SaveChangesAsync();

        var token = PasswordResetToken.Issue(user.Id, "hashed-reset-token", DateTimeOffset.UtcNow.AddHours(1), DateTimeOffset.UtcNow);
        _dbContext.PasswordResetTokens.Add(token);
        await _dbContext.SaveChangesAsync();
        _tokenHasher.Hash("raw-reset-token").Returns("hashed-reset-token");

        var handler = CreateHandler();
        var act = () => handler.Handle(new ResetPasswordCommand("raw-reset-token", "NewSecurePass123"), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidPasswordResetTokenException>();
    }
}
