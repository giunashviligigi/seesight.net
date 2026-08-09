using FluentAssertions;
using NetArchTest.Rules;
using SeeSight.Tenant.Api.Controllers;
using SeeSight.Tenant.Application;
using SeeSight.Tenant.Domain;
using SeeSight.Tenant.Infrastructure;

namespace SeeSight.Tenant.ArchitectureTests;

/// <summary>
/// Enforces the layer-reference rules from docs/ProjectReferenceDiagram.md §6 at
/// build time — the automated backstop referenced there, not just a convention.
/// Mirrors SeeSight.Identity.ArchitectureTests.LayeringTests exactly.
/// </summary>
public sealed class LayeringTests
{
    private static readonly System.Reflection.Assembly DomainAssembly = typeof(Company).Assembly;
    private static readonly System.Reflection.Assembly ApplicationAssembly = typeof(ApplicationServiceCollectionExtensions).Assembly;
    private static readonly System.Reflection.Assembly InfrastructureAssembly = typeof(InfrastructureServiceCollectionExtensions).Assembly;
    private static readonly System.Reflection.Assembly ApiAssembly = typeof(CompaniesController).Assembly;

    [Fact]
    public void Domain_does_not_depend_on_application()
    {
        var result = Types.InAssembly(DomainAssembly)
            .Should().NotHaveDependencyOn(ApplicationAssembly.GetName().Name)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(FailureSummary(result));
    }

    [Fact]
    public void Domain_does_not_depend_on_infrastructure()
    {
        var result = Types.InAssembly(DomainAssembly)
            .Should().NotHaveDependencyOn(InfrastructureAssembly.GetName().Name)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(FailureSummary(result));
    }

    [Fact]
    public void Domain_does_not_depend_on_api()
    {
        var result = Types.InAssembly(DomainAssembly)
            .Should().NotHaveDependencyOn(ApiAssembly.GetName().Name)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(FailureSummary(result));
    }

    [Theory]
    [InlineData("Microsoft.EntityFrameworkCore")]
    [InlineData("Microsoft.AspNetCore")]
    [InlineData("MediatR")]
    [InlineData("FluentValidation")]
    public void Domain_has_no_framework_or_infrastructure_package_dependency(string forbiddenNamespace)
    {
        var result = Types.InAssembly(DomainAssembly)
            .Should().NotHaveDependencyOn(forbiddenNamespace)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(FailureSummary(result));
    }

    [Fact]
    public void Application_does_not_depend_on_infrastructure()
    {
        var result = Types.InAssembly(ApplicationAssembly)
            .Should().NotHaveDependencyOn(InfrastructureAssembly.GetName().Name)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(FailureSummary(result));
    }

    [Fact]
    public void Application_does_not_depend_on_api()
    {
        var result = Types.InAssembly(ApplicationAssembly)
            .Should().NotHaveDependencyOn(ApiAssembly.GetName().Name)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(FailureSummary(result));
    }

    [Theory]
    [InlineData("Npgsql")]
    [InlineData("Microsoft.EntityFrameworkCore.Design")]
    public void Application_has_no_provider_specific_infrastructure_dependency(string forbiddenNamespace)
    {
        // Application may reference the EF Core *abstraction* (DbSet<T> in
        // ITenantDbContext — docs/ProjectReferenceDiagram.md §6) but never a
        // concrete provider or infrastructure library.
        var result = Types.InAssembly(ApplicationAssembly)
            .Should().NotHaveDependencyOn(forbiddenNamespace)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(FailureSummary(result));
    }

    [Fact]
    public void Infrastructure_does_not_depend_on_api()
    {
        var result = Types.InAssembly(InfrastructureAssembly)
            .Should().NotHaveDependencyOn(ApiAssembly.GetName().Name)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(FailureSummary(result));
    }

    [Fact]
    public void No_layer_depends_on_the_gateway()
    {
        foreach (var assembly in new[] { DomainAssembly, ApplicationAssembly, InfrastructureAssembly, ApiAssembly })
        {
            var result = Types.InAssembly(assembly)
                .Should().NotHaveDependencyOn("SeeSight.Gateway")
                .GetResult();

            result.IsSuccessful.Should().BeTrue(FailureSummary(result));
        }
    }

    [Fact]
    public void No_layer_depends_on_another_services_project()
    {
        // docs/ProjectReferenceDiagram.md §1: no service project ever references
        // another service's project — Tenant Service reaches Identity Service
        // only over HTTP (IIdentityServiceClient), never a ProjectReference.
        foreach (var assembly in new[] { DomainAssembly, ApplicationAssembly, InfrastructureAssembly, ApiAssembly })
        {
            var result = Types.InAssembly(assembly)
                .Should().NotHaveDependencyOn("SeeSight.Identity.Domain")
                .And().NotHaveDependencyOn("SeeSight.Identity.Application")
                .And().NotHaveDependencyOn("SeeSight.Identity.Infrastructure")
                .And().NotHaveDependencyOn("SeeSight.Identity.Api")
                .GetResult();

            result.IsSuccessful.Should().BeTrue(FailureSummary(result));
        }
    }

    private static string FailureSummary(TestResult result) =>
        result.FailingTypes is null
            ? "unknown failure"
            : string.Join(", ", result.FailingTypes.Select(t => t.FullName));
}
