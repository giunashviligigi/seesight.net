using FluentAssertions;
using NSubstitute;
using SeeSight.Identity.Application.Abstractions;
using SeeSight.Identity.Application.Users;
using SeeSight.Identity.Domain;
using SeeSight.Identity.UnitTests.TestSupport;

namespace SeeSight.Identity.UnitTests.Application;

public sealed class LogoutCommandHandlerTests : IDisposable
{
    private readonly FakeIdentityDbContext _dbContext = new();
    private readonly ITokenHasher _tokenHasher = Substitute.For<ITokenHasher>();
    private readonly TimeProvider _timeProvider = TimeProvider.System;

    public void Dispose() => _dbContext.Dispose();

    private LogoutCommandHandler CreateHandler() => new(_dbContext, _tokenHasher, _timeProvider);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Handle_is_a_no_op_for_a_missing_token(string? refreshToken)
    {
        var handler = CreateHandler();

        await handler.Handle(new LogoutCommand(refreshToken), CancellationToken.None);

        _tokenHasher.DidNotReceive().Hash(Arg.Any<string>());
    }

    [Fact]
    public async Task Handle_is_a_no_op_for_an_unknown_token()
    {
        _tokenHasher.Hash("unknown-token").Returns("unknown-hash");

        var handler = CreateHandler();
        var act = () => handler.Handle(new LogoutCommand("unknown-token"), CancellationToken.None);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task Handle_revokes_a_known_active_token()
    {
        var user = User.Register("user@example.com", "hashed-password", null, null, DateTimeOffset.UtcNow);
        _dbContext.Users.Add(user);
        var token = RefreshToken.Issue(user.Id, "hashed-token", DateTimeOffset.UtcNow.AddDays(30), null, DateTimeOffset.UtcNow);
        _dbContext.RefreshTokens.Add(token);
        await _dbContext.SaveChangesAsync();
        _tokenHasher.Hash("raw-token").Returns("hashed-token");

        var handler = CreateHandler();
        await handler.Handle(new LogoutCommand("raw-token"), CancellationToken.None);

        var storedToken = _dbContext.RefreshTokens.Single(t => t.Id == token.Id);
        storedToken.RevokedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Handle_is_idempotent_for_an_already_revoked_token()
    {
        var user = User.Register("user@example.com", "hashed-password", null, null, DateTimeOffset.UtcNow);
        _dbContext.Users.Add(user);
        var token = RefreshToken.Issue(user.Id, "hashed-token", DateTimeOffset.UtcNow.AddDays(30), null, DateTimeOffset.UtcNow);
        token.Revoke(DateTimeOffset.UtcNow.AddMinutes(-1));
        _dbContext.RefreshTokens.Add(token);
        await _dbContext.SaveChangesAsync();
        _tokenHasher.Hash("raw-token").Returns("hashed-token");

        var handler = CreateHandler();
        var act = () => handler.Handle(new LogoutCommand("raw-token"), CancellationToken.None);

        await act.Should().NotThrowAsync();
    }
}
