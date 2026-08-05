using FluentAssertions;
using NetArchTest.Rules;

namespace SeeSight.Gateway.Tests;

/// <summary>
/// Structurally verifies the "thin gateway" rule (docs/Microservices.md §1,
/// [ADR 0001]) — the Gateway project cannot reference any service's business
/// logic, because no such reference exists in its dependency graph at all.
/// </summary>
public sealed class GatewayHasNoBusinessLogicTests
{
    private static readonly System.Reflection.Assembly GatewayAssembly = typeof(Program).Assembly;

    [Theory]
    [InlineData("SeeSight.Identity.Domain")]
    [InlineData("SeeSight.Identity.Application")]
    [InlineData("SeeSight.Identity.Infrastructure")]
    [InlineData("SeeSight.Identity.Api")]
    public void Gateway_does_not_depend_on_any_service_business_logic(string forbiddenAssembly)
    {
        var result = Types.InAssembly(GatewayAssembly)
            .Should().NotHaveDependencyOn(forbiddenAssembly)
            .GetResult();

        result.IsSuccessful.Should().BeTrue();
    }

    [Fact]
    public void Gateway_only_references_the_shared_libraries_allowed_by_ProjectReferenceDiagram()
    {
        // Allowed: SharedKernel, Shared.Observability (docs/ProjectReferenceDiagram.md §6).
        // Forbidden: Shared.Contracts, Shared.Messaging, Shared.Common — the
        // Gateway neither publishes/consumes events nor needs money/CSV helpers.
        var forbidden = new[] { "SeeSight.Shared.Contracts", "SeeSight.Shared.Messaging", "SeeSight.Shared.Common" };

        foreach (var assemblyName in forbidden)
        {
            var result = Types.InAssembly(GatewayAssembly)
                .Should().NotHaveDependencyOn(assemblyName)
                .GetResult();

            result.IsSuccessful.Should().BeTrue($"Gateway should not depend on {assemblyName}");
        }
    }
}
