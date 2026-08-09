using FluentAssertions;
using SeeSight.SharedKernel.Tenancy;
using SeeSight.Tenant.Application.Common;
using SeeSight.Tenant.Application.Departments;
using SeeSight.Tenant.Application.Exceptions;
using SeeSight.Tenant.Domain;
using SeeSight.Tenant.UnitTests.TestSupport;

namespace SeeSight.Tenant.UnitTests.Application;

public sealed class CreateDepartmentCommandHandlerTests : IDisposable
{
    private readonly FakeTenantDbContext _dbContext = new();
    private readonly ITenantResolver _tenantResolver = new TenantResolver();
    private readonly TimeProvider _timeProvider = TimeProvider.System;
    private readonly Guid _companyId = Guid.CreateVersion7();

    public void Dispose() => _dbContext.Dispose();

    private CreateDepartmentCommandHandler CreateHandler() =>
        new(_dbContext, new FakeTenantContext(new TenantId(_companyId), isSuperAdmin: false), _tenantResolver, _timeProvider);

    [Fact]
    public async Task Handle_creates_a_department_for_the_callers_company()
    {
        var handler = CreateHandler();

        var result = await handler.Handle(new CreateDepartmentCommand(null, "Engineering", "ENG"), CancellationToken.None);

        result.CompanyId.Should().Be(_companyId);
        result.Name.Should().Be("Engineering");
    }

    [Fact]
    public async Task Handle_throws_for_a_duplicate_name_within_the_same_company()
    {
        _dbContext.Departments.Add(Department.Create(_companyId, "Engineering", null, DateTimeOffset.UtcNow));
        await _dbContext.SaveChangesAsync();

        var handler = CreateHandler();
        var act = () => handler.Handle(new CreateDepartmentCommand(null, "Engineering", null), CancellationToken.None);

        await act.Should().ThrowAsync<DuplicateDepartmentNameException>();
    }

    [Fact]
    public async Task Handle_allows_the_same_name_in_a_different_company()
    {
        var otherCompanyId = Guid.CreateVersion7();
        _dbContext.Departments.Add(Department.Create(otherCompanyId, "Engineering", null, DateTimeOffset.UtcNow));
        await _dbContext.SaveChangesAsync();

        var handler = CreateHandler();
        var act = () => handler.Handle(new CreateDepartmentCommand(null, "Engineering", null), CancellationToken.None);

        await act.Should().NotThrowAsync();
    }
}
