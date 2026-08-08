using FluentAssertions;
using NSubstitute;
using SeeSight.Identity.Application.Abstractions;
using SeeSight.Identity.Application.Exceptions;
using SeeSight.Identity.Application.Users;
using SeeSight.Identity.Domain;
using SeeSight.Identity.UnitTests.TestSupport;

namespace SeeSight.Identity.UnitTests.Application;

public sealed class RegisterUserCommandHandlerTests : IDisposable
{
    private readonly FakeIdentityDbContext _dbContext = new();
    private readonly IPasswordHasher _passwordHasher = Substitute.For<IPasswordHasher>();
    private readonly IJwtIssuer _jwtIssuer = Substitute.For<IJwtIssuer>();
    private readonly IOpaqueTokenGenerator _tokenGenerator = Substitute.For<IOpaqueTokenGenerator>();
    private readonly ITokenHasher _tokenHasher = Substitute.For<ITokenHasher>();
    private readonly TimeProvider _timeProvider = TimeProvider.System;

    public void Dispose() => _dbContext.Dispose();

    private RegisterUserCommandHandler CreateHandler() =>
        new(_dbContext, _passwordHasher, _jwtIssuer, _tokenGenerator, _tokenHasher, _timeProvider);

    [Fact]
    public async Task Handle_creates_a_user_and_returns_an_access_and_refresh_token_for_a_new_email()
    {
        _passwordHasher.Hash("SecurePass123").Returns("hashed-value");
        var accessExpiry = DateTimeOffset.UtcNow.AddMinutes(15);
        var refreshExpiry = DateTimeOffset.UtcNow.AddDays(30);
        _jwtIssuer.IssueAccessToken(Arg.Any<User>()).Returns(new AccessToken("access-token-value", accessExpiry));
        _jwtIssuer.ComputeRefreshTokenExpiry(Arg.Any<DateTimeOffset>()).Returns(refreshExpiry);
        _tokenGenerator.Generate().Returns("raw-refresh-token");
        _tokenHasher.Hash("raw-refresh-token").Returns("hashed-refresh-token");

        var handler = CreateHandler();
        var command = new RegisterUserCommand("new@example.com", "SecurePass123", "First", "Last");

        var result = await handler.Handle(command, CancellationToken.None);

        result.AccessToken.Should().Be("access-token-value");
        result.AccessTokenExpiresAt.Should().Be(accessExpiry);
        result.RefreshToken.Should().Be("raw-refresh-token");
        result.RefreshTokenExpiresAt.Should().Be(refreshExpiry);
        result.User.Email.Should().Be("new@example.com");

        var storedUser = _dbContext.Users.Single();
        storedUser.PasswordHash.Should().Be("hashed-value");

        var storedRefreshToken = _dbContext.RefreshTokens.Single();
        storedRefreshToken.TokenHash.Should().Be("hashed-refresh-token");
        storedRefreshToken.UserId.Should().Be(storedUser.Id);
    }

    [Fact]
    public async Task Handle_throws_when_the_email_is_already_registered()
    {
        _dbContext.Users.Add(User.Register("taken@example.com", "some-hash", null, null, DateTimeOffset.UtcNow));
        await _dbContext.SaveChangesAsync();

        _passwordHasher.Hash(Arg.Any<string>()).Returns("hashed-value");

        var handler = CreateHandler();
        var command = new RegisterUserCommand("taken@example.com", "SecurePass123", null, null);

        var act = () => handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<EmailAlreadyInUseException>();
    }

    [Fact]
    public async Task Handle_treats_email_uniqueness_case_insensitively()
    {
        _dbContext.Users.Add(User.Register("taken@example.com", "some-hash", null, null, DateTimeOffset.UtcNow));
        await _dbContext.SaveChangesAsync();

        _passwordHasher.Hash(Arg.Any<string>()).Returns("hashed-value");

        var handler = CreateHandler();
        var command = new RegisterUserCommand("TAKEN@EXAMPLE.COM", "SecurePass123", null, null);

        var act = () => handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<EmailAlreadyInUseException>();
    }
}
