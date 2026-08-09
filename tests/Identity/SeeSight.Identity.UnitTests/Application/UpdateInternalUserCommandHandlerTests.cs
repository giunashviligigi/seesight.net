using FluentAssertions;
using SeeSight.Identity.Application.Exceptions;
using SeeSight.Identity.Application.Users;
using SeeSight.Identity.Domain;
using SeeSight.Identity.UnitTests.TestSupport;

namespace SeeSight.Identity.UnitTests.Application;

public sealed class UpdateInternalUserCommandHandlerTests : IDisposable
{
    private readonly FakeIdentityDbContext _dbContext = new();
    private readonly TimeProvider _timeProvider = TimeProvider.System;

    public void Dispose() => _dbContext.Dispose();

    private UpdateInternalUserCommandHandler CreateHandler() => new(_dbContext, _timeProvider);

    private async Task<User> SeedUserAsync()
    {
        var user = User.Register("someone@example.com", "hash", "Old", "Name", DateTimeOffset.UtcNow);
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();
        return user;
    }

    [Fact]
    public async Task Handle_updates_names_when_supplied()
    {
        var user = await SeedUserAsync();

        var handler = CreateHandler();
        await handler.Handle(new UpdateInternalUserCommand(user.Id, "New", "Person", false, null), CancellationToken.None);

        var storedUser = _dbContext.Users.Single(u => u.Id == user.Id);
        storedUser.FirstName.Should().Be("New");
        storedUser.LastName.Should().Be("Person");
    }

    [Fact]
    public async Task Handle_leaves_names_unchanged_when_not_supplied()
    {
        var user = await SeedUserAsync();

        var handler = CreateHandler();
        await handler.Handle(new UpdateInternalUserCommand(user.Id, null, null, false, null), CancellationToken.None);

        var storedUser = _dbContext.Users.Single(u => u.Id == user.Id);
        storedUser.FirstName.Should().Be("Old");
        storedUser.LastName.Should().Be("Name");
    }

    [Fact]
    public async Task Handle_assigns_a_company_when_supplied()
    {
        var user = await SeedUserAsync();
        var companyId = Guid.CreateVersion7();

        var handler = CreateHandler();
        await handler.Handle(new UpdateInternalUserCommand(user.Id, null, null, false, companyId), CancellationToken.None);

        var storedUser = _dbContext.Users.Single(u => u.Id == user.Id);
        storedUser.CompanyId.Should().Be(companyId);
    }

    [Fact]
    public async Task Handle_clears_the_company_when_ClearCompanyId_is_set()
    {
        var user = User.ProvisionForEmployee("someone@example.com", "hash", null, null, Guid.CreateVersion7(), DateTimeOffset.UtcNow);
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        var handler = CreateHandler();
        await handler.Handle(new UpdateInternalUserCommand(user.Id, null, null, true, null), CancellationToken.None);

        var storedUser = _dbContext.Users.Single(u => u.Id == user.Id);
        storedUser.CompanyId.Should().BeNull();
    }

    [Fact]
    public async Task Handle_throws_for_an_unknown_user()
    {
        var handler = CreateHandler();
        var act = () => handler.Handle(new UpdateInternalUserCommand(Guid.CreateVersion7(), "New", null, false, null), CancellationToken.None);

        await act.Should().ThrowAsync<UserNotFoundException>();
    }
}
