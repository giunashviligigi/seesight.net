using FluentAssertions;
using NSubstitute;
using SeeSight.SharedKernel.Tenancy;
using SeeSight.Tenant.Application.Abstractions;
using SeeSight.Tenant.Application.Common;
using SeeSight.Tenant.Application.Employees;
using SeeSight.Tenant.Application.Exceptions;
using SeeSight.Tenant.Domain;
using SeeSight.Tenant.UnitTests.TestSupport;

namespace SeeSight.Tenant.UnitTests.Application;

/// <summary>
/// Covers the createLogin compensating-rollback path from docs/TenantArchitecture.md
/// §6 — the highest-risk logic in M3.
/// </summary>
public sealed class CreateEmployeeCommandHandlerTests : IDisposable
{
    private readonly FakeTenantDbContext _dbContext = new();
    private readonly IIdentityServiceClient _identityServiceClient = Substitute.For<IIdentityServiceClient>();
    private readonly ITenantResolver _tenantResolver = new TenantResolver();
    private readonly TimeProvider _timeProvider = TimeProvider.System;
    private readonly Guid _companyId = Guid.CreateVersion7();

    public void Dispose() => _dbContext.Dispose();

    private CreateEmployeeCommandHandler CreateHandler(bool isSuperAdmin = false) =>
        new(_dbContext, new FakeTenantContext(new TenantId(_companyId), isSuperAdmin), _tenantResolver, _identityServiceClient, _timeProvider);

    private static CreateEmployeeCommand CommandFor(Guid companyId, bool createLogin) => new(
        companyId, null, "new.employee@example.com", "First", "Last", null, null, null, null, null, createLogin);

    [Fact]
    public async Task Handle_without_createLogin_never_calls_Identity_and_leaves_UserId_null()
    {
        var handler = CreateHandler();

        var result = await handler.Handle(CommandFor(_companyId, createLogin: false), CancellationToken.None);

        result.TempPassword.Should().BeNull();
        result.Employee.UserId.Should().BeNull();
        await _identityServiceClient.DidNotReceiveWithAnyArgs().ProvisionEmployeeUserAsync(default!, default, default, default, default);
    }

    [Fact]
    public async Task Handle_with_createLogin_provisions_a_user_and_links_it()
    {
        var provisionedUserId = Guid.CreateVersion7();
        _identityServiceClient
            .ProvisionEmployeeUserAsync("new.employee@example.com", "First", "Last", _companyId, Arg.Any<CancellationToken>())
            .Returns(new ProvisionedUser(provisionedUserId, "temp-password-123"));

        var handler = CreateHandler();
        var result = await handler.Handle(CommandFor(_companyId, createLogin: true), CancellationToken.None);

        result.TempPassword.Should().Be("temp-password-123");
        result.Employee.UserId.Should().Be(provisionedUserId);
    }

    [Fact]
    public async Task Handle_throws_and_never_calls_Identity_when_the_email_is_already_taken_in_this_company()
    {
        var existing = Employee.Create(_companyId, null, null, "new.employee@example.com", "Existing", "Person", null, null, null, null, null, DateTimeOffset.UtcNow);
        _dbContext.Employees.Add(existing);
        await _dbContext.SaveChangesAsync();

        var handler = CreateHandler();
        var act = () => handler.Handle(CommandFor(_companyId, createLogin: true), CancellationToken.None);

        await act.Should().ThrowAsync<DuplicateEmployeeEmailException>();
        await _identityServiceClient.DidNotReceiveWithAnyArgs().ProvisionEmployeeUserAsync(default!, default, default, default, default);
    }

    [Fact]
    public async Task Handle_compensates_by_deleting_the_just_created_user_when_the_local_save_fails()
    {
        var provisionedUserId = Guid.CreateVersion7();
        _identityServiceClient
            .ProvisionEmployeeUserAsync(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(new ProvisionedUser(provisionedUserId, "temp-password-123"));

        _dbContext.ThrowOnSaveChanges = new InvalidOperationException("simulated local save failure");

        var handler = CreateHandler();
        var act = () => handler.Handle(CommandFor(_companyId, createLogin: true), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>("the original failure must still propagate to the caller");
        await _identityServiceClient.Received(1).DeleteUserAsync(provisionedUserId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_does_not_call_Identity_delete_when_createLogin_was_false_and_the_save_fails()
    {
        _dbContext.ThrowOnSaveChanges = new InvalidOperationException("simulated local save failure");

        var handler = CreateHandler();
        var act = () => handler.Handle(CommandFor(_companyId, createLogin: false), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
        await _identityServiceClient.DidNotReceiveWithAnyArgs().DeleteUserAsync(default, default);
    }

    [Fact]
    public async Task Handle_as_super_admin_requires_an_explicit_companyId()
    {
        var command = new CreateEmployeeCommand(null, null, "new.employee@example.com", "First", "Last", null, null, null, null, null, false);
        var handler = CreateHandler(isSuperAdmin: true);

        var act = () => handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<CompanyIdRequiredException>();
    }

    [Fact]
    public async Task Handle_rejects_a_non_super_admin_creating_an_employee_in_a_different_company()
    {
        var otherCompanyId = Guid.CreateVersion7();
        var handler = CreateHandler();

        var act = () => handler.Handle(CommandFor(otherCompanyId, createLogin: false), CancellationToken.None);

        await act.Should().ThrowAsync<CrossTenantAccessException>();
    }
}
