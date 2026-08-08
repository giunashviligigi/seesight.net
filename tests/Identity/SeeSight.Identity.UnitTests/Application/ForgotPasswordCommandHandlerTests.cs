using FluentAssertions;
using NSubstitute;
using SeeSight.Identity.Application.Abstractions;
using SeeSight.Identity.Application.Users;
using SeeSight.Identity.Domain;
using SeeSight.Identity.UnitTests.TestSupport;

namespace SeeSight.Identity.UnitTests.Application;

public sealed class ForgotPasswordCommandHandlerTests : IDisposable
{
    private readonly FakeIdentityDbContext _dbContext = new();
    private readonly IOpaqueTokenGenerator _tokenGenerator = Substitute.For<IOpaqueTokenGenerator>();
    private readonly ITokenHasher _tokenHasher = Substitute.For<ITokenHasher>();
    private readonly TimeProvider _timeProvider = TimeProvider.System;

    public void Dispose() => _dbContext.Dispose();

    private ForgotPasswordCommandHandler CreateHandler() =>
        new(_dbContext, _tokenGenerator, _tokenHasher, _timeProvider);

    [Fact]
    public async Task Handle_returns_no_token_for_an_unknown_email()
    {
        var handler = CreateHandler();

        var result = await handler.Handle(new ForgotPasswordCommand("nobody@example.com"), CancellationToken.None);

        result.DebugToken.Should().BeNull();
        result.DebugExpiresAt.Should().BeNull();
        _dbContext.PasswordResetTokens.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_issues_and_persists_a_reset_token_for_a_known_email()
    {
        var user = User.Register("user@example.com", "hashed-password", null, null, DateTimeOffset.UtcNow);
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();
        _tokenGenerator.Generate().Returns("raw-reset-token");
        _tokenHasher.Hash("raw-reset-token").Returns("hashed-reset-token");

        var handler = CreateHandler();
        var result = await handler.Handle(new ForgotPasswordCommand("user@example.com"), CancellationToken.None);

        result.DebugToken.Should().Be("raw-reset-token");
        result.DebugExpiresAt.Should().NotBeNull();

        var storedToken = _dbContext.PasswordResetTokens.Single();
        storedToken.UserId.Should().Be(user.Id);
        storedToken.TokenHash.Should().Be("hashed-reset-token");
    }

    [Fact]
    public async Task Handle_matches_email_case_insensitively()
    {
        var user = User.Register("user@example.com", "hashed-password", null, null, DateTimeOffset.UtcNow);
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();
        _tokenGenerator.Generate().Returns("raw-reset-token");
        _tokenHasher.Hash("raw-reset-token").Returns("hashed-reset-token");

        var handler = CreateHandler();
        var result = await handler.Handle(new ForgotPasswordCommand("USER@EXAMPLE.COM"), CancellationToken.None);

        result.DebugToken.Should().Be("raw-reset-token");
    }
}
