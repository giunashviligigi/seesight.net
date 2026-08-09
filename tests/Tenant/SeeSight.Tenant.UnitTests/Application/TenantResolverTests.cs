using FluentAssertions;
using SeeSight.SharedKernel.Tenancy;
using SeeSight.Tenant.Application.Common;
using SeeSight.Tenant.Application.Exceptions;
using SeeSight.Tenant.UnitTests.TestSupport;

namespace SeeSight.Tenant.UnitTests.Application;

/// <summary>
/// Exhaustively covers docs/TenantArchitecture.md §4's rule — the single most
/// safety-critical piece of tenant-isolation logic in this service.
/// </summary>
public sealed class TenantResolverTests
{
    private readonly TenantResolver _resolver = new();

    [Fact]
    public void SuperAdmin_with_an_explicit_companyId_resolves_to_that_company()
    {
        var companyId = Guid.CreateVersion7();
        var tenantContext = new FakeTenantContext(companyId: null, isSuperAdmin: true);

        var result = _resolver.Resolve(tenantContext, companyId);

        result.Should().Be(companyId);
    }

    [Fact]
    public void SuperAdmin_without_an_explicit_companyId_throws()
    {
        var tenantContext = new FakeTenantContext(companyId: null, isSuperAdmin: true);

        var act = () => _resolver.Resolve(tenantContext, null);

        act.Should().Throw<CompanyIdRequiredException>();
    }

    [Fact]
    public void NonSuperAdmin_without_an_explicit_companyId_resolves_to_their_own_company()
    {
        var ownCompanyId = Guid.CreateVersion7();
        var tenantContext = new FakeTenantContext(new TenantId(ownCompanyId), isSuperAdmin: false);

        var result = _resolver.Resolve(tenantContext, null);

        result.Should().Be(ownCompanyId);
    }

    [Fact]
    public void NonSuperAdmin_passing_their_own_companyId_explicitly_resolves_successfully()
    {
        var ownCompanyId = Guid.CreateVersion7();
        var tenantContext = new FakeTenantContext(new TenantId(ownCompanyId), isSuperAdmin: false);

        var result = _resolver.Resolve(tenantContext, ownCompanyId);

        result.Should().Be(ownCompanyId);
    }

    [Fact]
    public void NonSuperAdmin_passing_a_different_companyId_throws()
    {
        var ownCompanyId = Guid.CreateVersion7();
        var otherCompanyId = Guid.CreateVersion7();
        var tenantContext = new FakeTenantContext(new TenantId(ownCompanyId), isSuperAdmin: false);

        var act = () => _resolver.Resolve(tenantContext, otherCompanyId);

        act.Should().Throw<CrossTenantAccessException>();
    }

    [Fact]
    public void NonSuperAdmin_with_no_company_assigned_throws()
    {
        var tenantContext = new FakeTenantContext(companyId: null, isSuperAdmin: false);

        var act = () => _resolver.Resolve(tenantContext, null);

        act.Should().Throw<NoCompanyAssignedException>();
    }

    [Fact]
    public void NonSuperAdmin_with_no_company_assigned_and_an_explicit_companyId_still_throws_NoCompanyAssigned()
    {
        var tenantContext = new FakeTenantContext(companyId: null, isSuperAdmin: false);

        var act = () => _resolver.Resolve(tenantContext, Guid.CreateVersion7());

        act.Should().Throw<NoCompanyAssignedException>("having no company at all is the more fundamental problem than the mismatch");
    }
}
