using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using SeeSight.SharedKernel.Http;

namespace SeeSight.Identity.IntegrationTests;

public sealed class AuthEndpointsTests : IClassFixture<IdentityApiFactory>
{
    private readonly IdentityApiFactory _factory;

    public AuthEndpointsTests(IdentityApiFactory factory)
    {
        _factory = factory;
    }

    private static string UniqueEmail() => $"user-{Guid.CreateVersion7()}@example.com";

    [Fact]
    public async Task Register_then_login_then_me_returns_the_same_user()
    {
        using var client = _factory.CreateClient();
        var email = UniqueEmail();

        var registerResponse = await client.PostAsJsonAsync("/auth/register", new
        {
            email,
            password = "SecurePass123",
            firstName = "Integration",
            lastName = "Test",
        });
        registerResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var registerBody = await registerResponse.Content.ReadFromJsonAsync<JsonElement>();
        registerBody.GetProperty("accessToken").GetString().Should().NotBeNullOrEmpty();
        var userId = registerBody.GetProperty("user").GetProperty("id").GetString();

        var loginResponse = await client.PostAsJsonAsync("/auth/login", new { email, password = "SecurePass123" });
        loginResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var loginBody = await loginResponse.Content.ReadFromJsonAsync<JsonElement>();
        loginBody.GetProperty("user").GetProperty("id").GetString().Should().Be(userId);

        // Identity.Api trusts the Gateway-forwarded identity headers directly
        // (it never validates a JWT itself — see docs/Authentication.md §8) —
        // simulate what the Gateway would have set after validating the token.
        using var meRequest = new HttpRequestMessage(HttpMethod.Get, "/auth/me");
        meRequest.Headers.Add(ForwardedIdentityHeaders.UserId, userId);
        var meResponse = await client.SendAsync(meRequest);

        meResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var meBody = await meResponse.Content.ReadFromJsonAsync<JsonElement>();
        meBody.GetProperty("id").GetString().Should().Be(userId);
        meBody.GetProperty("email").GetString().Should().Be(email);
    }

    [Fact]
    public async Task Register_with_an_already_used_email_returns_409()
    {
        using var client = _factory.CreateClient();
        var email = UniqueEmail();
        var payload = new { email, password = "SecurePass123" };

        (await client.PostAsJsonAsync("/auth/register", payload)).StatusCode.Should().Be(HttpStatusCode.Created);
        var second = await client.PostAsJsonAsync("/auth/register", payload);

        second.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Login_with_the_wrong_password_returns_401()
    {
        using var client = _factory.CreateClient();
        var email = UniqueEmail();
        await client.PostAsJsonAsync("/auth/register", new { email, password = "SecurePass123" });

        var response = await client.PostAsJsonAsync("/auth/login", new { email, password = "WrongPassword" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Login_for_an_unknown_email_returns_401()
    {
        using var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/auth/login", new { email = UniqueEmail(), password = "SecurePass123" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Me_without_an_identity_header_returns_401()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/auth/me");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Register_rejects_an_unmapped_json_property()
    {
        using var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/auth/register", new
        {
            email = UniqueEmail(),
            password = "SecurePass123",
            notARealField = "should be rejected",
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Register_rejects_an_invalid_password()
    {
        using var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/auth/register", new { email = UniqueEmail(), password = "short" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Jwks_endpoint_returns_only_public_key_material()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/.well-known/jwks.json");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var key = body.GetProperty("keys")[0];

        key.TryGetProperty("n", out _).Should().BeTrue();
        key.TryGetProperty("e", out _).Should().BeTrue();
        key.TryGetProperty("d", out _).Should().BeFalse("the private exponent must never be exposed");
        key.TryGetProperty("p", out _).Should().BeFalse("private key material must never be exposed");
        key.TryGetProperty("q", out _).Should().BeFalse("private key material must never be exposed");
    }

    [Fact]
    public async Task Health_endpoints_report_healthy()
    {
        using var client = _factory.CreateClient();

        (await client.GetAsync("/health/live")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.GetAsync("/health/ready")).StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
