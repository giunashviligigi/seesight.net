using System.Security.Claims;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using SeeSight.Gateway.Authentication;
using SeeSight.SharedKernel.Http;

namespace SeeSight.Gateway.Tests;

public sealed class MustChangePasswordMiddlewareTests
{
    private static DefaultHttpContext CreateContext(string path, bool authenticated, bool? mustChangePassword = null)
    {
        var context = new DefaultHttpContext();
        context.Request.Path = path;
        context.Response.Body = new MemoryStream();

        var claims = new List<Claim>();
        if (mustChangePassword is not null)
        {
            claims.Add(new Claim(SeeSightClaimTypes.MustChangePassword, mustChangePassword.Value ? "true" : "false"));
        }

        var identity = authenticated
            ? new ClaimsIdentity(claims, "TestAuth")
            : new ClaimsIdentity();

        context.User = new ClaimsPrincipal(identity);
        return context;
    }

    private static (MustChangePasswordMiddleware Middleware, Func<bool> WasNextCalled) CreateMiddleware()
    {
        var nextCalled = false;
        var middleware = new MustChangePasswordMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });
        return (middleware, () => nextCalled);
    }

    [Fact]
    public async Task Unauthenticated_requests_pass_through_untouched()
    {
        var (middleware, wasNextCalled) = CreateMiddleware();
        var context = CreateContext("/trips", authenticated: false);

        await middleware.InvokeAsync(context);

        wasNextCalled().Should().BeTrue();
        context.Response.StatusCode.Should().Be(StatusCodes.Status200OK);
    }

    [Fact]
    public async Task Authenticated_requests_without_the_claim_pass_through()
    {
        var (middleware, wasNextCalled) = CreateMiddleware();
        var context = CreateContext("/trips", authenticated: true);

        await middleware.InvokeAsync(context);

        wasNextCalled().Should().BeTrue();
    }

    [Fact]
    public async Task Authenticated_requests_with_MustChangePassword_false_pass_through()
    {
        var (middleware, wasNextCalled) = CreateMiddleware();
        var context = CreateContext("/trips", authenticated: true, mustChangePassword: false);

        await middleware.InvokeAsync(context);

        wasNextCalled().Should().BeTrue();
    }

    [Theory]
    [InlineData("/auth/change-password")]
    [InlineData("/auth/me")]
    [InlineData("/auth/logout")]
    [InlineData("/auth/refresh")]
    public async Task MustChangePassword_users_may_reach_allowlisted_paths(string path)
    {
        var (middleware, wasNextCalled) = CreateMiddleware();
        var context = CreateContext(path, authenticated: true, mustChangePassword: true);

        await middleware.InvokeAsync(context);

        wasNextCalled().Should().BeTrue();
    }

    [Fact]
    public async Task MustChangePassword_users_are_blocked_from_other_paths_with_403()
    {
        var (middleware, wasNextCalled) = CreateMiddleware();
        var context = CreateContext("/trips", authenticated: true, mustChangePassword: true);

        await middleware.InvokeAsync(context);

        wasNextCalled().Should().BeFalse();
        context.Response.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        context.Response.ContentType.Should().Be("application/problem+json");

        context.Response.Body.Position = 0;
        var body = await JsonSerializer.DeserializeAsync<JsonElement>(context.Response.Body);
        body.GetProperty("status").GetInt32().Should().Be(StatusCodes.Status403Forbidden);
    }
}
