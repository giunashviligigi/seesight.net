using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using SeeSight.SharedKernel.Http;

namespace SeeSight.Identity.IntegrationTests;

public sealed class RefreshAndPasswordFlowsTests : IClassFixture<IdentityApiFactory>
{
    private readonly IdentityApiFactory _factory;

    public RefreshAndPasswordFlowsTests(IdentityApiFactory factory)
    {
        _factory = factory;
    }

    private static string UniqueEmail() => $"user-{Guid.CreateVersion7()}@example.com";

    private async Task<(HttpClient Client, string Email, string UserId, string RefreshToken)> RegisterAsync()
    {
        var client = _factory.CreateClient();
        var email = UniqueEmail();

        var response = await client.PostAsJsonAsync("/auth/register", new { email, password = "SecurePass123" });
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        var userId = body.GetProperty("user").GetProperty("id").GetString()!;
        var refreshToken = body.GetProperty("refreshToken").GetString()!;
        return (client, email, userId, refreshToken);
    }

    [Fact]
    public async Task Register_returns_both_an_access_and_a_refresh_token()
    {
        using var client = _factory.CreateClient();
        var email = UniqueEmail();

        var response = await client.PostAsJsonAsync("/auth/register", new { email, password = "SecurePass123" });
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        body.GetProperty("accessToken").GetString().Should().NotBeNullOrEmpty();
        body.GetProperty("accessTokenExpiresAt").GetDateTimeOffset().Should().BeAfter(DateTimeOffset.UtcNow);
        body.GetProperty("refreshToken").GetString().Should().NotBeNullOrEmpty();
        body.GetProperty("refreshTokenExpiresAt").GetDateTimeOffset().Should().BeAfter(DateTimeOffset.UtcNow.AddDays(29));
    }

    [Fact]
    public async Task Refresh_with_a_valid_token_rotates_to_a_new_token_pair()
    {
        var (client, _, userId, refreshToken) = await RegisterAsync();
        using var _ = client;

        var response = await client.PostAsJsonAsync("/auth/refresh", new { refreshToken });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("user").GetProperty("id").GetString().Should().Be(userId);
        body.GetProperty("refreshToken").GetString().Should().NotBe(refreshToken);
    }

    [Fact]
    public async Task Refresh_with_an_already_rotated_token_is_rejected()
    {
        var (client, _, _, refreshToken) = await RegisterAsync();
        using var _ = client;
        await client.PostAsJsonAsync("/auth/refresh", new { refreshToken });

        var reuseResponse = await client.PostAsJsonAsync("/auth/refresh", new { refreshToken });

        reuseResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Refresh_with_an_unknown_token_returns_401()
    {
        using var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/auth/refresh", new { refreshToken = "not-a-real-token" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Refresh_with_no_token_at_all_returns_401()
    {
        using var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/auth/refresh", new { });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Logout_revokes_the_refresh_token_so_it_can_no_longer_be_used()
    {
        var (client, _, _, refreshToken) = await RegisterAsync();
        using var _ = client;

        var logoutResponse = await client.PostAsJsonAsync("/auth/logout", new { refreshToken });
        logoutResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var refreshResponse = await client.PostAsJsonAsync("/auth/refresh", new { refreshToken });
        refreshResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Logout_with_no_token_is_a_no_op_204()
    {
        using var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/auth/logout", new { });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task ForgotPassword_for_an_unknown_email_returns_a_generic_response_with_no_debug_token()
    {
        using var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/auth/forgot-password", new { email = UniqueEmail() });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("message").GetString().Should().NotBeNullOrEmpty();
        body.GetProperty("debugToken").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task ForgotPassword_then_ResetPassword_then_login_with_the_new_password_succeeds()
    {
        var (client, email, _, _) = await RegisterAsync();
        using var _ = client;

        var forgotResponse = await client.PostAsJsonAsync("/auth/forgot-password", new { email });
        forgotResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var forgotBody = await forgotResponse.Content.ReadFromJsonAsync<JsonElement>();
        var debugToken = forgotBody.GetProperty("debugToken").GetString();
        debugToken.Should().NotBeNullOrEmpty("the test host runs in the Development environment");

        var resetResponse = await client.PostAsJsonAsync("/auth/reset-password", new { token = debugToken, newPassword = "BrandNewPass456" });
        resetResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var oldPasswordLogin = await client.PostAsJsonAsync("/auth/login", new { email, password = "SecurePass123" });
        oldPasswordLogin.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var newPasswordLogin = await client.PostAsJsonAsync("/auth/login", new { email, password = "BrandNewPass456" });
        newPasswordLogin.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ResetPassword_with_an_invalid_token_returns_400()
    {
        using var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/auth/reset-password", new { token = "not-a-real-token", newPassword = "BrandNewPass456" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ChangePassword_without_an_identity_header_returns_401()
    {
        using var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/auth/change-password", new { currentPassword = "SecurePass123", newPassword = "BrandNewPass456" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ChangePassword_with_the_correct_current_password_succeeds_and_the_new_password_can_log_in()
    {
        var (client, email, userId, _) = await RegisterAsync();
        using var _ = client;

        using var changeRequest = new HttpRequestMessage(HttpMethod.Post, "/auth/change-password")
        {
            Content = JsonContent.Create(new { currentPassword = "SecurePass123", newPassword = "BrandNewPass456" }),
        };
        changeRequest.Headers.Add(ForwardedIdentityHeaders.UserId, userId);
        var changeResponse = await client.SendAsync(changeRequest);

        changeResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var newPasswordLogin = await client.PostAsJsonAsync("/auth/login", new { email, password = "BrandNewPass456" });
        newPasswordLogin.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ChangePassword_with_the_wrong_current_password_returns_400()
    {
        var (client, _, userId, _) = await RegisterAsync();
        using var _ = client;

        using var changeRequest = new HttpRequestMessage(HttpMethod.Post, "/auth/change-password")
        {
            Content = JsonContent.Create(new { currentPassword = "WrongPassword1", newPassword = "BrandNewPass456" }),
        };
        changeRequest.Headers.Add(ForwardedIdentityHeaders.UserId, userId);
        var changeResponse = await client.SendAsync(changeRequest);

        changeResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
