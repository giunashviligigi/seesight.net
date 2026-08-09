using FluentAssertions;
using NSubstitute;
using SeeSight.SharedKernel.Http;
using SeeSight.SharedKernel.Tenancy;
using SeeSight.Tenant.Application.Abstractions;
using SeeSight.Tenant.Application.Companies;
using SeeSight.Tenant.Application.Exceptions;
using SeeSight.Tenant.UnitTests.TestSupport;

namespace SeeSight.Tenant.UnitTests.Application;

public sealed class CreateCompanyCommandHandlerTests : IDisposable
{
    private readonly FakeTenantDbContext _dbContext = new();
    private readonly IIdentityServiceClient _identityServiceClient = Substitute.For<IIdentityServiceClient>();
    private readonly ICurrentUserContext _currentUser = Substitute.For<ICurrentUserContext>();
    private readonly TimeProvider _timeProvider = TimeProvider.System;

    public void Dispose() => _dbContext.Dispose();

    private static readonly CreateCompanyCommand Command = new("Acme Corp", null, null, null, "UTC", null);

    [Fact]
    public async Task Handle_by_an_unassigned_CompanyAdmin_creates_the_company_and_self_assigns_as_admin()
    {
        var callerId = Guid.CreateVersion7();
        _currentUser.UserId.Returns(callerId);
        var tenantContext = new FakeTenantContext(companyId: null, isSuperAdmin: false);
        var handler = new CreateCompanyCommandHandler(_dbContext, tenantContext, _currentUser, _identityServiceClient, _timeProvider);

        var result = await handler.Handle(Command, CancellationToken.None);

        result.Name.Should().Be("Acme Corp");
        await _identityServiceClient.Received(1).UpdateUserAsync(callerId, null, null, false, result.Id, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_by_a_CompanyAdmin_who_already_has_a_company_throws()
    {
        var tenantContext = new FakeTenantContext(new TenantId(Guid.CreateVersion7()), isSuperAdmin: false);
        var handler = new CreateCompanyCommandHandler(_dbContext, tenantContext, _currentUser, _identityServiceClient, _timeProvider);

        var act = () => handler.Handle(Command, CancellationToken.None);

        await act.Should().ThrowAsync<CompanyAlreadyAssignedException>();
        await _identityServiceClient.DidNotReceiveWithAnyArgs().UpdateUserAsync(default, default, default, default, default, default);
    }

    [Fact]
    public async Task Handle_by_a_SuperAdmin_creates_the_company_without_self_assigning()
    {
        var tenantContext = new FakeTenantContext(companyId: null, isSuperAdmin: true);
        var handler = new CreateCompanyCommandHandler(_dbContext, tenantContext, _currentUser, _identityServiceClient, _timeProvider);

        var result = await handler.Handle(Command, CancellationToken.None);

        result.Name.Should().Be("Acme Corp");
        await _identityServiceClient.DidNotReceiveWithAnyArgs().UpdateUserAsync(default, default, default, default, default, default);
    }
}
