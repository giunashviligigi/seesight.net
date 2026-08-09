using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using SeeSight.SharedKernel.InternalAuth;

namespace SeeSight.Identity.IntegrationTests;

public sealed class InternalUsersEndpointsTests : IClassFixture<IdentityApiFactory>
{
    private readonly IdentityApiFactory _factory;

    public InternalUsersEndpointsTests(IdentityApiFactory factory)
    {
        _factory = factory;
    }

    private static string UniqueEmail() => $"employee-{Guid.CreateVersion7()}@example.com";

    private HttpClient CreateAuthorizedClient()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(InternalServiceTokenHeaders.ServiceToken, IdentityApiFactory.TestInternalServiceToken);
        return client;
    }

    [Fact]
    public async Task Create_without_the_internal_token_returns_401()
    {
        using var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/internal/users", new
        {
            email = UniqueEmail(),
            firstName = "First",
            lastName = "Last",
            companyId = Guid.CreateVersion7(),
        });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Create_with_the_wrong_internal_token_returns_401()
    {
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(InternalServiceTokenHeaders.ServiceToken, "not-the-right-token");

        var response = await client.PostAsJsonAsync("/internal/users", new
        {
            email = UniqueEmail(),
            firstName = "First",
            lastName = "Last",
            companyId = Guid.CreateVersion7(),
        });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Public_routes_are_unaffected_by_the_internal_token_check()
    {
        using var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/auth/register", new { email = UniqueEmail(), password = "SecurePass123" });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Create_with_a_valid_token_provisions_an_Employee_role_user_with_MustChangePassword()
    {
        using var client = CreateAuthorizedClient();
        var email = UniqueEmail();
        var companyId = Guid.CreateVersion7();

        var response = await client.PostAsJsonAsync("/internal/users", new { email, firstName = "First", lastName = "Last", companyId });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("userId").GetGuid().Should().NotBeEmpty();
        body.GetProperty("tempPassword").GetString().Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Create_with_an_already_used_email_returns_409()
    {
        using var client = CreateAuthorizedClient();
        var email = UniqueEmail();
        var payload = new { email, firstName = "First", lastName = "Last", companyId = Guid.CreateVersion7() };

        (await client.PostAsJsonAsync("/internal/users", payload)).StatusCode.Should().Be(HttpStatusCode.Created);
        var second = await client.PostAsJsonAsync("/internal/users", payload);

        second.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    private static async Task<Guid> ProvisionUserAsync(HttpClient client, Guid? companyId = null)
    {
        var response = await client.PostAsJsonAsync("/internal/users", new
        {
            email = UniqueEmail(),
            firstName = "First",
            lastName = "Last",
            companyId = companyId ?? Guid.CreateVersion7(),
        });
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("userId").GetGuid();
    }

    [Fact]
    public async Task Deactivate_then_activate_round_trips_successfully()
    {
        using var client = CreateAuthorizedClient();
        var userId = await ProvisionUserAsync(client);

        var deactivateResponse = await client.PostAsync($"/internal/users/{userId}/deactivate", content: null);
        deactivateResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var activateResponse = await client.PostAsync($"/internal/users/{userId}/activate", content: null);
        activateResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Deactivate_for_an_unknown_user_returns_404()
    {
        using var client = CreateAuthorizedClient();

        var response = await client.PostAsync($"/internal/users/{Guid.CreateVersion7()}/deactivate", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Patch_updates_names_and_assigns_a_company()
    {
        using var client = CreateAuthorizedClient();
        var userId = await ProvisionUserAsync(client);
        var newCompanyId = Guid.CreateVersion7();

        var response = await client.PatchAsJsonAsync($"/internal/users/{userId}", new
        {
            firstName = "Updated",
            lastName = "Name",
            clearCompanyId = false,
            companyId = newCompanyId,
        });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Patch_can_clear_the_company_id()
    {
        using var client = CreateAuthorizedClient();
        var userId = await ProvisionUserAsync(client);

        var response = await client.PatchAsJsonAsync($"/internal/users/{userId}", new
        {
            firstName = (string?)null,
            lastName = (string?)null,
            clearCompanyId = true,
            companyId = (Guid?)null,
        });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Delete_removes_the_user()
    {
        using var client = CreateAuthorizedClient();
        var userId = await ProvisionUserAsync(client);

        var response = await client.DeleteAsync($"/internal/users/{userId}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Delete_is_idempotent_for_an_already_deleted_user()
    {
        using var client = CreateAuthorizedClient();
        var userId = await ProvisionUserAsync(client);
        await client.DeleteAsync($"/internal/users/{userId}");

        var secondDelete = await client.DeleteAsync($"/internal/users/{userId}");

        secondDelete.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }
}
