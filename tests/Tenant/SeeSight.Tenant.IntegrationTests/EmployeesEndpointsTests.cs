using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using SeeSight.SharedKernel.Http;
using SeeSight.Tenant.IntegrationTests.TestSupport;

namespace SeeSight.Tenant.IntegrationTests;

public sealed class EmployeesEndpointsTests : IClassFixture<TenantApiFactory>
{
    private readonly TenantApiFactory _factory;

    public EmployeesEndpointsTests(TenantApiFactory factory)
    {
        _factory = factory;
    }

    private static string UniqueEmail() => $"employee-{Guid.CreateVersion7()}@example.com";

    [Fact]
    public async Task Create_without_createLogin_does_not_call_Identity_Service()
    {
        using var client = _factory.CreateClient();
        var companyId = await TenantSeedHelpers.CreateCompanyAsync(client);
        var provisionCallsBefore = _factory.IdentityServiceClient.ProvisionCalls.Count;

        var response = await client.SendAsUserAsync(HttpMethod.Post, "/employees", Guid.CreateVersion7(), SeeSightRoles.CompanyAdmin, companyId,
            body: new { companyId = (Guid?)null, departmentId = (Guid?)null, email = UniqueEmail(), firstName = "First", lastName = "Last", createLogin = false });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("tempPassword").ValueKind.Should().Be(JsonValueKind.Null);
        body.GetProperty("employee").GetProperty("userId").ValueKind.Should().Be(JsonValueKind.Null);
        _factory.IdentityServiceClient.ProvisionCalls.Should().HaveCount(provisionCallsBefore);
    }

    [Fact]
    public async Task Create_with_createLogin_calls_Identity_Service_and_returns_a_temp_password()
    {
        using var client = _factory.CreateClient();
        var companyId = await TenantSeedHelpers.CreateCompanyAsync(client);
        var email = UniqueEmail();

        var response = await client.SendAsUserAsync(HttpMethod.Post, "/employees", Guid.CreateVersion7(), SeeSightRoles.CompanyAdmin, companyId,
            body: new { companyId = (Guid?)null, departmentId = (Guid?)null, email, firstName = "First", lastName = "Last", createLogin = true });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("tempPassword").GetString().Should().NotBeNullOrEmpty();
        body.GetProperty("employee").GetProperty("userId").ValueKind.Should().Be(JsonValueKind.String);
        _factory.IdentityServiceClient.ProvisionCalls.Should().Contain(c => c.Email == email && c.CompanyId == companyId);
    }

    [Fact]
    public async Task Create_with_an_already_used_email_in_this_company_returns_409_without_calling_Identity()
    {
        using var client = _factory.CreateClient();
        var companyId = await TenantSeedHelpers.CreateCompanyAsync(client);
        var email = UniqueEmail();
        var payload = new { companyId = (Guid?)null, departmentId = (Guid?)null, email, firstName = "First", lastName = "Last", createLogin = false };

        var first = await client.SendAsUserAsync(HttpMethod.Post, "/employees", Guid.CreateVersion7(), SeeSightRoles.CompanyAdmin, companyId, payload);
        first.StatusCode.Should().Be(HttpStatusCode.Created);

        var provisionCallsBefore = _factory.IdentityServiceClient.ProvisionCalls.Count;
        var secondPayload = payload with { createLogin = true };
        var second = await client.SendAsUserAsync(HttpMethod.Post, "/employees", Guid.CreateVersion7(), SeeSightRoles.CompanyAdmin, companyId, secondPayload);

        second.StatusCode.Should().Be(HttpStatusCode.Conflict);
        _factory.IdentityServiceClient.ProvisionCalls.Should().HaveCount(provisionCallsBefore, "the pre-check must reject before ever calling Identity Service");
    }

    [Fact]
    public async Task The_same_email_is_allowed_across_different_companies()
    {
        using var client = _factory.CreateClient();
        var email = UniqueEmail();
        var payload = new { companyId = (Guid?)null, departmentId = (Guid?)null, email, firstName = "First", lastName = "Last", createLogin = false };
        var companyAId = await TenantSeedHelpers.CreateCompanyAsync(client);
        var companyBId = await TenantSeedHelpers.CreateCompanyAsync(client);

        var first = await client.SendAsUserAsync(HttpMethod.Post, "/employees", Guid.CreateVersion7(), SeeSightRoles.CompanyAdmin, companyAId, payload);
        first.StatusCode.Should().Be(HttpStatusCode.Created);

        var second = await client.SendAsUserAsync(HttpMethod.Post, "/employees", Guid.CreateVersion7(), SeeSightRoles.CompanyAdmin, companyBId, payload);
        second.StatusCode.Should().Be(HttpStatusCode.Created, "docs/DatabaseDesign.md §4: employee email uniqueness is tenant-scoped only");
    }

    [Fact]
    public async Task Deactivate_syncs_the_linked_Identity_user()
    {
        using var client = _factory.CreateClient();
        var companyId = await TenantSeedHelpers.CreateCompanyAsync(client);

        var createResponse = await client.SendAsUserAsync(HttpMethod.Post, "/employees", Guid.CreateVersion7(), SeeSightRoles.CompanyAdmin, companyId,
            body: new { companyId = (Guid?)null, departmentId = (Guid?)null, email = UniqueEmail(), firstName = "First", lastName = "Last", createLogin = true });
        var created = await createResponse.Content.ReadFromJsonAsync<JsonElement>();
        var employeeId = created.GetProperty("employee").GetProperty("id").GetGuid();
        var userId = created.GetProperty("employee").GetProperty("userId").GetGuid();

        var response = await client.SendAsUserAsync(HttpMethod.Post, $"/employees/{employeeId}/deactivate", Guid.CreateVersion7(), SeeSightRoles.CompanyAdmin, companyId);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        _factory.IdentityServiceClient.DeactivateCalls.Should().Contain(userId);
    }

    [Fact]
    public async Task Delete_tombstones_the_employee_and_does_not_touch_Identity()
    {
        using var client = _factory.CreateClient();
        var companyId = await TenantSeedHelpers.CreateCompanyAsync(client);

        var createResponse = await client.SendAsUserAsync(HttpMethod.Post, "/employees", Guid.CreateVersion7(), SeeSightRoles.CompanyAdmin, companyId,
            body: new { companyId = (Guid?)null, departmentId = (Guid?)null, email = UniqueEmail(), firstName = "First", lastName = "Last", createLogin = true });
        var created = await createResponse.Content.ReadFromJsonAsync<JsonElement>();
        var employeeId = created.GetProperty("employee").GetProperty("id").GetGuid();
        var deleteCallsBefore = _factory.IdentityServiceClient.DeleteCalls.Count;
        var deactivateCallsBefore = _factory.IdentityServiceClient.DeactivateCalls.Count;

        var response = await client.SendAsUserAsync(HttpMethod.Delete, $"/employees/{employeeId}", Guid.CreateVersion7(), SeeSightRoles.CompanyAdmin, companyId);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        _factory.IdentityServiceClient.DeleteCalls.Should().HaveCount(deleteCallsBefore);
        _factory.IdentityServiceClient.DeactivateCalls.Should().HaveCount(deactivateCallsBefore);

        var getResponse = await client.SendAsUserAsync(HttpMethod.Get, $"/employees/{employeeId}", Guid.CreateVersion7(), SeeSightRoles.CompanyAdmin, companyId);
        getResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetMine_returns_the_callers_own_employee_record()
    {
        using var client = _factory.CreateClient();
        var companyId = await TenantSeedHelpers.CreateCompanyAsync(client);

        var createResponse = await client.SendAsUserAsync(HttpMethod.Post, "/employees", Guid.CreateVersion7(), SeeSightRoles.CompanyAdmin, companyId,
            body: new { companyId = (Guid?)null, departmentId = (Guid?)null, email = UniqueEmail(), firstName = "First", lastName = "Last", createLogin = true });
        var created = await createResponse.Content.ReadFromJsonAsync<JsonElement>();
        var userId = created.GetProperty("employee").GetProperty("userId").GetGuid();

        var response = await client.SendAsUserAsync(HttpMethod.Get, "/employees/me", userId, SeeSightRoles.Employee, companyId);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("userId").GetGuid().Should().Be(userId);
    }

    [Fact]
    public async Task An_Employee_cannot_read_another_employees_record_by_id()
    {
        using var client = _factory.CreateClient();
        var companyId = await TenantSeedHelpers.CreateCompanyAsync(client);

        var createResponse = await client.SendAsUserAsync(HttpMethod.Post, "/employees", Guid.CreateVersion7(), SeeSightRoles.CompanyAdmin, companyId,
            body: new { companyId = (Guid?)null, departmentId = (Guid?)null, email = UniqueEmail(), firstName = "First", lastName = "Last", createLogin = false });
        var created = await createResponse.Content.ReadFromJsonAsync<JsonElement>();
        var employeeId = created.GetProperty("employee").GetProperty("id").GetGuid();

        var differentEmployeeUserId = Guid.CreateVersion7();
        var response = await client.SendAsUserAsync(HttpMethod.Get, $"/employees/{employeeId}", differentEmployeeUserId, SeeSightRoles.Employee, companyId);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound, "self-scoping hides existence, matching the tenant-isolation pattern");
    }
}
