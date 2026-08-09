using System.Reflection;
using FluentAssertions;
using Microsoft.Extensions.Configuration;

namespace SeeSight.Gateway.Tests;

/// <summary>
/// Structurally verifies "internal-only endpoints are never Gateway-routed"
/// (docs/APIContracts.md, docs/adr/0006-internal-service-to-service-authentication.md)
/// by loading the actual yarp.config.json the Gateway ships with and asserting
/// no route matches an <c>/internal/*</c> path — a request to one 404s at the
/// Gateway (no route = no proxy), which is the desired behavior, not something
/// a middleware needs to explicitly block.
/// </summary>
public sealed class InternalRoutesNotExposedTests
{
    [Fact]
    public void No_configured_route_targets_an_internal_only_path()
    {
        var gatewayAssemblyDirectory = Path.GetDirectoryName(typeof(Program).Assembly.Location)!;
        var configPath = Path.Combine(gatewayAssemblyDirectory, "yarp.config.json");
        File.Exists(configPath).Should().BeTrue($"expected to find yarp.config.json at {configPath}");

        var configuration = new ConfigurationBuilder()
            .AddJsonFile(configPath, optional: false)
            .Build();

        var routePaths = configuration.GetSection("ReverseProxy:Routes")
            .GetChildren()
            .Select(route => route.GetSection("Match")["Path"])
            .Where(path => path is not null)
            .ToList();

        routePaths.Should().NotBeEmpty("the route table should never be accidentally empty");
        routePaths.Should().NotContain(path => path!.StartsWith("/internal", StringComparison.OrdinalIgnoreCase));
    }
}
