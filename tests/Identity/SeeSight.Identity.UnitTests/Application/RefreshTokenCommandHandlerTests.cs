using FluentAssertions;
using NSubstitute;
using SeeSight.Identity.Application.Abstractions;
using SeeSight.Identity.Application.Exceptions;
using SeeSight.Identity.Application.Users;
using SeeSight.Identity.Domain;
using SeeSight.Identity.UnitTests.TestSupport;

namespace SeeSight.Identity.UnitTests.Application;

public sealed class RefreshTokenCommandHandlerTests : IDisposable
{
    private readonly FakeIdentityDbContext _dbContext = new();
    private readonly IJwtIssuer _jwtIssuer = Substitute.For<IJwtIssuer>();
    private readonly IOpaqueTokenGenerator _tokenGenerator = Substitute.For<IOpaqueTokenGenerator>();
    private readonly ITokenHasher _tokenHasher = Substitute.For<ITokenHasher>();
    private readonly TimeProvider _timeProvider = TimeProvider.System;

    public RefreshTokenCommandHandlerTests()
    {
        _jwtIssuer.ComputeRefreshTokenExpiry(Arg.Any<DateTimeOffset>()).Returns(DateTimeOffset.UtcNow.AddDays(30));
    }

    public void Dispose() => _dbContext.Dispose();

    private RefreshTokenCommandHandler CreateHandler() =>
        new(_dbContext, _jwtIssuer, _tokenGenerator, _tokenHasher, _timeProvider);

    private async Task<(User User, RefreshToken Token)> SeedActiveTokenAsync(string rawToken = "raw-token", string tokenHash = "hashed-token")
    {
        var user = User.Register("user@example.com", "hashed-password", null, null, DateTimeOffset.UtcNow);
        _dbContext.Users.Add(user);

        var token = RefreshToken.Issue(user.Id, tokenHash, DateTimeOffset.UtcNow.AddDays(30), "203.0.113.5", DateTimeOffset.UtcNow);
        _dbContext.RefreshTokens.Add(token);
        await _dbContext.SaveChangesAsync();

        _tokenHasher.Hash(rawToken).Returns(tokenHash);
        return (user, token);
    }

    [Fact]
    public async Task Handle_rotates_the_token_and_returns_a_new_access_and_refresh_token()
    {
        var (user, oldToken) = await SeedActiveTokenAsync();
        var accessExpiry = DateTimeOffset.UtcNow.AddMinutes(15);
        _jwtIssuer.IssueAccessToken(Arg.Any<User>()).Returns(new AccessToken("new-access-token", accessExpiry));
        _tokenGenerator.Generate().Returns("new-raw-refresh-token");
        _tokenHasher.Hash("new-raw-refresh-token").Returns("new-hashed-refresh-token");

        var handler = CreateHandler();
        var result = await handler.Handle(new RefreshTokenCommand("raw-token"), CancellationToken.None);

        result.AccessToken.Should().Be("new-access-token");
        result.AccessTokenExpiresAt.Should().Be(accessExpiry);
        result.RefreshToken.Should().Be("new-raw-refresh-token");
        result.User.Id.Should().Be(user.Id);

        var refreshedOldToken = _dbContext.RefreshTokens.Single(t => t.Id == oldToken.Id);
        refreshedOldToken.RevokedAt.Should().NotBeNull();

        var newToken = _dbContext.RefreshTokens.Single(t => t.TokenHash == "new-hashed-refresh-token");
        refreshedOldToken.ReplacedByTokenId.Should().Be(newToken.Id);
        newToken.RevokedAt.Should().BeNull();
    }

    [Fact]
    public async Task Handle_throws_for_an_unknown_token()
    {
        _tokenHasher.Hash("unknown-token").Returns("unknown-hash");

        var handler = CreateHandler();
        var act = () => handler.Handle(new RefreshTokenCommand("unknown-token"), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidRefreshTokenException>();
    }

    [Fact]
    public async Task Handle_throws_for_an_expired_token()
    {
        var user = User.Register("user@example.com", "hashed-password", null, null, DateTimeOffset.UtcNow);
        _dbContext.Users.Add(user);
        var expiredToken = RefreshToken.Issue(user.Id, "hashed-token", DateTimeOffset.UtcNow.AddMilliseconds(50), null, DateTimeOffset.UtcNow);
        _dbContext.RefreshTokens.Add(expiredToken);
        await _dbContext.SaveChangesAsync();
        _tokenHasher.Hash("raw-token").Returns("hashed-token");

        // Let real time pass the token's short expiry window rather than
        // reaching into the entity's private state.
        await Task.Delay(150);

        var handler = CreateHandler();
        var act = () => handler.Handle(new RefreshTokenCommand("raw-token"), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidRefreshTokenException>();
    }

    [Fact]
    public async Task Handle_detects_reuse_of_an_already_revoked_token_and_revokes_every_active_token_for_the_user()
    {
        var (user, revokedToken) = await SeedActiveTokenAsync();
        revokedToken.Revoke(DateTimeOffset.UtcNow.AddMinutes(-5));

        var otherActiveToken = RefreshToken.Issue(user.Id, "other-hash", DateTimeOffset.UtcNow.AddDays(30), null, DateTimeOffset.UtcNow);
        _dbContext.RefreshTokens.Add(otherActiveToken);
        await _dbContext.SaveChangesAsync();

        var handler = CreateHandler();
        var act = () => handler.Handle(new RefreshTokenCommand("raw-token"), CancellationToken.None);

        await act.Should().ThrowAsync<RefreshTokenReuseDetectedException>();

        var refreshedOtherToken = _dbContext.RefreshTokens.Single(t => t.Id == otherActiveToken.Id);
        refreshedOtherToken.RevokedAt.Should().NotBeNull("reuse of a rotated-away token must revoke every other active token for the user");
    }

    [Fact]
    public async Task Handle_throws_for_an_inactive_user()
    {
        var user = User.Register("inactive@example.com", "hashed-password", null, null, DateTimeOffset.UtcNow);
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();
        _dbContext.Entry(user).Property("Status").CurrentValue = UserStatus.Inactive;
        await _dbContext.SaveChangesAsync();

        var token = RefreshToken.Issue(user.Id, "hashed-token", DateTimeOffset.UtcNow.AddDays(30), null, DateTimeOffset.UtcNow);
        _dbContext.RefreshTokens.Add(token);
        await _dbContext.SaveChangesAsync();
        _tokenHasher.Hash("raw-token").Returns("hashed-token");

        var handler = CreateHandler();
        var act = () => handler.Handle(new RefreshTokenCommand("raw-token"), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidRefreshTokenException>();
    }
}
