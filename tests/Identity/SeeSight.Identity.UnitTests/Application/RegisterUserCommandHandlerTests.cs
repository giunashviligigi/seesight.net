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
    private readonly TimeProvider _timeProvider = TimeProvider.System;

    public void Dispose() => _dbContext.Dispose();

    private RegisterUserCommandHandler CreateHandler() =>
        new(_dbContext, _passwordHasher, _jwtIssuer, _timeProvider);

    [Fact]
    public async Task Handle_creates_a_user_and_returns_an_access_token_for_a_new_email()
    {
        _passwordHasher.Hash("SecurePass123").Returns("hashed-value");
        var expectedExpiry = DateTimeOffset.UtcNow.AddMinutes(15);
        _jwtIssuer.IssueAccessToken(Arg.Any<User>()).Returns(new AccessToken("token-value", expectedExpiry));

        var handler = CreateHandler();
        var command = new RegisterUserCommand("new@example.com", "SecurePass123", "First", "Last");

        var result = await handler.Handle(command, CancellationToken.None);

        result.AccessToken.Should().Be("token-value");
        result.ExpiresAt.Should().Be(expectedExpiry);
        result.User.Email.Should().Be("new@example.com");

        var stored = _dbContext.Users.Single();
        stored.PasswordHash.Should().Be("hashed-value");
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
