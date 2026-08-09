using FluentAssertions;
using NSubstitute;
using SeeSight.Identity.Application.Abstractions;
using SeeSight.Identity.Application.Exceptions;
using SeeSight.Identity.Application.Users;
using SeeSight.Identity.Domain;
using SeeSight.Identity.UnitTests.TestSupport;

namespace SeeSight.Identity.UnitTests.Application;

public sealed class ProvisionEmployeeUserCommandHandlerTests : IDisposable
{
    private readonly FakeIdentityDbContext _dbContext = new();
    private readonly IPasswordHasher _passwordHasher = Substitute.For<IPasswordHasher>();
    private readonly IOpaqueTokenGenerator _tokenGenerator = Substitute.For<IOpaqueTokenGenerator>();
    private readonly TimeProvider _timeProvider = TimeProvider.System;

    public void Dispose() => _dbContext.Dispose();

    private ProvisionEmployeeUserCommandHandler CreateHandler() =>
        new(_dbContext, _passwordHasher, _tokenGenerator, _timeProvider);

    [Fact]
    public async Task Handle_creates_an_Employee_role_user_that_must_change_password()
    {
        var companyId = Guid.CreateVersion7();
        _tokenGenerator.Generate().Returns("raw-temp-password");
        _passwordHasher.Hash("raw-temp-password").Returns("hashed-temp-password");

        var handler = CreateHandler();
        var command = new ProvisionEmployeeUserCommand("new.employee@example.com", "First", "Last", companyId);

        var result = await handler.Handle(command, CancellationToken.None);

        result.TempPassword.Should().Be("raw-temp-password");

        var storedUser = _dbContext.Users.Single(u => u.Id == result.UserId);
        storedUser.Role.Should().Be(UserRole.Employee);
        storedUser.CompanyId.Should().Be(companyId);
        storedUser.MustChangePassword.Should().BeTrue();
        storedUser.PasswordHash.Should().Be("hashed-temp-password");
        storedUser.Email.Should().Be("new.employee@example.com");
    }

    [Fact]
    public async Task Handle_throws_when_the_email_is_already_registered()
    {
        _dbContext.Users.Add(User.Register("taken@example.com", "some-hash", null, null, DateTimeOffset.UtcNow));
        await _dbContext.SaveChangesAsync();
        _tokenGenerator.Generate().Returns("raw-temp-password");
        _passwordHasher.Hash(Arg.Any<string>()).Returns("hashed-temp-password");

        var handler = CreateHandler();
        var command = new ProvisionEmployeeUserCommand("taken@example.com", null, null, Guid.CreateVersion7());

        var act = () => handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<EmailAlreadyInUseException>();
    }
}
