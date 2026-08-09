using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using SeeSight.SharedKernel.Http;
using SeeSight.SharedKernel.InternalAuth;
using SeeSight.Tenant.IntegrationTests.TestSupport;

namespace SeeSight.Tenant.IntegrationTests;

public sealed class InternalEmployeesEndpointsTests : IClassFixture<TenantApiFactory>
{
    private readonly TenantApiFactory _factory;

    public InternalEmployeesEndpointsTests(TenantApiFactory factory)
    {
        _factory = factory;
    }

    private HttpClient CreateAuthorizedClient()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(InternalServiceTokenHeaders.ServiceToken, TenantApiFactory.TestInternalServiceToken);
        return client;
    }

    [Fact]
    public async Task Validate_without_the_internal_token_returns_401()
    {
        using var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/internal/employees/validate", new { companyId = Guid.CreateVersion7(), employeeIds = new[] { Guid.CreateVersion7() } });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Validate_with_the_wrong_internal_token_returns_401()
    {
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(InternalServiceTokenHeaders.ServiceToken, "not-the-right-token");

        var response = await client.PostAsJsonAsync("/internal/employees/validate", new { companyId = Guid.CreateVersion7(), employeeIds = new[] { Guid.CreateVersion7() } });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Public_routes_are_unaffected_by_the_internal_token_check()
    {
        using var client = _factory.CreateClient();

        var response = await client.SendAsUserAsync(HttpMethod.Get, "/companies/me", Guid.CreateVersion7(), SeeSightRoles.CompanyAdmin, Guid.CreateVersion7());

        response.StatusCode.Should().NotBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Validate_with_a_valid_token_correctly_partitions_valid_and_invalid_employee_ids()
    {
        using var authorizedClient = CreateAuthorizedClient();
        using var userClient = _factory.CreateClient();
        var companyId = await TenantSeedHelpers.CreateCompanyAsync(userClient);

        var employeeResponse = await userClient.SendAsUserAsync(HttpMethod.Post, "/employees", Guid.CreateVersion7(), SeeSightRoles.CompanyAdmin, companyId,
            body: new { companyId = (Guid?)null, departmentId = (Guid?)null, email = $"validate-{Guid.CreateVersion7()}@example.com", firstName = "First", lastName = "Last", createLogin = false });
        var employee = await employeeResponse.Content.ReadFromJsonAsync<JsonElement>();
        var validEmployeeId = employee.GetProperty("employee").GetProperty("id").GetGuid();
        var unknownEmployeeId = Guid.CreateVersion7();

        var response = await authorizedClient.PostAsJsonAsync("/internal/employees/validate", new { companyId, employeeIds = new[] { validEmployeeId, unknownEmployeeId } });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("validEmployeeIds").EnumerateArray().Select(e => e.GetGuid()).Should().Contain(validEmployeeId);
        body.GetProperty("invalidEmployeeIds").EnumerateArray().Select(e => e.GetGuid()).Should().Contain(unknownEmployeeId);
    }

    [Fact]
    public async Task Validate_rejects_an_employee_belonging_to_a_different_company()
    {
        using var authorizedClient = CreateAuthorizedClient();
        using var userClient = _factory.CreateClient();
        var companyAId = await TenantSeedHelpers.CreateCompanyAsync(userClient);
        var companyBId = await TenantSeedHelpers.CreateCompanyAsync(userClient);

        var employeeResponse = await userClient.SendAsUserAsync(HttpMethod.Post, "/employees", Guid.CreateVersion7(), SeeSightRoles.CompanyAdmin, companyAId,
            body: new { companyId = (Guid?)null, departmentId = (Guid?)null, email = $"validate-{Guid.CreateVersion7()}@example.com", firstName = "First", lastName = "Last", createLogin = false });
        var employee = await employeeResponse.Content.ReadFromJsonAsync<JsonElement>();
        var companyAEmployeeId = employee.GetProperty("employee").GetProperty("id").GetGuid();

        // Trip Service asks Tenant Service to validate this employee against
        // Company B — must be rejected even though the employee genuinely
        // exists (just under a different company), per docs/TenantArchitecture.md §5.
        var response = await authorizedClient.PostAsJsonAsync("/internal/employees/validate", new { companyId = companyBId, employeeIds = new[] { companyAEmployeeId } });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("validEmployeeIds").EnumerateArray().Select(e => e.GetGuid()).Should().NotContain(companyAEmployeeId);
        body.GetProperty("invalidEmployeeIds").EnumerateArray().Select(e => e.GetGuid()).Should().Contain(companyAEmployeeId);
    }

    [Fact]
    public async Task Validate_rejects_an_inactive_employee()
    {
        using var authorizedClient = CreateAuthorizedClient();
        using var userClient = _factory.CreateClient();
        var companyId = await TenantSeedHelpers.CreateCompanyAsync(userClient);

        var employeeResponse = await userClient.SendAsUserAsync(HttpMethod.Post, "/employees", Guid.CreateVersion7(), SeeSightRoles.CompanyAdmin, companyId,
            body: new { companyId = (Guid?)null, departmentId = (Guid?)null, email = $"validate-{Guid.CreateVersion7()}@example.com", firstName = "First", lastName = "Last", createLogin = false });
        var employee = await employeeResponse.Content.ReadFromJsonAsync<JsonElement>();
        var employeeId = employee.GetProperty("employee").GetProperty("id").GetGuid();

        await userClient.SendAsUserAsync(HttpMethod.Post, $"/employees/{employeeId}/deactivate", Guid.CreateVersion7(), SeeSightRoles.CompanyAdmin, companyId);

        var response = await authorizedClient.PostAsJsonAsync("/internal/employees/validate", new { companyId, employeeIds = new[] { employeeId } });

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("validEmployeeIds").EnumerateArray().Select(e => e.GetGuid()).Should().NotContain(employeeId);
        body.GetProperty("invalidEmployeeIds").EnumerateArray().Select(e => e.GetGuid()).Should().Contain(employeeId);
    }
}
