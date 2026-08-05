using FluentAssertions;
using NSubstitute;
using SeeSight.Identity.Application.Abstractions;
using SeeSight.Identity.Application.Exceptions;
using SeeSight.Identity.Application.Users;
using SeeSight.Identity.Domain;
using SeeSight.Identity.UnitTests.TestSupport;

namespace SeeSight.Identity.UnitTests.Application;

public sealed class LoginCommandHandlerTests : IDisposable
{
    private readonly FakeIdentityDbContext _dbContext = new();
    private readonly IPasswordHasher _passwordHasher = Substitute.For<IPasswordHasher>();
    private readonly IJwtIssuer _jwtIssuer = Substitute.For<IJwtIssuer>();

    public void Dispose() => _dbContext.Dispose();

    private LoginCommandHandler CreateHandler() => new(_dbContext, _passwordHasher, _jwtIssuer);

    private async Task<User> SeedUserAsync(string email = "user@example.com", string passwordHash = "hashed-password", UserStatus status = UserStatus.Active)
    {
        var user = User.Register(email, passwordHash, null, null, DateTimeOffset.UtcNow);
        if (status == UserStatus.Inactive)
        {
            // No public deactivation API exists yet in M1 — write the status
            // directly via EF Core change tracking to simulate a pre-existing
            // inactive row for this one negative test.
            _dbContext.Users.Add(user);
            await _dbContext.SaveChangesAsync();
            _dbContext.Entry(user).Property("Status").CurrentValue = status;
            await _dbContext.SaveChangesAsync();
            return user;
        }

        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();
        return user;
    }

    [Fact]
    public async Task Handle_returns_an_access_token_for_valid_credentials()
    {
        await SeedUserAsync("user@example.com", "hashed-password");
        _passwordHasher.Verify("correct-password", "hashed-password").Returns(true);
        var expectedExpiry = DateTimeOffset.UtcNow.AddMinutes(15);
        _jwtIssuer.IssueAccessToken(Arg.Any<User>()).Returns(new AccessToken("token-value", expectedExpiry));

        var handler = CreateHandler();
        var result = await handler.Handle(new LoginCommand("user@example.com", "correct-password"), CancellationToken.None);

        result.AccessToken.Should().Be("token-value");
        result.User.Email.Should().Be("user@example.com");
    }

    [Fact]
    public async Task Handle_throws_for_an_unknown_email()
    {
        _passwordHasher.Verify(Arg.Any<string>(), Arg.Any<string>()).Returns(true);

        var handler = CreateHandler();
        var act = () => handler.Handle(new LoginCommand("nobody@example.com", "whatever"), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidCredentialsException>();
    }

    [Fact]
    public async Task Handle_throws_for_a_wrong_password()
    {
        await SeedUserAsync("user@example.com", "hashed-password");
        _passwordHasher.Verify("wrong-password", "hashed-password").Returns(false);

        var handler = CreateHandler();
        var act = () => handler.Handle(new LoginCommand("user@example.com", "wrong-password"), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidCredentialsException>();
    }

    [Fact]
    public async Task Handle_throws_for_an_inactive_user_even_with_the_correct_password()
    {
        await SeedUserAsync("inactive@example.com", "hashed-password", UserStatus.Inactive);
        _passwordHasher.Verify("correct-password", "hashed-password").Returns(true);

        var handler = CreateHandler();
        var act = () => handler.Handle(new LoginCommand("inactive@example.com", "correct-password"), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidCredentialsException>();
    }

    [Fact]
    public async Task Handle_matches_email_case_insensitively()
    {
        await SeedUserAsync("user@example.com", "hashed-password");
        _passwordHasher.Verify("correct-password", "hashed-password").Returns(true);
        _jwtIssuer.IssueAccessToken(Arg.Any<User>()).Returns(new AccessToken("token-value", DateTimeOffset.UtcNow.AddMinutes(15)));

        var handler = CreateHandler();
        var result = await handler.Handle(new LoginCommand("USER@EXAMPLE.COM", "correct-password"), CancellationToken.None);

        result.User.Email.Should().Be("user@example.com");
    }
}
