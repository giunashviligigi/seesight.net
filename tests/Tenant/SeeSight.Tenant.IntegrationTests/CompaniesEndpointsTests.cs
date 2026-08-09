using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using SeeSight.SharedKernel.Http;
using SeeSight.Tenant.IntegrationTests.TestSupport;

namespace SeeSight.Tenant.IntegrationTests;

public sealed class CompaniesEndpointsTests : IClassFixture<TenantApiFactory>
{
    private readonly TenantApiFactory _factory;

    public CompaniesEndpointsTests(TenantApiFactory factory)
    {
        _factory = factory;
    }

    private static string UniqueName() => $"Acme-{Guid.CreateVersion7()}";

    [Fact]
    public async Task An_unassigned_CompanyAdmin_can_self_create_a_company_and_becomes_its_admin()
    {
        using var client = _factory.CreateClient();
        var callerId = Guid.CreateVersion7();

        var response = await client.SendAsUserAsync(HttpMethod.Post, "/companies", callerId, SeeSightRoles.CompanyAdmin, companyId: null,
            body: new { name = UniqueName(), timezone = "UTC" });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var companyId = body.GetProperty("id").GetGuid();

        _factory.IdentityServiceClient.UpdateCalls.Should().ContainSingle(c => c.UserId == callerId && c.CompanyId == companyId);
    }

    [Fact]
    public async Task A_CompanyAdmin_who_already_has_a_company_cannot_self_create_another()
    {
        using var client = _factory.CreateClient();
        var callerId = Guid.CreateVersion7();
        var existingCompanyId = Guid.CreateVersion7();

        var response = await client.SendAsUserAsync(HttpMethod.Post, "/companies", callerId, SeeSightRoles.CompanyAdmin, existingCompanyId,
            body: new { name = UniqueName(), timezone = "UTC" });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task An_Employee_cannot_create_a_company()
    {
        using var client = _factory.CreateClient();

        var response = await client.SendAsUserAsync(HttpMethod.Post, "/companies", Guid.CreateVersion7(), SeeSightRoles.Employee, Guid.CreateVersion7(),
            body: new { name = UniqueName(), timezone = "UTC" });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task SuperAdmin_can_create_a_company_for_any_target_without_self_assigning()
    {
        using var client = _factory.CreateClient();

        var response = await client.SendAsUserAsync(HttpMethod.Post, "/companies", Guid.CreateVersion7(), SeeSightRoles.SuperAdmin, companyId: null,
            body: new { name = UniqueName(), timezone = "UTC" });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task GetMine_returns_the_callers_own_company()
    {
        using var client = _factory.CreateClient();
        var callerId = Guid.CreateVersion7();
        var createResponse = await client.SendAsUserAsync(HttpMethod.Post, "/companies", callerId, SeeSightRoles.CompanyAdmin, null,
            body: new { name = UniqueName(), timezone = "UTC" });
        var created = await createResponse.Content.ReadFromJsonAsync<JsonElement>();
        var companyId = created.GetProperty("id").GetGuid();

        var response = await client.SendAsUserAsync(HttpMethod.Get, "/companies/me", callerId, SeeSightRoles.CompanyAdmin, companyId);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("id").GetGuid().Should().Be(companyId);
    }

    [Fact]
    public async Task GetAll_requires_SuperAdmin()
    {
        using var client = _factory.CreateClient();

        var response = await client.SendAsUserAsync(HttpMethod.Get, "/companies", Guid.CreateVersion7(), SeeSightRoles.CompanyAdmin, Guid.CreateVersion7());

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Deactivate_then_activate_round_trips_and_activate_clears_a_soft_delete()
    {
        using var client = _factory.CreateClient();
        var callerId = Guid.CreateVersion7();
        var createResponse = await client.SendAsUserAsync(HttpMethod.Post, "/companies", callerId, SeeSightRoles.CompanyAdmin, null,
            body: new { name = UniqueName(), timezone = "UTC" });
        var created = await createResponse.Content.ReadFromJsonAsync<JsonElement>();
        var companyId = created.GetProperty("id").GetGuid();
        var superAdminId = Guid.CreateVersion7();

        var deactivateResponse = await client.SendAsUserAsync(HttpMethod.Post, $"/companies/{companyId}/deactivate", superAdminId, SeeSightRoles.SuperAdmin, null);
        deactivateResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var deleteResponse = await client.SendAsUserAsync(HttpMethod.Delete, $"/companies/{companyId}", superAdminId, SeeSightRoles.SuperAdmin, null);
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Deleted (soft) -- a non-super-admin scoped read must now 404.
        var scopedGet = await client.SendAsUserAsync(HttpMethod.Get, $"/companies/{companyId}", callerId, SeeSightRoles.CompanyAdmin, companyId);
        scopedGet.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var activateResponse = await client.SendAsUserAsync(HttpMethod.Post, $"/companies/{companyId}/activate", superAdminId, SeeSightRoles.SuperAdmin, null);
        activateResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var afterActivate = await client.SendAsUserAsync(HttpMethod.Get, $"/companies/{companyId}", callerId, SeeSightRoles.CompanyAdmin, companyId);
        afterActivate.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
